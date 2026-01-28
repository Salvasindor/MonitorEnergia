using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using TFG_MonitorEnergia.SessionData.Export;
using TFG_MonitorEnergia.SessionData.Models;
using TFG_MonitorEnergia.SessionData.Monitoring;
using TFG_MonitorEnergia.WpfApp.Commands;

namespace TFG_MonitorEnergia.WpfApp.ViewModels;

public sealed class MainWindowViewModel : NotifyBase
{
    public ObservableCollection<AppRowVm> Apps { get; } = new();

    public string StatusLeft { get => _statusLeft; private set => Set(ref _statusLeft, value); }
    public string StatusRight { get => _statusRight; private set => Set(ref _statusRight, value); }
    private string _statusLeft = "";
    private string _statusRight = "";

    public double PricePerKwh
    {
        get => _pricePerKwh;
        set
        {
            if (value < 0) value = 0;
            if (Set(ref _pricePerKwh, value))
                RefreshCostOnly();
        }
    }
    private double _pricePerKwh = 0.20;

    public string CostSummaryLine1 { get => _cost1; private set => Set(ref _cost1, value); }
    public string CostSummaryLine2 { get => _cost2; private set => Set(ref _cost2, value); }
    private string _cost1 = "";
    private string _cost2 = "";

    public int UiRefreshMs { get => _uiRefreshMs; private set => Set(ref _uiRefreshMs, Clamp(value, 50, 5000)); }
    private int _uiRefreshMs = 500;

    public int TopNApps { get => _topNApps; private set => Set(ref _topNApps, Clamp(value, 10, 2000)); }
    private int _topNApps = 200;

    public enum EnergyUnit { Wh, J }

    public EnergyUnit Unit
    {
        get => _unit;
        private set
        {
            if (Set(ref _unit, value))
            {
                Raise(nameof(IsWhChecked));
                Raise(nameof(IsJChecked));
                Refresh();
            }
        }
    }
    private EnergyUnit _unit = EnergyUnit.Wh;

    public bool IsWhChecked { get => Unit == EnergyUnit.Wh; set { if (value) Unit = EnergyUnit.Wh; } }
    public bool IsJChecked { get => Unit == EnergyUnit.J; set { if (value) Unit = EnergyUnit.J; } }

    public bool ColCpuPercent { get => _colCpuPercent; set => Set(ref _colCpuPercent, value); }
    public bool ColPids { get => _colPids; set => Set(ref _colPids, value); }
    public bool ColProcNow { get => _colProcNow; set => Set(ref _colProcNow, value); }
    public bool ColWattsNow { get => _colWattsNow; set => Set(ref _colWattsNow, value); }
    public bool ColWattsMax { get => _colWattsMax; set => Set(ref _colWattsMax, value); }
    public bool ColEnergy { get => _colEnergy; set => Set(ref _colEnergy, value); }

    private bool _colCpuPercent = false;
    private bool _colPids = true;
    private bool _colProcNow = true;
    private bool _colWattsNow = true;
    private bool _colWattsMax = true;
    private bool _colEnergy = true;

    public ICommand SetIntervalCommand { get; }
    public ICommand SetUiRefreshCommand { get; }
    public ICommand SetTopNCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ExportExcelCommand { get; }

    private readonly SessionMonitorService _service = new();
    private DateTime _lastUiUpdateUtc = DateTime.MinValue;

    private string? _excelExportPath;
    private readonly Dictionary<string, string> _aliasByApp = new(StringComparer.OrdinalIgnoreCase);

    public MainWindowViewModel()
    {
        SetIntervalCommand = new RelayCommand(p => _service.SetTargetPeriodMs(ParseInt(p, 500)));

        SetUiRefreshCommand = new RelayCommand(p =>
        {
            UiRefreshMs = ParseInt(p, 500);
            _lastUiUpdateUtc = DateTime.MinValue;
        });

        SetTopNCommand = new RelayCommand(p =>
        {
            TopNApps = ParseInt(p, 200);
            Refresh();
        });

        ResetCommand = new RelayCommand(_ =>
        {
            _service.ResetSession();
            Refresh();
        });

        StartCommand = new RelayCommand(_ => _service.Start());
        StopCommand = new RelayCommand(_ => _service.Stop());

        ExportExcelCommand = new RelayCommand(_ => ExportExcelInteractive());

        _service.Updated += OnUpdatedFromService;

        _service.Start();
        Refresh();

        AskExportOnStartup();
    }

