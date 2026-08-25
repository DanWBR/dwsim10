using System.Collections;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Interfaces.Enums;
using DWSIM.Validation.Tests.Framework;
using PP = DWSIM.Thermodynamics.PropertyPackages;

namespace DWSIM.Validation.Tests.PropertyPackageConfig
{
    /// <summary>WithFlashApproach + WithFlashSetting - configures the flash via the Fluent API.
    /// Verifies: FlashCalculationApproach enum was applied and flash settings were stored.</summary>
    internal static class P03_FlashSettings
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("P03_FlashSettings")
                .WithCompounds("Water", "Ethanol")
                .WithPropertyPackage(PropertyPackages.NRTL, pp => pp
                    .WithFlashApproach(PP.PropertyPackage.FlashCalculationApproachType.GibbsMinimization)
                    .WithFlashSetting(FlashSetting.PTFlash_External_Loop_Tolerance, 1e-7)
                    .WithFlashSetting(FlashSetting.PTFlash_Maximum_Number_Of_External_Iterations, 200)
                    .WithFlashSetting(FlashSetting.CalculateBubbleAndDewPoints, true));

            // Read props via reflection to avoid pulling the CAPE-OPEN interfaces into the consumer.
            var ppObj = fs.Inner.PropertyPackages.Values.First();
            var approach = (int)ppObj.GetType().GetProperty("FlashCalculationApproach").GetValue(ppObj);
            var settings = (IDictionary)ppObj.GetType().GetProperty("FlashSettings").GetValue(ppObj);

            new ResultTable("Flash settings via Fluent API")
                .RowInRange("FlashApproach = GibbsMinimization (=2)", 2, 2, approach, "")
                .RowInRange("PTFlash external tol set", 1, 1,
                    settings[FlashSetting.PTFlash_External_Loop_Tolerance] != null ? 1 : 0, "")
                .RowInRange("Max iter = 200", 1, 1,
                    (string)settings[FlashSetting.PTFlash_Maximum_Number_Of_External_Iterations] == "200" ? 1 : 0, "")
                .RowInRange("CalcBubbleDew enabled", 1, 1,
                    (string)settings[FlashSetting.CalculateBubbleAndDewPoints] == "True" ? 1 : 0, "")
                .PrintAndThrowIfFailed();
        }
    }
}
