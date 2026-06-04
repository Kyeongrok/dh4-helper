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
    protected readonly IPortraitService _portraits;
    protected readonly IGameLauncherService _launcher;
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
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
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

    /// <summary>편집할 파일 목록 — 파생 VM에서 컷신 등 다른 대상으로 교체 가능.</summary>
    protected virtual IReadOnlyList<PortraitFile> DiscoverFiles()
        => _portraits.FindFiles(_launcher.GameDirectory);

    /// <summary>대상 파일을 못 찾았을 때 표시할 안내.</summary>
    protected virtual string NotFoundMessage
        => "초상화 파일(bustup.dk4 / Portrait.dk4)을 찾을 수 없습니다. (게임 폴더 지정/실행 필요)";

    /// <summary>탭이 처음 열릴 때 호출 — 파일 목록 채우고 첫 파일 로드.</summary>
    public void EnsureLoaded()
    {
        if (_loaded)
            return;
        _loaded = true;

        Files.Clear();
        foreach (var f in DiscoverFiles())
            Files.Add(f);

        if (Files.Count == 0)
        {
            Status = NotFoundMessage;
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
            RefreshSlot(index);
            RestoreCommand.NotifyCanExecuteChanged(); // 교체로 백업이 생겼으니 되돌리기 활성화
            Status = $"{index}번 교체 완료 (원본 자동 백업됨 · '원본으로 되돌리기'로 복구 가능)";
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

    private bool CanRestore() => HasSelection() && _portraits.HasBackup(_portraitPath!);

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private void Restore()
    {
        if (Selected is null || _portraitPath is null)
            return;
        var index = Selected.Index;
        try
        {
            if (!_portraits.Restore(_portraitPath, index))
            {
                Status = "백업이 없습니다 (이 파일은 교체한 적이 없습니다).";
                return;
            }
            RefreshSlot(index);
            Status = $"{index}번 원본으로 되돌림";
        }
        catch (IOException)
        {
            Status = "파일이 잠겨 있습니다 (게임 종료 후 다시 시도).";
        }
        catch (Exception ex)
        {
            Status = $"되돌리기 실패: {ex.Message}";
        }
    }

    /// <summary>교체/복원 후 해당 인덱스의 미리보기·썸네일만 갱신한다.</summary>
    private void RefreshSlot(int index)
    {
        FullImage = _portraits.DecodeFull(_portraitPath!, index);
        var refreshed = _portraits.LoadOne(_portraitPath!, index);
        var old = Selected;
        var slot = old is null ? -1 : Portraits.IndexOf(old);
        if (slot >= 0)
        {
            Portraits[slot] = refreshed;
            Selected = refreshed;
        }
    }
}
