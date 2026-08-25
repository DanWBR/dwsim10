using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using DWSIM.UnitOperations.Reactors;

namespace DWSIM.Validation.Tests.Flowsheets
{
    /// <summary>F18 - Anaerobic digester in all three models.
    /// (A) BlackBox (Buswell): glucose feed, HRT 20 d, 80% COD removal, 65% CH4 override.
    /// (B) ADM1-Lite: same feed, HRT 25 d, ODE integration with 4 biomass populations.
    /// (C) ADM1-Full: same feed, HRT 25 d, 29-state Batstone 2002 model.
    /// Validates: COD removal, CH4/CO2 in biogas, mass balance, biomass populations,
    /// and property profile access via Fluent API.</summary>
    internal static class F18_AnaerobicDigesterADM1
    {
        public static void Run()
        {
            var rt = new ResultTable("F18 - Anaerobic digester: BlackBox + ADM1-Lite + ADM1-Full");

            RunBlackBox(rt);
            RunADM1Lite(rt);
            RunADM1Full(rt);

            rt.PrintAndThrowIfFailed();
        }

        private static void RunBlackBox(ResultTable rt)
        {
            var fs = Flowsheet.Create("F18A_BlackBox")
                .WithCompounds("Water", "Methane", "Carbon dioxide", "Glucose",
                               "Ammonia", "Biomass_ActivatedSludge")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .At(308.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 0.94)
                .SetCompoundMassFlow("Glucose", 0.05)
                .SetCompoundMassFlow("Biomass_ActivatedSludge", 0.01);

            var effluent = fs.AddMaterialStream("effluent");
            var biogas = fs.AddMaterialStream("biogas");

            var ad = fs.AddAnaerobicDigester("AD-BB")
                .Configure(o => o.CreateConnectors())
                .WithVolume(2000.0.CubicMeters())
                .WithHydraulicRetentionTime(20.0.Days())
                .WithCODRemoval(0.80)
                .WithBiomassYieldGVssPerGCOD(0.08)
                .WithMethaneFractionOverride(0.65)
                .WithThermalMode(BioReactorThermalMode.Isothermal)
                .WithModel(DigesterModel.BlackBox)
                .Configure(o =>
                {
                    o.SubstrateCompound = "Glucose";
                    o.BiomassCompound = "Biomass_ActivatedSludge";
                    o.MethaneCompound = "Methane";
                    o.CO2Compound = "Carbon dioxide";
                    o.WaterCompound = "Water";
                    o.NH3Compound = "Ammonia";
                })
                .ConnectFeed(feed, 0)
                .ConnectProduct(effluent, 0)
                .ConnectProduct(biogas, 1);

            var pp = System.Linq.Enumerable.First(fs.Inner.PropertyPackages).Value;
            feed.Object.SetPropertyPackageInstance(pp);
            feed.Object.Calculate(true, true);
            ad.Object.SetPropertyPackageInstance(pp);
            ad.Object.Calculate();

            effluent.Object.SetPropertyPackageInstance(pp);
            effluent.Object.Calculate(true, true);
            biogas.Object.SetPropertyPackageInstance(pp);
            biogas.Object.Calculate(true, true);

            double codIn = ad.Object.Result_CODin_kgs;
            double codRemoved = ad.Object.Result_CODremoved_kgs;
            double codEff = codIn > 1e-12 ? codRemoved / codIn : 0.0;
            double ch4Frac = ad.Object.Result_CH4MoleFraction;
            double ch4Yield = ad.Object.Result_SpecificCH4Yield_Nm3kgCOD;

            double mEff = effluent.MassFlowKgPerSecond;
            double mGas = biogas.MassFlowKgPerSecond;

            rt.RowInRange("[BB] COD removal ~80%", 0.50, 0.95, codEff, "-");
            rt.RowInRange("[BB] CH4 mole frac ~65%", 0.40, 0.90, ch4Frac, "-");
            rt.RowInRange("[BB] Specific CH4 yield > 0", 1e-6, 1.0, ch4Yield, "Nm³/kgCOD");
            rt.RowInRange("[BB] Effluent mass > 0", 0.1, 10.0, mEff, "kg/s");
            rt.RowInRange("[BB] Biogas mass > 0", 1e-6, 1.0, mGas, "kg/s");
        }

