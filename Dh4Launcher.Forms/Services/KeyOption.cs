namespace Dh4Launcher.Forms.Services;

/// <summary>키 선택 콤보 항목. Vk = Windows Virtual-Key 코드.</summary>
public record KeyOption(string Display, byte Vk)
{
    public override string ToString() => Display;
}
