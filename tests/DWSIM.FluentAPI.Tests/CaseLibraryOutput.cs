using System;
using System.IO;
using System.Linq;
using DWSIM.Automation.FluentAPI;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Saves a solved sample flowsheet (.dwxmz + PFD screenshot) for the dwsim-case-library
    /// and proves the file is worth publishing: it must load back and re-solve cleanly.
    /// Output goes to DWSIM_CASE_LIBRARY_DIR, or to "case-library" beside the test binaries.
    /// </summary>
    internal static class CaseLibraryOutput
    {
        public static string DirFor(string caseName)
        {
            var root = Environment.GetEnvironmentVariable("DWSIM_CASE_LIBRARY_DIR");
            if (string.IsNullOrWhiteSpace(root))
                root = Path.Combine(AppContext.BaseDirectory, "case-library");
            var dir = Path.Combine(root, caseName);
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void Emit(Flowsheet fs, string caseName)
        {
            var dir = DirFor(caseName);
            var dwxmz = Path.Combine(dir, caseName + ".dwxmz");

            // NaturalLayout dies on some external-UO topologies; AutoLayout always works.
            try { fs.NaturalLayout(); }
            catch (Exception) { fs.AutoLayout(); }
            fs.Save(dwxmz);
            fs.SaveScreenshot(Path.Combine(dir, caseName + ".png"));

            // The publishing gate: the saved file must open and solve again on its own.
            var reloaded = Flowsheet.Load(dwxmz);
            var errors = reloaded.TrySolve();
            if (errors.Count > 0)
                throw new Exception(
                    $"{caseName}: the saved flowsheet does not re-solve: {errors[0].Message}");

            // gauges and other indicators display, they do not calculate; a PID controller calculates only
            // during a dynamic run, so a file saved before its first run carries it uncalculated
            var broken = reloaded.Inner.SimulationObjects.Values
                .Where(o => !o.Calculated)
                .Where(o => o.GraphicObject == null ||
                            (o.GraphicObject.ObjectType != DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.AnalogGauge &&
                             o.GraphicObject.ObjectType != DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.DigitalGauge &&
                             o.GraphicObject.ObjectType != DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.LevelGauge &&
                             o.GraphicObject.ObjectType != DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.Controller_PID))
                .Select(o => o.GraphicObject?.Tag ?? o.Name)
                .ToList();
            if (broken.Count > 0)
                throw new Exception(
                    $"{caseName}: after reload, not calculated: {string.Join(", ", broken)}");

            Console.WriteLine($"  [case-library] {dwxmz} saved, reloaded and re-solved.");
        }
    }
}
