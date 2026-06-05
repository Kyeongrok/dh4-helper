using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        Height = 780;
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

        WireMapEditor();
    }

    /// <summary>지도 편집기: 뷰포트 타일 렌더링 + 마우스 칠하기/팬/줌.</summary>
    private void WireMapEditor()
    {
        var image = GetTemplateChild("PART_MapImage") as Image;
        var host = GetTemplateChild("PART_MapHost") as FrameworkElement;
        if (image is null || host is null)
            return;

        WorldMapViewModel? Map() => (DataContext as MainWindowViewModel)?.Map;

        // 캔버스 크기 = 호스트 크기
        host.SizeChanged += (s, e) =>
            Map()?.SetViewport((int)host.ActualWidth, (int)host.ActualHeight);

        Point lastPan = default;

        image.MouseLeftButtonDown += (s, e) =>
        {
            var p = e.GetPosition(image);
            Map()?.PaintAtScreen(p.X, p.Y);
            image.CaptureMouse();
        };
        image.MouseLeftButtonUp += (s, e) => image.ReleaseMouseCapture();

        image.MouseRightButtonDown += (s, e) =>
        {
            lastPan = e.GetPosition(image);
            image.CaptureMouse();
        };
        image.MouseRightButtonUp += (s, e) => image.ReleaseMouseCapture();

        image.MouseMove += (s, e) =>
        {
            var map = Map();
            if (map is null)
                return;
            var p = e.GetPosition(image);
            map.HoverAtScreen(p.X, p.Y);

            if (e.LeftButton == MouseButtonState.Pressed)
                map.PaintAtScreen(p.X, p.Y);
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                map.PanByPixels(p.X - lastPan.X, p.Y - lastPan.Y);
                lastPan = p;
            }
        };

        // Ctrl+휠 = 커서 기준 확대/축소, Shift+휠 = 가로 이동, 그냥 휠 = 세로 이동
        host.PreviewMouseWheel += (s, e) =>
        {
            var map = Map();
            if (map is null)
                return;
            e.Handled = true;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                var p = e.GetPosition(image);
                map.ZoomAtCursor(e.Delta, p.X, p.Y);
            }
            else
            {
                map.WheelPan(e.Delta, (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
            }
        };
    }
}
