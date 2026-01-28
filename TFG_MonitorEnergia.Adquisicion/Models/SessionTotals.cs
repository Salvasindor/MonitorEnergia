namespace TFG_MonitorEnergia.SessionData.Models;

public sealed record SessionTotals(
    double LastCpuWatts,
    double TotalCpuJoules,
    double LastGpuWatts,
    double TotalGpuJoules,
    double SessionSeconds
)
{
    public double TotalCpuWh => TotalCpuJoules / 3600.0;
    public double TotalGpuWh => TotalGpuJoules / 3600.0;
    public double TotalWh => (TotalCpuJoules + TotalGpuJoules) / 3600.0;
}
