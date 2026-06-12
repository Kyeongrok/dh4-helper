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
///   0x422F4 = 지도    : mov ecx, 0x70 (VK_F1)
///   0x42315 = 기능F2  : mov ecx, 0x71 (VK_F2)
///   0x42336 = 기능F3  : mov ecx, 0x72 (VK_F3)
///   0x42357 = 기능F4  : mov ecx, 0x73 (VK_F4)
///   0x42378 = 기능F5  : mov ecx, 0x74 (VK_F5)
/// 기능 키(F1~F5)는 0x21바이트 간격으로 연속된 폴링 테이블에 위치한다.
/// 각 위치는 'B9 [VK] 00 00 00' 형태이며, VK 바이트만 바꾼다.
/// 안전을 위해 주변 바이트(B9 .. 00 00 00) 시그니처를 검증한 뒤에만 패치한다.
/// </summary>
public class KeyMappingService : IKeyMappingService
{
    private const long LeftVkOffset = 0x42216;
    private const long RightVkOffset = 0x42237;
    private const long UpVkOffset = 0x421A9;
    private const long DownVkOffset = 0x421E9;
    private const long MapVkOffset = 0x422F4;
    private const long F2VkOffset = 0x42315;
    private const long F3VkOffset = 0x42336;
    private const long F4VkOffset = 0x42357;
    private const long F5VkOffset = 0x42378;

    // 보조(넘패드 기능) 키 — 넘패드 없는 키보드용으로 글자키 재매핑.
    private const long NumPlusVkOffset = 0x42102;  // 넘패드 +  (슬롯2)
    private const long NumMinusVkOffset = 0x4212B; // 넘패드 −  (슬롯3)
    private const long Num4VkOffset = 0x42154;     // 넘패드 4  (슬롯7)
    private const long Num6VkOffset = 0x42175;     // 넘패드 6  (슬롯5)
    private const long Num7VkOffset = 0x42258;     // 넘패드 7
    private const long Num9VkOffset = 0x4227F;     // 넘패드 9
    private const long Num3VkOffset = 0x422A6;     // 넘패드 3
    private const long Num1VkOffset = 0x422CD;     // 넘패드 1

    public byte DefaultLeftVk => 0x25;  // VK_LEFT
    public byte DefaultRightVk => 0x27; // VK_RIGHT
    public byte DefaultUpVk => 0x26;    // VK_UP
    public byte DefaultDownVk => 0x28;  // VK_DOWN
    public byte DefaultMapVk => 0x70;   // VK_F1
    public byte DefaultNumPlusVk => 0x6B;  // VK_ADD
    public byte DefaultNumMinusVk => 0x6D; // VK_SUBTRACT
    public byte DefaultNum4Vk => 0x64;     // VK_NUMPAD4
    public byte DefaultNum6Vk => 0x66;     // VK_NUMPAD6
    public byte DefaultF2Vk => 0x71;       // VK_F2
    public byte DefaultF3Vk => 0x72;       // VK_F3
    public byte DefaultF4Vk => 0x73;       // VK_F4
    public byte DefaultF5Vk => 0x74;       // VK_F5
    public byte DefaultNum7Vk => 0x67;     // VK_NUMPAD7
    public byte DefaultNum9Vk => 0x69;     // VK_NUMPAD9
    public byte DefaultNum1Vk => 0x61;     // VK_NUMPAD1
    public byte DefaultNum3Vk => 0x63;     // VK_NUMPAD3

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
                ReadByte(fs, UpVkOffset), ReadByte(fs, DownVkOffset),
                ReadByte(fs, MapVkOffset),
                ReadByte(fs, NumPlusVkOffset), ReadByte(fs, NumMinusVkOffset),
                ReadByte(fs, Num4VkOffset), ReadByte(fs, Num6VkOffset),
                ReadByte(fs, F2VkOffset), ReadByte(fs, F3VkOffset),
                ReadByte(fs, F4VkOffset), ReadByte(fs, F5VkOffset),
                ReadByte(fs, Num7VkOffset), ReadByte(fs, Num9VkOffset),
                ReadByte(fs, Num1VkOffset), ReadByte(fs, Num3VkOffset));
        }
        catch
        {
            return null;
        }
    }

    public void Apply(string exePath, byte leftVk, byte rightVk, byte upVk, byte downVk, byte mapVk,
        byte numPlusVk, byte numMinusVk, byte num4Vk, byte num6Vk,
        byte f2Vk, byte f3Vk, byte f4Vk, byte f5Vk,
        byte num7Vk, byte num9Vk, byte num1Vk, byte num3Vk)
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
        Write(fs, MapVkOffset, mapVk);
        Write(fs, NumPlusVkOffset, numPlusVk);
        Write(fs, NumMinusVkOffset, numMinusVk);
        Write(fs, Num4VkOffset, num4Vk);
        Write(fs, Num6VkOffset, num6Vk);
        Write(fs, F2VkOffset, f2Vk);
        Write(fs, F3VkOffset, f3Vk);
        Write(fs, F4VkOffset, f4Vk);
        Write(fs, F5VkOffset, f5Vk);
        Write(fs, Num7VkOffset, num7Vk);
        Write(fs, Num9VkOffset, num9Vk);
        Write(fs, Num1VkOffset, num1Vk);
        Write(fs, Num3VkOffset, num3Vk);
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
        foreach (var vkOff in new[] { LeftVkOffset, RightVkOffset, UpVkOffset, DownVkOffset, MapVkOffset,
                     NumPlusVkOffset, NumMinusVkOffset, Num4VkOffset, Num6VkOffset,
                     F2VkOffset, F3VkOffset, F4VkOffset, F5VkOffset,
                     Num7VkOffset, Num9VkOffset, Num1VkOffset, Num3VkOffset })
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
