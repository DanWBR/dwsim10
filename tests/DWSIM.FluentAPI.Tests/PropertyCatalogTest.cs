using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Checks that property identifiers come back with the names a person would recognise.
    /// </summary>
    /// <remarks>
    /// <c>PROP_MS_2</c> is a mass flow, and nothing about the identifier says so. Whoever reads
    /// the catalogue — a person or a model — picks the property to monitor, disturb or control by
    /// its name, so a catalogue that echoes the identifier back is no catalogue at all.
    ///
    /// The name lives in a resource file the flowsheet resolves through
    /// <c>GetTranslatedString</c>, which needs the property resource manager to be reachable from
    /// wherever the object is. That is easy to lose headless, and the loss is silent.
    /// </remarks>
    internal static class PropertyCatalogTest
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("PropertyNames")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            fs.AddMaterialStream("feed")
                .At(300.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.KgPerSecond());

            var entries = fs.MonitorableProperties("feed");

            Console.WriteLine();
            Console.WriteLine("First few properties of a material stream:");
            foreach (var e in entries.Take(6))
                Console.WriteLine($"  {e.Id,-14} {e.Description,-28} ({e.Units})");

            // The identifiers whose names are worth being sure about, because they are the ones
            // an event, a monitored variable or a controller will address.
            var expected = new[]
            {
                ("PROP_MS_0", "Temperature"),
                ("PROP_MS_1", "Pressure"),
                ("PROP_MS_2", "Mass Flow"),
            };

            foreach (var (id, name) in expected)
            {
                var entry = entries.FirstOrDefault(e => e.Id == id);
                if (entry == null)
                    throw new Exception($"The catalogue has no '{id}'.");

                if (!string.Equals(entry.Description, name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception(
                        $"'{id}' is described as '{entry.Description}', expected '{name}'. " +
                        "The property resource manager is not reachable from the object, so every " +
                        "identifier reads back as itself.");
                }
            }

            var echoed = entries.Count(e => e.Description == e.Id);
            Console.WriteLine($"  {echoed} of {entries.Count} properties have no name of their own");

            // A handful of identifiers genuinely have no entry in the resource file; most having
            // none means the lookup is broken rather than incomplete.
            if (echoed > entries.Count / 2)
            {
                throw new Exception(
                    $"{echoed} of {entries.Count} properties echo their identifier back, so the " +
                    "name lookup is not working.");
            }
        }
    }
}
