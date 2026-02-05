using System;

namespace TFG_MonitorEnergia.CpuUsage.Models;


/// Snapshot crudo de un proceso en un instante: tiempo total de CPU acumulado (User+Kernel).

public readonly record struct ProcessCpuSample(
    int Pid,
    string Name,
    TimeSpan TotalProcessorTime
);
