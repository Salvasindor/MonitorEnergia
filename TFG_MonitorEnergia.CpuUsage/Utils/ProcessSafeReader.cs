using System;
using System.Collections.Generic;
using System.Diagnostics;
using TFG_MonitorEnergia.CpuUsage.Models;

namespace TFG_MonitorEnergia.CpuUsage.Utils;

public static class ProcessSafeReader
{
    public static IEnumerable<ProcessCpuSample> GetProcessSamples()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch
        {
            yield break;
        }

        foreach (var p in processes)
        {
            int pid;
            string name;

            try
            {
                pid = p.Id;
                name = p.ProcessName ?? "unknown";
            }
            catch
            {
                SafeDispose(p);
                continue;
            }

            TimeSpan totalCpu;
            try
            {
                totalCpu = p.TotalProcessorTime;
            }
            catch
            {
                SafeDispose(p);
                continue;
            }

            SafeDispose(p);
            yield return new ProcessCpuSample(pid, name, totalCpu);
        }
    }

    private static void SafeDispose(Process p)
    {
        try { p.Dispose(); } catch { /* ignore */ }
    }
}
