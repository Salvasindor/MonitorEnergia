using System;
using System.Threading;
using TFG_MonitorEnergia.Core;
using TFG_MonitorEnergia.SessionData.Storage;

namespace TFG_MonitorEnergia.SessionData.Monitoring;

public sealed class SessionMonitorService : IDisposable
{
    private readonly object _lock = new();

    private Timer? _timer;
    private bool _running;
    private bool _inTick;

    private readonly EnergyMonitorCore _core;

    public MonitorConfig Config { get; } = new();
    public SessionDataStore Store { get; } = new();

    public event Action? Updated;

    public SessionMonitorService(MonitorOptions? coreOptions = null)
    {
        _core = new EnergyMonitorCore(coreOptions);
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_running) return;

            // 1) Captura nombre CPU/GPU (WMI) UNA vez, sin tocar LibreHardwareMonitor
            var (cpuName, gpuName) = Store.GetHardwareNames();
            if (string.IsNullOrWhiteSpace(cpuName) || string.IsNullOrWhiteSpace(gpuName))
            {
                var cpu = SessionData.HardwareIdentity.TryGetCpuName();
                var gpu = SessionData.HardwareIdentity.TryGetGpuName();
                Store.SetHardwareNames(cpu, gpu);
            }

            _running = true;

            // 2) Arranca loop
            _timer = new Timer(
                callback: _ => TickSafe(),
                state: null,
                dueTime: 0,
                period: Config.TargetPeriodMs
            );
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _running = false;
            _timer?.Dispose();
            _timer = null;
        }
    }

    public void ResetSession()
    {
        Store.Reset();
    }

    public void SetTargetPeriodMs(int ms)
    {
        ms = Math.Clamp(ms, 50, 5000);

        lock (_lock)
        {
            Config.TargetPeriodMs = ms;

            if (_timer is not null)
                _timer.Change(0, ms);
        }
    }

    private void TickSafe()
    {
        lock (_lock)
        {
            if (!_running) return;
            if (_inTick) return;
            _inTick = true;
        }

        try
        {
            var snap = _core.ExecuteTick();
            if (snap is null) return;

            Store.ApplySnapshot(snap);

            try { Updated?.Invoke(); }
            catch { }
        }
        finally
        {
            lock (_lock)
            {
                _inTick = false;
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _core.Dispose();
    }
}
