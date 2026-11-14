using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Interfaces.Enums;
using DWSIM.Thermodynamics.PropertyPackages.Auxiliary;
using DWSIM.Validation.Tests.Framework;
using TPP = DWSIM.Thermodynamics.PropertyPackages;

namespace DWSIM.Validation.Tests.Thermodynamics
{
    /// <summary>Solid forms of a compound. Two forms are given to naphthalene in toluene: form I with the fusion data
    /// of the compound and a made-up form II that melts 10 K lower with a 15 % higher enthalpy of fusion, which makes
    /// the pair enantiotropic. The transition temperature agrees with its closed form, the stable form changes at it,
    /// the list survives being stored in the compound, the saturation with a form satisfies the solid-liquid
    /// equilibrium expression, and after a form is applied to the compound the flash with solids gives the liquid that
    /// the saturation with that form gives.</summary>
    internal static class T37_NRTL_SolidForms
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("T37_SolidForms")
                .WithCompounds("Naphthalene", "Toluene")
                .WithPropertyPackage(PropertyPackages.NRTL,
                    p => p.WithFlashSetting(FlashSetting.HandleSolidsInDefaultEqCalcMode, true));

            const double P = 101325.0, T = 280.0;

            var s = fs.AddMaterialStream("slurry").At(T.Kelvin(), P.Pascal()).WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Naphthalene", 0.4).SetCompoundMolarFlow("Toluene", 0.6);
            fs.Solve();

            var cp = s.Object.Phases[0].Compounds["Naphthalene"].ConstantProperties;
            var pp = (TPP.PropertyPackage)s.Object.PropertyPackage;
            pp.CurrentMaterialStream = s.Object;
            int solute = Array.IndexOf(pp.RET_VNAMES(), "Naphthalene");
            var solvent = new double[2];
            solvent[1 - solute] = 1.0;

            double Tf1 = cp.TemperatureOfFusion, H1 = cp.EnthalpyOfFusionAtTf;
            var formI = new SolidForm { Name = "Form I", TemperatureOfFusion = Tf1, EnthalpyOfFusion = H1 };
            var formII = new SolidForm { Name = "Form II", TemperatureOfFusion = Tf1 - 10.0, EnthalpyOfFusion = H1 * 1.15, Density = 1100.0 };

            // without a heat capacity difference the two fugacity lines cross at this temperature
            double ttrExact = (formII.EnthalpyOfFusion - H1) / (formII.EnthalpyOfFusion / formII.TemperatureOfFusion - H1 / Tf1);
            var pair = SolidForms.Compare(formI, formII);

            SolidForms.SetForms(cp, new List<SolidForm> { formI, formII });
            var stored = SolidForms.GetForms(cp);
            double storedH2 = stored.Count == 2 ? stored[1].EnthalpyOfFusion : double.NaN;

            // a monotropic pair: the second form melts lower with a lower enthalpy of fusion
            var formM = new SolidForm { Name = "Form M", TemperatureOfFusion = Tf1 - 10.0, EnthalpyOfFusion = H1 * 0.9 };
            var mono = SolidForms.Compare(formI, formM);

            pp.CurrentMaterialStream = s.Object;
            var satDefault = SolidSolubility.Saturation(pp, solute, solvent, T, P);
            var satI = SolidSolubility.Saturation(pp, solute, solvent, T, P, formI);
            var satII = SolidSolubility.Saturation(pp, solute, solvent, T, P, formII);
            double liquidI = s.Object.Phases[3].Compounds["Naphthalene"].MoleFraction.GetValueOrDefault();

            SolidForms.Apply(cp, formII);
            fs.Solve();
            double liquidII = s.Object.Phases[3].Compounds["Naphthalene"].MoleFraction.GetValueOrDefault();
            string active = SolidForms.GetActiveFormName(cp);
            pp.CurrentMaterialStream = s.Object;
            var satApplied = SolidSolubility.Saturation(pp, solute, solvent, T, P);

            new ResultTable("Solid forms - naphthalene / toluene (NRTL), form II made up")
                .Row("Transition temperature against its closed form", ttrExact, pair.TransitionTemperature, 0.0001, "K")
                .Row("The pair is enantiotropic", 1.0, pair.Enantiotropic ? 1.0 : 0.0, 0.001, "-")
                .Row("Form II is the stable one 5 K below the transition", 1.0, SolidForms.MostStable(stored, ttrExact - 5.0).Name == "Form II" ? 1.0 : 0.0, 0.001, "-")
                .Row("Form I is the stable one 5 K above the transition", 1.0, SolidForms.MostStable(stored, ttrExact + 5.0).Name == "Form I" ? 1.0 : 0.0, 0.001, "-")
                .Row("Forms stored in the compound and read back", formII.EnthalpyOfFusion, storedH2, 1e-9, "kJ/mol")
                .Row("A form that melts lower with a lower enthalpy of fusion is monotropic", 0.0, mono.Enantiotropic ? 1.0 : 0.0, 0.001, "-")
                .Row("Saturation with form I equals the one with the data of the compound", satDefault.MoleFraction, satI.MoleFraction, 1e-9, "-")
                .Row("x.gamma with form II equals the activity of form II", Math.Exp(formII.LnActivity(T)), satII.MoleFraction * satII.ActivityCoefficient, 1e-6, "-")
                .Row("Flash with solids before applying a form against the saturation with form I", satI.MoleFraction, liquidI, 0.001, "-")
                .Row("Flash with solids after applying form II against the saturation with form II", satII.MoleFraction, liquidII, 0.001, "-")
                .Row("Saturation without a form after applying form II", satII.MoleFraction, satApplied.MoleFraction, 1e-9, "-")
                .Row("Name of the applied form is recorded", 1.0, active == "Form II" ? 1.0 : 0.0, 0.001, "-")
                .RowInRange("Form II is the less soluble one at 280 K", 0.0, 0.999, satII.MoleFraction / satI.MoleFraction, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
