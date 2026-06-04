using System.Windows.Media.Imaging;

namespace Dh4Launcher.Forms.Services;

/// <summary>초상화 한 개의 정보(인덱스/크기/썸네일).</summary>
public record PortraitItem(int Index, int Width, int Height, BitmapSource Thumbnail);

/// <summary>편집 가능한 초상화 파일(표시명 + 경로).</summary>
public record PortraitFile(string Display, string Path);

public interface IPortraitService
{
    /// <summary>게임 폴더의 첫 초상화 파일 경로. 없으면 null.</summary>
    string? FindPortraitFile(string? gameDirectory);

    /// <summary>게임 폴더의 편집 가능한 초상화 파일들(bustup/Portrait).</summary>
    IReadOnlyList<PortraitFile> FindFiles(string? gameDirectory);

    /// <summary>게임 폴더의 이벤트 컷신 CG 파일들(EventBG1~8 / EventBGEX).</summary>
    IReadOnlyList<PortraitFile> FindCutsceneFiles(string? gameDirectory);

    /// <summary>모든 초상화를 디코드해 썸네일 목록을 만든다.</summary>
    IReadOnlyList<PortraitItem> Load(string portraitPath, int thumbWidth = 110);

    /// <summary>해당 인덱스 초상화를 원본 크기로 디코드.</summary>
    BitmapSource DecodeFull(string portraitPath, int index);

    /// <summary>해당 인덱스 초상화를 PNG로 내보낸다.</summary>
    void ExportPng(string portraitPath, int index, string outPng);

    /// <summary>이미지 파일로 해당 인덱스 초상화를 교체한다(최초 1회 .bak 백업, 같은 크기 in-place).</summary>
    void Replace(string portraitPath, int index, string imagePath);
}
