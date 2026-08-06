using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Lists every external unit operation (bioprocess + refining + Plus) discovered
    /// at runtime via IFlowsheet.AvailableSimulationObjects, then attempts to
    /// instantiate the bioprocess UOs we know are free (no patron-key required).
    /// </summary>
    internal static class ExternalCatalogTest
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("ExternalCatalogProbe");

            Console.WriteLine("Available external unit operations:");
            foreach (var name in fs.AvailableExternalUnitOperationNames)
                Console.WriteLine("  - " + name + (ExternalCatalog.RequiresPlus(name) ? "  [Plus]" : ""));

            // Spot-check: bioprocess UOs are free, instantiate one of each known name
            // that the runtime actually has registered. Plus UOs are skipped because
            // License.Activate would be required.
            int created = 0;
            foreach (var name in ExternalCatalog.Bioprocess.All)
            {
                if (!fs.AvailableExternalUnitOperationNames.Contains(name)) continue;
                try
                {
                    var b = fs.AddExternalUnitOperation(name, "T-" + (created + 1));
                    Console.WriteLine("  + created '" + name + "' as " + b.Object.GraphicObject.Tag);
                    created++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("  ! failed '" + name + "': " + ex.Message);
                }
            }
            Console.WriteLine("Bioprocess UOs instantiated: " + created);
        }
    }
}
