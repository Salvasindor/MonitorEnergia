using System;

namespace TFG_MonitorEnergia.CpuUsage.Models;


/// Uso de CPU estimado de un proceso durante el intervalo entre dos ticks.
/// CpuPercent está normalizado a 0..100 (100 = 1 CPU lógica al 100% de la máquina completa).

public readonly record struct ProcessCpuUsage(
    int Pid,
    string Name,
    double CpuPercent,
    TimeSpan CpuTimeDelta,
    double IntervalSeconds
);
