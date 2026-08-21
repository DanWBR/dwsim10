using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using SkiaSharp;
using Charts = DWSIM.Drawing.SkiaSharp.GraphicObjects.Charts;
using Shapes = DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes;
using Tables = DWSIM.Drawing.SkiaSharp.GraphicObjects.Tables;
using Text = DWSIM.Drawing.SkiaSharp.GraphicObjects;

namespace DWSIM.UI.Desktop.Editors
{
    /// <summary>
    /// Editors for the annotation objects: the property tables, the chart, the text blocks, the
    /// rectangle, the button and the embedded picture. These are graphics with no simulation object
    /// behind them, which is why they do not go through the factory's object lookup.
    /// </summary>
    public static class AnnotationEditors
    {

        /// <summary>Builds the editor for an annotation, or null if the object is not one.</summary>
        public static AvaloniaEditorPanel? Build(IGraphicObject obj, IFlowsheet flowsheet, Action? redraw)
        {
            var panel = new AvaloniaEditorPanel();

            switch (obj)
            {
                case Tables.TableGraphic table:
                    BuildPropertyTable(table, flowsheet, panel);
                    break;
                case Tables.MasterTableGraphic master:
                    BuildMasterTable(master, flowsheet, panel);
                    break;
                case Tables.SpreadsheetTableGraphic sheet:
                    BuildSpreadsheetTable(sheet, panel);
                    break;
                case Charts.OxyPlotGraphic chart:
                    BuildChart(chart, flowsheet, panel);
                    break;
                case Text.HTMLTextGraphic html:
                    BuildHtmlText(html, panel);
                    break;
                case Text.TextGraphic text:
                    BuildText(text, panel);
                    break;
                case Shapes.RectangleGraphic rect:
                    BuildRectangle(rect, panel);
                    break;
                case Shapes.ButtonGraphic button:
                    BuildButton(button, flowsheet, panel);
                    break;
                case Shapes.EmbeddedImageGraphic image:
                    BuildImage(image, panel);
                    break;
                default:
                    return null;
            }

            AddGeometryRows(obj, panel);

            // an annotation changes nothing in the process, so the after-edit hook only repaints
            if (redraw != null) panel.OnAfterEdit = redraw;

            return panel;
        }

        // ---------------------------------------------------------------------------------------

        private static void AddGeometryRows(IGraphicObject obj, AvaloniaEditorPanel panel)
        {
            panel.CreateAndAddEmptySpace();
            panel.CreateAndAddLabelRow("Position and Size");

            panel.CreateAndAddNumericEditorRow("X", obj.X, -100000, 100000, 0,
                (s, e) => obj.X = (int)(s.Value ?? 0));
            panel.CreateAndAddNumericEditorRow("Y", obj.Y, -100000, 100000, 0,
                (s, e) => obj.Y = (int)(s.Value ?? 0));
            panel.CreateAndAddNumericEditorRow("Width", obj.Width, 1, 100000, 0,
                (s, e) => obj.Width = (int)(s.Value ?? 1));
            panel.CreateAndAddNumericEditorRow("Height", obj.Height, 1, 100000, 0,
                (s, e) => obj.Height = (int)(s.Value ?? 1));
        }

