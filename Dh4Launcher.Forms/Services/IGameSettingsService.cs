namespace Dh4Launcher.Forms.Services;

public interface IGameSettingsService
{
    /// <summary>현재 레지스트리에 저장된 화면 설정을 읽는다. 키가 없으면 기본값.</summary>
    GameSettings Load();

    /// <summary>화면 설정을 레지스트리에 기록한다. 다른 값(언어/사운드 등)은 보존된다.</summary>
    void Save(GameSettings settings);
}
