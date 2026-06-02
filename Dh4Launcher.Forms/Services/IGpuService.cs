namespace Dh4Launcher.Forms.Services;

public interface IGpuService
{
    /// <summary>시스템에 장착된 고성능(외장) GPU 이름. 없으면 null.</summary>
    string? HighPerformanceGpuName { get; }
}
