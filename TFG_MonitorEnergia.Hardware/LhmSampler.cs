using LibreHardwareMonitor.Hardware;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TFG_MonitorEnergia.Hardware
{
    public sealed class LhmSampler : IDisposable
    {
        private readonly Computer _computer;

        public LhmSampler()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
                IsNetworkEnabled = true,
                IsStorageEnabled = true
            };
        }

        public void Open()
        {
            _computer.Open();
            _computer.Accept(new UpdateVisitor());
        }

        public IReadOnlyList<(string HwType, string HwName, string SensorType, string SensorName, float? Value)> ReadAll()
        {
            _computer.Accept(new UpdateVisitor());

            var list = new List<(string, string, string, string, float?)>();
            foreach (var hw in _computer.Hardware)
                ReadRecursive(hw, list);

            return list;
        }

        public double? GetCpuPackagePowerWatts()
        {
            _computer.Accept(new UpdateVisitor());

            var cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            if (cpu == null) return null;

            cpu.Update();

            var sensor = cpu.Sensors.FirstOrDefault(s =>
                s.SensorType == SensorType.Power &&
                s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase));

            if (sensor?.Value is null) return null;

            var v = (double)sensor.Value.Value;
            return double.IsNaN(v) ? null : v;
        }

        public double? GetCpuTotalLoadPercent()
        {
            _computer.Accept(new UpdateVisitor());

            var cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            if (cpu == null) return null;

            cpu.Update();

            var sensor = cpu.Sensors.FirstOrDefault(s =>
                s.SensorType == SensorType.Load &&
                s.Name.Contains("CPU Total", StringComparison.OrdinalIgnoreCase));

            if (sensor?.Value is null) return null;

            var v = (double)sensor.Value.Value;
            return double.IsNaN(v) ? null : v;
        }

        public double? GetGpuPackagePowerWatts()
        {
            _computer.Accept(new UpdateVisitor());

            // Primera GPU detectada (para tu RX 6650 XT suele ser suficiente)
            var gpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType.ToString().StartsWith("Gpu"));
            if (gpu == null) return null;

            gpu.Update();

            var sensor = gpu.Sensors.FirstOrDefault(s =>
                s.SensorType == SensorType.Power &&
                s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase));

            if (sensor?.Value is null) return null;

            var v = (double)sensor.Value.Value;
            return double.IsNaN(v) ? null : v;
        }

        private static void ReadRecursive(
            IHardware hw,
            List<(string, string, string, string, float?)> list)
        {
            foreach (var s in hw.Sensors)
                list.Add((hw.HardwareType.ToString(), hw.Name, s.SensorType.ToString(), s.Name, s.Value));

            foreach (var sub in hw.SubHardware)
            {
                sub.Update();
                ReadRecursive(sub, list);
            }
        }

        public void Dispose()
        {
            try { _computer.Close(); } catch { }
        }

        private sealed class UpdateVisitor : IVisitor
        {
            public void VisitComputer(IComputer computer) => computer.Traverse(this);

            public void VisitHardware(IHardware hardware)
            {
                hardware.Update();
                foreach (var sub in hardware.SubHardware)
                    sub.Accept(this);
            }

            public void VisitSensor(ISensor sensor) { }
            public void VisitParameter(IParameter parameter) { }
        }
    }
}