        private static void RunADM1Lite(ResultTable rt)
        {
            var fs = Flowsheet.Create("F18B_ADM1Lite")
                .WithCompounds("Water", "Methane", "Carbon dioxide", "Glucose",
                               "Ammonia", "Biomass_ActivatedSludge")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .At(308.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 0.94)
                .SetCompoundMassFlow("Glucose", 0.05)
                .SetCompoundMassFlow("Biomass_ActivatedSludge", 0.01);

            var effluent = fs.AddMaterialStream("effluent");
            var biogas = fs.AddMaterialStream("biogas");

            var ad = fs.AddAnaerobicDigester("AD-ADM1L")
                .Configure(o => o.CreateConnectors())
                .WithVolume(2000.0.CubicMeters())
                .WithHydraulicRetentionTime(25.0.Days())
                .WithCODRemoval(0.75)
                .WithBiomassYieldGVssPerGCOD(0.06)
                .WithMethaneFractionOverride(-1.0)
                .WithThermalMode(BioReactorThermalMode.Isothermal)
                .WithModel(DigesterModel.ADM1Lite)
                .WithADM1HydrolysisRatePerDay(10.0)
                .WithADM1SugarUptakePerDay(30.0)
                .WithADM1AcetateUptakePerDay(8.0)
                .Configure(o =>
                {
                    o.SubstrateCompound = "Glucose";
                    o.BiomassCompound = "Biomass_ActivatedSludge";
                    o.MethaneCompound = "Methane";
                    o.CO2Compound = "Carbon dioxide";
                    o.WaterCompound = "Water";
                    o.NH3Compound = "Ammonia";
                    o.ADM1_S_s0 = 0.5;
                    o.ADM1_S_VFA0 = 0.2;
                    o.ADM1_S_Ac0 = 0.1;
                    o.ADM1_X_hyd0 = 0.2;
                    o.ADM1_X_ace0 = 0.1;
                    o.ADM1_X_am0 = 0.1;
                    o.ADM1_X_hm0 = 0.05;
                })
                .ConnectFeed(feed, 0)
                .ConnectProduct(effluent, 0)
                .ConnectProduct(biogas, 1);

            var pp = System.Linq.Enumerable.First(fs.Inner.PropertyPackages).Value;
            feed.Object.SetPropertyPackageInstance(pp);
            feed.Object.Calculate(true, true);
            ad.Object.SetPropertyPackageInstance(pp);
            ad.Object.Calculate();

            effluent.Object.SetPropertyPackageInstance(pp);
            effluent.Object.Calculate(true, true);
            biogas.Object.SetPropertyPackageInstance(pp);
            biogas.Object.Calculate(true, true);

            double codIn = ad.Object.Result_CODin_kgs;
            double codRemoved = ad.Object.Result_CODremoved_kgs;
            double codEff = codIn > 1e-12 ? codRemoved / codIn : 0.0;
            double xAce = ad.Object.ADM1_Result_X_ace;
            double xAm = ad.Object.ADM1_Result_X_am;
            double ch4Yield = ad.Object.Result_SpecificCH4Yield_Nm3kgCOD;
            double mEff = effluent.MassFlowKgPerSecond;
            double mGas = biogas.MassFlowKgPerSecond;

            rt.RowInRange("[ADM1L] COD removal > 0", 0.001, 1.0, codEff, "-");
            rt.RowInRange("[ADM1L] CH4 yield > 0", 1e-6, 2.0, ch4Yield, "Nm³/kgCOD");
            rt.RowInRange("[ADM1L] Acetogen pop > 0", 1e-6, 100.0, xAce, "g VSS/L");
            rt.RowInRange("[ADM1L] Methanogen pop > 0", 1e-6, 100.0, xAm, "g VSS/L");
            rt.RowInRange("[ADM1L] Effluent mass > 0", 0.1, 10.0, mEff, "kg/s");
            rt.RowInRange("[ADM1L] Biogas mass > 0", 1e-6, 1.0, mGas, "kg/s");

            // ---- Property profile validation via Fluent API ----
            var traj = ad.ADM1Trajectory;
            if (traj != null)
            {
                var series = ad.ProfileSeriesNames;
                rt.RowInRange("[ADM1L] Profile series count > 0", 1, 30, series?.Length ?? 0, "-");

                var csv = ad.ProfileToCSV();
                rt.RowInRange("[ADM1L] Profile CSV length > 0", 10, 1e8, csv?.Length ?? 0, "chars");
            }
        }

