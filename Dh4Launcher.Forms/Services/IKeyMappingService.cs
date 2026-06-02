namespace Dh4Launcher.Forms.Services;

/// <summary>방향 키 현재 매핑. 좌/우=돛 조타, 위/아래=배 선회. Vk = Virtual-Key 코드.</summary>
public record KeyMapState(byte LeftVk, byte RightVk, byte UpVk, byte DownVk);

public interface IKeyMappingService
{
    /// <summary>원본 기본값: ←0x25 →0x27 ↑0x26 ↓0x28.</summary>
    byte DefaultLeftVk { get; }
    byte DefaultRightVk { get; }
    byte DefaultUpVk { get; }
    byte DefaultDownVk { get; }

    /// <summary>exe가 패치 가능한(시그니처 일치) DK4HD_kr.exe 인지.</summary>
    bool IsSupported(string exePath);

    /// <summary>현재 4방향 키 매핑을 읽는다. 미지원이면 null.</summary>
    KeyMapState? Read(string exePath);

    /// <summary>4방향 키를 지정한 VK 코드로 패치한다(최초 1회 .bak 백업).</summary>
    void Apply(string exePath, byte leftVk, byte rightVk, byte upVk, byte downVk);

    /// <summary>게임(DK4HD_kr)이 실행 중이라 파일이 잠겨 있는지.</summary>
    bool IsGameRunning();
}
