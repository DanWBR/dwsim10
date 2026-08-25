using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using DWSIM.UnitOperations.Reactors;

namespace DWSIM.Validation.Tests.Flowsheets
{
    /// <summary>F21 - Anaerobic digester sulfur balance.
    /// Standard ADM1 (Batstone 2002) excludes sulfate reduction, so the digester used to emit a
    /// strictly binary CH4/CO2 biogas and strand any fed H2S in the liquid. This exercises the
    /// stoichiometric sulfur balance added on top.
    ///
    /// The decisive check is the sulfate/organic pair: reducing sulfate to sulfide costs 64 kg
    /// COD/kmol S taken from the methane pool, so sulfate MUST cut CH4; organic sulfur arrives
    /// already reduced inside the substrate and MUST NOT. If both behave alike the accounting is
    /// wrong, however plausible the resulting H2S number looks.
    ///
    /// Atom conservation is asserted for all three models. It is the sharpest check available for
    /// ADM1-Full, because it only holds at a steady state: sulfur in must equal sulfur out, and a
    /// reactor still filling up retains the difference. ADM1-Full could not be held to it until the
    /// integrator was made to reach its horizon - it used to stop at t ~ 3.8 d of the 200 d asked
    /// for and label the result tEnd regardless, so every state it produced was an early
    /// transient.</summary>
    internal static class F21_AnaerobicDigesterSulfur
    {
        private const double MW_S = 32.06;
        private const double MW_H2S = 34.08;

        /// <summary>Influent inorganic nitrogen for the ADM1-Full cases (kmol/m³).
        /// ADM1-Full reads nitrogen from Sin_IN and never from the feed stream's ammonia, and the
        /// 0.01 kmol/m³ default against this feed's ~50 kg COD/m³ of carbohydrate is a C:N near
        /// 130:1. ADM1 answers that correctly with a starved reactor: pH ~4, methanogens washed out,
        /// no methane - on which "sulfate cuts CH4" would compare two numbers that are both noise.
        /// 0.07 kmol/m³ (~1 g N/L) is an ordinary digester ammonia level and sits clear of both
        /// failure modes this feed has: nitrogen starvation, which is abrupt below 0.05, and free
        /// ammonia poisoning the acetate degraders, which bites above ~0.2. It lands the reactor
        /// near pH 6.9, which is where a glucose feed belongs.</summary>
        private const double ADM1FullInfluentN = 0.07;

        public static void Run()
        {
            var rt = new ResultTable("F21 - Anaerobic digester: sulfur balance");

            RunSulfatePair(rt, DigesterModel.BlackBox, "BB", assertCODNeutrality: true);
            RunSulfatePair(rt, DigesterModel.ADM1Lite, "ADM1L", assertCODNeutrality: true);
            // ADM1-Full is held to the sulfur atom balance like the others, but not to organic
            // sulfur leaving CH4 untouched - see RunSulfatePair.
            RunSulfatePair(rt, DigesterModel.ADM1Full, "ADM1F", assertCODNeutrality: false);

            RunFullGasPhase(rt);
            RunSulfateKinetics(rt);

            rt.PrintAndThrowIfFailed();
        }

        /// <summary>ADM1-S through the unit operation, which B14 does not touch.</summary>
        /// <remarks>
        /// B14 drives the integrator directly. This is the other half: that the digester wires the
        /// influent sulfate into the kinetics, seeds the reducers, carries the sulfate it could not
        /// reduce out in the effluent, and reports the three ADM1-S results. The sulfur balance here
        /// has to count that residual sulfate as well as the sulfide - it is the whole difference
        /// between this model and the three that assume complete reduction.
        /// </remarks>
        private static void RunSulfateKinetics(ResultTable rt)
        {
            var bas = RunCase(DigesterModel.ADM1Sulfate, "S_base", 0.0, -1.0);
            var sul = RunCase(DigesterModel.ADM1Sulfate, "S_so4", 1000.0, -1.0);

            rt.RowInRange("[ADM1S] No sulfur -> no H2S in gas", 0.0, 1e-12, bas.H2SGasMass, "kg/s");
            rt.RowInRange("[ADM1S] Sulfate makes H2S", 1e-9, 1.0, sul.H2SGasMass, "kg/s");
            rt.RowInRange("[ADM1S] H2S in biogas reported", 1.0, 200000.0, sul.H2Sppmv, "ppmv");

            double ch4Drop = bas.CH4 > 1e-12 ? (bas.CH4 - sul.CH4) / bas.CH4 : 0.0;
            rt.RowInRange("[ADM1S] Sulfate cuts CH4", 0.005, 0.50, ch4Drop, "-");

            // The reducers must be a live population, and the conversion must be a number the
            // kinetics produced rather than the 100% the other models assume.
            rt.RowInRange("[ADM1S] SRB establish a population", 1e-6, 100.0, sul.SRBBiomass, "kg COD/m3");
            rt.RowInRange("[ADM1S] Sulfate conversion is kinetic", 0.05, 1.0, sul.SulfateReduction, "-");

            // Sulfur closes only if the unreduced sulfate leaves with the effluent: sulfide alone
            // would come up short by exactly whatever the reducers did not get to.
            double sOut = (sul.H2SGasMass + sul.H2SLiqMass) / MW_H2S * MW_S + sul.SulfateOutS;
            rt.RowInRange("[ADM1S] Sulfur conserved with residual sulfate", 0.98, 1.02,
                          sOut / sul.SulfurIn, "-");
        }

