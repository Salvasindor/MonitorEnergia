using System.Collections.Generic;
using TFG_MonitorEnergia.CpuUsage.Models;

namespace TFG_MonitorEnergia.CpuUsage.Sampling;

public interface ICpuUsageSampler
{

    /// Toma una muestra y devuelve el uso de CPU por proceso desde el último Tick.
    /// En la primera llamada normalmente devolverá una lista vacía (no hay delta).
   
    IReadOnlyList<ProcessCpuUsage> Tick();

    void Reset();
}
