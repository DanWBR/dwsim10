using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.UnitOperations.UnitOperations;
using DWSIM.UI.Shared.Avalonia;
using ALayout = Avalonia.Layout;
using AMedia = Avalonia.Media;
using AControls = Avalonia.Controls;
using AShapes = Avalonia.Controls.Shapes;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Editors
{
    /// <summary>
    /// Builds the secondary tabs (Connections / Custom Properties / Results / Appearance)
    /// for the Avalonia ObjectEditorContainer. These mirror the Eto editors under
    /// DWSIM.UI.Desktop.Editors\General\ but render with AvaloniaEditorPanel.
    /// </summary>
    internal static class AvaloniaTabBuilders
    {
        // -----------------------------------------------------------------
        // Connections tab
        // -----------------------------------------------------------------

        public static AvaloniaEditorPanel BuildConnections(ISimulationObject simobj)
        {
            var panel = new AvaloniaEditorPanel();
            var fs = simobj.GetFlowsheet();

            panel.CreateAndAddDescriptionRow(
                "Pick a stream from each dropdown to (re)attach a connector. Selecting the blank entry disconnects.");

            // Collect candidate stream tags (only currently unattached streams of the right type).
            var msAvailIn = fs.GraphicObjects.Values
                .Where(x => x.ObjectType == ObjectType.MaterialStream
                            && x.OutputConnectors.Count > 0 && !x.OutputConnectors[0].IsAttached)
                .Select(m => m.Tag).ToList();
            var esAvailIn = fs.GraphicObjects.Values
                .Where(x => x.ObjectType == ObjectType.EnergyStream
                            && x.OutputConnectors.Count > 0 && !x.OutputConnectors[0].IsAttached)
                .Select(m => m.Tag).ToList();
            var msAvailOut = fs.GraphicObjects.Values
                .Where(x => x.ObjectType == ObjectType.MaterialStream
                            && x.InputConnectors.Count > 0 && !x.InputConnectors[0].IsAttached)
                .Select(m => m.Tag).ToList();
            var esAvailOut = fs.GraphicObjects.Values
                .Where(x => x.ObjectType == ObjectType.EnergyStream
                            && x.InputConnectors.Count > 0 && !x.InputConnectors[0].IsAttached)
                .Select(m => m.Tag).ToList();

            // Material inputs
            foreach (var cp in simobj.GraphicObject.InputConnectors)
                if (cp.Type != ConType.ConEn)
                    AddConnectorRow(panel, fs, simobj, cp, msAvailIn, isInput: true, isEnergy: false);

            // Material outputs
            foreach (var cp in simobj.GraphicObject.OutputConnectors)
                if (cp.Type != ConType.ConEn)
                    AddConnectorRow(panel, fs, simobj, cp, msAvailOut, isInput: false, isEnergy: false);

            // Energy inputs
            foreach (var cp in simobj.GraphicObject.InputConnectors)
                if (cp.Type == ConType.ConEn)
                    AddConnectorRow(panel, fs, simobj, cp, esAvailIn, isInput: true, isEnergy: true);

            // Energy outputs
            foreach (var cp in simobj.GraphicObject.OutputConnectors)
                if (cp.Type == ConType.ConEn)
                    AddConnectorRow(panel, fs, simobj, cp, esAvailOut, isInput: false, isEnergy: true);

            // Reactor / heater energy connector (the special "Q" port some UOs expose)
            if (simobj.GraphicObject.EnergyConnector != null && simobj.GraphicObject.EnergyConnector.Active)
                AddConnectorRow(panel, fs, simobj, simobj.GraphicObject.EnergyConnector, esAvailOut,
                    isInput: false, isEnergy: true);

            return panel;
        }

        private static void AddConnectorRow(AvaloniaEditorPanel panel, IFlowsheet fs,
            ISimulationObject simobj, IConnectionPoint connector, List<string> options,
            bool isInput, bool isEnergy)
        {
            // Always offer "(disconnect)" as the first option.
            var entries = new List<string> { "(disconnect)" };
            entries.AddRange(options);

            // If this connector is already attached, the currently-bound stream isn't in the
            // "available" list (since it's attached) — insert it so the dropdown can reflect it.
            string currentTag = "";
            if (connector.IsAttached && connector.AttachedConnector != null)
            {
                if (isInput)
                {
                    var src = connector.AttachedConnector.AttachedFrom;
                    if (src != null) currentTag = src.Tag;
                }
                else
                {
                    var dst = connector.AttachedConnector.AttachedTo;
                    if (dst != null) currentTag = dst.Tag;
                }
                if (!string.IsNullOrEmpty(currentTag) && !entries.Contains(currentTag))
                    entries.Add(currentTag);
            }

            int selectedIdx = string.IsNullOrEmpty(currentTag) ? 0 : entries.IndexOf(currentTag);
            if (selectedIdx < 0) selectedIdx = 0;

            // Use the ConnectorName as label; fall back to a short positional name if the
            // name is empty or looks like a GUID (some objects auto-generate connector IDs).
            var cname = connector.ConnectorName;
            if (string.IsNullOrWhiteSpace(cname) || cname.Length > 30 || Guid.TryParse(cname.Split('|')[0].TrimStart("AQ-".ToCharArray()), out _))
                cname = (isInput ? "In" : "Out") + (isEnergy ? " (Energy)" : "");
            string label = cname;

            panel.CreateAndAddDropDownRow(label, entries, selectedIdx, (dd, e) =>
            {
                if (dd.SelectedIndex < 0) return;
                var newTag = (string)dd.SelectedItem;

                // 1. Disconnect current attachment first (if any).
                if (connector.IsAttached && connector.AttachedConnector != null)
                {
                    var partner = isInput
                        ? connector.AttachedConnector.AttachedFrom
                        : connector.AttachedConnector.AttachedTo;
                    if (partner != null)
                    {
                        if (isInput)
                            fs.DisconnectObjects(partner, simobj.GraphicObject);
                        else
                            fs.DisconnectObjects(simobj.GraphicObject, partner);
                    }
                }

                if (newTag == "(disconnect)")
                {
                    fs.UpdateInterface();
                    return;
                }

                // 2. Resolve the new partner graphic object by tag.
                var partnerObj = fs.GraphicObjects.Values.FirstOrDefault(g => g.Tag == newTag);
                if (partnerObj == null) return;

                int fromIdx, toIdx;
                if (isInput)
                {
                    // partner is a stream feeding us: stream's output 0 -> our connector index
                    fromIdx = 0;
                    toIdx = simobj.GraphicObject.InputConnectors.IndexOf(connector);
                    fs.ConnectObjects(partnerObj, simobj.GraphicObject, fromIdx, toIdx);
                }
                else
                {
                    // we're feeding the partner: our connector index -> stream's input 0
                    fromIdx = simobj.GraphicObject.OutputConnectors.IndexOf(connector);
                    if (fromIdx < 0 && connector == simobj.GraphicObject.EnergyConnector) fromIdx = 0;
                    toIdx = 0;
                    fs.ConnectObjects(simobj.GraphicObject, partnerObj, fromIdx, toIdx);
                }

                fs.UpdateInterface();
            });
        }

        // -----------------------------------------------------------------
        // Custom Properties tab
        // -----------------------------------------------------------------

        public static AvaloniaEditorPanel BuildCustomProperties(ISimulationObject simobj)
        {
            var panel = new AvaloniaEditorPanel();
            panel.CreateAndAddDescriptionRow(
                "User-defined properties stored on this object. Values commit on edit.");

            var col1 = (IDictionary<string, object>)simobj.ExtraProperties;
            var col2 = (IDictionary<string, object>)simobj.ExtraPropertiesDescriptions;
            var col3 = (IDictionary<string, object>)simobj.ExtraPropertiesUnitTypes;

            int shown = 0;
            foreach (var p in col1)
            {
                // Skip entries that carry a description or unit type — those belong to a
                // structured custom-property editor that the engine builds elsewhere.
                if (col2.ContainsKey(p.Key) || col3.ContainsKey(p.Key)) continue;

                var key = p.Key;
                panel.CreateAndAddStringEditorRow(key, p.Value?.ToString() ?? "",
                    (tb, e) => { col1[key] = tb.Text; });
                shown++;
            }

            if (shown == 0)
                panel.CreateAndAddDescriptionRow("(no custom properties defined)");

            return panel;
        }

        // -----------------------------------------------------------------
        // Dynamics tab
        // -----------------------------------------------------------------

        public static AvaloniaEditorPanel BuildDynamics(ISimulationObject simobj)
        {
            var panel = new AvaloniaEditorPanel();
            PopulateDynamics(simobj, panel);
            return panel;
        }

        /// <summary>
        /// Appends the dynamic-mode parameters to an existing panel, for editors that show them
        /// inline instead of on a tab of their own: the material stream puts them with its
        /// stream conditions, as the WinForms editor does.
        /// </summary>
        public static void PopulateDynamics(ISimulationObject simobj, AvaloniaEditorPanel panel)
        {
            if (!simobj.SupportsDynamicMode)
            {
                panel.CreateAndAddDescriptionRow("This unit operation does not support dynamic simulation.");
                return;
            }

            panel.CreateAndAddDescriptionRow(
                "Parameters listed below drive the dynamic-mode integration (volumes, conductances, " +
                "initial conditions). They are stored on the object and persisted with the simulation.");

            var col1 = (IDictionary<string, object>)simobj.ExtraProperties;
            var col2 = (IDictionary<string, object>)simobj.ExtraPropertiesDescriptions;
            var col3 = (IDictionary<string, object>)simobj.ExtraPropertiesUnitTypes;

            // Dynamic props are characterized by HAVING a description AND a unit-type entry.
            var dynProps = col1.Where(p => col2.ContainsKey(p.Key) && col3.ContainsKey(p.Key)).ToList();

            if (dynProps.Count == 0)
            {
                panel.CreateAndAddDescriptionRow("(no dynamic properties yet)");
                panel.CreateAndAddButtonRow("Create / Refresh Dynamic Properties", null, (btn, e) =>
                {
                    try { simobj.CreateDynamicProperties(); }
                    catch { /* some UOs don't ship CreateDynamicProperties (interface allows no-op) */ }
                    simobj.GetFlowsheet().UpdateOpenEditForms();
                });
                return;
            }

            foreach (var p in dynProps)
            {
                var key = p.Key;
                var descObj = col2.TryGetValue(key, out var d) ? d : null;
                var unitObj = col3.TryGetValue(key, out var u) ? u : null;
                var desc = descObj?.ToString() ?? "";
                var label = key + (unitObj is DWSIM.Interfaces.Enums.UnitOfMeasure uom && uom != DWSIM.Interfaces.Enums.UnitOfMeasure.none
                    ? $" ({uom})" : "");

                // Bool, double, int — switch on the live value type so we render the right control.
                if (p.Value is bool b)
                {
                    panel.CreateAndAddCheckBoxRow(label, b,
                        (cb, e) => col1[key] = cb.IsChecked.GetValueOrDefault());
                }
                else if (p.Value is double dv || p.Value is int)
                {
                    double cur;
                    try { cur = Convert.ToDouble(p.Value); } catch { cur = 0; }
                    panel.CreateAndAddStringEditorRow(label, cur.ToString("G6", System.Globalization.CultureInfo.InvariantCulture),
                        (tb, e) =>
                        {
                            if (double.TryParse(tb.Text, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var nv))
                                col1[key] = nv;
                        });
                }
                else
                {
                    panel.CreateAndAddStringEditorRow(label, p.Value?.ToString() ?? "",
                        (tb, e) => col1[key] = tb.Text);
                }

                if (!string.IsNullOrWhiteSpace(desc))
                    panel.CreateAndAddDescriptionRow(desc);
            }
        }

        // -----------------------------------------------------------------
        // Results tab
        // -----------------------------------------------------------------

        public static AvaloniaEditorPanel BuildResults(ISimulationObject simobj)
        {
            var panel = new AvaloniaEditorPanel();

            string report;
            try
            {
                report = simobj.GetReport(
                    simobj.GetFlowsheet().FlowsheetOptions.SelectedUnitSystem,
                    System.Globalization.CultureInfo.InvariantCulture,
                    simobj.GetFlowsheet().FlowsheetOptions.NumberFormat);
            }
            catch (Exception ex)
            {
                report = "Could not generate report: " + ex.Message;
            }

            panel.CreateAndAddMultilineMonoSpaceTextBoxRow(report ?? "(no report)", 400, true, null);

            // Per-UO supplemental views
            if (simobj is Pipe pipe) AppendPipeProfileChart(panel, pipe);
            if (simobj is HeatExchanger hx) AppendHeatExchangerProfile(panel, hx);

            return panel;
        }

        // -----------------------------------------------------------------
        // Pipe hydraulic / thermal profile chart (Length vs Pressure / Temperature)
        // -----------------------------------------------------------------

        private static void AppendPipeProfileChart(AvaloniaEditorPanel panel, Pipe pipe)
        {
            // Build cumulative-length and pressure/temperature arrays from the per-increment
            // PipeResults stored on each Section. The engine populates these during Solve;
            // before that, every Results list is empty and we just show a hint.
            var su = pipe.GetFlowsheet().FlowsheetOptions.SelectedUnitSystem;
            var lengths = new List<double>();
            var pressures = new List<double>();
            var temps = new List<double>();
            double cumulative = 0.0;
            foreach (var sec in pipe.Profile.Sections.Values)
            {
                int incr = Math.Max(1, sec.Incrementos);
                double segL = sec.Comprimento;
                double step = segL / incr;
                foreach (var r in sec.Results)
                {
                    cumulative += step;
                    lengths.Add(cv.ConvertFromSI(su.distance, cumulative));
                    pressures.Add(cv.ConvertFromSI(su.pressure, r.Pressure_Initial.GetValueOrDefault()));
                    temps.Add(cv.ConvertFromSI(su.temperature, r.Temperature_Initial.GetValueOrDefault()));
                }
            }

            panel.CreateAndAddLabelRow("Pipe Profile");

            if (lengths.Count == 0)
            {
                panel.CreateAndAddDescriptionRow("Solve the flowsheet to populate per-increment hydraulic / thermal data.");
                return;
            }

            panel.CreateAndAddDescriptionRow($"{lengths.Count} increment(s). Two charts: pressure and temperature along the length.");
            panel.CreateAndAddControlRow(MakeLineChart($"Pressure along length ({su.pressure})", $"Length ({su.distance})", lengths, pressures));
            panel.CreateAndAddControlRow(MakeLineChart($"Temperature along length ({su.temperature})", $"Length ({su.distance})", lengths, temps));
        }

        // -----------------------------------------------------------------
        // HeatExchanger hot/cold side temperature profile (rigorous mode only)
        // -----------------------------------------------------------------

        private static void AppendHeatExchangerProfile(AvaloniaEditorPanel panel, HeatExchanger hx)
        {
            if (hx.TemperatureProfileCold == null || hx.TemperatureProfileHot == null
                || hx.TemperatureProfileCold.Length == 0 || hx.TemperatureProfileHot.Length == 0)
                return;

            var su = hx.GetFlowsheet().FlowsheetOptions.SelectedUnitSystem;

            var cold = hx.TemperatureProfileCold.Select(t => cv.ConvertFromSI(su.temperature, t)).ToList();
            var hot  = hx.TemperatureProfileHot .Select(t => cv.ConvertFromSI(su.temperature, t)).ToList();
            int n = Math.Min(cold.Count, hot.Count);
            var stages = Enumerable.Range(1, n).Select(i => (double)i).ToList();

            panel.CreateAndAddLabelRow("Heat Exchanger Temperature Profile");
            panel.CreateAndAddDescriptionRow("Hot vs. cold side temperature at each axial step (rigorous mode).");
            panel.CreateAndAddControlRow(MakeDualLineChart(
                $"Temperature profile ({su.temperature})", "Step",
                stages, hot.Take(n).ToList(), "Hot",
                stages, cold.Take(n).ToList(), "Cold"));
        }

        // -----------------------------------------------------------------
        // Lightweight line chart on Canvas (no OxyPlot dependency)
        // -----------------------------------------------------------------

        private static AControls.Border MakeLineChart(string title, string xLabel,
            List<double> xs, List<double> ys)
        {
            return MakeDualLineChart(title, xLabel, xs, ys, null, null, null, null);
        }

        private static AControls.Border MakeDualLineChart(string title, string xLabel,
            List<double> xs1, List<double> ys1, string label1,
            List<double> xs2, List<double> ys2, string label2)
        {
            var canvas = new AControls.Canvas
            {
                Background = AMedia.Brushes.White,
                Height = 240
            };

            var border = new AControls.Border
            {
                BorderBrush = AMedia.Brushes.LightGray,
                BorderThickness = new global::Avalonia.Thickness(1),
                Margin = new global::Avalonia.Thickness(0, 4, 0, 8),
                Padding = new global::Avalonia.Thickness(2),
                Child = canvas,
                Height = 250
            };

            // Render reactively on size change (also fires once on first layout).
            canvas.SizeChanged += (sender, args) => DrawLineChart(canvas, title, xLabel,
                xs1, ys1, label1,
                xs2, ys2, label2);

            return border;
        }

        private static void DrawLineChart(AControls.Canvas canvas, string title, string xLabel,
            List<double> xs1, List<double> ys1, string label1,
            List<double> xs2, List<double> ys2, string label2)
        {
            canvas.Children.Clear();
            double w = canvas.Bounds.Width, h = canvas.Bounds.Height;
            if (w < 100 || h < 80 || xs1.Count == 0) return;

            const double leftPad = 60, rightPad = 80, topPad = 24, bottomPad = 36;
            double pw = w - leftPad - rightPad;
            double ph = h - topPad - bottomPad;
            if (pw <= 0 || ph <= 0) return;

            var allY = new List<double>(ys1);
            if (ys2 != null) allY.AddRange(ys2);
            var allX = new List<double>(xs1);
            if (xs2 != null) allX.AddRange(xs2);
            double xMin = allX.Min(), xMax = allX.Max();
            double yMin = allY.Min(), yMax = allY.Max();
            if (xMax == xMin) xMax = xMin + 1;
            if (yMax == yMin) yMax = yMin + 1;

            // Title
            AddCanvasText(canvas, title, leftPad, 4, pw, ALayout.HorizontalAlignment.Center,
                fontSize: 11, bold: true);

            // Axes
            AddCanvasLine(canvas, leftPad, topPad, leftPad, topPad + ph, AMedia.Brushes.Black);
            AddCanvasLine(canvas, leftPad, topPad + ph, leftPad + pw, topPad + ph, AMedia.Brushes.Black);

            // Y ticks (4)
            for (int t = 0; t <= 4; t++)
            {
                double frac = t / 4.0;
                double v = yMin + frac * (yMax - yMin);
                double y = topPad + ph - frac * ph;
                AddCanvasLine(canvas, leftPad - 4, y, leftPad, y, AMedia.Brushes.Gray);
                AddCanvasText(canvas, v.ToString("G4"), 2, y - 7, leftPad - 6,
                    ALayout.HorizontalAlignment.Right, fontSize: 9);
            }

            // X ticks (5)
            for (int t = 0; t <= 5; t++)
            {
                double frac = t / 5.0;
                double v = xMin + frac * (xMax - xMin);
                double x = leftPad + frac * pw;
                AddCanvasLine(canvas, x, topPad + ph, x, topPad + ph + 4, AMedia.Brushes.Gray);
                AddCanvasText(canvas, v.ToString("G4"), x - 25, topPad + ph + 6, 50,
                    ALayout.HorizontalAlignment.Center, fontSize: 9);
            }

            // X axis label
            AddCanvasText(canvas, xLabel, leftPad, topPad + ph + 20, pw,
                ALayout.HorizontalAlignment.Center, fontSize: 10);

            // Series 1
            DrawSeries(canvas, xs1, ys1, xMin, xMax, yMin, yMax, leftPad, topPad, pw, ph,
                AMedia.Brushes.SteelBlue);
            if (label1 != null)
                AddCanvasText(canvas, "■ " + label1, leftPad + pw + 6, topPad,
                    rightPad - 8, ALayout.HorizontalAlignment.Left, fontSize: 10,
                    color: AMedia.Color.FromRgb(70, 130, 180));

            // Series 2
            if (xs2 != null && ys2 != null && xs2.Count > 0)
            {
                DrawSeries(canvas, xs2, ys2, xMin, xMax, yMin, yMax, leftPad, topPad, pw, ph,
                    AMedia.Brushes.Crimson);
                if (label2 != null)
                    AddCanvasText(canvas, "■ " + label2, leftPad + pw + 6, topPad + 16,
                        rightPad - 8, ALayout.HorizontalAlignment.Left, fontSize: 10,
                        color: AMedia.Color.FromRgb(220, 20, 60));
            }
        }

        private static void DrawSeries(AControls.Canvas canvas, List<double> xs, List<double> ys,
            double xMin, double xMax, double yMin, double yMax,
            double leftPad, double topPad, double pw, double ph, AMedia.IBrush brush)
        {
            for (int i = 0; i < xs.Count - 1; i++)
            {
                double x1 = leftPad + (xs[i]     - xMin) / (xMax - xMin) * pw;
                double x2 = leftPad + (xs[i + 1] - xMin) / (xMax - xMin) * pw;
                double y1 = topPad + ph - (ys[i]     - yMin) / (yMax - yMin) * ph;
                double y2 = topPad + ph - (ys[i + 1] - yMin) / (yMax - yMin) * ph;
                AddCanvasLine(canvas, x1, y1, x2, y2, brush, thickness: 1.5);
            }
        }

        private static void AddCanvasLine(AControls.Canvas canvas, double x1, double y1,
            double x2, double y2, AMedia.IBrush brush, double thickness = 1)
        {
            var line = new AShapes.Line
            {
                StartPoint = new global::Avalonia.Point(x1, y1),
                EndPoint   = new global::Avalonia.Point(x2, y2),
                Stroke     = brush,
                StrokeThickness = thickness
            };
            canvas.Children.Add(line);
        }

        private static void AddCanvasText(AControls.Canvas canvas, string text, double x, double y,
            double width, ALayout.HorizontalAlignment align, double fontSize = 10,
            bool bold = false, AMedia.Color? color = null)
        {
            // the drawing-table canvas labels follow the persisted UI scaling factor
            fontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(fontSize);
            var tb = new AControls.TextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = bold ? AMedia.FontWeight.Bold : AMedia.FontWeight.Normal,
                Width = width,
                TextAlignment = align == ALayout.HorizontalAlignment.Right
                    ? AMedia.TextAlignment.Right
                    : align == ALayout.HorizontalAlignment.Center
                        ? AMedia.TextAlignment.Center
                        : AMedia.TextAlignment.Left
            };
            if (color.HasValue) tb.Foreground = new AMedia.SolidColorBrush(color.Value);
            AControls.Canvas.SetLeft(tb, x);
            AControls.Canvas.SetTop(tb, y);
            canvas.Children.Add(tb);
        }

        // -----------------------------------------------------------------
        // Appearance tab
        // -----------------------------------------------------------------

        public static AvaloniaEditorPanel BuildAppearance(ISimulationObject simobj)
        {
            var panel = new AvaloniaEditorPanel();

            // ShapeGraphic exposes Width/Height/Rotation/LineWidth/LineColor/OverrideColors/Flip.
            // We use reflection so this file doesn't take a hard dependency on the SkiaSharp
            // drawing project, which keeps the editor library lean and rebuild-friendly.
            var gobj = simobj.GraphicObject;
            if (gobj == null)
            {
                panel.CreateAndAddDescriptionRow("Object has no graphic representation.");
                return panel;
            }

            var t = gobj.GetType();

            // Dimensions ---------------------------------------------------
            panel.CreateAndAddLabelRow2("Dimensions");
            BindIntProperty(panel, t, gobj, "Width",  v => gobj.Width  = v, "Width (px)",  10, 2000);
            BindIntProperty(panel, t, gobj, "Height", v => gobj.Height = v, "Height (px)", 10, 2000);

            // Transform ----------------------------------------------------
            var rotationProp = t.GetProperty("Rotation");
            if (rotationProp != null && rotationProp.CanWrite)
            {
                panel.CreateAndAddLabelRow2("Transform");
                int rot = Convert.ToInt32(rotationProp.GetValue(gobj));
                panel.CreateAndAddNumericEditorRow("Rotation (deg)", rot, 0, 360, 0,
                    (sp, e) => rotationProp.SetValue(gobj, (int)sp.Value.GetValueOrDefault()));
            }
            BindBoolProperty(panel, t, gobj, "FlippedH", "Flip Horizontally");
            BindBoolProperty(panel, t, gobj, "FlippedV", "Flip Vertically");

            // Font ---------------------------------------------------------
            var fontSizeProp = t.GetProperty("FontSize");
            var fontStyleProp = t.GetProperty("FontStyle");
            if (fontSizeProp != null || fontStyleProp != null)
            {
                panel.CreateAndAddLabelRow2("Font");
                if (fontSizeProp != null && fontSizeProp.CanWrite)
                {
                    int sz = Convert.ToInt32(fontSizeProp.GetValue(gobj));
                    panel.CreateAndAddNumericEditorRow("Font Size", sz, 6, 72, 0,
                        (sp, e) => fontSizeProp.SetValue(gobj, (int)sp.Value.GetValueOrDefault()));
                }
                if (fontStyleProp != null && fontStyleProp.CanWrite)
                {
                    var enumType = fontStyleProp.PropertyType;
                    var names = Enum.GetNames(enumType).ToList();
                    int cur = Array.IndexOf(Enum.GetValues(enumType), fontStyleProp.GetValue(gobj));
                    panel.CreateAndAddDropDownRow("Font Style", names, Math.Max(0, cur),
                        (dd, e) => fontStyleProp.SetValue(gobj, Enum.Parse(enumType, names[dd.SelectedIndex])));
                }
            }

            // Border / fill ------------------------------------------------
            var lineWidthProp = t.GetProperty("LineWidth");
            var overrideColorsProp = t.GetProperty("OverrideColors");
            var lineColorProp = t.GetProperty("LineColor");
            if (lineWidthProp != null || lineColorProp != null)
            {
                panel.CreateAndAddLabelRow2("Border / Fill");
                if (lineWidthProp != null && lineWidthProp.CanWrite)
                {
                    int lw = Convert.ToInt32(lineWidthProp.GetValue(gobj));
                    panel.CreateAndAddNumericEditorRow("Border Width", lw, 1, 10, 0,
                        (sp, e) => lineWidthProp.SetValue(gobj, (int)sp.Value.GetValueOrDefault()));
                }
                if (overrideColorsProp != null && overrideColorsProp.CanWrite)
                {
                    bool ov = Convert.ToBoolean(overrideColorsProp.GetValue(gobj));
                    panel.CreateAndAddCheckBoxRow("Override Default Color", ov,
                        (cb, e) => overrideColorsProp.SetValue(gobj, cb.IsChecked.GetValueOrDefault()));
                }
                if (lineColorProp != null && lineColorProp.CanWrite)
                {
                    // LineColor is a SkiaSharp.SKColor; we round-trip through hex string so the
                    // bridge stays free of SkiaSharp references.
                    var raw = lineColorProp.GetValue(gobj);
                    var hex = raw?.ToString() ?? "#000000";
                    if (!global::Avalonia.Media.Color.TryParse(hex, out var parsed))
                        parsed = global::Avalonia.Media.Colors.Black;
                    panel.CreateAndAddColorPickerRow("Border Color", parsed,
                        (Action<global::Avalonia.Controls.ColorPicker, EventArgs>)((cp, e) =>
                        {
                            try
                            {
                                var skColorType = lineColorProp.PropertyType;
                                var parseMethod = skColorType.GetMethod("Parse", new[] { typeof(string) });
                                if (parseMethod != null)
                                {
                                    var asHex = cp.Color.ToString();
                                    var skColor = parseMethod.Invoke(null, new object[] { asHex });
                                    lineColorProp.SetValue(gobj, skColor);
                                }
                            }
                            catch { /* color parse failure shouldn't break the editor */ }
                        }));
                }
            }

            return panel;
        }

        private static void BindIntProperty(AvaloniaEditorPanel panel, Type t, object obj,
            string propName, Action<int> setter, string label, int min, int max)
        {
            var p = t.GetProperty(propName);
            if (p == null || !p.CanWrite) return;
            int cur = Convert.ToInt32(p.GetValue(obj));
            panel.CreateAndAddNumericEditorRow(label, cur, min, max, 0,
                (sp, e) => setter((int)sp.Value.GetValueOrDefault()));
        }

        private static void BindBoolProperty(AvaloniaEditorPanel panel, Type t, object obj,
            string propName, string label)
        {
            var p = t.GetProperty(propName);
            if (p == null || !p.CanWrite) return;
            bool cur = Convert.ToBoolean(p.GetValue(obj));
            panel.CreateAndAddCheckBoxRow(label, cur,
                (cb, e) => p.SetValue(obj, cb.IsChecked.GetValueOrDefault()));
        }
    }
}
