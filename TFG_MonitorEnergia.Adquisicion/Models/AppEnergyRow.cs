namespace TFG_MonitorEnergia.SessionData.Models;

/// <summary>
/// Fila final (sesión) por aplicación: esto es lo que consume WPF y export.
/// Mantiene nombres compatibles con tu export anterior:
/// - LastAllocatedWatts = W instantáneos actuales
/// - ProcessCount = PIDs distintos vistos en sesión
/// - LastSeenProcessCount = procesos activos en el tick actual
/// </summary>
public sealed record AppEnergyRow(
    string AppName,
    double LastCpuPercent,
    double LastAllocatedWatts,
    double TotalJoules,
    double MaxWatts,
    int ProcessCount,
    int LastSeenProcessCount
)
{
    public double TotalWh => TotalJoules / 3600.0;
}
