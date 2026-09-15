//    Vessel depressurization (blowdown) study.
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.Pipe;
using DynamicsSpecType = DWSIM.Interfaces.Enums.Dynamics.DynamicsSpecType;

namespace DWSIM.Automation.DynamicRunner.Depressurization
{
    /// <summary>How the content and the wall exchange heat during the blowdown.</summary>
    public enum DepressurizationMode
    {
        /// <summary>No fire; the content cools on expansion and exchanges heat with the wall and the ambient.</summary>
        Adiabatic = 0,
        /// <summary>API 521 pool fire on the wetted wall, with an optional flux on the dry wall.</summary>
        Fire = 1,
        /// <summary>The content keeps its temperature (the legacy vessel model); pressure only follows the mass.</summary>
        Isothermal = 2
    }

    /// <summary>Everything the study needs. Values in SI: Pa, K, m, s, W/m2.</summary>
    public sealed class DepressurizationInput
    {
        // fluid
        /// <summary>Name (not tag) of the material stream in the host flowsheet whose composition fills the vessel.</summary>
        public string SourceStreamName = "";
        public double InitialPressure = 50e5;
        public double InitialTemperature = 298.15;
        /// <summary>Liquid volume fraction at time zero, 0 to 1. Ignored when the flash at the initial condition has a single phase.</summary>
        public double InitialLiquidVolumeFraction = 0.0;

        // vessel
        public bool Horizontal = false;
        public double Diameter = 1.5;
        /// <summary>Tangent-to-tangent length (horizontal) or height (vertical), m.</summary>
        public double Length = 6.0;
        public string HeadType = "Ellipsoidal (2:1)";
        public double WallThickness = 0.010;
        public string WallMaterial = "Carbon Steel";
        /// <summary>Height of the top edge of the outlet nozzle above the vessel bottom, m (0 = at the top). Liquid leaves through it while the level is above it, a blend while the level is within the hole: a liquid-full vessel, or a pipe blown down through a hole at its end.</summary>
        public double OutletNozzleElevation = 0.0;
        /// <summary>The outlet carries the homogeneous two-phase mixture while both phases exist (a pipe blown down through a hole at its end, where the flow sweeps the liquid along).</summary>
        public bool OutletHomogeneous = false;

        // blowdown valve or restriction orifice
        /// <summary>Bore of the orifice, m. Ignored when FlowCoefficientCv is set.</summary>
        public double OrificeDiameter = 0.020;
        public double DischargeCoefficient = 0.62;
        /// <summary>Use this Cv (US gpm at 1 psi) instead of the orifice geometry when greater than zero.</summary>
        public double FlowCoefficientCv = 0.0;
        /// <summary>Pressure at the outlet of the valve (the flare header), Pa.</summary>
        public double BackPressure = 101325.0;
        /// <summary>Time for the valve to open fully, s (0 = instant).</summary>
        public double ValveOpeningTime = 0.0;

        // heat
        public DepressurizationMode Mode = DepressurizationMode.Adiabatic;
        public double AmbientTemperature = 298.15;
        /// <summary>False: the wall is ignored (a purely adiabatic content). True: metal thermal mass, wall-to-fluid and ambient heat transfer.</summary>
        public bool IncludeWallHeatTransfer = true;
        /// <summary>Multiplier on the estimated wall-to-fluid film coefficients; 1 = the natural-convection correlation as is.</summary>
        public double InternalHeatTransferFactor = 1.0;
        public double FireEnvironmentFactor = 1.0;
        public bool FireAdequateDrainage = true;
        public double FireDryWallHeatFlux = 0.0;
        public double VesselBottomElevation = 0.0;

        // integration
        public double TimeStep = 1.0;
        public double Duration = 900.0;
        /// <summary>Stop once the vessel pressure falls to this value, Pa (0 = run the whole duration).</summary>
        public double StopAtPressure = 0.0;
    }

    /// <summary>One row of the time series.</summary>
    public sealed class DepressurizationPoint
    {
        public double Time;                   // s
        public double Pressure;               // Pa
        public double Temperature;            // K, content
        public double WettedWallTemperature;  // K
        public double DryWallTemperature;     // K
        public double WettedWallHeatTransferCoefficient; // W/m2.K, liquid film on the wetted wall
        public double DryWallHeatTransferCoefficient;    // W/m2.K, vapour film on the dry wall
        public double MassFlow;               // kg/s through the valve
        public double CumulativeMass;         // kg released
        public double LiquidLevel;            // m
        public double LiquidVolumeFraction;   // 0-1
        public double FireHeat;               // kW
        public double ValveOpening;           // %
        public double VapourFractionOut;      // molar, at the valve inlet
    }

