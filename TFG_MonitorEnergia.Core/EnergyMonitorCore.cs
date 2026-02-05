using System.Diagnostics;
using TFG_MonitorEnergia.Core.Models;
using TFG_MonitorEnergia.CpuUsage.Sampling;
using TFG_MonitorEnergia.Hardware;

namespace TFG_MonitorEnergia.Core;


/// Core definitivo:
/// - Mide dt real con Stopwatch (jitter incluido)
/// - Lee CPU% por proceso (CpuUsageSampler)
/// - Lee potencia CPU/GPU total (LhmSampler)
/// - Filtra CPU watts total con EMA 
/// - Agrupa por app (nombre) por tick
/// - Reparte CPU_W_total proporcional al CPU% por app
/// - Devuelve TickSnapshot (deltas), NO guarda sesión

public sealed class EnergyMonitorCore : IDisposable
{
    private readonly CpuUsageSampler _cpuSampler;
    private readonly LhmSampler _lhm;
    private readonly Stopwatch _sw = Stopwatch.StartNew();

    private double _lastSeconds;
    private double? _cpuEmaWatts;

    public MonitorOptions Options { get; }

    public EnergyMonitorCore(MonitorOptions? options = null)
    {
        Options = options ?? new MonitorOptions();

        _cpuSampler = new CpuUsageSampler();
        _lhm = new LhmSampler();
        _lhm.Open();
        _cpuSampler.Tick();

        _lastSeconds = _sw.Elapsed.TotalSeconds;
    }

  
    /// Ejecuta un tick de cálculo y devuelve un snapshot inmutable listo para SessionData.
    /// Si el dt es inválido o no hay datos coherentes, devuelve null.

    public TickSnapshot? ExecuteTick()
    {
        // dt real 
        var nowSeconds = _sw.Elapsed.TotalSeconds;
        var dt = nowSeconds - _lastSeconds;
        _lastSeconds = nowSeconds;

        if (dt < Options.MinDtSeconds || dt > Options.MaxDtSeconds)
            return null;

        // 1) CPU% por proceso
        var usages = _cpuSampler.Tick();
        if (usages.Count == 0)
            return null;

        // Filtrar ruido + normalizar nombres
        var procs = usages
            .Where(u => u.CpuPercent >= Options.MinProcessCpuPercent)
            .Select(u => new ProcRow(
                Pid: u.Pid,
                AppName: NormalizeAppName(u.Name),
                CpuPercent: u.CpuPercent
            ))
            .ToList();

        //  Potencias totales (W)
        var cpuWattsRaw = _lhm.GetCpuPackagePowerWatts() ?? 0.0;
        var gpuWattsRaw = _lhm.GetGpuPackagePowerWatts() ?? 0.0;

        // Validación NaN/Inf y rangos
        if (!IsFinite(cpuWattsRaw) || cpuWattsRaw < Options.MinCpuWatts || cpuWattsRaw > Options.MaxCpuWatts)
            cpuWattsRaw = 0.0;

        if (!IsFinite(gpuWattsRaw) || gpuWattsRaw < Options.MinGpuWatts || gpuWattsRaw > Options.MaxGpuWatts)
            gpuWattsRaw = 0.0;

        //  Filtrado EMA CPU total
        double? cpuWattsFiltered = null;
        double cpuWattsForAllocation = cpuWattsRaw;

        if (Options.EnableCpuEma && cpuWattsRaw > 0.0)
        {
            cpuWattsFiltered = ApplyCpuEma(cpuWattsRaw, dt, Options.CpuEmaTauSeconds);
            cpuWattsForAllocation = cpuWattsFiltered.Value;
        }

        //  Agrupación por app (por tick)
        var groupedAll = procs
     .GroupBy(p => p.AppName, StringComparer.OrdinalIgnoreCase)
     .Select(g => new AppAgg(
         AppName: g.Key,
         ProcessesNow: g.Count(),
         CpuPercentSum: g.Sum(x => x.CpuPercent),
         PidsNow: g.Select(x => x.Pid).Distinct().ToList()
     ))
     .ToList();

        //  Sumatorio REAL de CPU% 
        var totalCpuPercent = groupedAll.Sum(a => a.CpuPercentSum);

        // Top-N solo para mostrar / exportar
        var grouped = groupedAll
            .OrderByDescending(a => a.CpuPercentSum)
            .Take(Options.MaxAppsPerTick)
            .ToList();

        // 5) Reparto y deltas
        var appRows = new List<AppTickRow>(grouped.Count);
        double cpuDeltaJTotal = 0.0;

        foreach (var a in grouped)
        {
            var wattsNow =
                (cpuWattsForAllocation > 0 && totalCpuPercent > 0)
                    ? cpuWattsForAllocation * (a.CpuPercentSum / totalCpuPercent)
                    : 0.0;

            var deltaJ = wattsNow * dt;
            cpuDeltaJTotal += deltaJ;

            appRows.Add(new AppTickRow(
                AppName: a.AppName,
                ProcessesNow: a.ProcessesNow,
                CpuPercentNow: a.CpuPercentSum,
                CpuWattsNow: wattsNow,
                CpuDeltaJoules: deltaJ,
                PidsNow: a.PidsNow,
                CpuWattsRawTotalNow: cpuWattsRaw,
                CpuWattsFilteredTotalNow: cpuWattsFiltered
            ));
        }

        // 6) Energía GPU total por tick
        var gpuDeltaJTotal = gpuWattsRaw * dt;

        // Orden final estilo Task Manager 
        appRows = appRows
            .OrderByDescending(r => r.CpuWattsNow)
            .ToList();

        return new TickSnapshot(
            TimestampUtc: DateTime.UtcNow,
            DeltaSeconds: dt,

            CpuWattsRaw: cpuWattsRaw,
            CpuWattsFiltered: cpuWattsFiltered,

            GpuWattsRaw: gpuWattsRaw,

            CpuDeltaJoulesTotal: cpuDeltaJTotal,
            GpuDeltaJoulesTotal: gpuDeltaJTotal,

            Apps: appRows
        );
    }

    private string NormalizeAppName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "unknown";

        name = name.Trim();

        if (Options.StripExeSuffix && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];

        return name;
    }

    private double ApplyCpuEma(double x, double dt, double tauSeconds)
    {
        tauSeconds = Math.Max(0.001, tauSeconds);

        var alpha = 1.0 - Math.Exp(-dt / tauSeconds);

        if (_cpuEmaWatts is null)
        {
            _cpuEmaWatts = x;
            return x;
        }

        _cpuEmaWatts = alpha * x + (1.0 - alpha) * _cpuEmaWatts.Value;
        return _cpuEmaWatts.Value;
    }

    private static bool IsFinite(double x) => !(double.IsNaN(x) || double.IsInfinity(x));

    public void Dispose()
    {
        _lhm.Dispose();
    }

    private readonly record struct ProcRow(int Pid, string AppName, double CpuPercent);

    private readonly record struct AppAgg(
        string AppName,
        int ProcessesNow,
        double CpuPercentSum,
        IReadOnlyList<int> PidsNow
    );
}
