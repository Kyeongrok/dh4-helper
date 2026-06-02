using System.Windows;

namespace Dh4Launcher.Support.UI.Units;

public class Dh4LauncherWindow : Window
{
    static Dh4LauncherWindow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Dh4LauncherWindow),
            new FrameworkPropertyMetadata(typeof(Dh4LauncherWindow)));
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Maximized)
            MaxHeight = SystemParameters.WorkArea.Height;
        else
            MaxHeight = double.PositiveInfinity;
    }
}
