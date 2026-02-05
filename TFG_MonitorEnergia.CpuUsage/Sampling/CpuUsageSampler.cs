using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TFG_MonitorEnergia.CpuUsage.Models;
using TFG_MonitorEnergia.CpuUsage.Utils;

namespace TFG_MonitorEnergia.CpuUsage.Sampling;

public sealed class CpuUsageSampler : ICpuUsageSampler
{
    private readonly Dictionary<int, ProcessCpuSample> _lastSamplesByPid = new();

    // se mide tiempo real con Stopwatch para que el dt sea estable
    private long _lastTickTimestamp = -1;

    private readonly int _logicalCores;

    public CpuUsageSampler()
    {
        _logicalCores = Math.Max(1, Environment.ProcessorCount);
    }

    public void Reset()
    {
        _lastSamplesByPid.Clear();
        _lastTickTimestamp = -1;
    }

    public IReadOnlyList<ProcessCpuUsage> Tick()
    {
        var nowTs = Stopwatch.GetTimestamp();

        // Primer tick: solo capturamos base, no hay delta
        if (_lastTickTimestamp < 0)
        {
            _lastTickTimestamp = nowTs;
            CaptureBaseline();
            return Array.Empty<ProcessCpuUsage>();
        }

        var dtSeconds = (nowTs - _lastTickTimestamp) / (double)Stopwatch.Frequency;
        if (dtSeconds <= 0)
        {
            // por seguridad: si dt sale raro, no calculamos
            _lastTickTimestamp = nowTs;
            CaptureBaseline();
            return Array.Empty<ProcessCpuUsage>();
        }

        var current = ProcessSafeReader.GetProcessSamples().ToList();

        // calculamos CPU% para los PIDs que existían antes y ahora
        var results = new List<ProcessCpuUsage>(capacity: current.Count);

        foreach (var cur in current)
        {
            if (!_lastSamplesByPid.TryGetValue(cur.Pid, out var prev))
                continue; // proceso nuevo: aún no hay delta

            var deltaCpu = cur.TotalProcessorTime - prev.TotalProcessorTime;
            var deltaCpuSeconds = deltaCpu.TotalSeconds;

            // si por cualquier motivo sale negativo (raro), lo ignoramos
            if (deltaCpuSeconds < 0)
                continue;

            // Normalización tipo Task Manager:
            // CPU% = (deltaCPU / dt) / numCores * 100
            var cpuPercent = (deltaCpuSeconds / dtSeconds) / _logicalCores * 100.0;

            // clamp suave (puede haber picos por scheduling/timing, evitamos basura)
            if (double.IsNaN(cpuPercent) || double.IsInfinity(cpuPercent)) continue;
            if (cpuPercent < 0) cpuPercent = 0;
            if (cpuPercent > 100) cpuPercent = 100;

            results.Add(new ProcessCpuUsage(
                cur.Pid,
                cur.Name,
                cpuPercent,
                deltaCpu,
                dtSeconds
            ));
        }

        // actualizamos baseline para el siguiente tick
        _lastTickTimestamp = nowTs;
        _lastSamplesByPid.Clear();
        foreach (var cur in current)
            _lastSamplesByPid[cur.Pid] = cur;

        return results;
    }

    private void CaptureBaseline()
    {
        _lastSamplesByPid.Clear();
        foreach (var sample in ProcessSafeReader.GetProcessSamples())
            _lastSamplesByPid[sample.Pid] = sample;
    }
}
