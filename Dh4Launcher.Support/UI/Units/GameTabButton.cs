using System.Windows;
using System.Windows.Controls;

namespace Dh4Launcher.Support.UI.Units;

/// <summary>게임 UI 풍 탭 버튼 (광택 파란 명판 + 밝은 크림 테두리).</summary>
public class GameTabButton : Button
{
    static GameTabButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GameTabButton),
            new FrameworkPropertyMetadata(typeof(GameTabButton)));
    }
}
