namespace TFG_MonitorEnergia.Core.Models;

/// <summary>
/// Resultado por aplicación para un tick (ya agrupado).
/// </summary>
public sealed record AppTickRow(
    string AppName,
    int ProcessesNow,                 // nº procesos activos en este tick
    double CpuPercentNow,             // suma de CPU% de la app en este tick
    double CpuWattsNow,               // W asignados a esta app en este tick
    double CpuDeltaJoules,            // energía (J) de esta app SOLO en este tick
    IReadOnlyList<int> PidsNow,       // PIDs vistos en este tick (para distinct en SessionData)
    double CpuWattsRawTotalNow,       // CPU watts total raw del sistema en este tick (debug)
    double? CpuWattsFilteredTotalNow  // CPU watts total filtrado del sistema en este tick (debug)
);