        private static void RunADM1Full(ResultTable rt)
        {
            var fs = Flowsheet.Create("F18C_ADM1Full")
                .WithCompounds("Water", "Methane", "Carbon dioxide", "Glucose",
                               "Ammonia", "Biomass_ActivatedSludge")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .At(308.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 0.94)
                .SetCompoundMassFlow("Glucose", 0.05)
                .SetCompoundMassFlow("Biomass_ActivatedSludge", 0.01);

            var effluent = fs.AddMaterialStream("effluent");
            var biogas = fs.AddMaterialStream("biogas");

            var ad = fs.AddAnaerobicDigester("AD-ADM1F")
                .Configure(o => o.CreateConnectors())
                .WithVolume(2000.0.CubicMeters())
                .WithHydraulicRetentionTime(25.0.Days())
                .WithCODRemoval(0.75)
                .WithBiomassYieldGVssPerGCOD(0.06)
                .WithMethaneFractionOverride(-1.0)
                .WithThermalMode(BioReactorThermalMode.Isothermal)
                .WithModel(DigesterModel.ADM1Full)
                .Configure(o =>
                {
                    o.SubstrateCompound = "Glucose";
                    o.BiomassCompound = "Biomass_ActivatedSludge";
                    o.MethaneCompound = "Methane";
                    o.CO2Compound = "Carbon dioxide";
                    o.WaterCompound = "Water";
                    o.NH3Compound = "Ammonia";
                    // ADM1-Full takes influent nitrogen from Sin_IN, never from the feed stream's
                    // ammonia. The 0.01 kmol/m³ default against this feed's ~50 kg COD/m³ of
                    // carbohydrate is a C:N near 130:1, far past the ~30:1 digestion needs, and ADM1
                    // rightly answers with a nitrogen-starved reactor that sours to pH 4 and makes no
                    // methane. 0.07 kmol/m³ (~1 g N/L) is an ordinary digester ammonia level and
                    // keeps the biology alive, which is what this case exists to exercise. Before the
                    // integrator reached its horizon this went unnoticed: every result was read off
                    // t ~ 3.8 d, where the seed biomass is still coasting on its initial substrate.
                    o.ADM1Params.Operating.Sin_IN = 0.07;
                })
                .ConnectFeed(feed, 0)
                .ConnectProduct(effluent, 0)
                .ConnectProduct(biogas, 1);

            var pp = System.Linq.Enumerable.First(fs.Inner.PropertyPackages).Value;
            feed.Object.SetPropertyPackageInstance(pp);
            feed.Object.Calculate(true, true);
            ad.Object.SetPropertyPackageInstance(pp);
            ad.Object.Calculate();

            effluent.Object.SetPropertyPackageInstance(pp);
            effluent.Object.Calculate(true, true);
            biogas.Object.SetPropertyPackageInstance(pp);
            biogas.Object.Calculate(true, true);

            double codIn = ad.Object.Result_CODin_kgs;
            double codRemoved = ad.Object.Result_CODremoved_kgs;
            double codEff = codIn > 1e-12 ? codRemoved / codIn : 0.0;
            double ch4Yield = ad.Object.Result_SpecificCH4Yield_Nm3kgCOD;
            double mEff = effluent.MassFlowKgPerSecond;
            double mGas = biogas.MassFlowKgPerSecond;

            rt.RowInRange("[ADM1F] COD removal > 0", 0.001, 1.0, codEff, "-");
            rt.RowInRange("[ADM1F] CH4 yield > 0", 1e-6, 2.0, ch4Yield, "Nm³/kgCOD");
            rt.RowInRange("[ADM1F] Effluent mass > 0", 0.1, 10.0, mEff, "kg/s");
            rt.RowInRange("[ADM1F] Biogas mass > 0", 1e-6, 1.0, mGas, "kg/s");

            // The four rows above only ask for more than nothing, and a soured digester clears every
            // one of them: acidogenesis alone removes COD and leaves a trace of methane. This case ran
            // for a long time on a reactor at pH 4 with 6 kg COD/m3 of acetate and its acetoclastic
            // methanogens washed out, and said OK. These three are what tell a working digester from a
            // failed one, and they are the state the reactor has to be left in, not a rate.
            var st = ad.Object.ADM1LastState;
            if (st != null)
            {
                rt.RowInRange("[ADM1F] pH in the methanogenic range", 6.5, 8.0, st.pH, "-");
                rt.RowInRange("[ADM1F] acetate not accumulated", 0.0, 0.5, st.S_ac, "kg COD/m³");
                rt.RowInRange("[ADM1F] acetoclastic methanogens survive", 0.01, 100.0, st.X_ac, "kg COD/m³");
            }

            // HRT is reported, not obeyed. A CSTR's residence time is V/Q, so the hydraulic retention
            // time is a third number against two degrees of freedom: this case asks for 25 d against a
            // volume and a feed flow that give about 22. ADM1-Lite has always said which one it used;
            // ADM1-Full never looked at HRT at all, so the property still read back whatever had been
            // entered and there was no way to tell. It now reports V/Q, as ADM1-Lite does.
            double rhoL = feed.Object.Phases[1].Properties.density.GetValueOrDefault();
            if (rhoL <= 250.0) rhoL = 1000.0;
            double qLiq = feed.Object.Phases[0].Properties.massflow.GetValueOrDefault() / rhoL;
            rt.Row("[ADM1F] reported HRT is V/Q", ad.Object.Volume / qLiq / 86400.0,
                   ad.Object.HRT_s / 86400.0, 0.01, "d");

            // ---- Property profile validation ----
            var traj = ad.ADM1Trajectory;
            if (traj != null)
            {
                var series = ad.ProfileSeriesNames;
                rt.RowInRange("[ADM1F] Profile series count > 0", 1, 50, series?.Length ?? 0, "-");

                var dt = ad.ProfileToDataTable();
                rt.RowInRange("[ADM1F] Profile DataTable rows > 0", 1, 1e6, dt?.Rows.Count ?? 0, "rows");
            }
        }
    }
}