    public sealed class DepressurizationResult
    {
        public List<DepressurizationPoint> Points = new List<DepressurizationPoint>();
        public double InitialMass, TotalMassReleased, PeakMassFlow, FinalPressure, FinalTemperature;
        public double MinimumFluidTemperature, MinimumWettedWallTemperature, MinimumDryWallTemperature, MaximumDryWallTemperature;
        /// <summary>Seconds to reach StopAtPressure, or null when it was not reached.</summary>
        public double? TimeToStopPressure;
        /// <summary>Seconds to halve the initial gauge pressure, or null.</summary>
        public double? TimeToHalfPressure;
        public double WettedAreaAtStart, VesselVolume;
        public List<string> Warnings = new List<string>();
        public TimeSpan Elapsed;
        public bool Aborted;
    }

    /// <summary>
    /// Runs a vessel blowdown on a private dynamic flowsheet built from the host's compounds and
    /// property package: a vessel with the study geometry and initial content, a valve sized as the
    /// blowdown orifice, and a pressure sink at the back pressure. The vessel runs its rigorous
    /// energy balance (internal energy, wetted and dry wall segments, API 521 fire) and the study
    /// collects the time series and the extremes a relief or material study needs.
    /// The unit operations are driven through late binding: this assembly stays free of the
    /// CAPE-OPEN interfaces their concrete types carry.
    /// </summary>
    public static class DepressurizationStudy
    {
        public static DepressurizationResult Run(IFlowsheet host, DepressurizationInput input,
            Action<double> progress = null, CancellationToken cancellation = default)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!host.SimulationObjects.TryGetValue(input.SourceStreamName ?? "", out var srcObj) || !(srcObj is IMaterialStream source))
                throw new ArgumentException("Pick a material stream of the flowsheet as the source of the composition.");
            if (input.TimeStep <= 0 || input.Duration <= 0) throw new ArgumentException("The time step and the duration must be positive.");
            if (input.Diameter <= 0 || input.Length <= 0) throw new ArgumentException("The vessel needs a diameter and a length.");
            if (input.InitialPressure <= input.BackPressure) throw new ArgumentException("The initial pressure must be above the back pressure.");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = new DepressurizationResult();

            // ---- a private flowsheet with the host's compounds and property package
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            var options = (DWSIM.SharedClasses.DWSIM.Flowsheet.FlowsheetVariables)fs.FlowsheetOptions;
            foreach (var kv in host.SelectedCompounds)
                options.SelectedComponents[kv.Key] = kv.Value;
            dynamic srcDyn = srcObj;
            IPropertyPackage hostPp = srcDyn.PropertyPackage as IPropertyPackage;
            if (hostPp == null) throw new ArgumentException("The source stream has no property package.");
            var pp = hostPp.Clone();
            if (pp == null) pp = (IPropertyPackage)Activator.CreateInstance(((object)hostPp).GetType());
            pp.Flowsheet = fs;
            fs.AddPropertyPackage(pp);

            dynamic Stream(string tag)
            {
                var o = fs.AddObject(ObjectType.MaterialStream, 0, 0, tag);
                dynamic ms = fs.SimulationObjects[o.Name];
                ms.SetFlowsheet(fs);
                ms.SetPropertyPackage(pp);
                return ms;
            }

            dynamic feed = Stream("Feed");
            dynamic gasOut = Stream("Gas to BDV");
            dynamic liqOut = Stream("Liquid");
            dynamic flare = Stream("Flare header");
            dynamic vessel = fs.SimulationObjects[fs.AddObject(ObjectType.Vessel, 0, 0, "Vessel").Name];
            vessel.SetFlowsheet(fs);
            vessel.PropertyPackage = pp;
            dynamic bdv = fs.SimulationObjects[fs.AddObject(ObjectType.Valve, 0, 0, "BDV").Name];
            bdv.SetFlowsheet(fs);
            bdv.PropertyPackage = pp;

