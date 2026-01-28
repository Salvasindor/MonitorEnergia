using System;
using System.Collections.Generic;
using System.Linq;
using TFG_MonitorEnergia.Core.Models;
using TFG_MonitorEnergia.SessionData.Export.Models;
using TFG_MonitorEnergia.SessionData.Models;

namespace TFG_MonitorEnergia.SessionData.Storage;

/// <summary>
/// Memoria de sesión:
/// - acumula energía total (CPU/GPU)
/// - acumula energía por app
/// - guarda máximos
/// - cuenta PIDs distintos por app (HashSet)
/// - guarda nombres de CPU/GPU (identidad del equipo)
/// </summary>
public sealed class SessionDataStore
{
    private readonly object _lock = new();

    private readonly Dictionary<string, AppMutable> _apps =
        new(StringComparer.OrdinalIgnoreCase);

    private double _totalCpuJ;
    private double _totalGpuJ;
    private double _lastCpuW;
    private double _lastGpuW;
    private double _sessionSeconds;

    // ===== Identidad HW (WMI) =====
    private string? _cpuName;
    private string? _gpuName;

    // ===== Sesión =====
    private DateTime _sessionStartUtc = DateTime.UtcNow;
    private DateTime _lastTickUtc = DateTime.UtcNow;

    // ===== Estadística dt (jitter) =====
    private double _dtMin = double.MaxValue;
    private double _dtMax = 0;
    private double _dtSum = 0;
    private long _dtCount = 0;

    // ===== Picos =====
    private double _cpuPeakW = 0;
    private DateTime _cpuPeakUtc = DateTime.MinValue;
    private double _gpuPeakW = 0;
    private DateTime _gpuPeakUtc = DateTime.MinValue;
    private double _totalPeakW = 0;
    private DateTime _totalPeakUtc = DateTime.MinValue;

    // ===== Timeline agregado 1s =====
    private readonly List<TimelineTotalPoint> _timelineTotal = new();

    // “bucket” de 1 segundo
    private DateTime _bucketStartUtc = DateTime.UtcNow;
    private double _bucketTStartS = 0.0;
    private double _bucketDtSum = 0.0;
    private double _bucketCpuWattSeconds = 0.0;
    private double _bucketGpuWattSeconds = 0.0;

    // ===== Eventos =====
    private readonly List<PeakEvent> _events = new();

    // ======================
    // Identidad hardware
    // ======================
    public void SetHardwareNames(string? cpuName, string? gpuName)
    {
        lock (_lock)
        {
            // Solo guardamos si vienen con texto; así no “pisamos” con null
            if (!string.IsNullOrWhiteSpace(cpuName)) _cpuName = cpuName.Trim();
            if (!string.IsNullOrWhiteSpace(gpuName)) _gpuName = gpuName.Trim();
        }
    }

    public (string? CpuName, string? GpuName) GetHardwareNames()
    {
        lock (_lock)
        {
            return (_cpuName, _gpuName);
        }
    }

    // ======================
    // Métricas de sesión
    // ======================
    public (DateTime StartUtc, DateTime EndUtc) GetSessionBoundsUtc()
    {
        lock (_lock)
        {
            return (_sessionStartUtc, _lastTickUtc);
        }
    }

    public (double DtMin, double DtAvg, double DtMax) GetDtStatsSeconds()
    {
        lock (_lock)
        {
            var avg = _dtCount > 0 ? _dtSum / _dtCount : 0.0;
            var min = _dtMin == double.MaxValue ? 0.0 : _dtMin;
            return (min, avg, _dtMax);
        }
    }

    public IReadOnlyList<TimelineTotalPoint> GetTimelineTotal()
    {
        lock (_lock)
        {
            return _timelineTotal.ToList();
        }
    }

    public IReadOnlyList<PeakEvent> GetEvents()
    {
        lock (_lock)
        {
            return _events.ToList();
        }
    }

    public (double CpuPeakW, DateTime CpuPeakUtc, double GpuPeakW, DateTime GpuPeakUtc, double TotalPeakW, DateTime TotalPeakUtc) GetPeaks()
    {
        lock (_lock)
        {
            return (_cpuPeakW, _cpuPeakUtc, _gpuPeakW, _gpuPeakUtc, _totalPeakW, _totalPeakUtc);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _apps.Clear();

            _totalCpuJ = 0;
            _totalGpuJ = 0;
            _lastCpuW = 0;
            _lastGpuW = 0;
            _sessionSeconds = 0;

            _sessionStartUtc = DateTime.UtcNow;
            _lastTickUtc = _sessionStartUtc;

            _dtMin = double.MaxValue;
            _dtMax = 0;
            _dtSum = 0;
            _dtCount = 0;

            _cpuPeakW = 0; _cpuPeakUtc = DateTime.MinValue;
            _gpuPeakW = 0; _gpuPeakUtc = DateTime.MinValue;
            _totalPeakW = 0; _totalPeakUtc = DateTime.MinValue;

            _timelineTotal.Clear();
            _events.Clear();

            _bucketStartUtc = _sessionStartUtc;
            _bucketTStartS = 0;
            _bucketDtSum = 0;
            _bucketCpuWattSeconds = 0;
            _bucketGpuWattSeconds = 0;

            // OJO: si quieres que los nombres permanezcan entre sesiones, comenta estas dos líneas
            _cpuName = null;
            _gpuName = null;
        }
    }

