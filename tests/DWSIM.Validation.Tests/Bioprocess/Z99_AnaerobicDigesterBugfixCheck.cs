using DWSIM.Automation.FluentAPI;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UnitOperations.Reactors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DWSIM.Validation.Tests.Bioprocess
{
    internal static class Z99_AnaerobicDigesterBugfixCheck
    {
        public static void Run()
        {
            var dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var files = new[]
            {
                Path.Combine(dl, "160-Biogas_blackbox.dwxmz"),
                Path.Combine(dl, "160-Biogas_LiteADM_HRT_240_V29.4m3.dwxmz"),
                Path.Combine(dl, "160-Biogas_LiteADM_240_HRT_0_V500.dwxmz"),
            };

            foreach (var f in files)
            {
                Console.WriteLine();
                Console.WriteLine("==== " + Path.GetFileName(f) + " ====");
                if (!File.Exists(f)) { Console.WriteLine("MISSING"); continue; }
                Flowsheet fs;
                try { fs = Flowsheet.Load(f); }
                catch (Exception ex) { Console.WriteLine("LOAD FAIL: " + ex.Message); continue; }

                var errs = fs.TrySolve();
                if (errs.Count > 0)
                    foreach (var e in errs.Take(3)) Console.WriteLine("  solve err: " + e.Message);

                Reactor_AnaerobicDigester ad = null;
                foreach (var so in fs.Inner.SimulationObjects.Values)
                    if (so is Reactor_AnaerobicDigester rad) { ad = rad; break; }
                if (ad == null) { Console.WriteLine("no AD found"); continue; }

                Console.WriteLine("  Tag                : " + ad.GraphicObject.Tag);
                Console.WriteLine("  Model              : " + ad.Model);
                Console.WriteLine("  Volume             : " + ad.Volume + " m3");
                Console.WriteLine("  HRT                : " + (ad.HRT_s / 86400.0).ToString("F3") + " d");
                Console.WriteLine("  COD removed        : " + (ad.Result_CODremoved_kgs * 3600.0).ToString("F4") + " kg/h");
                Console.WriteLine("  CH4 produced       : " + (ad.Result_CH4_kgs * 3600.0).ToString("F4") + " kg/h");
                Console.WriteLine("  CO2 produced       : " + (ad.Result_CO2_kgs * 3600.0).ToString("F4") + " kg/h");
                Console.WriteLine("  Sludge produced    : " + (ad.Result_Sludge_kgs * 3600.0).ToString("F4") + " kg/h");
                Console.WriteLine("  Spec.CH4 yield     : " + ad.Result_SpecificCH4Yield_Nm3kgCOD.ToString("F4") + " Nm3/kgCOD");
                Console.WriteLine("  Q_metabolic        : " + ad.Result_Q_metabolic_kW.ToString("F3") + " kW");
                Console.WriteLine("  Q_duty             : " + ad.Result_Q_duty_kW.ToString("F3") + " kW");

                MaterialStream feed = null, eff = null, gas = null;
                if (ad.GraphicObject.InputConnectors.Count > 0 && ad.GraphicObject.InputConnectors[0].IsAttached)
                    feed = (MaterialStream)fs.Inner.SimulationObjects[ad.GraphicObject.InputConnectors[0].AttachedConnector.AttachedFrom.Name];
                if (ad.GraphicObject.OutputConnectors.Count > 0 && ad.GraphicObject.OutputConnectors[0].IsAttached)
                    eff = (MaterialStream)fs.Inner.SimulationObjects[ad.GraphicObject.OutputConnectors[0].AttachedConnector.AttachedTo.Name];
                if (ad.GraphicObject.OutputConnectors.Count > 1 && ad.GraphicObject.OutputConnectors[1].IsAttached)
                    gas = (MaterialStream)fs.Inner.SimulationObjects[ad.GraphicObject.OutputConnectors[1].AttachedConnector.AttachedTo.Name];

                double mFeed = feed?.Phases[0].Properties.massflow.GetValueOrDefault() ?? 0.0;
                double mEff = eff?.Phases[0].Properties.massflow.GetValueOrDefault() ?? 0.0;
                double mGas = gas?.Phases[0].Properties.massflow.GetValueOrDefault() ?? 0.0;
                double residual = mFeed - mEff - mGas;
                Console.WriteLine();
                Console.WriteLine("  MASS BALANCE (kg/h):");
                Console.WriteLine("    feed             : " + (mFeed * 3600.0).ToString("F4"));
                Console.WriteLine("    effluent         : " + (mEff * 3600.0).ToString("F4"));
                Console.WriteLine("    biogas           : " + (mGas * 3600.0).ToString("F4"));
                Console.WriteLine("    residual         : " + (residual * 3600.0).ToString("F6") + "  ( " + (mFeed > 0 ? (residual / mFeed * 100.0).ToString("F4") : "n/a") + " % )");

                if (ad.Volume > 0 && feed != null)
                {
                    var Q = feed.Phases[1].Properties.volumetric_flow.GetValueOrDefault();
                    if (Q <= 0) Q = feed.Phases[0].Properties.volumetric_flow.GetValueOrDefault();
                    if (Q > 0)
                    {
                        double tau_VQ = ad.Volume / Q / 86400.0;
                        Console.WriteLine("  V/Q (HRT from V,Q): " + tau_VQ.ToString("F3") + " d");
                    }
                }
            }
        }
    }
}
