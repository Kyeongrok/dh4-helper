using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dh4Launcher.Forms.Services;
using Microsoft.Win32;

namespace Dh4Launcher.Forms.ViewModels;

public partial class PortraitViewModel : ObservableObject
{
    private readonly IPortraitService _portraits;
    private readonly IGameLauncherService _launcher;
    private bool _loaded;
    private string? _portraitPath;

    public ObservableCollection<PortraitItem> Portraits { get; } = [];

    /// <summary>편집할 초상화 파일 목록 (얼굴/대형).</summary>
    public ObservableCollection<PortraitFile> Files { get; } = [];

    [ObservableProperty]
    private PortraitFile? _selectedFile;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReplaceCommand))]
    private PortraitItem? _selected;

    [ObservableProperty]
    private BitmapSource? _fullImage;

    [ObservableProperty]
    private string _status = string.Empty;

    public PortraitViewModel(IPortraitService portraits, IGameLauncherService launcher)
    {
        _portraits = portraits;
        _launcher = launcher;
    }

    /// <summary>초상화 탭이 처음 열릴 때 호출 — 파일 목록 채우고 첫 파일 로드.</summary>
    public void EnsureLoaded()
    {
        if (_loaded)
            return;
        _loaded = true;

        Files.Clear();
        foreach (var f in _portraits.FindFiles(_launcher.GameDirectory))
            Files.Add(f);

        if (Files.Count == 0)
        {
            Status = "초상화 파일(bustup.dk4 / Portrait.dk4)을 찾을 수 없습니다. (게임 폴더 지정/실행 필요)";
            return;
        }
        SelectedFile = Files[0]; // 변경 핸들러가 로드
    }

    partial void OnSelectedFileChanged(PortraitFile? value)
    {
        _portraitPath = value?.Path;
        _ = LoadGalleryAsync();
    }

    private async System.Threading.Tasks.Task LoadGalleryAsync()
    {
        Selected = null;
        FullImage = null;
        Portraits.Clear();
        if (_portraitPath is null)
            return;

        Status = "초상화 불러오는 중...";
        try
        {
            var path = _portraitPath;
            var items = await System.Threading.Tasks.Task.Run(() => _portraits.Load(path));
            foreach (var it in items)
                Portraits.Add(it);
            Status = $"{Portraits.Count}개 초상화";
        }
        catch (Exception ex)
        {
            Status = $"불러오기 실패: {ex.Message}";
        }
    }

    partial void OnSelectedChanged(PortraitItem? value)
    {
        FullImage = null;
        if (value is null || _portraitPath is null)
            return;
        try
        {
            FullImage = _portraits.DecodeFull(_portraitPath, value.Index);
            Status = $"{value.Index}번 · {value.Width}×{value.Height}";
        }
        catch (Exception ex)
        {
            Status = $"미리보기 실패: {ex.Message}";
        }
    }

    private bool HasSelection() => Selected is not null && _portraitPath is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Export()
    {
        if (Selected is null || _portraitPath is null)
            return;
        var dialog = new SaveFileDialog
        {
            Title = "초상화 PNG로 내보내기",
            Filter = "PNG 이미지|*.png",
            FileName = $"portrait_{Selected.Index:00}.png",
        };
        if (dialog.ShowDialog() != true)
            return;
        try
        {
            _portraits.ExportPng(_portraitPath, Selected.Index, dialog.FileName);
            Status = $"내보냄: {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            Status = $"내보내기 실패: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Replace()
    {
        if (Selected is null || _portraitPath is null)
            return;
        var dialog = new OpenFileDialog
        {
            Title = $"{Selected.Index}번 초상화로 넣을 이미지 선택 ({Selected.Width}×{Selected.Height} 권장)",
            Filter = "PNG 이미지 (*.png)|*.png|모든 이미지|*.png;*.jpg;*.jpeg;*.bmp;*.gif|모든 파일 (*.*)|*.*",
        };
        if (dialog.ShowDialog() != true)
            return;

        var index = Selected.Index;
        try
        {
            _portraits.Replace(_portraitPath, index, dialog.FileName);
            // 썸네일 + 미리보기 갱신
            var newFull = _portraits.DecodeFull(_portraitPath, index);
            FullImage = newFull;
            var refreshed = _portraits.Load(_portraitPath)[index];
            var slot = Portraits.IndexOf(Selected);
            if (slot >= 0)
            {
                Portraits[slot] = refreshed;
                Selected = refreshed;
            }
            Status = $"{index}번 교체 완료 (원본은 portrait_backup 폴더에 백업됨)";
        }
        catch (IOException)
        {
            Status = "파일이 잠겨 있습니다 (게임 종료 후 다시 시도).";
        }
        catch (Exception ex)
        {
            Status = $"교체 실패: {ex.Message}";
        }
    }
}