            fs.ConnectObjects(feed.GraphicObject, vessel.GraphicObject, 0, 0);
            fs.ConnectObjects(vessel.GraphicObject, gasOut.GraphicObject, 0, 0);
            fs.ConnectObjects(vessel.GraphicObject, liqOut.GraphicObject, 1, 0);
            fs.ConnectObjects(gasOut.GraphicObject, bdv.GraphicObject, 0, 0);
            fs.ConnectObjects(bdv.GraphicObject, flare.GraphicObject, 0, 0);

            // the composition of the source, at the study's own initial condition; a whisper of feed keeps the
            // steady-state solver happy and is far too small to matter
            double[] z = source.GetOverallComposition();
            feed.SetOverallComposition(z);
            feed.SetTemperature(input.InitialTemperature);
            feed.SetPressure(input.InitialPressure);
            feed.SetMassFlow(1e-8);
            feed.DynamicsSpec = DynamicsSpecType.Flow;
            gasOut.DynamicsSpec = DynamicsSpecType.Pressure;
            liqOut.DynamicsSpec = DynamicsSpecType.Flow;
            flare.DynamicsSpec = DynamicsSpecType.Pressure;

            // ---- blowdown valve: an orifice or a given Cv, ISA choked-flow forms in the valve's Kv mode
            Type valveType = ((object)bdv).GetType();
            SetEnum(bdv, "CalcMode", "Kv_General");
            if (input.FlowCoefficientCv > 0)
            {
                SetEnum(bdv, "FlowCoefficient", "Cv");
                bdv.Kv = input.FlowCoefficientCv;
            }
            else
            {
                // the steady-state pass sizes with an equivalent Kv; the dynamic run uses the compressible
                // orifice equations (isentropic nozzle with choking) for the bore and Cd given
                SetEnum(bdv, "FlowCoefficient", "Kv");
                bdv.Kv = (double)valveType.GetMethod("KvFromOrifice", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { input.OrificeDiameter, input.DischargeCoefficient });
                bdv.UseOrificeFlow = true;
                bdv.OrificeDiameter = input.OrificeDiameter;
                bdv.OrificeDischargeCoefficient = input.DischargeCoefficient;
            }
            bdv.EnableOpeningKvRelationship = input.ValveOpeningTime > 0;
            SetEnum(bdv, "DefinedOpeningKvRelationShipType", "Linear");
            bdv.OpeningPct = 100.0;   // for the steady-state pass; the ramp starts closed below

            // ---- vessel geometry and heat model
            vessel.SelectedEquipmentType = input.Horizontal ? "Horizontal" : "Vertical";
            vessel.HeadType = input.HeadType;
            vessel.WallThickness = input.WallThickness;
            vessel.WallMaterial = input.WallMaterial;
            vessel.CreateDimensionsList();
            void SetDims() { vessel.Dimensions[0].Value = input.Diameter * 1000.0; vessel.Dimensions[1].Value = input.Length; }
            SetDims();
            vessel.CreateDynamicProperties();
            vessel.SetDynamicProperty("Vessel Orientation", input.Horizontal ? 1 : 0);
            vessel.SetDynamicProperty("Get Volume from Dimensions", true);
            vessel.SetDynamicProperty("Get Height from Dimensions", !input.Horizontal);
            vessel.SetDynamicProperty("Height", input.Horizontal ? input.Diameter : input.Length);
            vessel.SetDynamicProperty("Minimum Pressure", Math.Min(input.BackPressure, 101325.0) * 0.5);
            double volume = vessel.CalculateVolume();
            vessel.SetDynamicProperty("Volume", volume);
            result.VesselVolume = volume;

            bool isothermal = input.Mode == DepressurizationMode.Isothermal;
            bool fire = input.Mode == DepressurizationMode.Fire;
            vessel.CalculateRigorousHeatBalance = !isothermal && (input.IncludeWallHeatTransfer || fire);
            vessel.ThermalProperties.TipoPerfil = ThermalEditorDefinitions.ThermalProfileType.Estimar_CGTC;
            vessel.ThermalProperties.Temp_amb_estimar = input.AmbientTemperature;
            vessel.ThermalProperties.Temp_amb_definir = input.AmbientTemperature;
            vessel.SetDynamicProperty("Rigorous Energy Balance (UV)", !isothermal);
            vessel.SetDynamicProperty("Split Wall (Wetted/Dry)", !isothermal && input.IncludeWallHeatTransfer);
            vessel.SetDynamicProperty("Internal Heat Transfer Factor", input.InternalHeatTransferFactor > 0.0 ? input.InternalHeatTransferFactor : 1.0);
            vessel.SetDynamicProperty("Fire Case (API 521)", fire);
            vessel.SetDynamicProperty("Fire Environment Factor", input.FireEnvironmentFactor);
            vessel.SetDynamicProperty("Fire Adequate Drainage", input.FireAdequateDrainage);
            vessel.SetDynamicProperty("Fire Dry Wall Heat Flux", input.FireDryWallHeatFlux);
            vessel.SetDynamicProperty("Vessel Bottom Elevation", input.VesselBottomElevation);
            vessel.SetDynamicProperty("Gas Outlet Nozzle Elevation", input.OutletNozzleElevation);
            vessel.SetDynamicProperty("Gas Outlet Homogeneous", input.OutletHomogeneous);
            // a nozzle below the top is a hole in the wall: the outlet passes a blend while the level is
            // within the hole, so the transition band is the hole diameter below its top edge
            vessel.SetDynamicProperty("Gas Outlet Transition Height", input.OutletNozzleElevation > 0 ? Math.Max(0.01, input.OrificeDiameter) : 0.01);

