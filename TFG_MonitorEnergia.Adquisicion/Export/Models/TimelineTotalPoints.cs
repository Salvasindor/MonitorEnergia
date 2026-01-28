namespace TFG_MonitorEnergia.SessionData.Export.Models;

public sealed record TimelineTotalPoint(
    DateTime TimestampUtc,
    double TSeconds,
    double DtSeconds,
    double CpuWAvg,
    double GpuWAvg,
    double TotalWAvg,
    double CpuWhCum,
    double GpuWhCum,
    double TotalWhCum
);
