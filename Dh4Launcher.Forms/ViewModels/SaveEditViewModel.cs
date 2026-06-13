using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dh4Launcher.Forms.Services;

namespace Dh4Launcher.Forms.ViewModels;

/// <summary>
/// 세이브 편집 탭. 슬롯 목록을 읽어 보유 자금을 바꾼다.
/// </summary>
public partial class SaveEditViewModel : ObservableObject
{
    private readonly ISaveEditService _saves;
    private bool _loaded;

    public ObservableCollection<SaveSlot> Slots { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyMoneyCommand))]
    private SaveSlot? _selectedSlot;

    [ObservableProperty]
    private int _money;

    [ObservableProperty]
    private string _status = string.Empty;

    public SaveEditViewModel(ISaveEditService saves)
    {
        _saves = saves;
    }

    /// <summary>탭이 처음 열릴 때 한 번 슬롯을 읽는다.</summary>
    public void EnsureLoaded()
    {
        if (_loaded)
            return;
        _loaded = true;
        Reload();
    }

    partial void OnSelectedSlotChanged(SaveSlot? value)
    {
        if (value is not null)
            Money = value.Money;
    }

    [RelayCommand]
    private void Reload()
    {
        var dir = _saves.FindSaveDirectory();
        if (dir is null)
        {
            Slots.Clear();
            Status = "세이브 폴더를 찾을 수 없습니다 (Documents\\KoeiTecmo\\DK4HD\\Savedata).";
            return;
        }

        var keep = SelectedSlot?.FileName;
        Slots.Clear();
        foreach (var s in _saves.ListSlots())
            Slots.Add(s);

        SelectedSlot = Slots.FirstOrDefault(s => s.FileName == keep) ?? Slots.FirstOrDefault();
        Status = Slots.Count == 0
            ? "편집 가능한 세이브가 없습니다."
            : $"슬롯 {Slots.Count}개 · {dir}";
    }

    private bool CanApplyMoney() => SelectedSlot is not null;

    [RelayCommand(CanExecute = nameof(CanApplyMoney))]
    private void ApplyMoney()
    {
        var slot = SelectedSlot;
        if (slot is null)
            return;

        if (Money < 0)
        {
            Status = "자금은 0 이상이어야 합니다.";
            return;
        }

        if (_saves.IsGameRunning())
        {
            Status = "게임이 실행 중입니다. 종료한 뒤 다시 시도하세요.";
            return;
        }

        try
        {
            _saves.WriteMoney(slot.FullPath, Money);
            Status = $"슬롯 {slot.SlotNumber} 자금 {Money:N0}으로 적용됨 (원본은 {slot.FileName}.bak 백업).";
            Reload();
        }
        catch (System.IO.IOException)
        {
            Status = "파일이 잠겨 있습니다 (게임 종료 후 재시도).";
        }
        catch (System.Exception ex)
        {
            Status = $"실패: {ex.Message}";
        }
    }
}