    private void AskExportOnStartup()
    {
        var res = MessageBox.Show(
            "¿Quieres exportar los resultados de esta sesión a Excel?\n\n" +
            "Si dices que sí, elegirás ahora dónde guardar el archivo.",
            "Exportación a Excel",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (res != MessageBoxResult.Yes)
            return;

        var path = PickExcelPath();
        if (!string.IsNullOrWhiteSpace(path))
            _excelExportPath = path;
    }

    private void ExportExcelInteractive()
    {
        try
        {
            var path = _excelExportPath ?? PickExcelPath();
            if (string.IsNullOrWhiteSpace(path))
                return;

            _excelExportPath = path;

            ExcelSessionExporter.ExportSessionToXlsx(
                _service.Store,
                path,
                topNApps: TopNApps,
                priceEurPerKwh: PricePerKwh);

            MessageBox.Show(
                "Excel exportado correctamente.",
                "Exportación",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error exportando Excel:\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string? PickExcelPath()
    {
        var dlg = new SaveFileDialog
        {
            Title = "Guardar resumen de sesión",
            Filter = "Excel (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            FileName = $"session_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
        };

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private bool _refreshPending;

    private void OnUpdatedFromService()
    {
        try
        {
            var now = DateTime.UtcNow;
            if ((now - _lastUiUpdateUtc).TotalMilliseconds < UiRefreshMs) return;
            if (_refreshPending) return;

            _refreshPending = true;
            _lastUiUpdateUtc = now;

            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                try { Refresh(); }
                finally { _refreshPending = false; }
            }));
        }
        catch { _refreshPending = false; }
    }

    private void Refresh()
    {
        var top = _service.Store.GetTopAppsByEnergy(TopNApps);
        Apps.Clear();

        foreach (var a in top)
        {
            var alias = _aliasByApp.TryGetValue(a.AppName, out var v) ? v : a.AppName;
            Apps.Add(new AppRowVm(a, Unit, a.AppName, alias, OnRenameApp));
        }

        var totals = _service.Store.GetTotals();
        var (cpuName, gpuName) = _service.Store.GetHardwareNames();

        var cpuLabel = string.IsNullOrWhiteSpace(cpuName) ? "CPU" : $"CPU ({cpuName})";
        var gpuLabel = string.IsNullOrWhiteSpace(gpuName) ? "GPU" : $"GPU ({gpuName})";

        StatusLeft =
            $"Δt: {_service.Config.TargetPeriodMs} ms | UI: {UiRefreshMs} ms | " +
            $"{cpuLabel}: {totals.LastCpuWatts:0.0} W | {gpuLabel}: {totals.LastGpuWatts:0.0} W | TopN: {TopNApps}";

        StatusRight = $"t={totals.SessionSeconds:0}s";
        RefreshCostOnly();
    }

    private void OnRenameApp(string original, string? display)
    {
        if (string.IsNullOrWhiteSpace(display) || display == original)
            _aliasByApp.Remove(original);
        else
            _aliasByApp[original] = display.Trim();
    }

    private void RefreshCostOnly()
    {
        var totals = _service.Store.GetTotals();
        var kwh = totals.TotalWh / 1000.0;
        var eur = kwh * PricePerKwh;

        CostSummaryLine1 = $"Energía total: {totals.TotalWh:0.000} Wh";
        CostSummaryLine2 = $"Coste estimado: {eur:0.000} €";
    }

    private static int ParseInt(object? p, int fb) => int.TryParse(p?.ToString(), out var v) ? v : fb;
    private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;

    public sealed class AppRowVm : NotifyBase
    {
        private readonly AppEnergyRow _row;
        private readonly EnergyUnit _unit;
        private readonly Action<string, string?> _onRename;

        public string OriginalName { get; }

        public AppRowVm(AppEnergyRow row, EnergyUnit unit, string original, string display,
                        Action<string, string?> onRename)
        {
            _row = row;
            _unit = unit;
            OriginalName = original;
            _displayName = display;
            _onRename = onRename;
        }

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (Set(ref _displayName, value))
                {
                    _onRename(OriginalName, value);
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        _displayName = OriginalName;
                        Raise(nameof(DisplayName));
                    }
                }
            }
        }
        private string _displayName;

        public double LastCpuPercent => _row.LastCpuPercent;
        public int ProcessCount => _row.ProcessCount;
        public int LastSeenProcessCount => _row.LastSeenProcessCount;
        public double LastAllocatedWatts => _row.LastAllocatedWatts;
        public double MaxWatts => _row.MaxWatts;

        public string EnergyDisplay =>
            _unit == EnergyUnit.Wh
                ? $"{_row.TotalWh:0.000} Wh"
                : $"{_row.TotalJoules:0.0} J";
    }
}
