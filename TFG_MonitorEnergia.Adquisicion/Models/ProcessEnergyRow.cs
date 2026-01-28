namespace TFG_MonitorEnergia.SessionData.Models;

public sealed record ProcessEnergyRow(
    int Pid,
    string Name,
    double LastCpuPercent,
    double LastAllocatedWatts,
    double TotalJoules,
    double MaxWatts
)
{
    public double TotalWh => TotalJoules / 3600.0;
}
