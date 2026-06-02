using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dh4Launcher.Forms.Services;
using Microsoft.Win32;

namespace Dh4Launcher.Forms.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IGameSettingsService _settings;
    private readonly IGameLauncherService _launcher;
    private readonly IGpuService _gpu;

    private readonly string? _gpuName;
    private bool GpuExists => _gpuName is not null;

    /// <summary>
    /// 선택 가능한 해상도. 원본 런처와 달리 1920x1080 위쪽(QHD/4K)도 포함한다.
    /// </summary>
    public ObservableCollection<ScreenResolution> Resolutions { get; } =
    [
        new(1280, 720),
        new(1600, 900),
        new(1920, 1080),
        new(1920, 1200),
        new(2560, 1440),
        new(2560, 1600),
        new(3840, 2160),
    ];

    public ObservableCollection<GameLanguageOption> Languages { get; } =
    [
        new(GameLanguage.Japanese, "일본어"),
        new(GameLanguage.Korean, "한국어"),
        new(GameLanguage.SimplifiedChinese, "중국어(간체)"),
        new(GameLanguage.TraditionalChinese, "중국어(번체)"),
    ];

    [ObservableProperty]
    private ScreenResolution? _selectedResolution;

    [ObservableProperty]
    private GameLanguageOption? _selectedLanguage;

    [ObservableProperty]
    private bool _isFullscreen;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _gpuStatus = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyGpuCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelGpuCommand))]
    private bool _isGpuApplied;

    public MainWindowViewModel(IGameSettingsService settings, IGameLauncherService launcher, IGpuService gpu)
    {
        _settings = settings;
        _launcher = launcher;
        _gpu = gpu;
        _gpuName = _gpu.HighPerformanceGpuName;

        SelectedLanguage = Languages.First(l => l.Language == GameLanguage.Korean);
        Load();
        RefreshGpuStatus();
    }

    // 언어를 바꾸면 실행 파일이 달라지므로 GPU 적용 상태를 다시 읽는다.
    partial void OnSelectedLanguageChanged(GameLanguageOption? value) => RefreshGpuStatus();

    private void RefreshGpuStatus()
    {
        if (!GpuExists)
        {
            IsGpuApplied = false;
            GpuStatus = "고성능 GPU: 없음 (내장 그래픽만 감지됨)";
            return;
        }

        var language = SelectedLanguage?.Language ?? GameLanguage.Korean;
        var exe = _launcher.FindExecutable(language);
        IsGpuApplied = exe is not null && _launcher.IsHighPerformanceGpu(exe);
        GpuStatus = $"고성능 GPU: {_gpuName} · {(IsGpuApplied ? "적용됨" : "미적용")}";
    }

    private void Load()
    {
        var current = _settings.Load();

        // 현재 저장된 해상도가 목록에 없으면(원본 런처에 없던 커스텀 값) 동적으로 추가한다.
        SelectedResolution = Resolutions.FirstOrDefault(
            r => r.Width == current.ScreenWidth && r.Height == current.ScreenHeight);

        if (SelectedResolution is null)
        {
            var custom = new ScreenResolution(current.ScreenWidth, current.ScreenHeight);
            Resolutions.Add(custom);
            SelectedResolution = custom;
        }

        IsFullscreen = current.ScreenMode == 1;
    }

    [RelayCommand]
    private void Save()
    {
        if (SelectedResolution is null)
            return;

        _settings.Save(new GameSettings
        {
            ScreenWidth = SelectedResolution.Width,
            ScreenHeight = SelectedResolution.Height,
            ScreenMode = IsFullscreen ? 1 : 0,
        });

        StatusMessage = $"저장됨: {SelectedResolution} · {(IsFullscreen ? "전체화면" : "창 모드")}";
    }

    [RelayCommand]
    private void Launch()
    {
        // 실행 직전에 현재 화면 설정도 함께 반영한다.
        Save();

        var exePath = ResolveExecutable();
        if (exePath is null)
        {
            StatusMessage = "게임 실행 파일을 찾지 못했습니다.";
            return;
        }

        try
        {
            _launcher.Launch(exePath);
            StatusMessage = $"게임 실행: {Path.GetFileName(exePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"실행 실패: {ex.Message}";
        }
    }

    private bool CanApplyGpu() => GpuExists && !IsGpuApplied;

    [RelayCommand(CanExecute = nameof(CanApplyGpu))]
    private void ApplyGpu()
    {
        var exe = ResolveExecutable();
        if (exe is null)
        {
            StatusMessage = "게임 실행 파일을 찾지 못했습니다.";
            return;
        }

        _launcher.SetHighPerformanceGpu(exe, true);
        RefreshGpuStatus();
        StatusMessage = $"고성능 GPU 적용: {Path.GetFileName(exe)}";
    }

    private bool CanCancelGpu() => GpuExists && IsGpuApplied;

    [RelayCommand(CanExecute = nameof(CanCancelGpu))]
    private void CancelGpu()
    {
        var exe = ResolveExecutable();
        if (exe is null)
        {
            StatusMessage = "게임 실행 파일을 찾지 못했습니다.";
            return;
        }

        _launcher.SetHighPerformanceGpu(exe, false);
        RefreshGpuStatus();
        StatusMessage = $"고성능 GPU 해제: {Path.GetFileName(exe)}";
    }

    /// <summary>선택된 언어의 실행 파일 경로를 찾고, 없으면 파일 선택 후 폴더를 기억한다.</summary>
    private string? ResolveExecutable()
    {
        var language = SelectedLanguage?.Language ?? GameLanguage.Korean;
        var exe = _launcher.FindExecutable(language);
        if (exe is not null)
            return exe;

        var fileName = _launcher.GetExeFileName(language);
        var dialog = new OpenFileDialog
        {
            Title = "게임 실행 파일을 선택하세요",
            Filter = $"{fileName}|{fileName}|실행 파일 (*.exe)|*.exe",
        };

        if (dialog.ShowDialog() != true)
            return null;

        _launcher.GameDirectory = Path.GetDirectoryName(dialog.FileName);
        return dialog.FileName;
    }
}

public record GameLanguageOption(GameLanguage Language, string Display)
{
    public override string ToString() => Display;
}
