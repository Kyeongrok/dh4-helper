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

    /// <summary>지도 편집기: 이미지 위 마우스로 타일 칠하기 + 바로가기 스크롤.</summary>
    private void WireMapEditor()
    {
        var image = GetTemplateChild("PART_MapImage") as Image;
        var scroll = GetTemplateChild("PART_MapScroll") as ScrollViewer;
        if (image is null || scroll is null)
            return;

        WorldMapViewModel? Map() => (DataContext as MainWindowViewModel)?.Map;

        (int x, int y) Tile(MouseEventArgs e)
        {
            var p = e.GetPosition(image); // 이미지 로컬 좌표(줌 무관) = 타일 좌표
            return ((int)p.X, (int)p.Y);
        }

        image.MouseLeftButtonDown += (s, e) =>
        {
            var (x, y) = Tile(e);
            Map()?.PaintAt(x, y);
            image.CaptureMouse();
        };
        image.MouseLeftButtonUp += (s, e) => image.ReleaseMouseCapture();
        image.MouseMove += (s, e) =>
        {
            var (x, y) = Tile(e);
            var map = Map();
            if (map is null)
                return;
            map.HoverAt(x, y);
            if (e.LeftButton == MouseButtonState.Pressed)
                map.PaintAt(x, y);
        };

        var map = Map();
        if (map is not null)
            map.JumpRequested += (tx, ty) =>
            {
                double z = map.Zoom;
                scroll.ScrollToHorizontalOffset(tx * z - scroll.ViewportWidth / 2);
                scroll.ScrollToVerticalOffset(ty * z - scroll.ViewportHeight / 2);
            };

        // Ctrl + 휠 = 확대/축소(커서 위치 기준). Ctrl 없으면 일반 스크롤.
        scroll.PreviewMouseWheel += (s, e) =>
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                return;
            e.Handled = true;
            var m = Map();
            if (m is null)
                return;

            var tile = e.GetPosition(image);      // 이미지 로컬 = 타일 좌표(줌 무관)
            var vp = e.GetPosition(scroll);        // 뷰포트 내 커서 위치(px)
            double old = m.Zoom;
            if (e.Delta > 0)
                m.ZoomInCommand.Execute(null);
            else
                m.ZoomOutCommand.Execute(null);
            if (m.Zoom == old)
                return;

            scroll.UpdateLayout(); // 새 줌으로 콘텐츠 크기 갱신 후 오프셋 설정
            scroll.ScrollToHorizontalOffset(tile.X * m.Zoom - vp.X);
            scroll.ScrollToVerticalOffset(tile.Y * m.Zoom - vp.Y);
        };
    }
}
