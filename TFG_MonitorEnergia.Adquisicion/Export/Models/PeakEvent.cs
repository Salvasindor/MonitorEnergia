namespace TFG_MonitorEnergia.SessionData.Export.Models;

public sealed record PeakEvent(
    DateTime TimestampUtc,
    double TSeconds,
    string Type,     // CPU_PEAK, GPU_PEAK, TOTAL_PEAK
    string? AppName, // opcional
    double ValueW,
    string? Notes
);
