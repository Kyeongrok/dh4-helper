using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Dh4Launcher.Forms.Services;

/// <summary>
/// 大航海時代IV HD (DK4HD) 세이브(savedatNN.dk4) 편집.
///
/// 파일 구조(분석):
///   0x00: 매직 "DAIKOUKAI4PKW9 1"
///   0x14: 파일 크기(4바이트 LE)
///   0x18: 저장마다 바뀌는 4바이트 값(체크섬 추정 — 표준 CRC/Adler/합과 불일치)
///   보유 자금(4바이트 LE): 오프셋이 주인공(시나리오)마다 다르다. 아래 표 참조.
///
/// 자금 오프셋 분석 메모(중요):
///   - 게임이 읽는 실제 보유 자금의 오프셋은 주인공마다 다르며, 주인공별로 세이브 파일 크기가
///     고유하다(DK4HD는 시나리오별 고정 크기). 따라서 파일 크기로 주인공을 구분해 오프셋을 고른다.
///     아래 5개는 인게임 표시 자금과 일치 검증 완료(전 슬롯에서 시간순 증감이 자연스러움):
///         크기      주인공   오프셋     검증 예
///         107110   티알     0x154B5   savedat00 = 440,683
///         107322   마리아   0x15155   savedat07 = 40,000
///         107294   웃딘     0x15385   savedat15 = 87,334
///         107482   라파엘   0x15086   savedat20 = 2,229,900
///         107410   호드람   0x15112   savedat22 = 390,972
///         107454   릴       0x150CD   savedat30 = 35,000 / savedat31 = 34,985 (교차검증)
///         107098   교타로   0x15496   savedat40 = 30,000 / savedat41 = 29,985 (교차검증)
///     (DK4HD PK 주인공 7명 전원 확정.)
///   - 같은 파일 안에 0x15086/0x15112/0x15385/0x154B5 등 여러 자금성 4바이트가 있어 한 값이
///     다른 주인공의 자금처럼 보일 수 있으나, 게임이 읽는 건 위 표의 주인공별 오프셋 하나뿐이다.
///     예전 코드의 고정값 0x15385는 마침 웃딘 오프셋이라 웃딘만 동작했다.
///
/// 체크섬으로 보이는 0x18 값은 역산되지 않았으나, 게임이 로드 시 이를 강제 검증하지는 않는
/// 것으로 보인다(자금 바이트만 바꾼 세이브도 정상 로드됨). 안전을 위해 항상 .bak 백업을
/// 만든 뒤 자금 바이트만 바꾼다.
/// </summary>
public class SaveEditService : ISaveEditService
{
    private const string Magic = "DAIKOUKAI4";

    /// <summary>주인공별 세이브 파일 크기 → 보유 자금 오프셋. (크기가 주인공을 유일하게 식별한다.)</summary>
    private static readonly IReadOnlyDictionary<long, long> MoneyOffsetByFileSize = new Dictionary<long, long>
    {
        [107110] = 0x154B5, // 티알
        [107322] = 0x15155, // 마리아
        [107294] = 0x15385, // 웃딘
        [107482] = 0x15086, // 라파엘
        [107410] = 0x15112, // 호드람
        [107454] = 0x150CD, // 릴
        [107098] = 0x15496, // 교타로
    };

    public string? FindSaveDirectory()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var root = Path.Combine(docs, "KoeiTecmo", "DK4HD", "Savedata");
        if (!Directory.Exists(root))
            return null;

        // 언어 하위 폴더(ko/jp/sc/tc) 중 세이브가 있는 곳을 고른다. ko 우선.
        var subDirs = Directory.GetDirectories(root);
        var ko = subDirs.FirstOrDefault(d => string.Equals(Path.GetFileName(d), "ko", StringComparison.OrdinalIgnoreCase));
        if (ko is not null && HasSlots(ko))
            return ko;

        var withSlots = subDirs.FirstOrDefault(HasSlots);
        if (withSlots is not null)
            return withSlots;

        // 하위 폴더가 없고 루트에 바로 세이브가 있을 수도 있다.
        return HasSlots(root) ? root : null;
    }

    private static bool HasSlots(string dir) => SlotFiles(dir).Any();

    // savedatNN.dk4 (인덱스 파일 savedata.dk4 제외)
    private static IEnumerable<string> SlotFiles(string dir) =>
        Directory.EnumerateFiles(dir, "savedat*.dk4")
            .Where(p => Regex.IsMatch(Path.GetFileName(p), @"^savedat\d+\.dk4$", RegexOptions.IgnoreCase));

    public IReadOnlyList<SaveSlot> ListSlots()
    {
        var dir = FindSaveDirectory();
        if (dir is null)
            return [];

        var slots = new List<SaveSlot>();
        foreach (var path in SlotFiles(dir))
        {
            var name = Path.GetFileName(path);
            var m = Regex.Match(name, @"\d+");
            if (!m.Success || !int.TryParse(m.Value, out var index))
                continue;
            try
            {
                using var fs = File.OpenRead(path);
                if (!VerifyMagic(fs))
                    continue;
                if (!MoneyOffsetByFileSize.TryGetValue(fs.Length, out var off))
                    continue; // 미등록 주인공(크기) — 자금 위치를 모르므로 편집 대상에서 제외
                slots.Add(new SaveSlot(path, name, index + 1, ReadInt(fs, off),
                    File.GetLastWriteTime(path)));
            }
            catch
            {
                // 깨졌거나 잠긴 파일은 건너뛴다.
            }
        }

        return slots.OrderBy(s => s.SlotNumber).ToList();
    }

    public void WriteMoney(string savePath, int money)
    {
        long offset;
        using (var rfs = File.OpenRead(savePath))
        {
            if (!VerifyMagic(rfs))
                throw new InvalidOperationException("DK4HD 세이브 파일이 아닙니다 (매직 불일치).");
            if (!MoneyOffsetByFileSize.TryGetValue(rfs.Length, out offset))
                throw new InvalidOperationException(
                    $"등록되지 않은 주인공(파일 크기 {rfs.Length})입니다. 해당 주인공의 자금 오프셋을 먼저 확인해야 합니다.");
        }

        var bak = savePath + ".bak";
        if (!File.Exists(bak))
            File.Copy(savePath, bak);

        using var fs = new FileStream(savePath, FileMode.Open, FileAccess.Write, FileShare.None);
        fs.Seek(offset, SeekOrigin.Begin);
        fs.Write(BitConverter.GetBytes(money), 0, 4);
    }

    private static bool VerifyMagic(FileStream fs)
    {
        Span<byte> buf = stackalloc byte[Magic.Length];
        fs.Seek(0, SeekOrigin.Begin);
        if (fs.Read(buf) != buf.Length)
            return false;
        return Encoding.ASCII.GetString(buf) == Magic;
    }

    private static int ReadInt(FileStream fs, long off)
    {
        Span<byte> buf = stackalloc byte[4];
        fs.Seek(off, SeekOrigin.Begin);
        if (fs.Read(buf) != 4)
            throw new EndOfStreamException();
        return BitConverter.ToInt32(buf);
    }
}
