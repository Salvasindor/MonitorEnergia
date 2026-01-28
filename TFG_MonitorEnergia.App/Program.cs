using System;
using System.Linq;
using System.Threading;
using TFG_MonitorEnergia.Hardware;

namespace TFG_MonitorEnergia.App
{
    internal class Program
    {
        static void Main()
        {
            double intervalSec = 0.5;
            bool running = true;
            bool paused = false;

            using var sampler = new LhmSampler();
            sampler.Open();

            Console.WriteLine("Monitor de sensores (modo desarrollo)");
            Console.WriteLine("Teclas:");
            Console.WriteLine(" 1=0.25s  2=0.5s  3=0.75s  4=1.0s");
            Console.WriteLine(" S=pausa/reanudar   Q=salir\n");
            Console.WriteLine("Pulsa cualquier tecla para comenzar...");
            Console.ReadKey(true);

            while (running)
            {
                // --- Gestión de teclas ---
                while (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;
                    switch (key)
                    {
                        case ConsoleKey.D1:
                        case ConsoleKey.NumPad1:
                            intervalSec = 0.25;
                            break;

                        case ConsoleKey.D2:
                        case ConsoleKey.NumPad2:
                            intervalSec = 0.5;
                            break;

                        case ConsoleKey.D3:
                        case ConsoleKey.NumPad3:
                            intervalSec = 0.75;
                            break;

                        case ConsoleKey.D4:
                        case ConsoleKey.NumPad4:
                            intervalSec = 1.0;
                            break;

                        case ConsoleKey.S:
                            paused = !paused;
                            if (paused)
                            {
                                Console.WriteLine(
                                    "\n[PAUSA] Pantalla congelada");
                                Console.WriteLine("Pulsa S para continuar\n");
                            }
                            break;

                        case ConsoleKey.Q:
                        case ConsoleKey.Escape:
                            running = false;
                            break;
                    }
                }

                if (!running)
                    break;

                // --- Refresco ---
                if (!paused)
                {
                    var data = sampler.ReadAll();

                    Console.WriteLine(
                        $"\n--- Refresco {DateTime.Now:HH:mm:ss.fff} | Δt={intervalSec:0.##} s ---\n");

                    var hwList = data
                        .Select(d => (d.HwType, d.HwName))
                        .Distinct()
                        .OrderBy(x => x.HwType)
                        .ThenBy(x => x.HwName)
                        .ToList();

                    foreach (var hw in hwList)
                    {
                        Console.WriteLine($"[{hw.HwType}] {hw.HwName}");

                        foreach (var s in data.Where(d =>
                            d.HwType == hw.HwType && d.HwName == hw.HwName))
                        {
                            string value = s.Value.HasValue
                                ? s.Value.Value.ToString("0.###")
                                : "null";

                            string unit = UnitFor(s.SensorType);

                            Console.WriteLine(
                                $"   -> {s.SensorType} | {s.SensorName}: {value} {unit}".TrimEnd());
                        }

                        Console.WriteLine();
                    }
                }

                //  Espera controlada 
                int totalMs = (int)(intervalSec * 1000);
                int stepMs = 50;
                int waited = 0;

                while (waited < totalMs && running)
                {
                    Thread.Sleep(stepMs);
                    waited += stepMs;

                    if (Console.KeyAvailable)
                        break;
                }
            }
        }

        static string UnitFor(string sensorType) => sensorType switch
        {
            "Temperature" => "°C",
            "Power" => "W",
            "Load" => "%",
            "Clock" => "MHz",
            "Voltage" => "V",
            "Current" => "A",
            "Fan" => "RPM",
            "Flow" => "L/h",
            "Control" => "%",
            "Data" => "GB",
            "SmallData" => "MB",
            "Throughput" => "B/s",
            "Energy" => "Wh",
            _ => ""
        };
    }
}