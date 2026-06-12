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
    private readonly IKeyMappingService _keymap;
    private readonly IUpdateService _updates;

    private readonly string? _gpuName;
    private bool GpuExists => _gpuName is not null;

    private bool _keyMapAvailable;

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

    /// <summary>0 = 설정, 1 = 키설정, 2 = 초상화, 3 = 컷신, 4 = 지도, 5 = 도시, 6 = 건물.</summary>
    [ObservableProperty]
    private int _selectedTab;

    public PortraitViewModel Portrait { get; }

    public CutsceneViewModel Cutscene { get; }

    public WorldMapViewModel Map { get; }

    public TownViewModel Town { get; }

    public BuildingViewModel Building { get; }

    partial void OnSelectedTabChanged(int value)
    {
        if (value == 2)
            Portrait.EnsureLoaded();
        else if (value == 3)
            Cutscene.EnsureLoaded();
        else if (value == 4)
            Map.EnsureLoaded();
        else if (value == 5)
            Town.EnsureLoaded();
        else if (value == 6)
            Building.EnsureLoaded();
    }

    [RelayCommand]
    private void SelectTab(object? index) => SelectedTab = System.Convert.ToInt32(index);

    [ObservableProperty]
    private string _gamePath = string.Empty;

    [ObservableProperty]
    private string _updateStatus = string.Empty;

    [ObservableProperty]
    private string _gpuStatus = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyGpuCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelGpuCommand))]
    private bool _isGpuApplied;

    /// <summary>키 매핑에 지정 가능한 키 목록 (Vk = Virtual-Key 코드).</summary>
    public ObservableCollection<KeyOption> Keys { get; } =
    [
        new("A", 0x41), new("D", 0x44), new("W", 0x57), new("S", 0x53),
        new("Q", 0x51), new("E", 0x45), new("Z", 0x5A), new("X", 0x58),
        new("R", 0x52), new("F", 0x46), new("C", 0x43), new("V", 0x56),
        new("M", 0x4D), new("T", 0x54), new("G", 0x47), new("B", 0x42),
        new("1", 0x31), new("2", 0x32), new("3", 0x33), new("4", 0x34), new("5", 0x35),
        new("6", 0x36), new("7", 0x37), new("8", 0x38), new("9", 0x39), new("0", 0x30),
        new("← 왼쪽 화살표", 0x25), new("→ 오른쪽 화살표", 0x27),
        new("↑ 위 화살표", 0x26), new("↓ 아래 화살표", 0x28),
        new("F1", 0x70), new("F2", 0x71), new("F3", 0x72), new("F4", 0x73), new("F5", 0x74),
        new("넘패드 +", 0x6B), new("넘패드 −", 0x6D), new("넘패드 4", 0x64), new("넘패드 6", 0x66),
        new("넘패드 7", 0x67), new("넘패드 9", 0x69), new("넘패드 1", 0x61), new("넘패드 3", 0x63),
    ];

    [ObservableProperty]
    private KeyOption? _leftKey;

    [ObservableProperty]
    private KeyOption? _rightKey;

    [ObservableProperty]
    private KeyOption? _upKey;

    [ObservableProperty]
    private KeyOption? _downKey;

    [ObservableProperty]
    private KeyOption? _mapKey;

    [ObservableProperty]
    private KeyOption? _f2Key;

    [ObservableProperty]
    private KeyOption? _f3Key;

    [ObservableProperty]
    private KeyOption? _f4Key;

    [ObservableProperty]
    private KeyOption? _f5Key;

    [ObservableProperty]
    private KeyOption? _numPlusKey;

    [ObservableProperty]
    private KeyOption? _numMinusKey;

    [ObservableProperty]
    private KeyOption? _num4Key;

    [ObservableProperty]
    private KeyOption? _num6Key;

    [ObservableProperty]
    private KeyOption? _num7Key;

    [ObservableProperty]
    private KeyOption? _num9Key;

    [ObservableProperty]
    private KeyOption? _num1Key;

    [ObservableProperty]
    private KeyOption? _num3Key;

    [ObservableProperty]
    private string _keyMapStatus = string.Empty;

    public MainWindowViewModel(IGameSettingsService settings, IGameLauncherService launcher,
        IGpuService gpu, IKeyMappingService keymap, IUpdateService updates,
        PortraitViewModel portrait, CutsceneViewModel cutscene, WorldMapViewModel map,
        TownViewModel town, BuildingViewModel building)
    {
        _settings = settings;
        _launcher = launcher;
        _gpu = gpu;
        _keymap = keymap;
        _updates = updates;
        Portrait = portrait;
        Cutscene = cutscene;
        Map = map;
        Town = town;
        Building = building;
        _gpuName = _gpu.HighPerformanceGpuName;

        SelectedLanguage = Languages.First(l => l.Language == GameLanguage.Korean);
        Load();
        RefreshAll();
        _ = CheckForUpdatesAsync();
    }

    /// <summary>게임 경로 표시 + GPU/키매핑 상태를 현재 폴더 기준으로 새로고침.</summary>
    private void RefreshAll()
    {
        GamePath = string.IsNullOrEmpty(_launcher.GameDirectory)
            ? "(미설정 — '게임 실행' 또는 '폴더 변경'으로 지정)"
            : _launcher.GameDirectory!;
        RefreshGpuStatus();
        LoadKeyMap();
    }

    /// <summary>게임 실행 파일(폴더)을 사용자가 직접 다시 지정한다.</summary>
    [RelayCommand]
    private void ChangeGamePath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "게임 실행 파일 선택 (DK4HD_*.exe)",
            Filter = "DK4HD 실행 파일|DK4HD_*.exe|실행 파일 (*.exe)|*.exe",
        };

        if (dialog.ShowDialog() != true)
            return;

        _launcher.GameDirectory = Path.GetDirectoryName(dialog.FileName);
        RefreshAll();
        StatusMessage = $"게임 폴더 변경: {_launcher.GameDirectory}";
    }

    private async Task CheckForUpdatesAsync()
    {
        var version = await _updates.CheckAndStageAsync();
        if (version is not null)
            UpdateStatus = $"새 버전 {version} 준비됨 · 다음 실행 시 자동 적용";
    }

    /// <summary>지정 VK에 해당하는 콤보 항목을 찾고, 목록에 없으면 동적으로 추가한다.</summary>
    private KeyOption OptionForVk(byte vk)
    {
        var found = Keys.FirstOrDefault(k => k.Vk == vk);
        if (found is not null)
            return found;
        var custom = new KeyOption($"0x{vk:X2}", vk);
        Keys.Add(custom);
        return custom;
    }

    private void LoadKeyMap()
    {
        // 키매핑은 한국어 exe(DK4HD_kr.exe)만 지원한다.
        var exe = _launcher.FindExecutable(GameLanguage.Korean);
        if (exe is null)
        {
            _keyMapAvailable = false;
            KeyMapStatus = "DK4HD_kr.exe를 찾을 수 없음 (게임 실행 한 번 후 인식됨)";
        }
        else
        {
            var state = _keymap.Read(exe);
            if (state is null)
            {
                _keyMapAvailable = false;
                KeyMapStatus = "이 exe는 키매핑 미지원 (시그니처 불일치)";
            }
            else
            {
                _keyMapAvailable = true;
                LeftKey = OptionForVk(state.LeftVk);
                RightKey = OptionForVk(state.RightVk);
                UpKey = OptionForVk(state.UpVk);
                DownKey = OptionForVk(state.DownVk);
                MapKey = OptionForVk(state.MapVk);
                F2Key = OptionForVk(state.F2Vk);
                F3Key = OptionForVk(state.F3Vk);
                F4Key = OptionForVk(state.F4Vk);
                F5Key = OptionForVk(state.F5Vk);
                NumPlusKey = OptionForVk(state.NumPlusVk);
                NumMinusKey = OptionForVk(state.NumMinusVk);
                Num4Key = OptionForVk(state.Num4Vk);
                Num6Key = OptionForVk(state.Num6Vk);
                Num7Key = OptionForVk(state.Num7Vk);
                Num9Key = OptionForVk(state.Num9Vk);
                Num1Key = OptionForVk(state.Num1Vk);
                Num3Key = OptionForVk(state.Num3Vk);
                KeyMapStatus = $"현재: 돛 좌={LeftKey.Display}/우={RightKey.Display}, 선회 위={UpKey.Display}/아래={DownKey.Display}, 지도={MapKey.Display}";
            }
        }

        ApplyKeyMapCommand.NotifyCanExecuteChanged();
        RestoreKeyMapCommand.NotifyCanExecuteChanged();
    }

    private bool CanEditKeyMap() => _keyMapAvailable;

    [RelayCommand(CanExecute = nameof(CanEditKeyMap))]
    private void ApplyKeyMap()
    {
        if (LeftKey is null || RightKey is null || UpKey is null || DownKey is null || MapKey is null
            || NumPlusKey is null || NumMinusKey is null || Num4Key is null || Num6Key is null
            || F2Key is null || F3Key is null || F4Key is null || F5Key is null
            || Num7Key is null || Num9Key is null || Num1Key is null || Num3Key is null)
            return;
        WriteKeyMap(LeftKey.Vk, RightKey.Vk, UpKey.Vk, DownKey.Vk, MapKey.Vk,
            NumPlusKey.Vk, NumMinusKey.Vk, Num4Key.Vk, Num6Key.Vk,
            F2Key.Vk, F3Key.Vk, F4Key.Vk, F5Key.Vk,
            Num7Key.Vk, Num9Key.Vk, Num1Key.Vk, Num3Key.Vk);
    }

    [RelayCommand(CanExecute = nameof(CanEditKeyMap))]
    private void RestoreKeyMap()
    {
        LeftKey = OptionForVk(_keymap.DefaultLeftVk);
        RightKey = OptionForVk(_keymap.DefaultRightVk);
        UpKey = OptionForVk(_keymap.DefaultUpVk);
        DownKey = OptionForVk(_keymap.DefaultDownVk);
        MapKey = OptionForVk(_keymap.DefaultMapVk);
        F2Key = OptionForVk(_keymap.DefaultF2Vk);
        F3Key = OptionForVk(_keymap.DefaultF3Vk);
        F4Key = OptionForVk(_keymap.DefaultF4Vk);
        F5Key = OptionForVk(_keymap.DefaultF5Vk);
        NumPlusKey = OptionForVk(_keymap.DefaultNumPlusVk);
        NumMinusKey = OptionForVk(_keymap.DefaultNumMinusVk);
        Num4Key = OptionForVk(_keymap.DefaultNum4Vk);
        Num6Key = OptionForVk(_keymap.DefaultNum6Vk);
        Num7Key = OptionForVk(_keymap.DefaultNum7Vk);
        Num9Key = OptionForVk(_keymap.DefaultNum9Vk);
        Num1Key = OptionForVk(_keymap.DefaultNum1Vk);
        Num3Key = OptionForVk(_keymap.DefaultNum3Vk);
        WriteKeyMap(_keymap.DefaultLeftVk, _keymap.DefaultRightVk, _keymap.DefaultUpVk,
            _keymap.DefaultDownVk, _keymap.DefaultMapVk,
            _keymap.DefaultNumPlusVk, _keymap.DefaultNumMinusVk, _keymap.DefaultNum4Vk, _keymap.DefaultNum6Vk,
            _keymap.DefaultF2Vk, _keymap.DefaultF3Vk, _keymap.DefaultF4Vk, _keymap.DefaultF5Vk,
            _keymap.DefaultNum7Vk, _keymap.DefaultNum9Vk, _keymap.DefaultNum1Vk, _keymap.DefaultNum3Vk);
    }

    private void WriteKeyMap(byte leftVk, byte rightVk, byte upVk, byte downVk, byte mapVk,
        byte numPlusVk, byte numMinusVk, byte num4Vk, byte num6Vk,
        byte f2Vk, byte f3Vk, byte f4Vk, byte f5Vk,
        byte num7Vk, byte num9Vk, byte num1Vk, byte num3Vk)
    {
        var exe = _launcher.FindExecutable(GameLanguage.Korean);
        if (exe is null)
        {
            KeyMapStatus = "DK4HD_kr.exe를 찾을 수 없습니다.";
            return;
        }

        if (_keymap.IsGameRunning())
        {
            KeyMapStatus = "게임이 실행 중입니다. 종료한 뒤 다시 적용하세요.";
            return;
        }

        try
        {
            _keymap.Apply(exe, leftVk, rightVk, upVk, downVk, mapVk, numPlusVk, numMinusVk, num4Vk, num6Vk,
                f2Vk, f3Vk, f4Vk, f5Vk, num7Vk, num9Vk, num1Vk, num3Vk);
            KeyMapStatus = $"적용됨: 돛 좌={LeftKey?.Display}/우={RightKey?.Display}, 선회 위={UpKey?.Display}/아래={DownKey?.Display}, 지도={MapKey?.Display}";
        }
        catch (IOException)
        {
            KeyMapStatus = "파일이 잠겨 있습니다 (게임 종료 후 재시도).";
        }
        catch (Exception ex)
        {
            KeyMapStatus = $"실패: {ex.Message}";
        }
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
