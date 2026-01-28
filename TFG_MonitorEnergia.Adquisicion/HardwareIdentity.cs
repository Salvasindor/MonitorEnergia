using System;
using System.Linq;
using System.Management;

namespace TFG_MonitorEnergia.SessionData;

public static class HardwareIdentity
{
    public static string? TryGetCpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_Processor");

            using var results = searcher.Get();

            return results
                .Cast<ManagementObject>()
                .Select(mo => mo["Name"]?.ToString())
                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
                ?.Trim();
        }
        catch
        {
            return null;
        }
    }

    public static string? TryGetGpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, AdapterRAM FROM Win32_VideoController");

            using var results = searcher.Get();

            // Si hay varias GPUs (iGPU + dGPU), elegimos la de más VRAM (cuando está disponible)
            var best = results
                .Cast<ManagementObject>()
                .Select(mo => new
                {
                    Name = mo["Name"]?.ToString(),
                    Ram = TryToLong(mo["AdapterRAM"])
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .OrderByDescending(x => x.Ram)
                .FirstOrDefault();

            return best?.Name?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static long TryToLong(object? v)
    {
        try { return v is null ? 0 : Convert.ToInt64(v); }
        catch { return 0; }
    }
}
