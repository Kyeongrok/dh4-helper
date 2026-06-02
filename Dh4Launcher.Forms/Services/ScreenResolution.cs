namespace Dh4Launcher.Forms.Services;

/// <summary>콤보박스에 노출할 해상도 항목.</summary>
public record ScreenResolution(int Width, int Height)
{
    public override string ToString() => $"{Width} x {Height}";
}
