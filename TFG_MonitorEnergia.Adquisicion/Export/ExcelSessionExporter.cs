using ClosedXML.Excel;
using System.IO;
using System.Linq;
using TFG_MonitorEnergia.SessionData.Export.Models;
using TFG_MonitorEnergia.SessionData.Storage;

namespace TFG_MonitorEnergia.SessionData.Export;

public static class ExcelSessionExporter
{
    public static string ExportSessionToXlsx(
        SessionDataStore store,
        string outputPath,
        int topNApps = 200,
        double? priceEurPerKwh = null)
    {
        var apps = store.GetTopAppsByEnergy(topNApps);
        var totals = store.GetTotals();
        var (startUtc, endUtc) = store.GetSessionBoundsUtc();
        var (dtMin, dtAvg, dtMax) = store.GetDtStatsSeconds();
        var timeline = store.GetTimelineTotal();
        var eventsList = store.GetEvents();
        var peaks = store.GetPeaks();

        // Nombres HW (WMI)
        var (cpuName, gpuName) = store.GetHardwareNames();

        double totalCpuWh = totals.TotalCpuJoules / 3600.0;
        double totalGpuWh = totals.TotalGpuJoules / 3600.0;
        double totalWh = totalCpuWh + totalGpuWh;

        using var wb = new XLWorkbook();

        
        // Sheet 1: Summary
    
        var wsSummary = wb.Worksheets.Add("Summary");
        int r = 1;

        void KV(string k, object? v)
        {
            wsSummary.Cell(r, 1).Value = k;

            var cell = wsSummary.Cell(r, 2);

            if (v is null)
                cell.Value = "";
            else if (v is XLCellValue x)
                cell.Value = x;
            else if (v is System.DateTime dt)
                cell.Value = dt;
            else if (v is bool b)
                cell.Value = b;
            else if (v is int i)
                cell.Value = i;
            else if (v is long l)
                cell.Value = l;
            else if (v is float f)
                cell.Value = (double)f;
            else if (v is double d)
                cell.Value = d;
            else if (v is decimal m)
                cell.Value = (double)m;
            else
                cell.Value = v.ToString() ?? "";

            r++;
        }

        wsSummary.Cell(1, 1).Style.Font.Bold = true;
        wsSummary.Cell(1, 2).Style.Font.Bold = true;

        KV("CpuDeviceName", cpuName ?? "");
        KV("GpuDeviceName", gpuName ?? "");

        KV("SessionStartUtc", startUtc);
        KV("SessionEndUtc", endUtc);
        KV("Duration_s", totals.SessionSeconds);

        KV("DtMin_ms", dtMin * 1000.0);
        KV("DtAvg_ms", dtAvg * 1000.0);
        KV("DtMax_ms", dtMax * 1000.0);

        KV("CPU_Wh", totalCpuWh);
        KV("GPU_Wh", totalGpuWh);
        KV("Total_Wh", totalWh);

        KV("CPU_W_last", totals.LastCpuWatts);
        KV("GPU_W_last", totals.LastGpuWatts);
        KV("Total_W_last", totals.LastCpuWatts + totals.LastGpuWatts);

        KV("CPU_W_peak", peaks.CpuPeakW);
        KV("GPU_W_peak", peaks.GpuPeakW);
        KV("Total_W_peak", peaks.TotalPeakW);

        KV("AppsSeen_Count", apps.Count);

        if (apps.Count > 0)
        {
            var topApp = apps.OrderByDescending(a => a.TotalWh).First();
            KV("TopApp_ByEnergy", topApp.AppName);
            KV("TopApp_Wh", topApp.TotalWh);
        }

        if (priceEurPerKwh.HasValue)
        {
            var cost = (totalWh / 1000.0) * priceEurPerKwh.Value;
            KV("Price_EUR_per_kWh", priceEurPerKwh.Value);
            KV("Cost_EUR", cost);
        }

        wsSummary.Columns().AdjustToContents();

      
        // Sheet 2: Apps
      
        var wsApps = wb.Worksheets.Add("Apps");

        wsApps.Cell(1, 1).Value = "AppName";
        wsApps.Cell(1, 2).Value = "PIDsSeen";
        wsApps.Cell(1, 3).Value = "ProcNow";
        wsApps.Cell(1, 4).Value = "CPU%_last";
        wsApps.Cell(1, 5).Value = "W_last";
        wsApps.Cell(1, 6).Value = "W_peak";
        wsApps.Cell(1, 7).Value = "Energy_Wh";

        wsApps.Range(1, 1, 1, 7).Style.Font.Bold = true;

        int rr = 2;
        foreach (var a in apps)
        {
            wsApps.Cell(rr, 1).Value = a.AppName;
            wsApps.Cell(rr, 2).Value = a.ProcessCount;
            wsApps.Cell(rr, 3).Value = a.LastSeenProcessCount;
            wsApps.Cell(rr, 4).Value = a.LastCpuPercent;
            wsApps.Cell(rr, 5).Value = a.LastAllocatedWatts;
            wsApps.Cell(rr, 6).Value = a.MaxWatts;
            wsApps.Cell(rr, 7).Value = a.TotalWh;
            rr++;
        }

        wsApps.Columns().AdjustToContents();

   
        // Sheet 3: Timeline_Total
      
        var wsTime = wb.Worksheets.Add("Timeline_Total");

        wsTime.Cell(1, 1).Value = "TimestampUtc";
        wsTime.Cell(1, 2).Value = "t_s";
        wsTime.Cell(1, 3).Value = "dt_s";
        wsTime.Cell(1, 4).Value = "CPU_W_avg_1s";
        wsTime.Cell(1, 5).Value = "GPU_W_avg_1s";
        wsTime.Cell(1, 6).Value = "Total_W_avg_1s";
        wsTime.Cell(1, 7).Value = "CPU_Wh_cum";
        wsTime.Cell(1, 8).Value = "GPU_Wh_cum";
        wsTime.Cell(1, 9).Value = "Total_Wh_cum";

        wsTime.Range(1, 1, 1, 9).Style.Font.Bold = true;

        rr = 2;
        foreach (var p in timeline)
        {
            wsTime.Cell(rr, 1).Value = p.TimestampUtc;
            wsTime.Cell(rr, 2).Value = p.TSeconds;
            wsTime.Cell(rr, 3).Value = p.DtSeconds;
            wsTime.Cell(rr, 4).Value = p.CpuWAvg;
            wsTime.Cell(rr, 5).Value = p.GpuWAvg;
            wsTime.Cell(rr, 6).Value = p.TotalWAvg;
            wsTime.Cell(rr, 7).Value = p.CpuWhCum;
            wsTime.Cell(rr, 8).Value = p.GpuWhCum;
            wsTime.Cell(rr, 9).Value = p.TotalWhCum;
            rr++;
        }

        wsTime.Columns().AdjustToContents();

     
        // Sheet 4: Peaks_Events
      
        var wsEv = wb.Worksheets.Add("Peaks_Events");

        wsEv.Cell(1, 1).Value = "TimestampUtc";
        wsEv.Cell(1, 2).Value = "t_s";
        wsEv.Cell(1, 3).Value = "Type";
        wsEv.Cell(1, 4).Value = "AppName";
        wsEv.Cell(1, 5).Value = "Value_W";
        wsEv.Cell(1, 6).Value = "Notes";

        wsEv.Range(1, 1, 1, 6).Style.Font.Bold = true;

        rr = 2;
        foreach (var e in eventsList)
        {
            wsEv.Cell(rr, 1).Value = e.TimestampUtc;
            wsEv.Cell(rr, 2).Value = e.TSeconds;
            wsEv.Cell(rr, 3).Value = e.Type;
            wsEv.Cell(rr, 4).Value = e.AppName ?? "";
            wsEv.Cell(rr, 5).Value = e.ValueW;
            wsEv.Cell(rr, 6).Value = e.Notes ?? "";
            rr++;
        }

        wsEv.Columns().AdjustToContents();

        // Guardar archivo
       
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        wb.SaveAs(outputPath);

        return outputPath;
    }
}