        /// <summary>
        /// A scrolling list of checkboxes. The property lists run to a few hundred entries, so a
        /// filter box sits above it, as it does in the Windows dialogs.
        /// </summary>
        private static void AddCheckList(AvaloniaEditorPanel panel, int height,
            Func<IEnumerable<(string Key, string Label, bool Checked)>> source,
            Action<string, bool> onToggle)
        {
            var items = new StackPanel { Orientation = Orientation.Vertical };
            var scroller = new ScrollViewer
            {
                Height = height,
                Content = items,
                HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
            };

            void Repopulate(string filter)
            {
                items.Children.Clear();
                foreach (var entry in source())
                {
                    if (filter.Length > 0 &&
                        entry.Label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    var key = entry.Key;
                    var cb = new CheckBox { Content = entry.Label, IsChecked = entry.Checked };
                    cb.IsCheckedChanged += (_, _) =>
                    {
                        onToggle(key, cb.IsChecked.GetValueOrDefault());
                        panel.OnAfterEdit?.Invoke();
                    };
                    items.Children.Add(cb);
                }
            }

            panel.CreateAndAddStringEditorRow("Filter", "", (s, e) => Repopulate(s.Text ?? ""));
            panel.CreateAndAddControlRow(scroller);

            Repopulate("");

            // exposed so the object selector can rebuild the list when the selection changes
            panel.Tag = (Action<string>)Repopulate;
        }

        // ---------------------------------------------------------------------------------------

        private static void BuildPropertyTable(Tables.TableGraphic table, IFlowsheet flowsheet,
            AvaloniaEditorPanel panel)
        {
            panel.CreateAndAddLabelRow("Property Table");
            panel.CreateAndAddDescriptionRow(
                "Pick an object, then tick the properties it should show. More than one object can " +
                "be added to the same table.");

            panel.CreateAndAddStringEditorRow("Header", table.HeaderText,
                (s, e) => table.HeaderText = s.Text ?? "");

            panel.CreateAndAddDropDownRow("Sorting",
                Enum.GetNames(typeof(Tables.TableGraphic.SortMode)).ToList(),
                (int)table.SortingMode,
                (s, e) => table.SortingMode = (Tables.TableGraphic.SortMode)s.SelectedIndex);

            var objects = flowsheet.SimulationObjects.Values
                                   .OrderBy(o => o.GraphicObject.Tag).ToList();
            if (objects.Count == 0)
            {
                panel.CreateAndAddDescriptionRow("The flowsheet has no objects to show yet.");
                return;
            }

            var selected = objects[0];

            panel.CreateAndAddEmptySpace();
            panel.CreateAndAddLabelRow("Properties");

            var picker = panel.CreateAndAddDropDownRow("Object",
                objects.Select(o => o.GraphicObject.Tag).ToList(), 0, null);

            AddCheckList(panel, 260,
                () => selected.GetProperties(PropertyType.ALL)
                              .Select(p => (p, flowsheet.GetTranslatedString(p),
                                            table.VisibleProperties.ContainsKey(selected.Name) &&
                                            table.VisibleProperties[selected.Name].Contains(p))),
                (key, on) =>
                {
                    if (!table.VisibleProperties.ContainsKey(selected.Name))
                        table.VisibleProperties.Add(selected.Name, new List<string>());

                    var list = table.VisibleProperties[selected.Name];
                    if (on) { if (!list.Contains(key)) list.Add(key); }
                    else list.Remove(key);
                });

            var repopulate = (Action<string>)panel.Tag!;
            picker.SelectionChanged += (_, _) =>
            {
                if (picker.SelectedIndex < 0 || picker.SelectedIndex >= objects.Count) return;
                selected = objects[picker.SelectedIndex];
                repopulate("");
            };
        }

        // ---------------------------------------------------------------------------------------

        private static void BuildMasterTable(Tables.MasterTableGraphic table, IFlowsheet flowsheet,
            AvaloniaEditorPanel panel)
        {
            panel.CreateAndAddLabelRow("Master Property Table");
            panel.CreateAndAddDescriptionRow(
                "One table for every object of the same type: pick the type, then the objects and " +
                "the properties to show.");

            panel.CreateAndAddStringEditorRow("Header", table.HeaderText,
                (s, e) => table.HeaderText = s.Text ?? "");

            // only the types the flowsheet actually holds are worth offering
            var families = flowsheet.SimulationObjects.Values
                                    .Select(o => o.GraphicObject.ObjectType)
                                    .Distinct().OrderBy(t => t.ToString()).ToList();
            if (families.Count == 0)
            {
                panel.CreateAndAddDescriptionRow("The flowsheet has no objects to show yet.");
                return;
            }

            var familyIndex = Math.Max(0, families.IndexOf(table.ObjectFamily));
            table.ObjectFamily = families[familyIndex];

            panel.CreateAndAddNumericEditorRow("Columns per block", table.NumberOfLines, 1, 20, 0,
                (s, e) => table.NumberOfLines = (int)(s.Value ?? 1));

            var sortPicker = panel.CreateAndAddDropDownRow("Sort by",
                table.SortableItems.ToList(),
                Math.Max(0, Array.IndexOf(table.SortableItems, table.SortBy)),
                (s, e) => { if (s.SelectedItem is string v) table.SortBy = v; });

            var familyPicker = panel.CreateAndAddDropDownRow("Object type",
                families.Select(t => flowsheet.GetTranslatedString(t.ToString())).ToList(),
                familyIndex, null);

            panel.CreateAndAddEmptySpace();
            panel.CreateAndAddLabelRow("Objects");

            AddCheckList(panel, 150,
                () => flowsheet.SimulationObjects.Values
                               .Where(o => o.GraphicObject.ObjectType == table.ObjectFamily)
                               .OrderBy(o => o.GraphicObject.Tag)
                               .Select(o => (o.GraphicObject.Tag, o.GraphicObject.Tag,
                                             table.ObjectList.ContainsKey(o.GraphicObject.Tag) &&
                                             table.ObjectList[o.GraphicObject.Tag])),
                (key, on) => table.ObjectList[key] = on);

            var repopulateObjects = (Action<string>)panel.Tag!;

            panel.CreateAndAddEmptySpace();
            panel.CreateAndAddLabelRow("Properties");

            AddCheckList(panel, 220, () => MasterProperties(table, flowsheet),
                (key, on) => table.PropertyList[key] = on);

            var repopulateProperties = (Action<string>)panel.Tag!;

            familyPicker.SelectionChanged += (_, _) =>
            {
                if (familyPicker.SelectedIndex < 0 || familyPicker.SelectedIndex >= families.Count) return;

                // the setter clears both lists (only when the family really changes), which is what
                // makes switching type start clean
                table.ObjectFamily = families[familyPicker.SelectedIndex];
                repopulateObjects("");
                repopulateProperties("");

                sortPicker.Items.Clear();
                foreach (var item in table.SortableItems) sortPicker.Items.Add(item);
                sortPicker.SelectedIndex = 0;

                // repaint the canvas so the table reflects the new type immediately (this picker has no
                // per-control command, so nothing else fires the after-edit redraw)
                panel.OnAfterEdit?.Invoke();
            };
        }

        private static IEnumerable<(string, string, bool)> MasterProperties(
            Tables.MasterTableGraphic table, IFlowsheet flowsheet)
        {
            var first = flowsheet.SimulationObjects.Values
                                 .FirstOrDefault(o => o.GraphicObject.ObjectType == table.ObjectFamily);
            if (first == null) yield break;

            foreach (var p in first.GetProperties(PropertyType.ALL))
            {
                yield return (p, flowsheet.GetTranslatedString(p),
                              table.PropertyList.ContainsKey(p) && table.PropertyList[p]);
            }
        }

        // ---------------------------------------------------------------------------------------

        private static void BuildSpreadsheetTable(Tables.SpreadsheetTableGraphic table,
            AvaloniaEditorPanel panel)
        {
            panel.CreateAndAddLabelRow("Spreadsheet Table");
            panel.CreateAndAddDescriptionRow(
                "Shows a range of cells from the flowsheet spreadsheet, for instance A1:D10.");

            panel.CreateAndAddStringEditorRow("Cell range", table.SpreadsheetCellRange,
                (s, e) => table.SpreadsheetCellRange = s.Text ?? "");
        }

        // ---------------------------------------------------------------------------------------

        private static void BuildChart(Charts.OxyPlotGraphic chart, IFlowsheet flowsheet,
            AvaloniaEditorPanel panel)
        {
            panel.CreateAndAddLabelRow("Chart");
            panel.CreateAndAddDescriptionRow(
                "Shows a chart that belongs to an object, to the dynamics integrators, or to the " +
                "flowsheet's own chart list.");

            const string integrators = "Dynamic Mode Integrators";
            const string charts = "Chart Objects";

            var owners = new List<string> { integrators, charts };
            owners.AddRange(flowsheet.SimulationObjects.Values.Select(o => o.GraphicObject.Tag));

            var current = chart.OwnerID;
            var ownerIndex = 0;
            if (current == integrators) ownerIndex = 0;
            else if (current == charts) ownerIndex = 1;
            else if (!string.IsNullOrEmpty(current) && flowsheet.SimulationObjects.ContainsKey(current))
                ownerIndex = owners.IndexOf(flowsheet.SimulationObjects[current].GraphicObject.Tag);
            if (ownerIndex < 0) ownerIndex = 0;

            var modelPicker = panel.CreateAndAddDropDownRow("Chart", new List<string>(), -1,
                (s, e) => { if (s.SelectedItem is string v) chart.ModelName = v; });

            var ownerPicker = panel.CreateAndAddDropDownRow("Source", owners, ownerIndex, null);

            void RefreshModels()
            {
                modelPicker.Items.Clear();

                var index = ownerPicker.SelectedIndex;
                IEnumerable<string> names;

                if (index == 0)
                {
                    chart.OwnerID = integrators;
                    names = flowsheet.DynamicsManager.IntegratorList.Values.Select(i => i.Description);
                }
                else if (index == 1)
                {
                    chart.OwnerID = charts;
                    names = flowsheet.Charts.Values.Select(c => c.DisplayName);
                }
                else
                {
                    var obj = flowsheet.SimulationObjects.Values
                                       .FirstOrDefault(o => o.GraphicObject.Tag == owners[index]);
                    if (obj == null) return;
                    chart.OwnerID = obj.Name;
                    names = obj.GetChartModelNames();
                }

                foreach (var n in names) modelPicker.Items.Add(n);
                modelPicker.SelectedItem = chart.ModelName;
                if (modelPicker.SelectedIndex < 0 && modelPicker.Items.Count > 0)
                    modelPicker.SelectedIndex = 0;
            }

            ownerPicker.SelectionChanged += (_, _) => RefreshModels();
            RefreshModels();
        }

        // ---------------------------------------------------------------------------------------

        private static void BuildText(Text.TextGraphic text, AvaloniaEditorPanel panel)
        {
            panel.CreateAndAddLabelRow("Text Block");

            panel.CreateAndAddMultilineTextBoxRow(text.Text, false, false,
                (s, e) => text.Text = s.Text ?? "");

            panel.CreateAndAddNumericEditorRow("Font size", text.Size, 4, 200, 1,
                (s, e) => text.Size = (double)(s.Value ?? 14));

            panel.CreateAndAddColorPickerRow("Colour", ToAvalonia(text.Color),
                (s, e) => text.Color = ToSkia(s.Color));
        }

        private static void BuildHtmlText(Text.HTMLTextGraphic text, AvaloniaEditorPanel panel)
        {
            panel.CreateAndAddLabelRow("HTML Text Block");
            panel.CreateAndAddDescriptionRow("A subset of HTML is rendered on the flowsheet.");

            panel.CreateAndAddMultilineMonoSpaceTextBoxRow(text.Text, 180, false,
                (s, e) => text.Text = s.Text ?? "");

            panel.CreateAndAddNumericEditorRow("Font size", text.Size, 4, 200, 1,
                (s, e) => text.Size = (double)(s.Value ?? 14));

            panel.CreateAndAddColorPickerRow("Colour", ToAvalonia(text.Color),
                (s, e) => text.Color = ToSkia(s.Color));
        }

        private static void BuildRectangle(Shapes.RectangleGraphic rect, AvaloniaEditorPanel panel)
        {
            panel.CreateAndAddLabelRow("Rectangle");

            panel.CreateAndAddStringEditorRow("Text", rect.Text,
                (s, e) => rect.Text = s.Text ?? "");

            panel.CreateAndAddColorPickerRow("Text colour", ToAvalonia(rect.FontColor),
                (s, e) => rect.FontColor = ToSkia(s.Color));

            panel.CreateAndAddColorPickerRow("Fill colour", ToAvalonia(rect.Fill ? rect.FillColor : SKColors.Transparent),
                (s, e) => { rect.FillColor = ToSkia(s.Color); rect.Fill = true; });

            panel.CreateAndAddCheckBoxRow("Rounded corners", rect.RoundEdges,
                (s, e) => rect.RoundEdges = s.IsChecked.GetValueOrDefault());

            panel.CreateAndAddNumericEditorRow("Opacity (%)", rect.Opacity, 0, 100, 0,
                (s, e) => rect.Opacity = (int)(s.Value ?? 100));
        }

        private static void BuildButton(Shapes.ButtonGraphic button, IFlowsheet flowsheet,
            AvaloniaEditorPanel panel)
        {
            panel.CreateAndAddLabelRow("Button");
            panel.CreateAndAddDescriptionRow("Runs a script from the Script Manager when clicked.");

            panel.CreateAndAddStringEditorRow("Text", button.Text,
                (s, e) => button.Text = s.Text ?? "");

            var scripts = flowsheet.Scripts.Values.Select(x => x.Title).ToList();
            if (scripts.Count == 0)
            {
                panel.CreateAndAddDescriptionRow("There are no scripts in this simulation yet.");
                return;
            }

            panel.CreateAndAddDropDownRow("Script", scripts,
                Math.Max(0, scripts.IndexOf(button.SelectedScript)),
                (s, e) => { if (s.SelectedItem is string v) button.SelectedScript = v; });
        }

        private static void BuildImage(Shapes.EmbeddedImageGraphic image, AvaloniaEditorPanel panel)
        {
            panel.CreateAndAddLabelRow("Image");
            panel.CreateAndAddDescriptionRow(
                image.Image == null
                    ? "This object has no picture yet."
                    : $"Embedded picture, {image.Image.Width} by {image.Image.Height} pixels.");
        }

        // ---------------------------------------------------------------------------------------

        private static global::Avalonia.Media.Color ToAvalonia(SKColor c)
            => global::Avalonia.Media.Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue);

        private static SKColor ToSkia(global::Avalonia.Media.Color c)
            => new SKColor(c.R, c.G, c.B, c.A);
    }
}
