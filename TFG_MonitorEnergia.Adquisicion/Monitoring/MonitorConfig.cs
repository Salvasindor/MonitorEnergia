namespace TFG_MonitorEnergia.SessionData.Monitoring;

/// <summary>
/// Configuración "de aplicación" (lo que tocará la UI).
/// </summary>
public sealed class MonitorConfig
{
    public int TargetPeriodMs { get; set; } = 500;

    // Para UI: cuántas filas máximo devuelvo
    public int TopNApps { get; set; } = 200;
}
