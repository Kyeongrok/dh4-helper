namespace Dh4Launcher.Forms.Services;

/// <summary>
/// 키 매핑 상태. 좌/우=돛 조타, 위/아래=배 선회, 지도(F1)/F2/F3/F4/F5=기능 키,
/// Num*=넘패드 기능 보조 키(넘패드 없는 키보드용). Vk = Virtual-Key 코드.
/// </summary>
public record KeyMapState(byte LeftVk, byte RightVk, byte UpVk, byte DownVk, byte MapVk,
    byte NumPlusVk, byte NumMinusVk, byte Num4Vk, byte Num6Vk,
    byte F2Vk, byte F3Vk, byte F4Vk, byte F5Vk,
    byte Num7Vk, byte Num9Vk, byte Num1Vk, byte Num3Vk);

public interface IKeyMappingService
{
    /// <summary>원본 기본값: ←0x25 →0x27 ↑0x26 ↓0x28, 지도 F1=0x70, F2~F5=0x71~0x74, 넘패드 +/−/4/6.</summary>
    byte DefaultLeftVk { get; }
    byte DefaultRightVk { get; }
    byte DefaultUpVk { get; }
    byte DefaultDownVk { get; }
    byte DefaultMapVk { get; }
    byte DefaultNumPlusVk { get; }
    byte DefaultNumMinusVk { get; }
    byte DefaultNum4Vk { get; }
    byte DefaultNum6Vk { get; }
    byte DefaultF2Vk { get; }
    byte DefaultF3Vk { get; }
    byte DefaultF4Vk { get; }
    byte DefaultF5Vk { get; }
    byte DefaultNum7Vk { get; }
    byte DefaultNum9Vk { get; }
    byte DefaultNum1Vk { get; }
    byte DefaultNum3Vk { get; }

    /// <summary>exe가 패치 가능한(시그니처 일치) DK4HD_kr.exe 인지.</summary>
    bool IsSupported(string exePath);

    /// <summary>현재 키 매핑을 읽는다. 미지원이면 null.</summary>
    KeyMapState? Read(string exePath);

    /// <summary>키들을 지정한 VK 코드로 패치한다(최초 1회 .bak 백업).</summary>
    void Apply(string exePath, byte leftVk, byte rightVk, byte upVk, byte downVk, byte mapVk,
        byte numPlusVk, byte numMinusVk, byte num4Vk, byte num6Vk,
        byte f2Vk, byte f3Vk, byte f4Vk, byte f5Vk,
        byte num7Vk, byte num9Vk, byte num1Vk, byte num3Vk);

    /// <summary>게임(DK4HD_kr)이 실행 중이라 파일이 잠겨 있는지.</summary>
    bool IsGameRunning();
}
