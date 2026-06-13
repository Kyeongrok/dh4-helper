namespace Dh4Launcher.Forms.Services;

/// <summary>
/// 세이브 슬롯 한 칸. <see cref="SlotNumber"/>는 게임 내 표시 번호(파일 인덱스+1),
/// <see cref="FileName"/>은 실제 파일(savedatNN.dk4). Money는 보유 자금.
/// </summary>
public record SaveSlot(string FullPath, string FileName, int SlotNumber, int Money, DateTime LastModified);

public interface ISaveEditService
{
    /// <summary>세이브 폴더(Documents\KoeiTecmo\DK4HD\Savedata\&lt;lang&gt;)를 찾는다. 없으면 null.</summary>
    string? FindSaveDirectory();

    /// <summary>폴더 안의 savedatNN.dk4 슬롯들을 번호순으로 읽는다(index 파일 savedata.dk4 제외).</summary>
    IReadOnlyList<SaveSlot> ListSlots();

    /// <summary>해당 슬롯 파일의 자금을 바꾼다(최초 1회 .bak 백업). DK4 세이브가 아니면 예외.</summary>
    void WriteMoney(string savePath, int money);

    /// <summary>게임(DK4HD_*)이 실행 중이라 파일이 잠겨 있을 수 있는지.</summary>
    bool IsGameRunning();
}
