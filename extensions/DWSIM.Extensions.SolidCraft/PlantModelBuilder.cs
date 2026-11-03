//    Copyright 2026 Daniel Wagner O. de Medeiros
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
using System.Globalization;
using System.Linq;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UnitOperations.Reactors;
using DWSIM.UnitOperations.UnitOperations;
using ObjectType = DWSIM.Interfaces.Enums.GraphicObjects.ObjectType;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// Turns a flowsheet into a <see cref="PlantModel"/>: each unit operation becomes equipment sized
    /// from its own data (or estimated from its streams when it has none), placed where its icon is
    /// on the canvas, and each material stream becomes a pipe between the nozzles of the items it
    /// connects, sized for a typical velocity of its phase.
    /// </summary>
    public static class PlantModelBuilder
    {
        // the gap kept between the footprints of neighbouring items when the canvas is scaled
        const double LayoutGap = 2000;

        public static PlantModel Build(IFlowsheet fs, string name, IDictionary<string, Placement> layout = null)
        {
            var model = new PlantModel { Name = name };
            var byGraphic = new Dictionary<string, EquipmentItem>();

            foreach (var obj in fs.SimulationObjects.Values.OrderBy(o => o.GraphicObject?.Tag))
            {
                var go = obj.GraphicObject;
                if (go == null || IsSkipped(go.ObjectType)) continue;

                EquipmentItem item;
                try
                {
                    item = CreateItem(obj, go.ObjectType, model.Warnings);
                }
                catch (Exception ex)
                {
                    model.Warnings.Add(go.Tag + ": " + ex.Message + "; drawn as a block.");
                    item = EquipmentTemplates.Block(2000, 2000, 2500);
                }

                item.Name = obj.Name;
                item.Tag = go.Tag;
                item.Description = SafeDisplayName(obj);
                AddOperatingData(item, obj);
                item.CanvasX = go.X + go.Width / 2.0;
                item.CanvasY = go.Y + go.Height / 2.0;
                if (item.FootprintX <= 0) item.FootprintX = item.Envelope.HalfX;
                if (item.FootprintY <= 0) item.FootprintY = item.Envelope.HalfY;

                // an item placed in SolidCraft before keeps its place and turn
                if (layout != null && layout.TryGetValue(obj.Name, out var placed))
                {
                    item.Turn(placed.Rot);
                    item.Origin = new Vec3(placed.X, placed.Y, placed.Z);
                    item.Placed = true;
                }

                model.Equipment.Add(item);
                byGraphic[go.Name] = item;
            }

            Layout(model);

            foreach (var item in model.Equipment)
            {
                foreach (var p in item.InternalPipes)
                {
                    p.Tag = item.Tag + " " + p.Tag;
                    p.Path = p.Path.Select(v => v + item.Origin).ToList();
                    model.Pipes.Add(p);
                }
            }

            var index = 0;
            foreach (var obj in fs.SimulationObjects.Values.OrderBy(o => o.GraphicObject?.Tag))
            {
                var go = obj.GraphicObject;
                if (go == null || go.ObjectType != ObjectType.MaterialStream) continue;
                try
                {
                    var run = RouteStream(model, byGraphic, obj as IMaterialStream, go, index);
                    if (run != null)
                    {
                        model.Pipes.Add(run);
                        index++;
                    }
                }
                catch (Exception ex)
                {
                    model.Warnings.Add(go.Tag + ": no pipe (" + ex.Message + ").");
                }
            }

            LayOutRack(model);
            model.Pipes.RemoveAll(r => r.Path.Count < 2);

            return model;
        }

        static bool IsSkipped(ObjectType t)
        {
            switch (t)
            {
                case ObjectType.MaterialStream:
                case ObjectType.EnergyStream:
                case ObjectType.NodeEn:
                case ObjectType.EnergyMixer:
                case ObjectType.OT_Adjust:
                case ObjectType.OT_Spec:
                case ObjectType.OT_EnergyRecycle:
                case ObjectType.Switch:
                case ObjectType.Input:
                case ObjectType.AnalogGauge:
                case ObjectType.DigitalGauge:
                case ObjectType.LevelGauge:
                case ObjectType.Nenhum:
                    return true;
            }
            var s = t.ToString();
            return s.StartsWith("GO_") || s.StartsWith("Controller_");
        }

        static string SafeDisplayName(ISimulationObject obj)
        {
            try { return obj.GetDisplayName(); } catch { return obj.GetType().Name; }
        }

        #region Equipment

        static EquipmentItem CreateItem(ISimulationObject obj, ObjectType type, List<string> warnings)
        {
            var tag = obj.GraphicObject.Tag;

            switch (type)
            {
                case ObjectType.Vessel:
                    return CreateVessel(obj, tag, warnings);

                case ObjectType.Tank:
                    {
                        var v = (obj as Tank)?.Volume ?? 0;
                        var estimated = !(v > 0);
                        if (estimated) v = Math.Max(5, LiquidInletFlow(obj) * 1800);
                        v = Clamp(v, 2, 50000);
                        var d = Math.Pow(4 * v / Math.PI, 1.0 / 3) * 1000;
                        var item = EquipmentTemplates.StorageTank(d, d);
                        Describe(item, "Volume (m3)", v, estimated);
                        if (estimated) warnings.Add(tag + ": volume estimated for 30 min of the inlet liquid.");
                        return item;
                    }

                case ObjectType.DistillationColumn:
                case ObjectType.AbsorptionColumn:
                case ObjectType.RefluxedAbsorber:
                case ObjectType.ReboiledAbsorber:
                case ObjectType.ShortcutColumn:
                    return CreateColumn(obj, type, tag, warnings);

                case ObjectType.HeatExchanger:
                    return CreateExchanger(obj as HeatExchanger, tag, warnings);

                case ObjectType.Heater:
                case ObjectType.Cooler:
                case ObjectType.HeaterCooler:
                    {
                        double q = 0;
                        if (obj is Heater h) q = Math.Abs(h.DeltaQ.GetValueOrDefault());
                        if (obj is Cooler c) q = Math.Abs(c.DeltaQ.GetValueOrDefault());
                        if (type == ObjectType.Heater && q > 5000)
                        {
                            var fired = EquipmentTemplates.FiredHeater(Clamp(Math.Pow(q / 10000, 1.0 / 3), 0.7, 2.5));
                            Describe(fired, "Duty (kW)", q, false);
                            return fired;
                        }
                        // area for a typical U of 500 W/m2.K and 30 K of mean temperature difference
                        var area = q > 0 ? q * 1000 / (500 * 30) : 10;
                        return ExchangerFromArea(Clamp(area, 2, 2000), tag + " (from duty)", warnings);
                    }

                case ObjectType.AirCooler2:
                    return EquipmentTemplates.AirCooler(1);

                case ObjectType.Pump:
                    {
                        var p = Math.Abs((obj as Pump)?.DeltaQ.GetValueOrDefault() ?? 0);
                        var item = EquipmentTemplates.Pump(Clamp(Math.Pow(Math.Max(p, 1) / 15, 1.0 / 3), 0.6, 2.5));
                        Describe(item, "Power (kW)", p, false);
                        return item;
                    }

                case ObjectType.Compressor:
                case ObjectType.Expander:
                case ObjectType.CompressorExpander:
                    {
                        double p = 0;
                        if (obj is Compressor c) p = Math.Abs(c.DeltaQ);
                        if (obj is Expander e) p = Math.Abs(e.DeltaQ);
                        var item = EquipmentTemplates.Machine(Clamp(Math.Pow(Math.Max(p, 10) / 500, 1.0 / 3), 0.6, 3), type == ObjectType.Expander);
                        Describe(item, "Power (kW)", p, false);
                        return item;
                    }

                case ObjectType.Valve:
                    return EquipmentTemplates.InlineValve(false);

                case ObjectType.OrificePlate:
                    return EquipmentTemplates.InlineValve(true);

                case ObjectType.NodeIn:
                case ObjectType.NodeOut:
                case ObjectType.Mixer:
                case ObjectType.Splitter:
                    return EquipmentTemplates.Header();

                case ObjectType.OT_Recycle:
                    return EquipmentTemplates.Node();

                case ObjectType.Pipe:
                    return CreatePipeSegment(obj as Pipe);

                case ObjectType.RCT_CSTR:
                    {
                        var v = (obj as Reactor_CSTR)?.Volume ?? 0;
                        var estimated = !(v > 0);
                        if (estimated) v = 1;
                        var d = Clamp(Math.Pow(4 * v / (1.5 * Math.PI), 1.0 / 3), 0.5, 8) * 1000;
                        var item = EquipmentTemplates.VerticalVessel(d, 1.5 * d, 1000 + d / 2, EquipmentTemplates.Reactor);
                        var top = item.Envelope.ZTop;
                        item.Parts.Add(Primitive.Cylinder("agitator drive", new Vec3(0, 0, top - 0.05 * d), "Z", 0.25 * d, 0.35 * d, EquipmentTemplates.Motor));
                        Describe(item, "Volume (m3)", v, estimated);
                        return item;
                    }

                case ObjectType.RCT_PFR:
                    {
                        var pfr = obj as Reactor_PFR;
                        var n = Math.Max(1, pfr?.NumberOfTubes ?? 1);
                        var l = (pfr?.Length ?? 1) * 1000;
                        var dt = (pfr?.Diameter ?? 0.1) * 1000;
                        EquipmentItem item;
                        if (n > 1)
                        {
                            var ds = Clamp(1.3 * dt * Math.Sqrt(n) + 100, 300, 6000);
                            item = EquipmentTemplates.VerticalVessel(ds, Clamp(l, 1000, 30000), 2000 + ds / 2, EquipmentTemplates.Reactor);
                        }
                        else
                        {
                            item = EquipmentTemplates.PipeSpool(Math.Max(50, dt), Clamp(l, 1000, 30000), EquipmentTemplates.Reactor);
                        }
                        item.Properties["Tubes"] = n.ToString(CultureInfo.InvariantCulture);
                        Describe(item, "Tube length (m)", l / 1000, false);
                        Describe(item, "Tube diameter (mm)", dt, false);
                        return item;
                    }

                case ObjectType.RCT_Conversion:
                case ObjectType.RCT_Equilibrium:
                case ObjectType.RCT_Gibbs:
                case ObjectType.RCT_GibbsReaktoro:
                    {
                        var v = DimensionValue(obj, DimensionName.Volume);
                        var estimated = !(v > 0);
                        if (estimated) v = Clamp(InletVolumetricFlow(obj) * 600, 1, 500);
                        var d = Clamp(Math.Pow(4 * v / (3 * Math.PI), 1.0 / 3), 0.6, 8) * 1000;
                        var item = EquipmentTemplates.VerticalVessel(d, 3 * d, 1000 + d / 2, EquipmentTemplates.Reactor);
                        Describe(item, "Volume (m3)", v, estimated);
                        if (estimated) warnings.Add(tag + ": volume estimated for 10 min of the inlet flow.");
                        return item;
                    }

                default:
                    return EquipmentTemplates.Block(2000, 2000, 2500);
            }
        }

        static EquipmentItem CreateVessel(ISimulationObject obj, string tag, List<string> warnings)
        {
            var horizontal = false;
            try { horizontal = Convert.ToInt32(obj.GetDynamicProperty("Vessel Orientation")) == 1; } catch { }

            double d = 0, l = 0;
            var uo = obj as IUnitOperation;
            if (uo?.Dimensions != null && uo.Dimensions.Count >= 2)
            {
                d = uo.Dimensions[0].Value;          // mm
                l = uo.Dimensions[1].Value * 1000;   // m
            }

            var estimated = !(d > 0 && l > 0);
            if (estimated)
            {
                EstimateSeparator(obj, out d, out l);
                warnings.Add(tag + ": diameter and length estimated from the outlet streams (Souders-Brown, 5 min of liquid).");
            }

            var item = horizontal
                ? EquipmentTemplates.HorizontalVessel(d, l, 1000, EquipmentTemplates.Steel)
                : EquipmentTemplates.VerticalVessel(d, l, 1000 + d / 2, EquipmentTemplates.Steel);
            item.Properties["Orientation"] = horizontal ? "Horizontal" : "Vertical";
            Describe(item, "Diameter (mm)", d, estimated);
            Describe(item, "Length T/T (mm)", l, estimated);
            return item;
        }

        /// <summary>Vertical separator sized by Souders-Brown (K = 0.107 m/s) and 5 min of liquid holdup.</summary>
        static void EstimateSeparator(ISimulationObject obj, out double d, out double l)
        {
            var go = obj.GraphicObject;
            var vapor = OutletStream(go, 0);
            double qv = 0, rhov = 0, ql = 0, rhol = 0;
            if (vapor != null)
            {
                qv = Finite(vapor.GetVolumetricFlow());
                rhov = Finite(vapor.Phases[0].Properties.density.GetValueOrDefault());
            }
            for (var i = 1; i < go.OutputConnectors.Count; i++)
            {
                var liq = OutletStream(go, i);
                if (liq == null) continue;
                ql += Finite(liq.GetVolumetricFlow());
                rhol = Math.Max(rhol, Finite(liq.Phases[0].Properties.density.GetValueOrDefault()));
            }

            var dm = 1.0;
            if (qv > 0 && rhov > 0 && rhol > rhov)
            {
                var vmax = 0.107 * Math.Sqrt((rhol - rhov) / rhov);
                dm = Math.Sqrt(4 * qv / vmax / Math.PI);
            }
            dm = Clamp(dm, 0.6, 6);

            var vliq = ql * 300;
            var h = Math.Max(3 * dm, vliq / (Math.PI * dm * dm / 4) + 1.5 * dm);
            while (h > 5 * dm && dm < 6)
            {
                dm *= 1.1;
                h = Math.Max(3 * dm, vliq / (Math.PI * dm * dm / 4) + 1.5 * dm);
            }

            d = dm * 1000;
            l = Math.Min(h, 5 * dm) * 1000;
        }

        static EquipmentItem CreateColumn(ISimulationObject obj, ObjectType type, string tag, List<string> warnings)
        {
            double d = 0, h = 0;
            var stages = 10;
            var condenser = type == ObjectType.DistillationColumn || type == ObjectType.RefluxedAbsorber || type == ObjectType.ShortcutColumn;
            var reboiler = type == ObjectType.DistillationColumn || type == ObjectType.ReboiledAbsorber || type == ObjectType.ShortcutColumn;

            if (obj is Column col)
            {
                if (col.EstimatedDiameter > 0) d = col.EstimatedDiameter * 1000;
                if (col.EstimatedHeight > 0) h = col.EstimatedHeight * 1000;
                if (col.Stages != null && col.Stages.Count > 0) stages = col.Stages.Count;
                if (!(h > 0)) h = (stages * col.TraySpacing + col.TopSpacing + col.BottomSpacing) * 1000 + 2000;
                if (col is DistillationColumn dc)
                {
                    if (dc.ReboiledAbsorber) condenser = false;
                    if (dc.RefluxedAbsorber) reboiler = false;
                }
            }

            var estimated = !(d > 0);
            if (estimated)
            {
                d = obj is Column c2 ? ColumnDiameterFromVapor(c2) : 0;
                if (d > 0)
                {
                    warnings.Add(tag + ": diameter estimated from the stage vapor flows (F-factor 2 Pa^0.5); run the column sizing for a better one.");
                }
                else
                {
                    d = 1500;
                    warnings.Add(tag + ": no diameter on the column and no stage vapor flows; drawn at 1.5 m.");
                }
            }
            if (!(h > 0)) h = stages * 600 + 2000;
            h = Math.Max(h, 3 * d);

            var item = EquipmentTemplates.VerticalVessel(d, h, 3000, EquipmentTemplates.ColumnShell);
            EquipmentTemplates.AddColumnAuxiliaries(item, d, condenser, reboiler);
            item.Properties["Stages"] = stages.ToString(CultureInfo.InvariantCulture);
            Describe(item, "Diameter (mm)", d, estimated);
            Describe(item, "Height T/T (mm)", h, false);
            return item;
        }

        /// <summary>
        /// Tray column diameter (mm) for the largest stage vapor flow: an F-factor of 2 Pa^0.5 on an
        /// active area of 85 % of the section, with an ideal gas of molar mass 50. Zero without flows.
        /// </summary>
        static double ColumnDiameterFromVapor(Column col)
        {
            const double R = 8.314, mw = 0.05;
            double area = 0;
            foreach (var st in col.Stages)
            {
                var v = Finite(st.Vout?.Value ?? 0);   // mol/s
                if (v <= 0 || st.T <= 0 || st.P <= 0) continue;
                var q = v * R * st.T / st.P;          // m3/s
                var rho = st.P * mw / (R * st.T);     // kg/m3
                area = Math.Max(area, q * Math.Sqrt(rho) / 2 / 0.85);
            }
            return area > 0 ? Clamp(Math.Sqrt(4 * area / Math.PI), 0.4, 10) * 1000 : 0;
        }

        static EquipmentItem CreateExchanger(HeatExchanger hx, string tag, List<string> warnings)
        {
            if (hx == null) return ExchangerFromArea(50, tag, warnings);

            var st = hx.STProperties;
            var defaults = st == null || (st.Shell_Di == 500.0 && st.Tube_Length == 5.0);
            var area = hx.Area.GetValueOrDefault();

            if (defaults && area > 0) return ExchangerFromArea(area, null, warnings);

            var d = st != null && st.Shell_Di > 0 ? st.Shell_Di : 500;
            var l = st != null && st.Tube_Length > 0 ? st.Tube_Length * 1000 : 5000;
            var item = EquipmentTemplates.ShellAndTube(d, l);
            Describe(item, "Shell diameter (mm)", d, false);
            Describe(item, "Tube length (mm)", l, false);
            return item;
        }

        /// <summary>
        /// A shell-and-tube exchanger for an area: 3/4" tubes 6 m long (12 m for large areas), bundle
        /// diameter from the tube count for a triangular 1" pitch and two passes.
        /// </summary>
        static EquipmentItem ExchangerFromArea(double area, string estimatedFor, List<string> warnings)
        {
            const double dt = 19.05;
            double l = 6000, ds = 0;
            for (var pass = 0; pass < 2; pass++)
            {
                var n = Math.Max(10, area / (Math.PI * dt / 1000 * l / 1000));
                ds = dt * Math.Pow(n / 0.249, 1 / 2.207) + 60;
                if (ds <= 2000) break;
                l = 12000;
            }
            ds = Clamp(ds, 250, 2500);
            var item = EquipmentTemplates.ShellAndTube(ds, l);
            Describe(item, "Area (m2)", area, estimatedFor != null);
            Describe(item, "Shell diameter (mm)", ds, true);
            if (estimatedFor != null) warnings.Add(estimatedFor + ": exchanger size estimated (U = 500 W/m2.K, 30 K).");
            return item;
        }

        static EquipmentItem CreatePipeSegment(Pipe pipe)
        {
            double od = 168.3, length = 0;
            if (pipe?.Profile?.Sections != null)
            {
                foreach (var s in pipe.Profile.Sections.Values)
                {
                    if (s.DE > 0 && od == 168.3) od = s.DE * 25.4;
                    length += s.Comprimento * Math.Max(1, s.Quantidade);
                }
            }
            var item = EquipmentTemplates.PipeSpool(od, Clamp(length * 1000, 1000, 20000), EquipmentTemplates.Steel);
            Describe(item, "Total length (m)", length, false);
            Describe(item, "Outside diameter (mm)", od, false);
            return item;
        }

        /// <summary>
        /// The operating conditions of an item, from its first outlet stream (its first inlet when it
        /// has none): saved as properties and shown on its label.
        /// </summary>
        static void AddOperatingData(EquipmentItem item, ISimulationObject obj)
        {
            item.Label = item.Tag;
            var go = obj.GraphicObject;
            IMaterialStream ms = null;
            for (var i = 0; i < go.OutputConnectors.Count && ms == null; i++) ms = OutletStream(go, i);
            if (ms == null) ms = InletStreams(obj).FirstOrDefault();
            if (ms == null) return;

            var t = Finite(ms.GetTemperature()) - 273.15;
            var p = Finite(ms.GetPressure()) / 1e5;
            if (!(p > 0)) return;
            item.Properties["Operating temperature (C)"] = Fmt(t, "0.0");
            item.Properties["Operating pressure (bar)"] = Fmt(p, "0.###");
            item.Label = item.Tag + "\n" + Fmt(t, "0.#") + " °C  " + Fmt(p, "0.##") + " bar";
        }

        static string Fmt(double x, string format) => x.ToString(format, CultureInfo.InvariantCulture);

        static void Describe(EquipmentItem item, string key, double value, bool estimated)
        {
            item.Properties[key] = value.ToString("0.###", CultureInfo.InvariantCulture) + (estimated ? " (estimated)" : "");
        }

        static double DimensionValue(ISimulationObject obj, DimensionName name)
        {
            var uo = obj as IUnitOperation;
            if (uo?.Dimensions == null) return 0;
            foreach (var d in uo.Dimensions)
                if (d.Name == name) return Finite(d.Value);
            return 0;
        }

        #endregion

        #region Streams

        static IMaterialStream OutletStream(IGraphicObject go, int index)
        {
            if (index >= go.OutputConnectors.Count) return null;
            var cp = go.OutputConnectors[index];
            if (!cp.IsAttached || cp.AttachedConnector?.AttachedTo == null) return null;
            return go.Flowsheet?.SimulationObjects.TryGetValue(cp.AttachedConnector.AttachedTo.Name, out var s) == true ? s as IMaterialStream : null;
        }

        static IEnumerable<IMaterialStream> InletStreams(ISimulationObject obj)
        {
            var go = obj.GraphicObject;
            foreach (var cp in go.InputConnectors)
            {
                if (!cp.IsAttached || cp.AttachedConnector?.AttachedFrom == null) continue;
                if (go.Flowsheet?.SimulationObjects.TryGetValue(cp.AttachedConnector.AttachedFrom.Name, out var s) == true && s is IMaterialStream ms)
                    yield return ms;
            }
        }

        static double InletVolumetricFlow(ISimulationObject obj) => InletStreams(obj).Sum(s => Finite(s.GetVolumetricFlow()));

        static double LiquidInletFlow(ISimulationObject obj)
        {
            double q = 0;
            foreach (var s in InletStreams(obj))
            {
                var vf = Finite(s.Phases[2].Properties.molarfraction.GetValueOrDefault());
                q += Finite(s.GetVolumetricFlow()) * (1 - vf);
            }
            return q;
        }

        static PipeRun SizePipe(IMaterialStream ms, string tag)
        {
            double q = 0, vf = 0;
            if (ms != null)
            {
                q = Finite(ms.GetVolumetricFlow());
                vf = Finite(ms.Phases[2].Properties.molarfraction.GetValueOrDefault());
            }

            string phase;
            double velocity;
            if (vf > 0.995) { phase = "Vapor"; velocity = 20; }
            else if (vf < 0.005) { phase = "Liquid"; velocity = 2; }
            else { phase = "Two-phase"; velocity = 10; }

            var size = q > 0
                ? PipeSizes.ForInsideDiameter(Math.Sqrt(4 * q / (Math.PI * velocity)) * 1000)
                : PipeSizes.Table[1];   // 3" when the stream has no flow yet

            var run = new PipeRun
            {
                Tag = tag,
                Phase = phase,
                Size = size.Name,
                OuterDiameter = size.OD,
                Wall = size.Wall,
                Color = PipeSizes.ColorFor(phase),
                Velocity = q > 0 ? q / (Math.PI * Math.Pow(size.ID / 1000, 2) / 4) : 0,
                Label = tag + " " + size.Name
            };

            run.Properties["Size"] = size.Name;
            run.Properties["Phase"] = phase;
            if (ms != null)
            {
                var t = Finite(ms.GetTemperature()) - 273.15;
                var p = Finite(ms.GetPressure()) / 1e5;
                run.Properties["Temperature (C)"] = Fmt(t, "0.0");
                run.Properties["Pressure (bar)"] = Fmt(p, "0.###");
                run.Properties["Mass flow (kg/h)"] = Fmt(Finite(ms.GetMassFlow()) * 3600, "0.##");
                run.Properties["Volumetric flow (m3/h)"] = Fmt(q * 3600, "0.###");
                run.Properties["Vapor fraction"] = Fmt(vf, "0.####");
                run.Properties["Velocity (m/s)"] = Fmt(run.Velocity, "0.##");
                if (p > 0) run.Label += "  " + Fmt(t, "0.#") + " °C  " + Fmt(p, "0.##") + " bar";
            }
            return run;
        }

        static PipeRun RouteStream(PlantModel model, Dictionary<string, EquipmentItem> items, IMaterialStream ms, IGraphicObject sgo, int index)
        {
            EquipmentItem from = null, to = null;
            Vec3 pa = default, da = default, pb = default, db = default;

            var inCp = sgo.InputConnectors.Count > 0 ? sgo.InputConnectors[0] : null;
            if (inCp != null && inCp.IsAttached && inCp.AttachedConnector?.AttachedFrom != null)
            {
                var src = inCp.AttachedConnector.AttachedFrom;
                var i = inCp.AttachedConnector.AttachedFromConnectorIndex;
                if (items.TryGetValue(src.Name, out from) && i >= 0 && i < src.OutputConnectors.Count)
                    Nozzle(from, src, src.OutputConnectors[i], i, src.OutputConnectors.Count, true, out pa, out da);
                else from = null;
            }

            var outCp = sgo.OutputConnectors.Count > 0 ? sgo.OutputConnectors[0] : null;
            if (outCp != null && outCp.IsAttached && outCp.AttachedConnector?.AttachedTo != null)
            {
                var dst = outCp.AttachedConnector.AttachedTo;
                var i = outCp.AttachedConnector.AttachedToConnectorIndex;
                if (items.TryGetValue(dst.Name, out to) && i >= 0 && i < dst.InputConnectors.Count)
                    Nozzle(to, dst, dst.InputConnectors[i], i, dst.InputConnectors.Count, false, out pb, out db);
                else to = null;
            }

            if (from == null && to == null) return null;

            var run = SizePipe(ms, sgo.Tag);
            var stub = Math.Max(800, 4 * run.OuterDiameter);
            var path = new List<Vec3>();

            // the flanged nozzles where the line meets its items
            if (from != null) run.Flanges.Add(Tuple.Create(pa + da * NozzleNeck(run), da));
            if (to != null) run.Flanges.Add(Tuple.Create(pb + db * NozzleNeck(run), db));

            if (from != null && to != null)
            {
                var a1 = Lift(pa + da * stub, run);
                var b1 = Lift(pb + db * stub, run);
                if (da.X > 0.5 && db.X < -0.5 && b1.X > a1.X + 1)
                {
                    // facing nozzles, the common left-to-right flow: a Z-shaped run at nozzle height
                    var mx = (a1.X + b1.X) / 2;
                    path.Add(pa);
                    path.Add(a1);
                    path.Add(new Vec3(mx, a1.Y, a1.Z));
                    path.Add(new Vec3(mx, b1.Y, a1.Z));
                    path.Add(new Vec3(mx, b1.Y, b1.Z));
                    path.Add(b1);
                    path.Add(pb);
                }
                else
                {
                    // otherwise on the pipe rack, laid out once every rack line is known
                    run.OnRack = true;
                    run.A = pa; run.A1 = a1; run.B = pb; run.B1 = b1;
                    return run;
                }
            }
            else
            {
                // a feed or a product: from the nozzle to the stream icon, at the battery limit
                var p = from != null ? pa : pb;
                var d = from != null ? da : db;
                var p1 = Lift(p + d * stub, run);
                var end = ToWorld(model, sgo.X + sgo.Width / 2.0, sgo.Y + sgo.Height / 2.0);
                end.Z = p1.Z;
                if (Math.Abs(end.X - p1.X) < 1500 && Math.Abs(end.Y - p1.Y) < 1500)
                {
                    var dir = Math.Abs(d.X) > 0.5 ? d.X : (from != null ? 1 : -1);
                    end.X = p1.X + Math.Sign(dir) * 1500;
                }
                path.Add(p);
                path.Add(p1);
                path.Add(new Vec3(end.X, p1.Y, p1.Z));
                path.Add(end);
                if (to != null) path.Reverse();

                // a blind flange at the battery limit, facing out of the line
                var last = Simplify(from != null ? path : Enumerable.Reverse(path).ToList());
                if (last.Count >= 2)
                    run.Flanges.Add(Tuple.Create(last[last.Count - 1], (last[last.Count - 1] - last[last.Count - 2]).Normalized()));
            }

            run.Path = Simplify(path);
            return run.Path.Count >= 2 ? run : null;
        }

        /// <summary>Distance from the shell to the nozzle flange face, mm.</summary>
        static double NozzleNeck(PipeRun run) => Math.Max(150, 1.5 * run.OuterDiameter);

        /// <summary>
        /// Lays the rack lines out: the rack runs along X behind the plant (on its +Y side, away from
        /// the iso view), each line on its own lane on top of the beams. A line rises (or drops) from
        /// its nozzle to a transfer level over the rack, crosses to its lane, drops onto the rack,
        /// runs along it and leaves the same way, so its crossings pass above the other lanes.
        /// </summary>
        static void LayOutRack(PlantModel model)
        {
            var lines = model.Pipes.Where(r => r.OnRack).ToList();
            if (lines.Count == 0) return;

            const double top = 6000, gap = 150;
            var visible = model.Equipment.Where(e => e.Visible).ToList();
            var y0 = (visible.Count > 0 ? visible.Max(e => e.Origin.Y + e.FootprintY) : 0) + 3000;
            var width = Math.Max(2000, lines.Sum(r => r.OuterDiameter + gap) + 2 * gap);
            var transfer = top + Math.Max(900, lines.Max(r => r.OuterDiameter) + 600);

            var y = y0 + gap;
            foreach (var run in lines)
            {
                var lane = y + run.OuterDiameter / 2;
                y += run.OuterDiameter + gap;
                var zl = top + run.OuterDiameter / 2;
                var zt = transfer;
                run.Path = Simplify(new List<Vec3>
                {
                    run.A, run.A1,
                    new Vec3(run.A1.X, run.A1.Y, zt),
                    new Vec3(run.A1.X, lane, zt),
                    new Vec3(run.A1.X, lane, zl),
                    new Vec3(run.B1.X, lane, zl),
                    new Vec3(run.B1.X, lane, zt),
                    new Vec3(run.B1.X, run.B1.Y, zt),
                    run.B1, run.B
                });
            }

            var xs = lines.SelectMany(r => new[] { r.A1.X, r.B1.X }).ToList();
            model.Rack = new PipeRack
            {
                X0 = xs.Min() - 1500,
                X1 = xs.Max() + 1500,
                Y0 = y0,
                Width = width,
                Top = top
            };
        }

        /// <summary>Keeps a pipe off the ground.</summary>
        static Vec3 Lift(Vec3 p, PipeRun run)
        {
            var min = run.OuterDiameter / 2 + 300;
            if (p.Z < min) p.Z = min;
            return p;
        }

        /// <summary>
        /// Drops repeated points and the middle point of straight runs and of runs that double back
        /// (SolidCraft cannot sweep a pipe along a path that turns back on itself).
        /// </summary>
        static List<Vec3> Simplify(List<Vec3> path)
        {
            var pts = new List<Vec3>(path);
            var changed = true;
            while (changed)
            {
                changed = false;
                for (var i = 1; i < pts.Count; i++)
                {
                    if ((pts[i] - pts[i - 1]).Length <= 1)
                    {
                        pts.RemoveAt(i);
                        changed = true;
                        break;
                    }
                }
                if (changed) continue;
                for (var i = 1; i < pts.Count - 1; i++)
                {
                    var a = (pts[i] - pts[i - 1]).Normalized();
                    var b = (pts[i + 1] - pts[i]).Normalized();
                    if (Math.Abs(Vec3.Dot(a, b)) > 0.9999)
                    {
                        pts.RemoveAt(i);
                        changed = true;
                        break;
                    }
                }
            }
            return pts;
        }

        /// <summary>World position and outward direction of the nozzle for a connector of an icon.</summary>
        static void Nozzle(EquipmentItem item, IGraphicObject go, IConnectionPoint cp, int index, int count, bool output, out Vec3 position, out Vec3 direction)
        {
            double u = double.NaN, v = double.NaN;
            if (go.Width > 0 && go.Height > 0)
            {
                double cx = cp.X, cy = cp.Y;
                if (cp.Position != null && (cp.Position.X != 0 || cp.Position.Y != 0))
                {
                    cx = cp.Position.X;
                    cy = cp.Position.Y;
                }
                u = (cx - go.X) / go.Width;
                v = (cy - go.Y) / go.Height;
            }
            if (double.IsNaN(u) || u < -0.3 || u > 1.3 || v < -0.3 || v > 1.3)
            {
                // connector positions not laid out yet: inlets on the left, outlets on the right
                u = output ? 1 : 0;
                v = (index + 1.0) / (count + 1.0);
                if (go.FlippedH) u = 1 - u;
                if (go.FlippedV) v = 1 - v;
            }
            item.Envelope.Locate(u, v, out var local, out direction);
            position = item.Origin + local.TurnedZ(item.Rotation);
            direction = direction.TurnedZ(item.Rotation);
        }

        #endregion

        #region Layout

        /// <summary>
        /// Scales the canvas to the plot plan: the smallest scale that keeps every pair of items at
        /// least <see cref="LayoutGap"/> apart along X or Y, so the flowsheet arrangement is kept.
        /// </summary>
        static void Layout(PlantModel model)
        {
            var items = model.Equipment;
            double scale = 15;
            for (var i = 0; i < items.Count; i++)
            {
                for (var j = i + 1; j < items.Count; j++)
                {
                    var a = items[i];
                    var b = items[j];
                    var dx = Math.Abs(a.CanvasX - b.CanvasX);
                    var dy = Math.Abs(a.CanvasY - b.CanvasY);
                    if (dx < 1 && dy < 1) continue;
                    var sx = dx >= 1 ? (a.FootprintX + b.FootprintX + LayoutGap) / dx : double.MaxValue;
                    var sy = dy >= 1 ? (a.FootprintY + b.FootprintY + LayoutGap) / dy : double.MaxValue;
                    scale = Math.Max(scale, Math.Min(sx, sy));
                }
            }
            model.Scale = Math.Min(scale, 600);
            if (scale > 600) model.Warnings.Add("Some icons overlap on the flowsheet; their equipment overlaps too.");

            // the plant centred on the origin, so the coordinates stay as small as they can
            model.CanvasOriginX = items.Count > 0 ? (items.Min(e => e.CanvasX) + items.Max(e => e.CanvasX)) / 2 : 0;
            model.CanvasOriginY = items.Count > 0 ? (items.Min(e => e.CanvasY) + items.Max(e => e.CanvasY)) / 2 : 0;

            foreach (var item in items.Where(e => !e.Placed))
                item.Origin = ToWorld(model, item.CanvasX, item.CanvasY);
        }

        static Vec3 ToWorld(PlantModel model, double cx, double cy)
            => new Vec3(Math.Round((cx - model.CanvasOriginX) * model.Scale), -Math.Round((cy - model.CanvasOriginY) * model.Scale), 0);

        #endregion

        static double Clamp(double x, double a, double b) => x < a ? a : (x > b ? b : x);

        static double Finite(double x) => double.IsNaN(x) || double.IsInfinity(x) ? 0 : x;
    }
}
