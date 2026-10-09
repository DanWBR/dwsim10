using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.Pipe;
using Thickness = Avalonia.Thickness;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;
using Pipe = DWSIM.UnitOperations.UnitOperations.Pipe;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Pipe segment editor, following the Windows EditingForm_Pipe: the General tab (calculation
    /// mode, pressure drop correlation, tolerances) and the results, then the Hydraulic Profile
    /// (the section grid, one column per section: type or fitting, quantity, increments,
    /// material, roughness and wall conductivity of user-defined materials, length, elevation
    /// and diameters, with the standard pipe sizes and the elevation chart) and the Thermal
    /// Profile (defined HTC, defined heat exchange or calculated HTC, each with its inputs).
    /// </summary>
    public static class PipeEditor
    {

        private static readonly string[] Modes =
        {
            "Specify Length/Hydraulic Profile (Default)",
            "Specify Outlet Pressure",
            "Specify Outlet Temperature"
        };

        private static readonly string[] FlowPackages =
        {
            "Beggs & Brill",
            "Lockhart & Martinelli",
            "Petalas & Aziz",
            "Weymouth (gas)",
            "Panhandle A (gas)",
            "Panhandle B (gas)"
        };

        private static readonly string[] SlurryViscosity = { "Disabled", "Yoshida et al" };

        public static Control Build(Pipe pipe)
        {
            if (pipe.Profile == null) pipe.Profile = new PipeProfile();
            if (pipe.ThermalProfile == null) pipe.ThermalProfile = new ThermalEditorDefinitions();

            return UnitOpEditor.Build(pipe,
                input: panel => BuildParameters(pipe, panel),
                results: panel => BuildResults(pipe, panel),
                extras: new[]
                {
                    ("Hydraulic Profile", new HydraulicProfileEditor(pipe).Build()),
                    ("Thermal Profile", BuildThermalProfile(pipe))
                });
        }

        private static void BuildParameters(Pipe pipe, AvaloniaEditorPanel panel)
        {
            var nf = pipe.GetFlowsheet().FlowsheetOptions.NumberFormat;

            UnitOpEditorRows.ValueRow outletT = null, outletP = null;

            void ApplyMode()
            {
                if (outletT != null) outletT.IsEnabled = pipe.Specification == Pipe.Specmode.OutletTemperature;
                if (outletP != null) outletP.IsEnabled = pipe.Specification == Pipe.Specmode.OutletPressure;
            }

            panel.CreateAndAddDropDownRow("Calculation mode", new List<string>(Modes),
                (int)pipe.Specification, (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    pipe.Specification = (Pipe.Specmode)dd.SelectedIndex;
                    ApplyMode();
                    panel.OnAfterEdit?.Invoke();
                });

            outletT = panel.CreateAndAddValueUnitRow(pipe, "Outlet temperature (spec)",
                UnitOfMeasure.temperature, pipe.OutletTemperature, v => pipe.OutletTemperature = v);

            outletP = panel.CreateAndAddValueUnitRow(pipe, "Outlet pressure (spec)",
                UnitOfMeasure.pressure, pipe.OutletPressure, v => pipe.OutletPressure = v);

            panel.CreateAndAddDropDownRow("Pressure drop correlation", new List<string>(FlowPackages),
                (int)pipe.SelectedFlowPackage, (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    pipe.SelectedFlowPackage = (DWSIM.UnitOperations.UnitOperations.FlowPackage)dd.SelectedIndex;
                    panel.OnAfterEdit?.Invoke();
                });

            // Efficiency factor used only by the Weymouth / Panhandle gas pipeline equations.
            panel.CreateAndAddTextBoxRow(nf, "Pipeline efficiency (gas equations, 0-1)",
                pipe.PipelineEfficiency,
                (tb, e) =>
                {
                    if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pipe.PipelineEfficiency = v;
                });

            panel.CreateAndAddValueUnitRow(pipe, "Temp. error tolerance", UnitOfMeasure.deltaT,
                pipe.TolT, v => pipe.TolT = v);

            panel.CreateAndAddValueUnitRow(pipe, "Pressure error tolerance", UnitOfMeasure.deltaP,
                pipe.TolP, v => pipe.TolP = v);

            panel.CreateAndAddCheckBoxRow("Calculate equilibria along the pipe",
                pipe.CalculateEquilibrium,
                (cb, e) => pipe.CalculateEquilibrium = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddTextBoxRow(nf, "Calculate equilibria at each X sections",
                pipe.CalculateEquilibriumIntervalInSteps,
                (tb, e) =>
                {
                    if (UnitOpEditorRows.TryParse(tb.Text, out var v))
                        pipe.CalculateEquilibriumIntervalInSteps = (int)v;
                });

            panel.CreateAndAddCheckBoxRow("Calculate thermal balance with surroundings",
                pipe.CalculateHeatBalance,
                (cb, e) => pipe.CalculateHeatBalance = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddCheckBoxRow("Include emulsion effect", pipe.IncludeEmulsion,
                (cb, e) => pipe.IncludeEmulsion = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddDropDownRow("Slurry viscosity calculation", new List<string>(SlurryViscosity),
                pipe.SlurryViscosityMode, (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    pipe.SlurryViscosityMode = dd.SelectedIndex;
                    panel.OnAfterEdit?.Invoke();
                });

            panel.CreateAndAddCheckBoxRow("Use Global weather conditions", pipe.UseGlobalWeather,
                (cb, e) => pipe.UseGlobalWeather = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddDescriptionRow(
                "If checked, DWSIM will use Flowsheet-defined weather conditions for ambient " +
                "temperature, pressure and air (wind) speed.");

            ApplyMode();
        }

        private static void BuildResults(Pipe pipe, AvaloniaEditorPanel panel)
        {
            panel.CreateAndAddResultRow(pipe, "Pressure Difference", UnitOfMeasure.deltaP,
                pipe.DeltaP.GetValueOrDefault());
            panel.CreateAndAddResultRow(pipe, "Temperature Difference", UnitOfMeasure.deltaT,
                pipe.DeltaT.GetValueOrDefault());
            panel.CreateAndAddResultRow(pipe, "Heat Load", UnitOfMeasure.heatflow,
                pipe.DeltaQ.GetValueOrDefault());
        }

        // ---------------------------------------------------------------------
        // Hydraulic Profile
        // ---------------------------------------------------------------------

        /// <summary>
        /// The section grid of the Windows PipeHydraulicProfileEditor: one column per section, one
        /// row per field. Every cell writes to its section as it is committed; adding, inserting
        /// or removing a section renumbers the profile and redraws the grid.
        /// </summary>
        private sealed class HydraulicProfileEditor
        {

            /// <summary>The value the Windows editor stores for a straight tube.</summary>
            private const string StraightTube = "Tubulaosimples";

            /// <summary>The fitting whose "internal diameter" holds a fixed pressure drop.</summary>
            private const string FixedDeltaPTag = "[27]";

            private const double ColumnWidth = 150;

            private readonly Pipe pipe;
            private readonly PipeProfile profile;
            private readonly IFlowsheet flowsheet;
            private readonly IUnitsOfMeasure su;
            private readonly string nf;

            private readonly List<string> fittings = new List<string>();
            private readonly List<Material> materials;
            private readonly List<(string Nominal, string Item, double DE, double DI)> standardSizes;

            private AvaloniaEditorPanel panel;
            private Border gridHost;
            private TextBlock status;
            private XYPlot chart;

            public HydraulicProfileEditor(Pipe pipe)
            {
                this.pipe = pipe;
                profile = pipe.Profile;
                flowsheet = pipe.GetFlowsheet();
                su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
                nf = flowsheet.FlowsheetOptions.NumberFormat;

                materials = Material.List(flowsheet);
                standardSizes = StandardSizes();
                LoadFittings();
            }

            /// <summary>
            /// The fitting names from the fittings.dat resource of DWSIM.UnitOperations, listed
            /// after the straight tube. The file is ANSI (the threaded elbow carries a degree
            /// sign), so it is read as Latin-1, as the Windows editor reads it with the default
            /// code page.
            /// </summary>
            private void LoadFittings()
            {
                try
                {
                    var asm = typeof(PipeSection).Assembly;
                    using (var stream = asm.GetManifestResourceStream("DWSIM.UnitOperations.fittings.dat"))
                    {
                        if (stream == null)
                        {
                            flowsheet.ShowMessage(
                                "Pipe editor: embedded resource 'fittings.dat' not found. Only straight-tube sections will be selectable.",
                                IFlowsheet.MessageType.Warning);
                            return;
                        }
                        using (var reader = new StreamReader(stream, Encoding.GetEncoding(28591)))
                        {
                            while (!reader.EndOfStream)
                            {
                                var line = reader.ReadLine();
                                if (string.IsNullOrWhiteSpace(line)) continue;
                                fittings.Add(line.Split(';')[0]);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    flowsheet.ShowMessage("Pipe editor: failed to read fittings.dat - " + ex.Message,
                        IFlowsheet.MessageType.GeneralError);
                }
            }

            /// <summary>
            /// The standard sizes, labelled as the Windows default diameter combo stores them:
            /// "XS / 80 / 80S (2.375 OD / 1.939 ID)".
            /// </summary>
            private static List<(string Nominal, string Item, double DE, double DI)> StandardSizes()
            {
                var list = new List<(string Nominal, string Item, double DE, double DI)>();
                try
                {
                    foreach (var group in Pipe.GetStandardPipeSizes())
                    {
                        foreach (var size in group.Value)
                        {
                            var item = size.StandardSizeDescription.Replace("/", " / ") + " (" +
                                       size.ExternalDiameter_Inches.ToString(CultureInfo.InvariantCulture) + " OD / " +
                                       size.InternalDiameter_Inches.ToString(CultureInfo.InvariantCulture) + " ID)";
                            list.Add((group.Key, item, size.ExternalDiameter_Inches, size.InternalDiameter_Inches));
                        }
                    }
                }
                catch (Exception) { }
                return list;
            }

            public Control Build()
            {
                panel = new AvaloniaEditorPanel().AutoSolveOnEdit(pipe);

                // defaults for the sections added from here on, as the Windows editor offers them
                var defaultMaterial = panel.CreateAndAddDropDownRow("Default material",
                    materials.Select(m => m.Display).ToList(),
                    Material.IndexOf(materials, profile.DefaultMaterial), null);
                defaultMaterial.SelectionChanged += (s, e) =>
                {
                    var i = defaultMaterial.SelectedIndex;
                    if (i < 0 || i == Material.IndexOf(materials, profile.DefaultMaterial)) return;
                    profile.DefaultMaterial = materials[i].Stored;
                };

                var defaultDiameter = panel.CreateAndAddDropDownRow("Default diameter",
                    standardSizes.Select(x => x.Nominal + ": " + x.Item).ToList(),
                    standardSizes.FindIndex(x => x.Item == (profile.DefaultDiameter as string)), null);
                defaultDiameter.SelectionChanged += (s, e) =>
                {
                    if (defaultDiameter.SelectedIndex < 0) return;
                    profile.DefaultDiameter = standardSizes[defaultDiameter.SelectedIndex].Item;
                };

                var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                actions.Children.Add(ActionButton("Add Section", "Appends a section at the end of the pipe.",
                    () => Restructure(list => list.Add(NewSection()))));
                actions.Children.Add(ActionButton("Remove All Sections", "Removes every section of the pipe.",
                    () => Restructure(list => list.Clear())));
                panel.CreateAndAddControlRow(actions);

                gridHost = new Border();
                panel.CreateAndAddControlRow(new ScrollViewer
                {
                    Content = gridHost,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
                });

                status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };
                panel.CreateAndAddControlRow(status);

                panel.CreateAndAddDescriptionRow(
                    "Fittings use the quantity, the material and the internal diameter only. For the " +
                    "Fixed Delta-P fitting, enter the pressure drop (" + su.deltaP + ") in the internal " +
                    "diameter field. Roughness and wall thermal conductivity are entered for User " +
                    "Defined materials; the conductivity is an expression of T (K).");

                chart = new XYPlot { Height = 220 };
                panel.CreateAndAddControlRow(chart);

                RebuildGrid();
                ShowStatus(false);
                UpdateChart();

                return panel;
            }

            private static Button ActionButton(string text, string tip, Action click)
            {
                var b = new Button { Content = text };
                b.Classes.Add("panel");
                ToolTip.SetTip(b, tip);
                b.Click += (s, e) => click();
                return b;
            }

            private static bool IsStraight(PipeSection sec)
            {
                var t = sec.TipoSegmento ?? "";
                return t == "" || t == StraightTube || t == "Straight Tube Section" ||
                       t == "Straight Tube" || t == "Tubulação Simples";
            }

            private static bool IsFixedDeltaP(PipeSection sec) =>
                (sec.TipoSegmento ?? "").Contains(FixedDeltaPTag);

            /// <summary>The position of a section type in the type list: 0 for the straight
            /// tube, otherwise the fitting, matched by name or by its [n] index, which is what
            /// the calculation reads.</summary>
            private int TypeIndex(PipeSection sec)
            {
                if (IsStraight(sec)) return 0;
                var t = sec.TipoSegmento ?? "";
                var i = fittings.IndexOf(t);
                if (i >= 0) return i + 1;
                var tag = Regex.Match(t, @"\[\d+\]");
                if (tag.Success)
                {
                    i = fittings.FindIndex(f => f.Contains(tag.Value));
                    if (i >= 0) return i + 1;
                }
                return 0;
            }

            private PipeSection NewSection()
            {
                double de = 2.375, di = 1.939;
                var m = Regex.Match(profile.DefaultDiameter as string ?? "",
                    @"\(\s*([0-9.]+)\s*OD\s*/\s*([0-9.]+)\s*ID\s*\)");
                if (m.Success &&
                    double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mde) &&
                    double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mdi))
                {
                    de = mde;
                    di = mdi;
                }

                return new PipeSection
                {
                    TipoSegmento = StraightTube,
                    Quantidade = 1,
                    Incrementos = 5,
                    Material = string.IsNullOrEmpty(profile.DefaultMaterial) ? materials[1].Stored : profile.DefaultMaterial,
                    Comprimento = 1.0,
                    Elevacao = 0.0,
                    DE = de,
                    DI = di
                };
            }

            /// <summary>
            /// Applies a change to the ordered list of sections and writes it back numbered from 1,
            /// the keys the calculation walks.
            /// </summary>
            private void Restructure(Action<List<PipeSection>> change)
            {
                flowsheet.RegisterSnapshot(SnapshotType.ObjectData, pipe);

                var list = profile.Sections.Values.ToList();
                change(list);

                profile.Sections.Clear();
                for (int i = 0; i < list.Count; i++)
                {
                    list[i].Indice = i + 1;
                    profile.Sections.Add(i + 1, list[i]);
                }

                RebuildGrid();
                ShowStatus(true);
                UpdateChart();
                panel.OnAfterEdit?.Invoke();
            }

            /// <summary>Commits an edit of a section and solves the pipe.</summary>
            private void Edit(Action change, bool redraw)
            {
                flowsheet.RegisterSnapshot(SnapshotType.ObjectData, pipe);
                change();
                ShowStatus(true);
                UpdateChart();
                if (redraw) Dispatcher.UIThread.Post(RebuildGrid);
                panel.OnAfterEdit?.Invoke();
            }

            // the grid ----------------------------------------------------------

            private string[] RowLabels() => new[]
            {
                "Section",
                "Type",
                "Quantity",
                "Increments",
                "Material",
                "Roughness (" + su.distance + ")",
                "Wall th. cond. (" + su.thermalConductivity + ")",
                "Length (" + su.distance + ")",
                "Elevation (" + su.distance + ")",
                "External diameter (" + su.diameter + ")",
                "Internal diameter (" + su.diameter + ")",
                "Standard size",
                ""
            };

            private void RebuildGrid()
            {
                var labels = RowLabels();
                var sections = profile.Sections.Values.ToList();

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                foreach (var _ in sections)
                    grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(ColumnWidth)));
                foreach (var _ in labels)
                    grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                for (int r = 0; r < labels.Length; r++)
                {
                    Place(grid, new TextBlock
                    {
                        Text = labels[r],
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 2, 8, 2)
                    }, r, 0);
                }

                for (int c = 0; c < sections.Count; c++)
                    AddColumn(grid, sections[c], c);

                if (sections.Count == 0)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                    Place(grid, new TextBlock
                    {
                        Text = "No sections. Use Add Section to define the pipe.",
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 0, 0)
                    }, 1, 1);
                }

                gridHost.Child = grid;
            }

            private static void Place(Grid grid, Control control, int row, int column)
            {
                Grid.SetRow(control, row);
                Grid.SetColumn(control, column);
                grid.Children.Add(control);
            }

            private void AddColumn(Grid grid, PipeSection sec, int c)
            {
                var col = c + 1;
                var straight = IsStraight(sec);
                var fixedDp = IsFixedDeltaP(sec);
                var matIndex = Material.IndexOf(materials, sec.Material);
                var userDefined = materials[matIndex].IsUserDefined;

                Place(grid, new TextBlock
                {
                    Text = sec.Indice.ToString(CultureInfo.InvariantCulture),
                    FontWeight = FontWeight.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }, 0, col);

                // type
                var types = new List<string> { "Straight Tube Section" };
                types.AddRange(fittings);
                var type = Combo(types, TypeIndex(sec));
                type.SelectionChanged += (s, e) =>
                {
                    var i = type.SelectedIndex;
                    if (i < 0 || i == TypeIndex(sec)) return;
                    Edit(() => sec.TipoSegmento = i == 0 ? StraightTube : fittings[i - 1], true);
                };
                Place(grid, type, 1, col);

                // quantity and increments
                Place(grid, Cell(sec.Quantidade.ToString(CultureInfo.CurrentCulture), true, text =>
                {
                    if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var v)) return false;
                    Edit(() => sec.Quantidade = v, false);
                    return true;
                }), 2, col);

                Place(grid, Cell(sec.Incrementos.ToString(CultureInfo.CurrentCulture), straight, text =>
                {
                    if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var v)) return false;
                    Edit(() => sec.Incrementos = v, false);
                    return true;
                }), 3, col);

                // material, with the roughness and wall conductivity it implies
                var material = Combo(materials.Select(m => m.Display).ToList(), matIndex);
                material.SelectionChanged += (s, e) =>
                {
                    var i = material.SelectedIndex;
                    if (i < 0 || i == Material.IndexOf(materials, sec.Material)) return;
                    Edit(() =>
                    {
                        // a user-defined wall starts from the material it replaces, so the
                        // calculation has a roughness and a conductivity to work with
                        if (materials[i].IsUserDefined && !materials[Material.IndexOf(materials, sec.Material)].IsUserDefined)
                        {
                            if (!(sec.PipeWallRugosity > 0.0)) sec.PipeWallRugosity = Rugosity(sec);
                            if (string.IsNullOrWhiteSpace(sec.PipeWallThermalConductivityExpression))
                            {
                                try
                                {
                                    sec.PipeWallThermalConductivityExpression = cv.ConvertFromSI(su.thermalConductivity,
                                        pipe.k_parede(sec.Material, 298.15, sec)).ToString("G6", CultureInfo.InvariantCulture);
                                }
                                catch (Exception) { }
                            }
                        }
                        sec.Material = materials[i].Stored;
                    }, true);
                };
                Place(grid, material, 4, col);

                var roughness = userDefined ? sec.PipeWallRugosity : Rugosity(sec);
                Place(grid, Cell(Format(cv.ConvertFromSI(su.distance, roughness)), userDefined, text =>
                {
                    if (!UnitOpEditorRows.TryParse(text, out var v)) return false;
                    Edit(() => sec.PipeWallRugosity = cv.ConvertToSI(su.distance, v), false);
                    return true;
                }), 5, col);

                var conductivity = Cell(userDefined ? (sec.PipeWallThermalConductivityExpression ?? "") : "T-dependent",
                    userDefined, text =>
                    {
                        Edit(() => sec.PipeWallThermalConductivityExpression = text, false);
                        return true;
                    });
                conductivity.TextAlignment = TextAlignment.Left;
                ToolTip.SetTip(conductivity, "Expression of T (K), in " + su.thermalConductivity);
                Place(grid, conductivity, 6, col);

                // geometry: a fitting takes no length, elevation or external diameter
                Place(grid, Cell(Format(cv.ConvertFromSI(su.distance, sec.Comprimento)), straight, text =>
                {
                    if (!UnitOpEditorRows.TryParse(text, out var v)) return false;
                    Edit(() => sec.Comprimento = cv.ConvertToSI(su.distance, v), false);
                    return true;
                }), 7, col);

                Place(grid, Cell(Format(cv.ConvertFromSI(su.distance, sec.Elevacao)), straight, text =>
                {
                    if (!UnitOpEditorRows.TryParse(text, out var v)) return false;
                    Edit(() => sec.Elevacao = cv.ConvertToSI(su.distance, v), false);
                    return true;
                }), 8, col);

                Place(grid, Cell(Format(cv.Convert("in", su.diameter, sec.DE)), straight, text =>
                {
                    if (!UnitOpEditorRows.TryParse(text, out var v)) return false;
                    Edit(() => sec.DE = cv.Convert(su.diameter, "in", v), false);
                    return true;
                }), 9, col);

                // the Fixed Delta-P fitting keeps its pressure drop (SI) where the others keep
                // the internal diameter, as the Windows editor stores it
                var di = Cell(Format(fixedDp ? cv.ConvertFromSI(su.deltaP, sec.DI) : cv.Convert("in", su.diameter, sec.DI)),
                    true, text =>
                    {
                        if (!UnitOpEditorRows.TryParse(text, out var v)) return false;
                        Edit(() => sec.DI = fixedDp ? cv.ConvertToSI(su.deltaP, v) : cv.Convert(su.diameter, "in", v), false);
                        return true;
                    });
                if (fixedDp) ToolTip.SetTip(di, "Pressure drop (" + su.deltaP + ")");
                Place(grid, di, 10, col);

                // standard sizes, grouped by nominal diameter
                var sizes = new Button
                {
                    Content = "Select...",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    IsEnabled = !fixedDp,
                    Margin = new Thickness(2)
                };
                sizes.Classes.Add("panel");
                var menu = new MenuFlyout();
                foreach (var group in standardSizes.GroupBy(x => x.Nominal))
                {
                    var parent = new MenuItem { Header = group.Key };
                    foreach (var size in group)
                    {
                        var item = new MenuItem { Header = size.Item };
                        var chosen = size;
                        item.Click += (s, e) => Edit(() =>
                        {
                            if (IsStraight(sec)) sec.DE = chosen.DE;
                            sec.DI = chosen.DI;
                        }, true);
                        parent.Items.Add(item);
                    }
                    menu.Items.Add(parent);
                }
                sizes.Flyout = menu;
                Place(grid, sizes, 11, col);

                // insert after / remove, as the Windows toolbar does on the current column
                var buttons = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };
                buttons.Children.Add(ActionButton("Insert", "Inserts a new section after this one.",
                    () => Restructure(list => list.Insert(c + 1, NewSection()))));
                buttons.Children.Add(ActionButton("Remove", "Removes this section.",
                    () => Restructure(list => list.RemoveAt(c))));
                foreach (var b in buttons.Children.OfType<Button>())
                {
                    b.HorizontalAlignment = HorizontalAlignment.Stretch;
                    b.HorizontalContentAlignment = HorizontalAlignment.Center;
                    b.Margin = new Thickness(2, 0, 2, 0);
                }
                Place(grid, buttons, 12, col);
            }

            private double Rugosity(PipeSection sec)
            {
                try { return pipe.GetRugosity(sec.Material, sec); }
                catch (Exception) { return sec.PipeWallRugosity; }
            }

            private string Format(double value) => value.ToString(nf, CultureInfo.CurrentCulture);

            private static ComboBox Combo(List<string> options, int selected)
            {
                var cb = new ComboBox
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(2)
                };
                cb.SetOptions(options);
                if (selected >= 0 && selected < options.Count)
                {
                    cb.SelectedIndex = selected;
                    ToolTip.SetTip(cb, options[selected]);
                }
                return cb;
            }

            /// <summary>
            /// A grid cell that commits on Enter or when it loses focus, red while the text does
            /// not parse. Disabled cells show the value the calculation uses.
            /// </summary>
            private static TextBox Cell(string text, bool enabled, Func<string, bool> commit)
            {
                var tb = new TextBox
                {
                    Text = text,
                    IsEnabled = enabled,
                    TextAlignment = TextAlignment.Right,
                    Margin = new Thickness(2)
                };

                var committed = text;

                void Commit()
                {
                    var t = tb.Text ?? "";
                    if (t == committed) return;
                    if (commit(t))
                    {
                        committed = t;
                        tb.ClearValue(TextBox.ForegroundProperty);
                    }
                    else
                    {
                        tb.Foreground = Brushes.Red;
                    }
                }

                tb.KeyDown += (s, e) =>
                {
                    if (e.Key == global::Avalonia.Input.Key.Enter) { Commit(); e.Handled = true; }
                    else if (e.Key == global::Avalonia.Input.Key.Escape)
                    {
                        tb.Text = committed;
                        tb.ClearValue(TextBox.ForegroundProperty);
                        e.Handled = true;
                    }
                };
                tb.LostFocus += (s, e) => Commit();
                return tb;
            }

            // status and chart --------------------------------------------------

            /// <summary>
            /// What the Windows editor checks before it accepts the grid. The profile status is
            /// set only after an edit, so opening the editor never invalidates a loaded profile.
            /// </summary>
            private string Validate()
            {
                if (profile.Sections.Count == 0) return "No sections defined.";

                foreach (var sec in profile.Sections.Values)
                {
                    var name = "Section " + sec.Indice + ": ";
                    if (sec.Quantidade <= 0) return name + "invalid quantity.";
                    if (IsStraight(sec))
                    {
                        if (sec.Incrementos <= 0) return name + "invalid number of increments.";
                        if (!(sec.Comprimento > 0.0)) return name + "invalid length.";
                        if (Math.Abs(sec.Elevacao) > Math.Abs(sec.Comprimento)) return name + "the elevation is larger than the length.";
                        if (!(sec.DE > 0.0)) return name + "invalid external diameter.";
                        if (!(sec.DI > 0.0) || sec.DI > sec.DE) return name + "invalid internal diameter.";
                    }
                    else if (!(sec.DI > 0.0))
                    {
                        return name + (IsFixedDeltaP(sec) ? "invalid pressure drop." : "invalid internal diameter.");
                    }
                }
                return null;
            }

            private void ShowStatus(bool setProfileStatus)
            {
                var error = Validate();
                if (setProfileStatus)
                    profile.Status = error == null ? PipeEditorStatus.OK : PipeEditorStatus.Definir;

                if (error == null)
                {
                    var length = profile.Sections.Values.Where(IsStraight).Sum(s => s.Comprimento * s.Quantidade);
                    status.Text = "Profile OK: " + profile.Sections.Count + " section(s), total length " +
                                  Format(cv.ConvertFromSI(su.distance, length)) + " " + su.distance + ".";
                    status.Foreground = Brushes.Green;
                }
                else
                {
                    status.Text = error;
                    status.Foreground = Brushes.Red;
                }
            }

            /// <summary>Elevation against length along the pipe, as the Windows chart draws it.</summary>
            private void UpdateChart()
            {
                var x = new List<double> { 0.0 };
                var y = new List<double> { 0.0 };
                foreach (var sec in profile.Sections.Values)
                {
                    var straight = IsStraight(sec);
                    for (int q = 0; q < Math.Max(1, sec.Quantidade); q++)
                    {
                        x.Add(x[x.Count - 1] + (straight ? cv.ConvertFromSI(su.distance, sec.Comprimento) : 0.0));
                        y.Add(y[y.Count - 1] + (straight ? cv.ConvertFromSI(su.distance, sec.Elevacao) : 0.0));
                    }
                }

                chart.Clear();
                chart.XAxisTitle = "Length (" + su.distance + ")";
                chart.YAxisTitle = "Elevation (" + su.distance + ")";
                var line = chart.AddSeries("Profile", x, y);
                var points = chart.AddSeries("Sections", x, y, scatter: true);
                if (line != null) line.Color = Colors.SteelBlue;
                if (points != null) points.Color = Colors.SteelBlue;
                chart.InvalidateVisual();
            }

        }

        /// <summary>
        /// A wall material: the name shown, the value stored in the section (what the Windows
        /// editor stores, which the calculation's roughness and conductivity tables match) and
        /// the other spellings the calculation recognises for it.
        /// </summary>
        private sealed class Material
        {
            public string Display;
            public string Stored;
            public string[] Aliases = new string[0];
            public bool IsUserDefined;

            public static List<Material> List(IFlowsheet fs)
            {
                string Raw(string key)
                {
                    try { return fs.GetTranslatedString(key) ?? key; }
                    catch (Exception) { return key; }
                }

                string Shown(string key, string fallback)
                {
                    var t = Raw(key);
                    return string.IsNullOrEmpty(t) || t == key ? fallback : t;
                }

                return new List<Material>
                {
                    new Material { Display = Shown("AoComum", "Raw Steel"), Stored = Raw("AoComum"), Aliases = new[] { "Steel" } },
                    new Material { Display = Shown("AoCarbono", "Carbon Steel"), Stored = Raw("AoCarbono"), Aliases = new[] { "CarbonSteel" } },
                    new Material { Display = Shown("FerroBottomido", "Cast Iron"), Stored = Raw("FerroBottomido"), Aliases = new[] { "CastIron" } },
                    new Material { Display = Shown("AoInoxidvel", "Stainless Steel"), Stored = Raw("AoInoxidvel"), Aliases = new[] { "StainlessSteel" } },
                    new Material { Display = "PVC", Stored = "PVC" },
                    new Material { Display = "PVC+PFRV", Stored = "PVC+PFRV" },
                    new Material { Display = Shown("CommercialCopper", "Commercial Copper"), Stored = Raw("CommercialCopper"), Aliases = new[] { "CommercialCopper" } },
                    new Material { Display = Shown("UserDefined", "User Defined"), Stored = "UserDefined", IsUserDefined = true }
                };
            }

            /// <summary>
            /// The material a stored name stands for. A name the calculation does not recognise
            /// makes it use the section's own roughness and conductivity, so it shows as User
            /// Defined.
            /// </summary>
            public static int IndexOf(List<Material> list, string stored)
            {
                var s = stored ?? "";
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].IsUserDefined) continue;
                    if (s == list[i].Stored || list[i].Aliases.Contains(s)) return i;
                }
                return list.FindIndex(m => m.IsUserDefined);
            }
        }

        // ---------------------------------------------------------------------
        // Thermal Profile
        // ---------------------------------------------------------------------

        /// <summary>Insulation conductivities (W/[m.K]) the Windows editor fills in per material.</summary>
        private static readonly double[] InsulationConductivity = { 0.7, 1.0, 0.018, 0.04, 0.035, 0.036, 0.08 };

        /// <summary>
        /// The Windows PipeThermalProfileEditor: the profile type and the inputs of each type,
        /// enabled for the type selected.
        /// </summary>
        private static Control BuildThermalProfile(Pipe pipe)
        {
            var profile = pipe.ThermalProfile;
            var flowsheet = pipe.GetFlowsheet();
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
            var nf = flowsheet.FlowsheetOptions.NumberFormat;

            var panel = new AvaloniaEditorPanel().AutoSolveOnEdit(pipe);

            UnitOpEditorRows.ValueRow htc = null, tAmb = null, heat = null, tAmb2 = null, conductivity = null;
            TextBox gradient = null, gradient2 = null, thickness = null, velocity = null, emissivity = null, solarValue = null, solarEff = null;
            ComboBox insulation = null, medium = null;
            CheckBox userU = null, walls = null, internalHtc = null, insulated = null, externalHtc = null,
                     solar = null, solarGlobal = null;
            Control table = null;

            void Enable()
            {
                var type = profile.TipoPerfil;
                var isHtc = type == ThermalEditorDefinitions.ThermalProfileType.Definir_CGTC;
                var isQ = type == ThermalEditorDefinitions.ThermalProfileType.Definir_Q;
                var isCalc = type == ThermalEditorDefinitions.ThermalProfileType.Estimar_CGTC;

                htc.IsEnabled = isHtc && !profile.UseUserDefinedU;
                tAmb.IsEnabled = isHtc && !profile.UseUserDefinedU;
                gradient.IsEnabled = isHtc && !profile.UseUserDefinedU;
                userU.IsEnabled = isHtc;
                table.IsEnabled = isHtc && profile.UseUserDefinedU;

                heat.IsEnabled = isQ;

                tAmb2.IsEnabled = isCalc;
                gradient2.IsEnabled = isCalc;
                internalHtc.IsEnabled = isCalc;
                walls.IsEnabled = isCalc;
                insulated.IsEnabled = isCalc;
                insulation.IsEnabled = isCalc && profile.Incluir_isolamento;
                thickness.IsEnabled = isCalc && profile.Incluir_isolamento;
                conductivity.IsEnabled = isCalc && profile.Incluir_isolamento && profile.Material >= InsulationConductivity.Length;
                externalHtc.IsEnabled = isCalc;
                medium.IsEnabled = isCalc && profile.Incluir_cte;
                velocity.IsEnabled = isCalc && profile.Incluir_cte;
                emissivity.IsEnabled = isCalc && profile.Incluir_cte && profile.Meio == 0;
                solar.IsEnabled = isCalc;
                solarGlobal.IsEnabled = isCalc && profile.IncludeSolarRadiation;
                solarValue.IsEnabled = isCalc && profile.IncludeSolarRadiation && !profile.UseGlobalSolarRadiation;
                solarEff.IsEnabled = isCalc && profile.IncludeSolarRadiation;
            }

            panel.CreateAndAddDropDownRow("Profile type", StringArrays.thermalprofiletype().ToList(),
                (int)profile.TipoPerfil, (dd, e) =>
                {
                    if (dd.SelectedIndex < 0 || dd.SelectedIndex == (int)profile.TipoPerfil) return;
                    profile.TipoPerfil = (ThermalEditorDefinitions.ThermalProfileType)dd.SelectedIndex;
                    Enable();
                });

            // gradients are stored in K/m and shown in the deltaT and distance units
            var gradientUnit = su.deltaT + "/" + su.distance;
            double GradientFromSI(double v) => cv.ConvertFromSI(su.deltaT, v) / cv.ConvertFromSI(su.distance, 1.0);
            double GradientToSI(double v) => cv.ConvertToSI(su.deltaT, v) / cv.ConvertToSI(su.distance, 1.0);

            // defined overall HTC
            panel.CreateAndAddLabelRow("Defined HTC");

            htc = panel.CreateAndAddValueUnitRow(pipe, "Overall HTC", UnitOfMeasure.heat_transf_coeff,
                profile.CGTC_Definido, v => profile.CGTC_Definido = v);

            tAmb = panel.CreateAndAddValueUnitRow(pipe, "Ambient temperature", UnitOfMeasure.temperature,
                profile.Temp_amb_definir, v => profile.Temp_amb_definir = v);

            gradient = panel.CreateAndAddTextBoxRow(nf, "Ambient temperature gradient (" + gradientUnit + ")",
                GradientFromSI(profile.AmbientTemperatureGradient), (tb, e) =>
                {
                    if (UnitOpEditorRows.TryParse(tb.Text, out var v)) profile.AmbientTemperatureGradient = GradientToSI(v);
                });

            userU = panel.CreateAndAddCheckBoxRow("Use tabulated data (length, ambient temperature, HTC)",
                profile.UseUserDefinedU, (cb, e) =>
                {
                    profile.UseUserDefinedU = cb.IsChecked.GetValueOrDefault();
                    Enable();
                });

            table = BuildUserDefinedUTable(pipe, panel);
            panel.CreateAndAddControlRow(table);

            // defined heat exchange
            panel.CreateAndAddLabelRow("Defined Heat Exchange");

            heat = panel.CreateAndAddValueUnitRow(pipe, "Heat exchanged", UnitOfMeasure.heatflow,
                profile.Calor_trocado, v => profile.Calor_trocado = v);

            // calculated HTC
            panel.CreateAndAddLabelRow("Calculated HTC");

            tAmb2 = panel.CreateAndAddValueUnitRow(pipe, "Ambient temperature", UnitOfMeasure.temperature,
                profile.Temp_amb_estimar, v => profile.Temp_amb_estimar = v);

            gradient2 = panel.CreateAndAddTextBoxRow(nf, "Ambient temperature gradient (" + gradientUnit + ")",
                GradientFromSI(profile.AmbientTemperatureGradient_EstimateHTC), (tb, e) =>
                {
                    if (UnitOpEditorRows.TryParse(tb.Text, out var v)) profile.AmbientTemperatureGradient_EstimateHTC = GradientToSI(v);
                });

            internalHtc = panel.CreateAndAddCheckBoxRow("Include internal HTC", profile.Incluir_cti,
                (cb, e) => profile.Incluir_cti = cb.IsChecked.GetValueOrDefault());

            walls = panel.CreateAndAddCheckBoxRow("Include pipe walls", profile.Incluir_paredes,
                (cb, e) => profile.Incluir_paredes = cb.IsChecked.GetValueOrDefault());

            insulated = panel.CreateAndAddCheckBoxRow("Include insulation", profile.Incluir_isolamento, (cb, e) =>
            {
                profile.Incluir_isolamento = cb.IsChecked.GetValueOrDefault();
                Enable();
            });

            insulation = panel.CreateAndAddDropDownRow("Insulation material", StringArrays.insulationmaterial().ToList(),
                profile.Material, (dd, e) =>
                {
                    if (dd.SelectedIndex < 0 || dd.SelectedIndex == profile.Material) return;
                    profile.Material = dd.SelectedIndex;
                    if (profile.Material < InsulationConductivity.Length)
                    {
                        profile.Condtermica = InsulationConductivity[profile.Material];
                        conductivity.Value.Text = cv.ConvertFromSI(su.thermalConductivity, profile.Condtermica)
                            .ToString(nf, CultureInfo.CurrentCulture);
                    }
                    Enable();
                });

            // the unit system resolves no unit list for thickness, so this one is a plain row
            thickness = panel.CreateAndAddTextBoxRow(nf, "Insulation thickness (" + su.thickness + ")",
                cv.ConvertFromSI(su.thickness, profile.Espessura), (tb, e) =>
                {
                    if (UnitOpEditorRows.TryParse(tb.Text, out var v)) profile.Espessura = cv.ConvertToSI(su.thickness, v);
                });

            conductivity = panel.CreateAndAddValueUnitRow(pipe, "Insulation thermal conductivity",
                UnitOfMeasure.thermalConductivity, profile.Condtermica, v => profile.Condtermica = v);

            externalHtc = panel.CreateAndAddCheckBoxRow("Include external HTC", profile.Incluir_cte, (cb, e) =>
            {
                profile.Incluir_cte = cb.IsChecked.GetValueOrDefault();
                Enable();
            });

            medium = panel.CreateAndAddDropDownRow("External environment", StringArrays.external_env().ToList(),
                profile.Meio, (dd, e) =>
                {
                    if (dd.SelectedIndex < 0 || dd.SelectedIndex == profile.Meio) return;
                    profile.Meio = dd.SelectedIndex;
                    Enable();
                });

            velocity = panel.CreateAndAddTextBoxRow(nf, "Velocity (m/s) or burial depth (m)", profile.Velocidade,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) profile.Velocidade = v; });

            emissivity = panel.CreateAndAddTextBoxRow(nf, "External surface emissivity (air, 0-1)", profile.SurfaceEmissivity,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) profile.SurfaceEmissivity = v; });

            solar = panel.CreateAndAddCheckBoxRow("Include solar irradiation", profile.IncludeSolarRadiation, (cb, e) =>
            {
                profile.IncludeSolarRadiation = cb.IsChecked.GetValueOrDefault();
                Enable();
            });

            solarGlobal = panel.CreateAndAddCheckBoxRow("Use global solar irradiation", profile.UseGlobalSolarRadiation, (cb, e) =>
            {
                profile.UseGlobalSolarRadiation = cb.IsChecked.GetValueOrDefault();
                Enable();
            });

            solarValue = panel.CreateAndAddTextBoxRow(nf, "Solar irradiation (kWh/m2)", profile.SolarRadiationValue_kWh_m2,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) profile.SolarRadiationValue_kWh_m2 = v; });

            solarEff = panel.CreateAndAddTextBoxRow(nf, "Solar absorption efficiency (0-1)", profile.SolarRadiationAbsorptionEfficiency,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) profile.SolarRadiationAbsorptionEfficiency = v; });

            panel.CreateAndAddDescriptionRow(
                "The velocity is the air or water speed around the pipe; for a buried pipe it is the " +
                "burial depth. The emissivity applies to pipes in air only.");

            Enable();
            return panel;
        }

        /// <summary>One row of the tabulated overall HTC, in the units of the flowsheet.</summary>
        private sealed class UTableRow : INotifyPropertyChanged
        {
            private double _length, _temperature, _u;

            public UTableRow(double length, double temperature, double u)
            {
                _length = length;
                _temperature = temperature;
                _u = u;
            }

            public string Length
            {
                get { return _length.ToString("G6", CultureInfo.CurrentCulture); }
                set { if (UnitOpEditorRows.TryParse(value, out var v)) { _length = v; Raise("Length"); } }
            }

            public string Temperature
            {
                get { return _temperature.ToString("G6", CultureInfo.CurrentCulture); }
                set { if (UnitOpEditorRows.TryParse(value, out var v)) { _temperature = v; Raise("Temperature"); } }
            }

            public string U
            {
                get { return _u.ToString("G6", CultureInfo.CurrentCulture); }
                set { if (UnitOpEditorRows.TryParse(value, out var v)) { _u = v; Raise("U"); } }
            }

            public double LengthValue => _length;
            public double TemperatureValue => _temperature;
            public double UValue => _u;

            public event PropertyChangedEventHandler PropertyChanged;

            private void Raise(string name)
            {
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        /// <summary>
        /// The table of the Windows EditingForm_Pipe_UserDefinedU: length or depth, ambient
        /// temperature and overall HTC, interpolated along the pipe when the tabulated data is on.
        /// </summary>
        private static Control BuildUserDefinedUTable(Pipe pipe, AvaloniaEditorPanel panel)
        {
            var profile = pipe.ThermalProfile;
            var flowsheet = pipe.GetFlowsheet();
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
            var rows = new ObservableCollection<UTableRow>();

            void WriteBack()
            {
                profile.UserDefinedU_Length = rows.Select(r => cv.ConvertToSI(su.distance, r.LengthValue)).ToList();
                profile.UserDefinedU_Temp = rows.Select(r => cv.ConvertToSI(su.temperature, r.TemperatureValue)).ToList();
                profile.UserDefinedU_U = rows.Select(r => cv.ConvertToSI(su.heat_transf_coeff, r.UValue)).ToList();
            }

            var lengths = profile.UserDefinedU_Length ?? new List<double>();
            var temps = profile.UserDefinedU_Temp ?? new List<double>();
            var us = profile.UserDefinedU_U ?? new List<double>();
            var n = Math.Min(lengths.Count, Math.Min(temps.Count, us.Count));
            for (int i = 0; i < n; i++)
            {
                rows.Add(new UTableRow(cv.ConvertFromSI(su.distance, lengths[i]),
                                       cv.ConvertFromSI(su.temperature, temps[i]),
                                       cv.ConvertFromSI(su.heat_transf_coeff, us[i])));
            }

            var grid = new DataGrid
            {
                ItemsSource = rows,
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Height = 160
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Length/Depth (" + su.distance + ")",
                Binding = new Binding("Length") { Mode = BindingMode.TwoWay },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Ambient T (" + su.temperature + ")",
                Binding = new Binding("Temperature") { Mode = BindingMode.TwoWay },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Overall HTC (" + su.heat_transf_coeff + ")",
                Binding = new Binding("U") { Mode = BindingMode.TwoWay },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            grid.CellEditEnded += (s, e) =>
            {
                flowsheet.RegisterSnapshot(SnapshotType.ObjectData, pipe);
                WriteBack();
                panel.OnAfterEdit?.Invoke();
            };

            var add = new Button { Content = "Add Row", Margin = new Thickness(0, 0, 6, 0) };
            add.Classes.Add("panel");
            add.Click += (s, e) =>
            {
                var last = rows.Count > 0 ? rows[rows.Count - 1] : null;
                rows.Add(last != null
                    ? new UTableRow(last.LengthValue, last.TemperatureValue, last.UValue)
                    : new UTableRow(0.0, cv.ConvertFromSI(su.temperature, 298.15), 0.0));
                WriteBack();
            };

            var remove = new Button { Content = "Remove Row" };
            remove.Classes.Add("panel");
            remove.Click += (s, e) =>
            {
                if (!(grid.SelectedItem is UTableRow selected)) return;
                flowsheet.RegisterSnapshot(SnapshotType.ObjectData, pipe);
                rows.Remove(selected);
                WriteBack();
                panel.OnAfterEdit?.Invoke();
            };

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            actions.Children.Add(add);
            actions.Children.Add(remove);

            var host = new DockPanel();
            DockPanel.SetDock(actions, global::Avalonia.Controls.Dock.Bottom);
            host.Children.Add(actions);
            host.Children.Add(grid);
            return host;
        }

    }

}
