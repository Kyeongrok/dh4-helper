using System.Windows;
using System.Windows.Controls;

namespace Dh4Launcher.Support.UI.Units;

/// <summary>갈색/금색 그라데이션 테두리 프레임 + 파란 내부 패널 스타일의 게임 버튼.</summary>
public class GameFrameButton : Button
{
    static GameFrameButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GameFrameButton),
            new FrameworkPropertyMetadata(typeof(GameFrameButton)));
    }
}
