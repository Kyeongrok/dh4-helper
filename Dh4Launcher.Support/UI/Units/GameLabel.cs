using System.Windows;
using System.Windows.Controls;

namespace Dh4Launcher.Support.UI.Units;

/// <summary>게임 UI 풍 레이블 (남색 명판 + 금색 테두리). Content에 텍스트.</summary>
public class GameLabel : ContentControl
{
    static GameLabel()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GameLabel),
            new FrameworkPropertyMetadata(typeof(GameLabel)));
    }
}
