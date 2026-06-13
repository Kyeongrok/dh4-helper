using System.Diagnostics;
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
///   0x15385: 보유 자금(4바이트 LE) — 여러 슬롯에서 고정 오프셋임을 확인
///
/// 체크섬으로 보이는 0x18 값은 역산되지 않았으나, 게임이 로드 시 이를 강제 검증하는지는
/// 미확인이다. 안전을 위해 항상 .bak 백업을 만든 뒤 자금 바이트만 바꾼다.
/// </summary>
public class SaveEditService : ISaveEditService
{
    private const long MoneyOffset = 0x15385;
    private const string Magic = "DAIKOUKAI4";

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
                slots.Add(new SaveSlot(path, name, index + 1, ReadInt(fs, MoneyOffset),
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
        using (var rfs = File.OpenRead(savePath))
        {
            if (!VerifyMagic(rfs))
                throw new InvalidOperationException("DK4HD 세이브 파일이 아닙니다 (매직 불일치).");
        }

        var bak = savePath + ".bak";
        if (!File.Exists(bak))
            File.Copy(savePath, bak);

        using var fs = new FileStream(savePath, FileMode.Open, FileAccess.Write, FileShare.None);
        fs.Seek(MoneyOffset, SeekOrigin.Begin);
        fs.Write(BitConverter.GetBytes(money), 0, 4);
    }

    public bool IsGameRunning()
    {
        try
        {
            return Process.GetProcesses()
                .Any(p => p.ProcessName.StartsWith("DK4HD", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
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