        private sealed class Outcome
        {
            public double CH4;
            public double H2SGasMass;
            public double H2SLiqMass;
            public double H2Sppmv;
            public double SulfurIn;
            public double SulfateOutS;      // kg S/s left as sulfate in the effluent (ADM1-S only)
            public double SulfateReduction; // fraction of the influent sulfate respired
            public double SRBBiomass;       // kg COD/m³
        }

        /// <summary>Build, run and read back one digester case.</summary>
        private static Outcome RunCase(DigesterModel model, string tag,
                                       double sulfateMgL, double organicSGPerKg)
        {
            var fs = Flowsheet.Create("F21_" + tag)
                .WithCompounds("Water", "Methane", "Carbon dioxide", "Glucose",
                               "Ammonia", "Hydrogen sulfide", "Sulfuric acid",
                               "Biomass_ActivatedSludge")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .At(308.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 0.94)
                .SetCompoundMassFlow("Glucose", 0.05)
                .SetCompoundMassFlow("Biomass_ActivatedSludge", 0.01)
                // Zero these explicitly. Compounds left unset do not default to zero, and stray
                // feed H2S joins the sulfide pool and swamps the sulfur actually under test.
                .SetCompoundMassFlow("Hydrogen sulfide", 0.0)
                .SetCompoundMassFlow("Sulfuric acid", 0.0)
                .SetCompoundMassFlow("Methane", 0.0)
                .SetCompoundMassFlow("Carbon dioxide", 0.0)
                .SetCompoundMassFlow("Ammonia", 0.0);

            var effluent = fs.AddMaterialStream("effluent");
            var biogas = fs.AddMaterialStream("biogas");

            var ad = fs.AddAnaerobicDigester("AD-" + tag)
                .Configure(o => o.CreateConnectors())
                .WithVolume(2000.0.CubicMeters())
                .WithHydraulicRetentionTime(25.0.Days())
                .WithCODRemoval(0.80)
                .WithBiomassYieldGVssPerGCOD(0.06)
                .WithMethaneFractionOverride(-1.0)
                .WithThermalMode(BioReactorThermalMode.Isothermal)
                .WithModel(model)
                .Configure(o =>
                {
                    o.SubstrateCompound = "Glucose";
                    o.BiomassCompound = "Biomass_ActivatedSludge";
                    o.MethaneCompound = "Methane";
                    o.CO2Compound = "Carbon dioxide";
                    o.WaterCompound = "Water";
                    o.NH3Compound = "Ammonia";
                    o.H2SCompound = "Hydrogen sulfide";
                    o.SulfateCompound = "Sulfuric acid";
                    o.InfluentSulfateS_mgL = sulfateMgL;
                    o.SubstrateOrganicS_gPerKg = organicSGPerKg;
                    o.AssumedPH_ForSulfide = 7.2;
                    o.ADM1Params.Operating.Sin_IN = ADM1FullInfluentN;
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

            // Sulfur fed (kg S/s), from the feed's actual liquid volumetric flow. Assuming a
            // nominal 1 kg/s over 1000 kg/m³ puts a 6% error straight into the closure ratio,
            // because the real mixture is nearer 945 kg/m³ at 35 °C.
            double qLiq = feed.Object.Phases[1].Properties.volumetric_flow.GetValueOrDefault();
            double sIn = sulfateMgL * qLiq / 1000.0
                       + System.Math.Max(organicSGPerKg, 0.0) * 0.05 / 1000.0;

            // Sulfuric acid is H2SO4: one sulfur per molecule, so kg S = kg H2SO4 * MW_S / 98.07.
            double so4Mass = effluent.Object.Phases[0].Compounds["Sulfuric acid"].MassFlow.GetValueOrDefault();

            return new Outcome
            {
                CH4 = ad.Object.Result_CH4_kgs,
                H2SGasMass = biogas.Object.Phases[0].Compounds["Hydrogen sulfide"].MassFlow.GetValueOrDefault(),
                H2SLiqMass = effluent.Object.Phases[0].Compounds["Hydrogen sulfide"].MassFlow.GetValueOrDefault(),
                H2Sppmv = ad.Object.Result_H2S_ppmv,
                SulfurIn = sIn,
                SulfateOutS = so4Mass * MW_S / 98.07,
                SulfateReduction = ad.Object.Result_SulfateReduction,
                SRBBiomass = ad.Object.Result_SRBBiomass_kgCODm3
            };
        }

        /// <summary>The sulfate-vs-organic pair, plus sulfur conservation, for one model.</summary>
        /// <param name="assertCODNeutrality">Whether to require that organic sulfur leaves CH4
        /// alone. True for the models that fix pH by assumption. False for ADM1-Full, where it is
        /// not achievable and should not be: sulfide is a species in ADM1's charge balance, so the
        /// ~0.03 kmol/m³ this case feeds titrates the reactor down by ~0.3 pH units, and pH gates
        /// the biology through the pH envelopes and free-ammonia inhibition. The COD bookkeeping is
        /// still neutral - that is what the atom balance below checks - but the methane cannot be,
        /// and a digester whose CH4 did not move when 1 g S/L was added would be the wrong answer.
        /// </param>
        private static void RunSulfatePair(ResultTable rt, DigesterModel model, string tag,
                                           bool assertCODNeutrality)
        {
            var bas = RunCase(model, tag + "_base", 0.0, -1.0);
            var sul = RunCase(model, tag + "_so4", 1000.0, -1.0);
            var org = RunCase(model, tag + "_org", 0.0, 20.0);

            // Sulfur-free case must produce no H2S at all: this is the backward-compatibility edge.
            rt.RowInRange("[" + tag + "] No sulfur -> no H2S in gas", 0.0, 1e-12, bas.H2SGasMass, "kg/s");

            // Sulfate steals electrons from methanogenesis, so CH4 must fall...
            double ch4Drop = bas.CH4 > 1e-12 ? (bas.CH4 - sul.CH4) / bas.CH4 : 0.0;
            rt.RowInRange("[" + tag + "] Sulfate cuts CH4", 0.005, 0.50, ch4Drop, "-");

            // ...and turn up as H2S.
            rt.RowInRange("[" + tag + "] Sulfate makes H2S", 1e-9, 1.0, sul.H2SGasMass, "kg/s");
            rt.RowInRange("[" + tag + "] H2S in biogas reported", 1.0, 200000.0, sul.H2Sppmv, "ppmv");

            // Organic sulfur must make H2S too.
            rt.RowInRange("[" + tag + "] Organic S makes H2S", 1e-9, 1.0, org.H2SGasMass, "kg/s");

            if (assertCODNeutrality)
            {
                // Unlike sulfate, organic sulfur is COD-neutral for methane and must leave CH4
                // alone. This pair is what proves the split is real rather than cosmetic.
                double ch4Shift = bas.CH4 > 1e-12 ? System.Math.Abs(bas.CH4 - org.CH4) / bas.CH4 : 0.0;
                rt.RowInRange("[" + tag + "] Organic S leaves CH4 alone", 0.0, 0.01, ch4Shift, "-");
            }

            // Sulfur atom balance: everything fed leaves as gas H2S or dissolved sulfide. For
            // ADM1-Full this is the check with teeth, because it only closes at a steady state - a
            // reactor still filling up is still accumulating sulfide, and the ratio comes out low.
            double sOutSul = (sul.H2SGasMass + sul.H2SLiqMass) / MW_H2S * MW_S;
            rt.RowInRange("[" + tag + "] Sulfate S conserved (out/in)", 0.98, 1.02,
                          sOutSul / sul.SulfurIn, "-");

            double sOutOrg = (org.H2SGasMass + org.H2SLiqMass) / MW_H2S * MW_S;
            rt.RowInRange("[" + tag + "] Organic S conserved (out/in)", 0.98, 1.02,
                          sOutOrg / org.SulfurIn, "-");
        }

        /// <summary>ADM1-Full: the checks that hold whether or not the integrator converged.</summary>
        private static void RunFullGasPhase(ResultTable rt)
        {
            var fs = Flowsheet.Create("F21_FullGas")
                .WithCompounds("Water", "Methane", "Carbon dioxide", "Glucose",
                               "Ammonia", "Hydrogen sulfide", "Biomass_ActivatedSludge")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .At(308.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 0.94)
                .SetCompoundMassFlow("Glucose", 0.05)
                .SetCompoundMassFlow("Biomass_ActivatedSludge", 0.01)
                .SetCompoundMassFlow("Hydrogen sulfide", 0.0)
                .SetCompoundMassFlow("Methane", 0.0)
                .SetCompoundMassFlow("Carbon dioxide", 0.0)
                .SetCompoundMassFlow("Ammonia", 0.0);

            var effluent = fs.AddMaterialStream("effluent");
            var biogas = fs.AddMaterialStream("biogas");

            var ad = fs.AddAnaerobicDigester("AD-FullGas")
                .Configure(o => o.CreateConnectors())
                .WithVolume(2000.0.CubicMeters())
                .WithHydraulicRetentionTime(25.0.Days())
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
                    o.H2SCompound = "Hydrogen sulfide";
                    o.InfluentSulfateS_mgL = 1000.0;
                    o.ADM1Params.Operating.Sin_IN = ADM1FullInfluentN;
                })
                .ConnectFeed(feed, 0)
                .ConnectProduct(effluent, 0)
                .ConnectProduct(biogas, 1);

            var pp = System.Linq.Enumerable.First(fs.Inner.PropertyPackages).Value;
            feed.Object.SetPropertyPackageInstance(pp);
            feed.Object.Calculate(true, true);
            ad.Object.SetPropertyPackageInstance(pp);
            ad.Object.Calculate();

            var st = ad.Object.ADM1LastState;
            var pars = ad.Object.ADM1Params;

            // The four dry-basis mole fractions share one denominator and must add to 1. Nothing
            // else checks this, and adding H2S to only some of them would not have thrown.
            double xCH4 = DWSIM.UnitOperations.Reactors.ADM1.ADM1Equations.CH4MoleFraction(st, pars);
            double xCO2 = DWSIM.UnitOperations.Reactors.ADM1.ADM1Equations.CO2MoleFraction(st, pars);
            double xH2 = DWSIM.UnitOperations.Reactors.ADM1.ADM1Equations.H2MoleFraction(st, pars);
            double xH2S = DWSIM.UnitOperations.Reactors.ADM1.ADM1Equations.H2SMoleFraction(st, pars);
            rt.RowInRange("[ADM1F] Gas mole fractions sum to 1", 0.9999, 1.0001,
                          xCH4 + xCO2 + xH2 + xH2S, "-");
            rt.RowInRange("[ADM1F] H2S reaches the headspace", 1e-9, 0.5, xH2S, "-");

            // Only undissociated H2S is volatile, so how the sulfide splits is what sets the ppmv
            // above. S_hs_ion is a derived field that SolvePH has to refresh, at the temperature-
            // corrected Ka - a stale or mis-wired one would still leave a plausible-looking ppmv.
            // Recompute the split here from the reactor's own pH and check it agrees.
            //
            // Not asserted as a fixed range: this reactor sits near pH 6.9 and pKa1(H2S) at 35 C is
            // about 6.93, so the split is close to 50/50 and a range wide enough to be safe would
            // not even catch an inverted one.
            var physAtT = DWSIM.UnitOperations.Reactors.ADM1.ADM1Equations.TemperatureCorrect(pars.Physicochemical);
            double expectedHS = physAtT.K_a_h2s / (physAtT.K_a_h2s + st.S_H_ion);
            double hsFraction = st.S_IS > 1e-12 ? st.S_hs_ion / st.S_IS : 0.0;
            rt.Row("[ADM1F] HS- fraction tracks the reactor pH", expectedHS, hsFraction, 0.001, "-");

            // A name added to VarNames without a matching ExtractValue case yields a whole column
            // of NaN and no error, so assert the new series actually resolve.
            var traj = ad.ADM1Trajectory;
            var sIS = traj.GetSeries("S_IS");
            var sGas = traj.GetSeries("S_h2s_gas");
            bool isFinite = sIS.Length > 0 && !double.IsNaN(sIS[sIS.Length - 1]) &&
                            sGas.Length > 0 && !double.IsNaN(sGas[sGas.Length - 1]);
            rt.RowInRange("[ADM1F] S_IS / S_h2s_gas series are finite", 1, 1, isFinite ? 1 : 0, "-");
            rt.RowInRange("[ADM1F] Dissolved sulfide > 0", 1e-9, 10.0,
                          ad.Object.Result_DissolvedSulfide_kgSm3, "kg S/m³");
        }
    }
}
