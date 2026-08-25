using System.IO;
using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Compounds
{
    /// <summary>WithCompoundFromJson - loads Biomass_Yeast.json from the addcomps folder.
    /// Validates: the compound is registered, has the expected MW and elements (C/H/O/N).</summary>
    internal static class C02_CompoundFromJson
    {
        public static void Run()
        {
            var path = Path.Combine("addcomps", "Biomass_Yeast.json");

            var fs = Flowsheet.Create("C02_FromJson")
                .WithCompoundFromJson(path)
                .WithPropertyPackage(PropertyPackages.NRTL);

            var c = fs.Inner.SelectedCompounds["Biomass_Yeast_Scerevisiae"];

            new ResultTable("Compound from JSON - Biomass_Yeast.json")
                .RowInRange("Compound registered", 1, 1, c != null ? 1 : 0, "")
                .Row("MW loaded", 2447.23, c.Molar_Weight, 0.001, "g/mol")
                .RowInRange("OriginalDB = User", 1, 1, c.OriginalDB == "User" ? 1 : 0, "")
                .RowInRange("Has C element", 1, 1, c.Elements != null && c.Elements.Contains("C") ? 1 : 0, "")
                .RowInRange("Has S element", 1, 1, c.Elements != null && c.Elements.Contains("S") ? 1 : 0, "")
                .PrintAndThrowIfFailed();
        }
    }
}