            // ---- steady state, then the initial content
            // The steady pass only has to solve the flowsheet once so the dynamic run starts from valid
            // streams; the content is set afterwards. An all-liquid feed leaves the gas outlet and the
            // BDV without flow, so the pass runs the feed at a state that carries vapour.
            SteadyPassState(fs, feed, pp, z, input, result);
            var errors = fs.SolveFlowsheet2();
            if (errors.Count > 0)
                throw new Exception("The study flowsheet did not solve: " + string.Join("; ", errors.Select(e => e.Message)));
            SetDims();   // the steady-state pass rewrites the dimensions from its own sizing
            flare.SetPressure(input.BackPressure);
            liqOut.SetMassFlow(0.0);
            if (input.ValveOpeningTime > 0) bdv.OpeningPct = 0.0;

            double liquidFraction = SetInitialContent(fs, vessel, feed, pp, z, input, volume, result);
            result.InitialMass = vessel.AccumulationStream.GetMassFlow();
            double h0 = vessel.LiquidHeightFromFraction(liquidFraction, input.Diameter, input.Length);
            result.WettedAreaAtStart = vessel.WettedArea(h0, input.Diameter, input.Diameter + input.WallThickness, input.Length);

            // ---- integrator
            var integrator = new DWSIM.DynamicsManager.Integrator
            {
                ID = "depressurization", Description = "Depressurization",
                IntegrationStep = TimeSpan.FromSeconds(input.TimeStep),
                Duration = TimeSpan.FromSeconds(input.Duration),
                ShouldCalculateEquilibrium = true, ShouldCalculatePressureFlow = true, ShouldCalculateControl = true
            };
            var schedule = new DWSIM.DynamicsManager.Schedule { ID = "depressurization", Description = "Depressurization", CurrentIntegrator = "depressurization", UseCurrentStateAsInitial = true };
            fs.DynamicsManager.IntegratorList["depressurization"] = integrator;
            fs.DynamicsManager.ScheduleList["depressurization"] = schedule;
            fs.DynamicsManager.CurrentSchedule = "depressurization";

            double t = 0.0, cumulative = 0.0, p0 = input.InitialPressure;
            double halfTarget = input.BackPressure + 0.5 * (p0 - input.BackPressure);
            bool stop = false;

            result.Points.Add(Snapshot(vessel, gasOut, bdv, 0.0, 0.0));

            var runner = new IntegratorRunner(fs);
            var run = runner.Run(new IntegratorRunOptions
            {
                Schedule = "depressurization",
                RestoreInitialState = false,
                EnableHistorian = false,
                CancellationToken = cancellation,
                AbortRequested = () => stop,
                PreStep = _ =>
                {
                    if (input.ValveOpeningTime > 0)
                        bdv.OpeningPct = Math.Min(100.0, 100.0 * (t + input.TimeStep) / input.ValveOpeningTime);
                },
                PostStep = _ =>
                {
                    t += input.TimeStep;
                    cumulative += (double)gasOut.GetMassFlow() * input.TimeStep;
                    var pt = Snapshot(vessel, gasOut, bdv, t, cumulative);
                    result.Points.Add(pt);
                    if (result.TimeToHalfPressure == null && pt.Pressure <= halfTarget) result.TimeToHalfPressure = t;
                    if (input.StopAtPressure > 0 && result.TimeToStopPressure == null && pt.Pressure <= input.StopAtPressure)
                    {
                        result.TimeToStopPressure = t;
                        stop = true;
                    }
                    progress?.Invoke(Math.Min(1.0, t / input.Duration));
                }
            });

