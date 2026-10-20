using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.UI.Shared.Avalonia;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.Pipe;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;
using Pipe = DWSIM.UnitOperations.UnitOperations.Pipe;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Profiles along a solved pipe segment, as the Windows results tabs show them: any variable
    /// against any other on a chart, and every calculation increment in a table. Each increment
    /// is placed at the length where it starts (fittings take no length), and the outlet stream
    /// closes the profile at the total length.
    /// </summary>
    public static class PipeProfilesViewer
    {

        /// <summary>One point of the profile, in SI units.</summary>
        private sealed class Point
        {
            public double Length, Inclination, Pressure, Temperature, LiquidVelocity, VaporVelocity, Mach,
                Heat, Holdup, HTC, HTCInternal, HTCWall, HTCInsulation, HTCExternal, ExternalT, WallT, InsulationT,
                Radiation;
            public string Regime = "";
            public bool Outlet;
        }

        /// <summary>A variable that can go on either axis: its name, its unit dimension and its value.</summary>
        private sealed class Variable
        {
            public string Name = "";
            public Func<IUnitsOfMeasure, string> Unit = _ => "";
            public Func<Point, double> Value = _ => 0.0;
            public bool OnOutlet = true;
        }

        private static List<Variable> Variables() => new List<Variable>
        {
            new Variable { Name = "Length", Unit = su => su.distance, Value = p => p.Length },
            new Variable { Name = "Pressure", Unit = su => su.pressure, Value = p => p.Pressure },
            new Variable { Name = "Temperature", Unit = su => su.temperature, Value = p => p.Temperature },
            new Variable { Name = "Liquid Velocity", Unit = su => su.velocity, Value = p => p.LiquidVelocity, OnOutlet = false },
            new Variable { Name = "Vapor Velocity", Unit = su => su.velocity, Value = p => p.VaporVelocity, OnOutlet = false },
            new Variable { Name = "Mach Number", Value = p => p.Mach, OnOutlet = false },
            new Variable { Name = "Heat Transferred", Unit = su => su.heatflow, Value = p => p.Heat, OnOutlet = false },
            new Variable { Name = "Liquid Holdup", Value = p => p.Holdup, OnOutlet = false },
            new Variable { Name = "Overall HTC", Unit = su => su.heat_transf_coeff, Value = p => p.HTC, OnOutlet = false },
        };

        /// <summary>The profile points of the last calculation, or an empty list when there is none.</summary>
        private static List<Point> Points(Pipe pipe)
        {
            var list = new List<Point>();
            if (pipe.Profile == null) return list;

            double length = 0.0;
            foreach (var ps in pipe.Profile.Sections.Values)
            {
                if (ps.Results == null) continue;
                var straight = ps.TipoSegmento == "Tubulaosimples" || ps.TipoSegmento == "Straight Tube Section";
                var step = straight && ps.Incrementos > 0 ? ps.Comprimento / ps.Incrementos : 0.0;
                var start = length;
                int k = 0;
                var horizontal = ps.Comprimento * ps.Comprimento - ps.Elevacao * ps.Elevacao;
                var inclination = straight && horizontal > 0.0 ? Math.Atan(ps.Elevacao / Math.Sqrt(horizontal)) * 180.0 / Math.PI : 0.0;
                foreach (PipeResults res in ps.Results)
                {
                    // the calculation appends the outlet state to the last section, one result past its increments
                    list.Add(new Point
                    {
                        Length = start + Math.Min(k, Math.Max(ps.Incrementos, 0)) * step,
                        Inclination = inclination,
                        Pressure = res.Pressure_Initial.GetValueOrDefault(),
                        Temperature = res.Temperature_Initial.GetValueOrDefault(),
                        LiquidVelocity = res.LiqVel.GetValueOrDefault(),
                        VaporVelocity = res.VapVel.GetValueOrDefault(),
                        Mach = res.MachNumber,
                        Heat = res.HeatTransferred.GetValueOrDefault(),
                        Holdup = res.LiquidHoldup.GetValueOrDefault(),
                        Regime = res.FlowRegime ?? "",
                        HTC = res.HTC.GetValueOrDefault(),
                        HTCInternal = res.HTC_internal,
                        HTCWall = res.HTC_pipewall,
                        HTCInsulation = res.HTC_insulation,
                        HTCExternal = res.HTC_external,
                        ExternalT = res.External_Temperature,
                        WallT = res.Wall_Temperature,
                        InsulationT = res.Insulation_Temperature,
                        Radiation = res.Absorbed_Radiation,
                    });
                    k++;
                }
                length = start + (straight ? ps.Comprimento : 0.0);
            }

            if (list.Count == 0) return list;

            // the outlet, unless the calculation already left it as the last result: the increments
            // carry their initial state only
            var outlet = OutletStream(pipe);
            if (outlet != null && list[list.Count - 1].Length < length - 1e-9)
            {
                var last = list[list.Count - 1];
                list.Add(new Point
                {
                    Length = length,
                    Inclination = last.Inclination,
                    Pressure = outlet.GetPressure(),
                    Temperature = outlet.GetTemperature(),
                    LiquidVelocity = last.LiquidVelocity,
                    VaporVelocity = last.VaporVelocity,
                    Mach = last.Mach,
                    Holdup = last.Holdup,
                    HTC = last.HTC,
                    Regime = "",
                    Outlet = true
                });
            }
            return list;
        }

        private static IMaterialStream OutletStream(Pipe pipe)
        {
            try
            {
                var cp = pipe.GraphicObject.OutputConnectors[0];
                if (!cp.IsAttached) return null;
                return pipe.GetFlowsheet().SimulationObjects[cp.AttachedConnector.AttachedTo.Name] as IMaterialStream;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Adds the pressure-against-length chart of the last calculation to a results panel, with a
        /// button that opens the full profiles window. Nothing is added before the pipe is solved.
        /// </summary>
        public static void AddToResults(Pipe pipe, AvaloniaEditorPanel panel)
        {
            var points = Points(pipe);
            if (points.Count < 2) return;
            var su = pipe.GetFlowsheet().FlowsheetOptions.SelectedUnitSystem;
            var vars = Variables();

            panel.CreateAndAddLabelRow("Pressure Profile");
            var chart = new XYPlot { Height = 240 };
            Draw(chart, points, su, vars[0], vars[1]);
            panel.Children.Add(chart);
            panel.CreateAndAddButtonRow("View Profiles (Chart and Table)", null, (btn, e) => Show(pipe));
        }

        public static void Show(Pipe pipe)
        {
            var flowsheet = pipe.GetFlowsheet();
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
            var nf = flowsheet.FlowsheetOptions.NumberFormat;
            var points = Points(pipe);

            var panel = AvaloniaCommon.GetDefaultContainer();
            var window = AvaloniaCommon.GetDefaultEditorForm(pipe.GraphicObject.Tag + ": Profiles", 900, 640, panel);

            if (points.Count < 2)
            {
                panel.CreateAndAddDescriptionRow("Solve the pipe segment to see its profiles.");
                window.Show();
                return;
            }

            var tabs = new TabControl { Height = 540 };
            tabs.Items.Add(new TabItem { Header = "Chart", Content = BuildChart(points, su) });
            tabs.Items.Add(new TabItem { Header = "Table", Content = BuildTable(points, su, nf) });
            panel.Children.Add(tabs);
            window.Show();
        }

        private static Control BuildChart(List<Point> points, IUnitsOfMeasure su)
        {
            var vars = Variables();
            var names = vars.Select(v => v.Name).ToList();
            var panel = new AvaloniaEditorPanel();
            var chart = new XYPlot { Height = 400 };
            int ix = 0, iy = 1;

            panel.CreateAndAddDropDownRow("X axis", names, ix, (cb, e) =>
            {
                if (cb.SelectedIndex < 0) return;
                ix = cb.SelectedIndex;
                Draw(chart, points, su, vars[ix], vars[iy]);
            });
            panel.CreateAndAddDropDownRow("Y axis", names, iy, (cb, e) =>
            {
                if (cb.SelectedIndex < 0) return;
                iy = cb.SelectedIndex;
                Draw(chart, points, su, vars[ix], vars[iy]);
            });
            panel.Children.Add(chart);
            Draw(chart, points, su, vars[ix], vars[iy]);
            return panel;
        }

        private static void Draw(XYPlot chart, List<Point> points, IUnitsOfMeasure su, Variable vx, Variable vy)
        {
            // the outlet only carries what the outlet stream knows
            var used = points.Where(p => !p.Outlet || (vx.OnOutlet && vy.OnOutlet)).ToList();
            var x = used.Select(p => Convert(vx, su, p)).ToList();
            var y = used.Select(p => Convert(vy, su, p)).ToList();

            chart.Clear();
            chart.XAxisTitle = Title(vx, su);
            chart.YAxisTitle = Title(vy, su);
            var line = chart.AddSeries(vy.Name, x, y);
            if (line != null) line.Color = Colors.SteelBlue;
            chart.InvalidateVisual();
        }

        private static double Convert(Variable v, IUnitsOfMeasure su, Point p)
        {
            var unit = v.Unit(su);
            var si = v.Value(p);
            return string.IsNullOrEmpty(unit) ? si : cv.ConvertFromSI(unit, si);
        }

        private static string Title(Variable v, IUnitsOfMeasure su)
        {
            var unit = v.Unit(su);
            return string.IsNullOrEmpty(unit) ? v.Name : v.Name + " (" + unit + ")";
        }

        private static Control BuildTable(List<Point> points, IUnitsOfMeasure su, string nf)
        {
            string F(string unit, double si) =>
                (string.IsNullOrEmpty(unit) ? si : cv.ConvertFromSI(unit, si)).ToString(nf, CultureInfo.CurrentCulture);

            var columns = new List<(string Header, Func<Point, string> Cell)>
            {
                ("Length (" + su.distance + ")", p => F(su.distance, p.Length)),
                ("Inclination (deg)", p => p.Outlet ? "" : F("", p.Inclination)),
                ("Pressure (" + su.pressure + ")", p => F(su.pressure, p.Pressure)),
                ("Temperature (" + su.temperature + ")", p => F(su.temperature, p.Temperature)),
                ("Liquid Velocity (" + su.velocity + ")", p => p.Outlet ? "" : F(su.velocity, p.LiquidVelocity)),
                ("Vapor Velocity (" + su.velocity + ")", p => p.Outlet ? "" : F(su.velocity, p.VaporVelocity)),
                ("Mach Number", p => p.Outlet ? "" : F("", p.Mach)),
                ("Heat Transferred (" + su.heatflow + ")", p => p.Outlet ? "" : F(su.heatflow, p.Heat)),
                ("Liquid Holdup", p => p.Outlet ? "" : F("", p.Holdup)),
                ("Flow Regime", p => p.Outlet ? "Outlet" : p.Regime),
                ("Overall HTC (" + su.heat_transf_coeff + ")", p => p.Outlet ? "" : F(su.heat_transf_coeff, p.HTC)),
                ("Internal HTC (" + su.heat_transf_coeff + ")", p => p.Outlet ? "" : F(su.heat_transf_coeff, p.HTCInternal)),
                ("Pipe Wall HTC (" + su.heat_transf_coeff + ")", p => p.Outlet ? "" : F(su.heat_transf_coeff, p.HTCWall)),
                ("Insulation HTC (" + su.heat_transf_coeff + ")", p => p.Outlet ? "" : F(su.heat_transf_coeff, p.HTCInsulation)),
                ("External HTC (" + su.heat_transf_coeff + ")", p => p.Outlet ? "" : F(su.heat_transf_coeff, p.HTCExternal)),
                ("External Temperature (" + su.temperature + ")", p => p.Outlet ? "" : F(su.temperature, p.ExternalT)),
                ("Wall Temperature (" + su.temperature + ")", p => p.Outlet ? "" : F(su.temperature, p.WallT)),
                ("Insulation Temperature (" + su.temperature + ")", p => p.Outlet ? "" : F(su.temperature, p.InsulationT)),
                ("Absorbed Radiation (" + su.heatflow + ")", p => p.Outlet ? "" : F(su.heatflow, p.Radiation)),
            };

            var rows = points.Select(p =>
            {
                var row = new Dictionary<string, string>();
                for (int i = 0; i < columns.Count; i++) row["c" + i] = columns[i].Cell(p);
                return row;
            }).ToList();

            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                ItemsSource = rows
            };
            for (int i = 0; i < columns.Count; i++)
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = columns[i].Header,
                    Binding = new Binding("[c" + i + "]") { Mode = BindingMode.OneWay },
                    Width = DataGridLength.Auto
                });
            return grid;
        }

    }

}
