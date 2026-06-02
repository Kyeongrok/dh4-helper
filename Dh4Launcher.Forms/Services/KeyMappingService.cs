using System.Diagnostics;
using System.IO;

namespace Dh4Launcher.Forms.Services;

/// <summary>
/// DK4HD_kr.exe (v1.0.2)의 키 입력 폴링 루틴을 직접 패치해 돛 조타 좌/우 키를 바꾼다.
///
/// 해당 루틴은 GetAsyncKeyState(VK_LEFT)→좌측 액션, GetAsyncKeyState(VK_RIGHT)→우측 액션으로
/// 폴링한다. 'mov ecx, &lt;VK&gt;' 즉시값(아래 오프셋)을 바꾸면 좌/우 입력 키가 바뀐다.
///   0x42216 = 좌(돛)  : mov ecx, 0x25 (VK_LEFT)
///   0x42237 = 우(돛)  : mov ecx, 0x27 (VK_RIGHT)
///   0x421A9 = 위(선회): mov ecx, 0x26 (VK_UP,   넘패드8과 OR)
///   0x421E9 = 아래(선회): mov ecx, 0x28 (VK_DOWN, 넘패드2와 OR)
/// 각 위치는 'B9 [VK] 00 00 00' 형태이며, VK 바이트만 바꾼다.
/// 안전을 위해 주변 바이트(B9 .. 00 00 00) 시그니처를 검증한 뒤에만 패치한다.
/// </summary>
public class KeyMappingService : IKeyMappingService
{
    private const long LeftVkOffset = 0x42216;
    private const long RightVkOffset = 0x42237;
    private const long UpVkOffset = 0x421A9;
    private const long DownVkOffset = 0x421E9;

    public byte DefaultLeftVk => 0x25;  // VK_LEFT
    public byte DefaultRightVk => 0x27; // VK_RIGHT
    public byte DefaultUpVk => 0x26;    // VK_UP
    public byte DefaultDownVk => 0x28;  // VK_DOWN

    public bool IsSupported(string exePath)
    {
        try
        {
            using var fs = File.OpenRead(exePath);
            return VerifySignature(fs);
        }
        catch
        {
            return false;
        }
    }

    public KeyMapState? Read(string exePath)
    {
        try
        {
            using var fs = File.OpenRead(exePath);
            if (!VerifySignature(fs))
                return null;
            return new KeyMapState(
                ReadByte(fs, LeftVkOffset), ReadByte(fs, RightVkOffset),
                ReadByte(fs, UpVkOffset), ReadByte(fs, DownVkOffset));
        }
        catch
        {
            return null;
        }
    }

    public void Apply(string exePath, byte leftVk, byte rightVk, byte upVk, byte downVk)
    {
        using (var rfs = File.OpenRead(exePath))
        {
            if (!VerifySignature(rfs))
                throw new InvalidOperationException("지원하지 않는 실행 파일입니다 (시그니처 불일치).");
        }

        var bak = exePath + ".bak";
        if (!File.Exists(bak))
            File.Copy(exePath, bak);

        using var fs = new FileStream(exePath, FileMode.Open, FileAccess.Write, FileShare.None);
        Write(fs, LeftVkOffset, leftVk);
        Write(fs, RightVkOffset, rightVk);
        Write(fs, UpVkOffset, upVk);
        Write(fs, DownVkOffset, downVk);
    }

    private static void Write(FileStream fs, long off, byte value)
    {
        fs.Seek(off, SeekOrigin.Begin);
        fs.WriteByte(value);
    }

    public bool IsGameRunning()
    {
        try
        {
            return Process.GetProcessesByName("DK4HD_kr").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    // 네 곳 모두 'mov ecx, imm32' = B9 [VK] 00 00 00 형태인지 확인 (VK 바이트는 무관).
    private static bool VerifySignature(FileStream fs)
    {
        foreach (var vkOff in new[] { LeftVkOffset, RightVkOffset, UpVkOffset, DownVkOffset })
        {
            if (ReadByte(fs, vkOff - 1) != 0xB9) return false;
            if (ReadByte(fs, vkOff + 1) != 0x00) return false;
            if (ReadByte(fs, vkOff + 2) != 0x00) return false;
            if (ReadByte(fs, vkOff + 3) != 0x00) return false;
        }
        return true;
    }

    private static byte ReadByte(FileStream fs, long off)
    {
        fs.Seek(off, SeekOrigin.Begin);
        var b = fs.ReadByte();
        if (b < 0)
            throw new EndOfStreamException();
        return (byte)b;
    }
}