            if (run.Exceptions.Count > 0)
                result.Warnings.Add("The integration stopped early: " + run.Exceptions[0].Message);
            result.Aborted = cancellation.IsCancellationRequested;

            // ---- summary
            var pts = result.Points;
            result.TotalMassReleased = cumulative;
            result.PeakMassFlow = pts.Max(p => p.MassFlow);
            result.FinalPressure = pts[pts.Count - 1].Pressure;
            result.FinalTemperature = pts[pts.Count - 1].Temperature;
            result.MinimumFluidTemperature = pts.Min(p => p.Temperature);
            result.MinimumWettedWallTemperature = pts.Min(p => p.WettedWallTemperature);
            result.MinimumDryWallTemperature = pts.Min(p => p.DryWallTemperature);
            result.MaximumDryWallTemperature = pts.Max(p => p.DryWallTemperature);
            if (input.StopAtPressure > 0 && result.TimeToStopPressure == null)
                result.Warnings.Add("The target pressure was not reached within the duration.");
            if (isothermal)
                result.Warnings.Add("Isothermal mode: the content keeps its temperature, so the minimum temperatures are not meaningful.");
            result.Elapsed = sw.Elapsed;
            return result;
        }

        /// <summary>A nullable double read through late binding comes back boxed or null.</summary>
        private static double N(object v) => v == null ? 0.0 : Convert.ToDouble(v);

        /// <summary>Sets an enum-typed property by member name, without a compile-time reference to the enum.</summary>
        private static void SetEnum(object target, string property, string member)
        {
            var pi = target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)
                     ?? throw new MissingMemberException(target.GetType().Name, property);
            pi.SetValue(target, Enum.Parse(pi.PropertyType, member));
        }

        private static DepressurizationPoint Snapshot(dynamic vessel, dynamic gasOut, dynamic bdv, double t, double cumulative)
        {
            double Dyn(string n) { object v = vessel.GetDynamicProperty(n); return v == null ? 0.0 : Convert.ToDouble(v); }
            dynamic acc = vessel.AccumulationStream;
            double vol = Dyn("Volume");
            double liqVol = acc == null ? 0.0 : N(acc.Phases[1].Properties.volumetric_flow);
            return new DepressurizationPoint
            {
                Time = t,
                Pressure = t == 0.0 && acc != null ? (double)acc.GetPressure() : Dyn("Operating Pressure"),
                Temperature = acc == null ? 0.0 : (double)acc.GetTemperature(),
                WettedWallTemperature = (double)vessel.WallTemperatureWetted,
                DryWallTemperature = (double)vessel.WallTemperatureDry,
                WettedWallHeatTransferCoefficient = Dyn("Wetted Wall Heat Transfer Coefficient"),
                DryWallHeatTransferCoefficient = Dyn("Dry Wall Heat Transfer Coefficient"),
                MassFlow = t == 0.0 ? 0.0 : (double)gasOut.GetMassFlow(),
                CumulativeMass = cumulative,
                LiquidLevel = Dyn("Liquid Level"),
                LiquidVolumeFraction = vol > 0 ? Math.Min(1.0, liqVol / vol) : 0.0,
                FireHeat = Dyn("Fire Heat Input"),
                ValveOpening = (double)bdv.OpeningPct,
                VapourFractionOut = N(gasOut.Phases[2].Properties.molarfraction)
            };
        }

        /// <summary>
        /// Fills the vessel with the flash of the source composition at the initial pressure and
        /// temperature: liquid to the requested volume fraction, vapour above it. Returns the liquid
        /// volume fraction actually used.
        /// </summary>
        /// <summary>Puts the feed at the initial state, or at a state with vapour when the initial state is all liquid.</summary>
        private static void SteadyPassState(DWSIM.DynamicRunner.Flowsheet fs, dynamic feed, dynamic pp, double[] z, DepressurizationInput input, DepressurizationResult result)
        {
            foreach (var (T, P) in new[] { (input.InitialTemperature, input.InitialPressure), (input.InitialTemperature, input.BackPressure), (input.InitialTemperature + 150.0, input.BackPressure) })
            {
                dynamic probe = feed.Clone();
                probe.SetFlowsheet(fs);
                probe.SetPropertyPackage(pp);
                probe.SetOverallComposition(z);
                probe.SetTemperature(T);
                probe.SetPressure(P);
                probe.SetMassFlow(1.0);
                probe.SpecType = StreamSpec.Temperature_and_Pressure;
                try { probe.Calculate(); } catch { continue; }
                if (N(probe.Phases[2].Properties.molarfraction) > 1e-6)
                {
                    feed.SetTemperature(T);
                    feed.SetPressure(P);
                    return;
                }
            }
        }

        private static double SetInitialContent(DWSIM.DynamicRunner.Flowsheet fs, dynamic vessel, dynamic feed,
            IPropertyPackage pp, double[] z, DepressurizationInput input, double volume, DepressurizationResult result)
        {
            dynamic probe = feed.Clone();
            probe.SetFlowsheet(fs);
            probe.SetPropertyPackage(pp);
            probe.SetOverallComposition(z);
            probe.SetTemperature(input.InitialTemperature);
            probe.SetPressure(input.InitialPressure);
            probe.SetMassFlow(1.0);
            probe.SpecType = StreamSpec.Temperature_and_Pressure;
            probe.Calculate();

            double vapFrac = N(probe.Phases[2].Properties.molarfraction);
            double f = Math.Min(1.0, Math.Max(0.0, input.InitialLiquidVolumeFraction));
            int n = probe.Phases[0].Compounds.Count;
            double[] wl = new double[n], wg = new double[n];
            double rl = N(probe.Phases[1].Properties.density), rg = N(probe.Phases[2].Properties.density);
            int i = 0;
            foreach (dynamic c in probe.Phases[0].Compounds.Values)
            {
                string name = c.Name;
                wl[i] = N(probe.Phases[1].Compounds[name].MassFraction);
                wg[i] = N(probe.Phases[2].Compounds[name].MassFraction);
                i++;
            }

            double[] w;
            double mass;
            if (vapFrac >= 1.0 - 1e-6 || rl <= 0.0)
            {
                if (f > 0) result.Warnings.Add("The fluid is all vapour at the initial condition; the vessel starts gas-filled.");
                f = 0.0;
                w = wg; mass = rg * volume;
            }
            else if (vapFrac <= 1e-6 || rg <= 0.0 || f >= 0.999)
            {
                if (f < 1 && vapFrac <= 1e-6) result.Warnings.Add("The fluid is all liquid at the initial condition; the vessel starts liquid-full.");
                f = 1.0;
                if (vapFrac <= 1e-6) w = wl;
                else
                {
                    // the overall composition, the vessel being full of the two-phase mixture
                    var wo = new List<double>();
                    foreach (dynamic c in probe.Phases[0].Compounds.Values) wo.Add(N(c.MassFraction));
                    w = wo.ToArray();
                }
                mass = rl * volume;
                // a liquid-full vessel: fill from the volume surface the vessel's flashes use (the
                // compressed liquid takes its compression from the equation of state there), so the
                // first step does not start with a pressure jump
                try
                {
                    dynamic ppd = pp;
                    ppd.CurrentMaterialStream = probe;
                    double vm = (double)ppd.FlashBase.MixtureMolarVolumeAtTP(z, input.InitialTemperature, input.InitialPressure, ppd);
                    double mw = (double)ppd.AUX_MMM(z);
                    if (vm > 0 && !double.IsNaN(vm)) mass = volume / vm * mw / 1000.0;
                }
                catch { }
            }
            else
            {
                double ml = f * volume * rl, mg = (1 - f) * volume * rg;
                mass = ml + mg;
                w = wl.Select((x, k) => (ml * x + mg * wg[k]) / mass).ToArray();
            }

            dynamic acc = feed.Clone();
            acc.SetFlowsheet(fs);
            acc.SetPropertyPackage(pp);
            acc.SetOverallMassComposition(w);
            acc.SetMassFlow(mass);
            acc.SetTemperature(input.InitialTemperature);
            acc.SetPressure(input.InitialPressure);
            acc.SpecType = StreamSpec.Temperature_and_Pressure;
            acc.Calculate();
            vessel.AccumulationStream = acc;
            vessel.WallTemperature = input.InitialTemperature;
            vessel.WallTemperatureWetted = input.InitialTemperature;
            vessel.WallTemperatureDry = input.InitialTemperature;
            vessel.SetDynamicProperty("Liquid Level", f * Convert.ToDouble(vessel.GetDynamicProperty("Height")));
            return f;
        }
    }
}
