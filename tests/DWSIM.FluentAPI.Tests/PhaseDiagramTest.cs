using System;
using DWSIM.Automation.FluentAPI;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Exercises CalculatePhaseEnvelope, CalculateBinaryDiagram_*, CalculateCriticalPoints on a Methane/Ethane mixture.</summary>
    internal static class PhaseDiagramTest
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("PhaseDiagramTest")
                .WithCompound("Methane")
                .WithCompound("Ethane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var stream = fs.AddMaterialStream("feed")
                .WithComposition(c => c.Mole("Methane", 0.7).Mole("Ethane", 0.3));

            // Critical points
            var cps = stream.CalculateCriticalPoints();
            Console.WriteLine("Critical points: " + cps.Count);
            foreach (var cp in cps)
                Console.WriteLine($"  T={cp.TemperatureK:F2} K  P={cp.PressurePa / 1e5:F2} bar  V={cp.MolarVolumeM3PerMol:F6} m3/mol");
            if (cps.Count == 0) throw new Exception("Expected at least one mixture critical point.");

            // Phase envelope
            var env = stream.CalculatePhaseEnvelope();
            Console.WriteLine($"Phase envelope: {env.BubbleTemperaturesK.Count} bubble pts, {env.DewTemperaturesK.Count} dew pts, {env.CriticalPoints.Count} CP");
            if (env.BubbleTemperaturesK.Count < 5) throw new Exception("Bubble curve too short.");
            if (env.DewTemperaturesK.Count < 5) throw new Exception("Dew curve too short.");
            if (env.BubblePressuresPa.Count != env.BubbleTemperaturesK.Count) throw new Exception("Bubble T/P length mismatch.");

            // T-x-y at 10 bar
            var txy = stream.CalculateBinaryDiagram_Txy(pressurePa: 10e5, steps: 20);
            Console.WriteLine($"T-x-y: type={txy.DiagramType}, {txy.X.Count} points, T range {Min(txy.Y1):F2}-{Max(txy.Y2):F2} K");
            if (txy.X.Count < 10) throw new Exception("T-x-y too few points.");

            // P-x-y at 200 K
            var pxy = stream.CalculateBinaryDiagram_Pxy(temperatureK: 200.0, steps: 20);
            Console.WriteLine($"P-x-y: type={pxy.DiagramType}, {pxy.X.Count} points, P range {Min(pxy.Y1) / 1e5:F2}-{Max(pxy.Y2) / 1e5:F2} bar");
            if (pxy.X.Count < 10) throw new Exception("P-x-y too few points.");
        }

        private static double Min(System.Collections.Generic.IReadOnlyList<double> xs)
        {
            double m = double.PositiveInfinity;
            foreach (var x in xs) if (x < m) m = x;
            return m;
        }

        private static double Max(System.Collections.Generic.IReadOnlyList<double> xs)
        {
            double m = double.NegativeInfinity;
            foreach (var x in xs) if (x > m) m = x;
            return m;
        }
    }
}
