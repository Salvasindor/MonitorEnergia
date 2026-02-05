namespace TFG_MonitorEnergia.Core.Models;


/// Snapshot por tick: incluye dt real delata seleccionado+jitter, potencia total CPU/GPU y tabla por app.
/// Core NO acumula sesión: devuelve solo deltas; SessionData acumula.

public sealed record TickSnapshot(
    DateTime TimestampUtc,
    double DeltaSeconds,

    double CpuWattsRaw,
    double? CpuWattsFiltered,

    double GpuWattsRaw,

    double CpuDeltaJoulesTotal,
    double GpuDeltaJoulesTotal,

    IReadOnlyList<AppTickRow> Apps
);