    /// <summary>
    /// Aplica un snapshot del Core (delta de tick).
    /// </summary>
    public void ApplySnapshot(TickSnapshot snap)
    {
        lock (_lock)
        {
            _sessionSeconds += snap.DeltaSeconds;

            // Potencias "actuales"
            _lastCpuW = snap.CpuWattsFiltered ?? snap.CpuWattsRaw;
            _lastGpuW = snap.GpuWattsRaw;

            // Energías acumuladas totales
            _totalCpuJ += snap.CpuDeltaJoulesTotal;
            _totalGpuJ += snap.GpuDeltaJoulesTotal;

            _lastTickUtc = DateTime.UtcNow;

            // dt stats (jitter)
            var dt = snap.DeltaSeconds;
            _dtMin = Math.Min(_dtMin, dt);
            _dtMax = Math.Max(_dtMax, dt);
            _dtSum += dt;
            _dtCount++;

            // Peaks (CPU/GPU/Total)
            var cpuW = snap.CpuWattsFiltered ?? snap.CpuWattsRaw;
            var gpuW = snap.GpuWattsRaw;
            var totalW = cpuW + gpuW;

            if (cpuW > _cpuPeakW) { _cpuPeakW = cpuW; _cpuPeakUtc = _lastTickUtc; _events.Add(new PeakEvent(_lastTickUtc, _sessionSeconds, "CPU_PEAK", null, cpuW, null)); }
            if (gpuW > _gpuPeakW) { _gpuPeakW = gpuW; _gpuPeakUtc = _lastTickUtc; _events.Add(new PeakEvent(_lastTickUtc, _sessionSeconds, "GPU_PEAK", null, gpuW, null)); }
            if (totalW > _totalPeakW) { _totalPeakW = totalW; _totalPeakUtc = _lastTickUtc; _events.Add(new PeakEvent(_lastTickUtc, _sessionSeconds, "TOTAL_PEAK", null, totalW, null)); }

            // Timeline agregado a 1 segundo: acumulamos W*dt (watt-seconds)
            _bucketDtSum += dt;
            _bucketCpuWattSeconds += cpuW * dt;
            _bucketGpuWattSeconds += gpuW * dt;

            if (_bucketDtSum >= 1.0)
            {
                var cpuAvg = _bucketCpuWattSeconds / _bucketDtSum;
                var gpuAvg = _bucketGpuWattSeconds / _bucketDtSum;

                var cpuWhCum = _totalCpuJ / 3600.0;
                var gpuWhCum = _totalGpuJ / 3600.0;

                _timelineTotal.Add(new TimelineTotalPoint(
                    TimestampUtc: _lastTickUtc,
                    TSeconds: _sessionSeconds,
                    DtSeconds: _bucketDtSum,
                    CpuWAvg: cpuAvg,
                    GpuWAvg: gpuAvg,
                    TotalWAvg: cpuAvg + gpuAvg,
                    CpuWhCum: cpuWhCum,
                    GpuWhCum: gpuWhCum,
                    TotalWhCum: cpuWhCum + gpuWhCum
                ));

                _bucketStartUtc = _lastTickUtc;
                _bucketTStartS = _sessionSeconds;
                _bucketDtSum = 0;
                _bucketCpuWattSeconds = 0;
                _bucketGpuWattSeconds = 0;
            }
            // 0) Reset de valores instantáneos (apps no vistas en este tick)
            foreach (var a in _apps.Values)
            {
                a.LastCpuPercent = 0.0;
                a.LastWatts = 0.0;
                a.LastProcessesNow = 0;
            }

            // Actualización por app
            foreach (var r in snap.Apps)
            {
                if (!_apps.TryGetValue(r.AppName, out var a))
                {
                    a = new AppMutable(r.AppName);
                    _apps[r.AppName] = a;
                }

                a.LastCpuPercent = r.CpuPercentNow;
                a.LastWatts = r.CpuWattsNow;
                a.LastProcessesNow = r.ProcessesNow;

                a.TotalJoules += r.CpuDeltaJoules;
                if (r.CpuWattsNow > a.MaxWatts) a.MaxWatts = r.CpuWattsNow;

                if (r.PidsNow is not null)
                    a.PidsEver.UnionWith(r.PidsNow);
            }
        }
    }

    public SessionTotals GetTotals()
    {
        lock (_lock)
        {
            return new SessionTotals(
                LastCpuWatts: _lastCpuW,
                TotalCpuJoules: _totalCpuJ,
                LastGpuWatts: _lastGpuW,
                TotalGpuJoules: _totalGpuJ,
                SessionSeconds: _sessionSeconds
            );
        }
    }

    public IReadOnlyList<AppEnergyRow> GetTopAppsByEnergy(int topN = 200)
    {
        lock (_lock)
        {
            return _apps.Values
                .OrderByDescending(a => a.TotalJoules)
                .Take(topN)
                .Select(a => new AppEnergyRow(
                    AppName: a.AppName,
                    LastCpuPercent: a.LastCpuPercent,
                    LastAllocatedWatts: a.LastWatts,
                    TotalJoules: a.TotalJoules,
                    MaxWatts: a.MaxWatts,
                    ProcessCount: a.PidsEver.Count,
                    LastSeenProcessCount: a.LastProcessesNow
                ))
                .ToList();
        }
    }

    private sealed class AppMutable
    {
        public string AppName { get; }

        public double LastCpuPercent { get; set; }
        public double LastWatts { get; set; }
        public int LastProcessesNow { get; set; }

        public double TotalJoules { get; set; }
        public double MaxWatts { get; set; }

        public HashSet<int> PidsEver { get; } = new();

        public AppMutable(string appName) => AppName = appName;
    }
}
