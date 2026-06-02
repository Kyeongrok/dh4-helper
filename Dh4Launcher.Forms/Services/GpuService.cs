using System.Management;

namespace Dh4Launcher.Forms.Services;

/// <summary>
/// WMI(Win32_VideoController)로 외장 GPU 장착 여부를 판별한다.
/// 가상/기본 디스플레이 어댑터(Meta Virtual Monitor, Microsoft Basic 등)는 제외한다.
/// </summary>
public class GpuService : IGpuService
{
    private static readonly string[] Discrete =
        ["NVIDIA", "GeForce", "RTX", "GTX", "Quadro", "Radeon", "AMD", "Arc"];

    private static readonly string[] Virtual =
        ["Basic", "Virtual", "Meta", "Remote", "Mirror", "Parsec", "DameWare", "Citrix"];

    public string? HighPerformanceGpuName
    {
        get
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_VideoController");

                foreach (var device in searcher.Get())
                {
                    var name = device["Name"] as string;
                    if (!string.IsNullOrWhiteSpace(name) && IsDiscrete(name))
                        return name.Trim();
                }
            }
            catch
            {
                // WMI 사용 불가 환경에서는 '감지 안 됨'으로 처리한다.
            }

            return null;
        }
    }

    private static bool IsDiscrete(string name)
    {
        foreach (var v in Virtual)
            if (name.Contains(v, StringComparison.OrdinalIgnoreCase))
                return false;

        foreach (var d in Discrete)
            if (name.Contains(d, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }
}
