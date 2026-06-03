using System.Windows;
using System.Windows.Controls;

namespace Dh4Launcher.Support.UI.Units;

/// <summary>대항해시대 게임 UI 풍의 버튼 (파란 광택 명판 + 금색 장식 끝, hover 시 밝아짐).</summary>
public class GameButton : Button
{
    static GameButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GameButton),
            new FrameworkPropertyMetadata(typeof(GameButton)));
    }
}
