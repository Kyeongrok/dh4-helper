using System.Windows;
using System.Windows.Controls;
using Dh4Launcher.Forms.ViewModels;
using Dh4Launcher.Support.UI.Units;

namespace Dh4Launcher.Forms.UI.Views;

public class MainWindow : Dh4LauncherWindow
{
    static MainWindow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MainWindow),
            new FrameworkPropertyMetadata(typeof(MainWindow)));
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        // 창 크기/시작 위치는 테마 스타일 Setter 로는 초기 크기에 반영되지 않으므로 여기서 로컬 값으로 지정한다.
        Width = 600;
        Height = 660;
        MinWidth = 480;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        DataContext = viewModel;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        var minimizeButton = GetTemplateChild("PART_MinimizeButton") as Button;
        if (minimizeButton != null)
            minimizeButton.Click += (s, e) => WindowState = System.Windows.WindowState.Minimized;

        var maximizeButton = GetTemplateChild("PART_MaximizeButton") as Button;
        if (maximizeButton != null)
            maximizeButton.Click += (s, e) =>
                WindowState = WindowState == System.Windows.WindowState.Maximized
                    ? System.Windows.WindowState.Normal
                    : System.Windows.WindowState.Maximized;

        var closeButton = GetTemplateChild("PART_CloseButton") as Button;
        if (closeButton != null)
            closeButton.Click += (s, e) => Close();
    }
}
