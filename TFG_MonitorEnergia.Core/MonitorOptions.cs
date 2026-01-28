namespace TFG_MonitorEnergia.Core;

/// <summary>
/// Opciones del motor de cálculo (Core).
/// NOTA: el periodo objetivo (timer) NO va aquí; lo gestiona SessionData.
/// Aquí solo hay filtrado/validación/capas de cálculo.
/// </summary>
public sealed class MonitorOptions
{
    // Validación potencia CPU (W)
    public double MinCpuWatts { get; init; } = 0.1;
    public double MaxCpuWatts { get; init; } = 500.0;

    // Validación potencia GPU (W)
    public double MinGpuWatts { get; init; } = 0.0;
    public double MaxGpuWatts { get; init; } = 800.0;

    // Validación dt real (segundos)
    public double MinDtSeconds { get; init; } = 0.001; // 1 ms
    public double MaxDtSeconds { get; init; } = 2.0;   // si hay cuelgue del timer

    // Filtrado EMA para CPU total (W)
    public bool EnableCpuEma { get; init; } = true;
    public double CpuEmaTauSeconds { get; init; } = 0.5;

    // Procesos: umbral de CPU% para ignorar ruido
    public double MinProcessCpuPercent { get; init; } = 0.05;

    // Límite de apps devueltas por tick
    public int MaxAppsPerTick { get; init; } = 250;

    // Normalización nombres
    public bool StripExeSuffix { get; init; } = true;
}
