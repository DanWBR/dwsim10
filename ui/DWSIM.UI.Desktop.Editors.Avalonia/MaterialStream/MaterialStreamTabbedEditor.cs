using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.UI.Shared.Avalonia;
using CompoundAmounts = DWSIM.Thermodynamics.Streams.CompoundAmounts;
using MaterialStream = DWSIM.Thermodynamics.Streams.MaterialStream;
using Thickness = Avalonia.Thickness;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Material stream editor for the Avalonia UI, laid out like the WinForms
    /// MaterialStreamEditor, tab for tab:
    ///
    ///   Input Data       Stream Conditions    the flat property panel
    ///                    Compound Amounts     basis, editable grid and the input actions
    ///   Results          Compounds            Amounts (per phase) and Properties (per compound)
    ///                    Phase Properties     one property grid per phase
    ///   Annotations
    ///   Floating Tables  compound amount basis of this stream
    ///
    /// The grids are refreshed from the registry below, so a solve updates the numbers without
    /// rebuilding the tabs and losing the user's place.
    /// </summary>
    public static class MaterialStreamTabbedEditor
    {

        /// <summary>
        /// Refresh action of every editor built so far, keyed by the control that hosts it, held
        /// weakly so a closed editor does not keep the stream alive. <see cref="RefreshAll"/>
        /// drives them from the host's UpdateInterface, which is what fires after a solve.
        /// </summary>
        private static readonly List<(WeakReference Host, Action Refresh)> Registry =
            new List<(WeakReference, Action)>();

        /// <summary>Repopulates every live editor. Safe to call from any thread the UI owns.</summary>
        public static void RefreshAll()
        {
            lock (Registry)
            {
                Registry.RemoveAll(x => !x.Host.IsAlive);

                foreach (var item in Registry.ToList())
                {
                    try { item.Refresh(); }
                    catch (Exception) { }
                }
            }
        }

        private static void Register(Control host, Action refresh)
        {
            lock (Registry)
            {
                Registry.RemoveAll(x => !x.Host.IsAlive);
                Registry.Add((new WeakReference(host), refresh));
            }
        }

        /// <summary>Phases as the engine indexes them, in the order the WinForms editor shows.</summary>
        private static readonly (string Title, int Index)[] Phases =
        {
            ("Mixture", 0),
            ("Vapor", 2),
            ("Overall Liquid", 1),
            ("Liquid 1", 3),
            ("Liquid 2", 4),
            ("Solid", 7)
        };

        /// <summary>Phases that carry per-compound properties; the mixture pseudo-phase does not.</summary>
        private static readonly (string Title, int Index)[] RealPhases =
        {
            ("Vapor", 2),
            ("Liquid 1", 3),
            ("Liquid 2", 4),
            ("Solid", 7)
        };

        private static readonly string[] FloatingTableBases =
        {
            "Molar Fractions",
            "Mass Fractions",
            "Volumetric Fractions",
            "Molar Flows",
            "Mass Flows",
            "Volumetric Flows",
            "Simulation Default"
        };

        /// <summary>
        /// Host hook for configuring a property package, which the WinForms form does with the
        /// cog button next to the picker. Set by the Avalonia host; the button is hidden when
        /// nothing is wired.
        /// </summary>
        public static Action<IFlowsheet, IPropertyPackage> ConfigurePropertyPackage;

        public static Control Build(MaterialStream ms)
        {
            var flowsheet = ms.GetFlowsheet();
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
            var nf = flowsheet.FlowsheetOptions.NumberFormat;
            var nff = flowsheet.FlowsheetOptions.FractionNumberFormat;

            var resultGrids = new List<(CompoundGrid Grid, int Phase)>();
            var propertyGrids = new List<(PhasePropertyGrid Grid, int Phase)>();
            var compoundPropertyGrids = new List<(CompoundPropertyGrid Grid, int Phase)>();

            Action refresh = null;

            // ---- Input Data ---------------------------------------------------

            var conditions = new AvaloniaEditorPanel();
            // the name, the status and the property package live in the header above, as they do
            // in the WinForms form, so the conditions panel starts at the state specification
            MaterialStreamEditorAvalonia.Populate(ms, conditions, includeComposition: false, includeHeader: false);

            var inputGrid = new CompoundGrid(su, nff, editable: true);

            var inputTabs = new TabControl();
            inputTabs.Items.Add(Tab("Stream Conditions", new ScrollViewer { Content = conditions }));

            var amountsTab = Tab("Compound Amounts",
                BuildInputAmounts(ms, su, nf, inputGrid, () => refresh?.Invoke()));

            // a stream written by the object upstream carries its composition too
            if (MaterialStreamEditorAvalonia.IsDrivenUpstream(ms)) amountsTab.IsEnabled = false;

            inputTabs.Items.Add(amountsTab);

            // ---- Results: compound amounts per phase --------------------------

            var amountsBasis = Combo(CompoundAmounts.BasisNames, 0, 240);
            var amountsUnits = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
                Opacity = 0.85
            };

            var amountsTabs = new TabControl();
            foreach (var phase in Phases)
            {
                var grid = new CompoundGrid(su, nff, editable: false);
                resultGrids.Add((grid, phase.Index));
                amountsTabs.Items.Add(Tab(phase.Title, grid));
            }

            var phaseTotal = BuildPhaseTotalGrid();

            Action updatePhaseTotal = () =>
            {
                var index = Math.Max(0, amountsTabs.SelectedIndex);
                var phase = PhaseOf(ms, Phases[index].Index);
                var basis = (CompoundAmounts.Basis)Math.Max(0, amountsBasis.SelectedIndex);
                var units = CompoundAmounts.Units(basis, su);

                double total;
                try { total = CompoundAmounts.PhaseTotal(ms, phase, basis, su); }
                catch (Exception) { total = double.NaN; }

                phaseTotal.ItemsSource = new[]
                {
                    new PhaseTotalRow
                    {
                        Caption = string.IsNullOrEmpty(units) ? "Phase Total" : "Phase Total (" + units + ")",
                        Value = double.IsNaN(total) ? "" : total.ToString(nf)
                    }
                };
            };

            amountsBasis.SelectionChanged += (s, e) =>
            {
                var basis = (CompoundAmounts.Basis)Math.Max(0, amountsBasis.SelectedIndex);
                foreach (var item in resultGrids) item.Grid.Basis = basis;
                amountsUnits.Text = CompoundAmounts.Units(basis, su);
                updatePhaseTotal();
            };
            amountsTabs.SelectionChanged += (s, e) => updatePhaseTotal();

            var amountsHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(4, 6, 4, 6)
            };
            amountsHeader.Children.Add(new TextBlock
            {
                Text = "Basis",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            amountsHeader.Children.Add(amountsBasis);
            amountsHeader.Children.Add(amountsUnits);

            var amountsHost = new DockPanel();
            DockPanel.SetDock(amountsHeader, global::Avalonia.Controls.Dock.Top);
            DockPanel.SetDock(phaseTotal, global::Avalonia.Controls.Dock.Bottom);
            amountsHost.Children.Add(amountsHeader);
            amountsHost.Children.Add(phaseTotal);
            amountsHost.Children.Add(amountsTabs);

            // ---- Results: per-compound properties -----------------------------

            var propertySelector = Combo(CompoundPropertyGrid.PropertyNames, 0, 250);
            var propertyUnits = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
                Opacity = 0.85
            };

            var compPropTabs = new TabControl();
            foreach (var phase in RealPhases)
            {
                var grid = new CompoundPropertyGrid(su, nff);
                compoundPropertyGrids.Add((grid, phase.Index));
                compPropTabs.Items.Add(Tab(phase.Title, grid));
            }

            propertySelector.SelectionChanged += (s, e) =>
            {
                if (propertySelector.SelectedIndex < 0) return;
                var kind = (CompoundPropertyGrid.PropertyKind)propertySelector.SelectedIndex;
                foreach (var item in compoundPropertyGrids) item.Grid.Property = kind;
                var units = compoundPropertyGrids.Count > 0 ? compoundPropertyGrids[0].Grid.CurrentUnits : "";
                propertyUnits.Text = string.IsNullOrEmpty(units) ? "" : "Units: " + units;
            };

            var compPropHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(4, 6, 4, 6)
            };
            compPropHeader.Children.Add(new TextBlock
            {
                Text = "Property",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            compPropHeader.Children.Add(propertySelector);
            compPropHeader.Children.Add(propertyUnits);

            var compPropHost = new DockPanel();
            DockPanel.SetDock(compPropHeader, global::Avalonia.Controls.Dock.Top);
            compPropHost.Children.Add(compPropHeader);
            compPropHost.Children.Add(compPropTabs);

            var compoundTabs = new TabControl();
            compoundTabs.Items.Add(Tab("Amounts", amountsHost));
            compoundTabs.Items.Add(Tab("Properties", compPropHost));

            // ---- Results: phase properties ------------------------------------

            var phasePropTabs = new TabControl();
            foreach (var phase in Phases)
            {
                var grid = new PhasePropertyGrid(su, nf);
                propertyGrids.Add((grid, phase.Index));
                phasePropTabs.Items.Add(Tab(phase.Title, grid));
            }

            var resultsTabs = new TabControl();
            resultsTabs.Items.Add(Tab("Compounds", compoundTabs));
            resultsTabs.Items.Add(Tab("Phase Properties", phasePropTabs));

            // ---- Annotations --------------------------------------------------

            var annotations = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Text = ms.Annotation ?? "",
                Margin = new Thickness(4)
            };
            annotations.TextChanged += (s, e) => ms.Annotation = annotations.Text;

            // ---- Floating Tables ----------------------------------------------

            var floatingBasis = Combo(FloatingTableBases, (int)ms.FloatingTableAmountBasis, 300);
            floatingBasis.SelectionChanged += (s, e) =>
            {
                if (floatingBasis.SelectedIndex < 0) return;
                ms.FloatingTableAmountBasis = (Interfaces.Enums.CompositionBasis)floatingBasis.SelectedIndex;
            };

            var floatingRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(4, 6, 4, 6)
            };
            floatingRow.Children.Add(new TextBlock
            {
                Text = "Basis",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            floatingRow.Children.Add(floatingBasis);

            var floatingHost = new StackPanel { Margin = new Thickness(4) };
            floatingHost.Children.Add(new TextBlock
            {
                Text = "Compound Amounts",
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(4, 6, 0, 0)
            });
            floatingHost.Children.Add(floatingRow);

            // ---- Outer notebook -----------------------------------------------

            var main = new TabControl();
            main.Items.Add(Tab("Input Data", inputTabs));
            main.Items.Add(Tab("Results", resultsTabs));
            main.Items.Add(Tab("Annotations", annotations));
            main.Items.Add(Tab("Floating Tables", floatingHost));
            main.Items.Add(Tab("Utilities", AttachedUtilitiesEditor.Build(ms)));

            // the form the WinForms editor draws: the object notebook and the property package
            // group above, the main notebook filling the rest
            var root = new DockPanel { Margin = new Thickness(2) };
            Action informationRefresh;
            var objectTabs = BuildObjectTabs(ms, out informationRefresh);
            DockPanel.SetDock(objectTabs, global::Avalonia.Controls.Dock.Top);
            var packageGroup = BuildPropertyPackageGroup(ms);
            DockPanel.SetDock(packageGroup, global::Avalonia.Controls.Dock.Top);
            root.Children.Add(objectTabs);
            root.Children.Add(packageGroup);
            root.Children.Add(main);

            refresh = () =>
            {
                inputGrid.Populate(ms, PhaseOf(ms, 0));
                foreach (var item in resultGrids) item.Grid.Populate(ms, PhaseOf(ms, item.Phase));
                foreach (var item in propertyGrids) item.Grid.Populate(PhaseOf(ms, item.Phase));
                foreach (var item in compoundPropertyGrids) item.Grid.Populate(PhaseOf(ms, item.Phase));
                updatePhaseTotal();
                informationRefresh();
            };

            refresh();

            // the host calls RefreshAll from UpdateInterface, which is what fires after a solve;
            // the visual-tree hook covers the editor being brought back to the front
            Register(root, refresh);
            root.AttachedToVisualTree += (s, e) => refresh();

            return root;
        }

        // ---------------------------------------------------------------------
        // Input: compound amounts
        // ---------------------------------------------------------------------

        /// <summary>
        /// The Compound Amounts input tab: the basis and solvent pickers, the editable grid and
        /// the actions the WinForms editor stacks down its right side.
        /// </summary>
        private static Control BuildInputAmounts(MaterialStream ms, IUnitsOfMeasure su, string nf,
                                                 CompoundGrid grid, Action refresh)
        {
            var flowsheet = ms.GetFlowsheet();

            var basis = Combo(CompoundAmounts.BasisNames, 0, 0);

            var compounds = ms.Phases[0].Compounds.Keys.ToList();
            var solvent = Combo(compounds.ToArray(),
                compounds.IndexOf(ms.ReferenceSolvent ?? ""), 0);
            solvent.IsEnabled = false;

            var total = new TextBlock
            {
                FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
                Foreground = Brushes.Blue,
                Margin = new Thickness(0, 0, 0, 4),
                TextWrapping = TextWrapping.Wrap
            };

            Action updateTotal = () =>
            {
                var units = grid.Units;
                total.Text = "Total: " + grid.Total.ToString(nf) + (string.IsNullOrEmpty(units) ? "" : " " + units);
            };

            grid.Edited += () => updateTotal();

            basis.SelectionChanged += (s, e) =>
            {
                var selected = (CompoundAmounts.Basis)Math.Max(0, basis.SelectedIndex);
                grid.Basis = selected;
                solvent.IsEnabled = CompoundAmounts.NeedsSolvent(selected);
                updateTotal();
            };

            // the input rows, laid out as the WinForms panel stacks them
            var header = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                Margin = new Thickness(4, 6, 4, 6)
            };
            var basisLabel = new TextBlock { Text = "Basis", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 8, 2) };
            var solventLabel = new TextBlock { Text = "Solvent", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 8, 2) };
            Grid.SetRow(basisLabel, 0); Grid.SetColumn(basisLabel, 0);
            Grid.SetRow(basis, 0); Grid.SetColumn(basis, 1);
            Grid.SetRow(solventLabel, 1); Grid.SetColumn(solventLabel, 0);
            Grid.SetRow(solvent, 1); Grid.SetColumn(solvent, 1);
            header.Children.Add(basisLabel);
            header.Children.Add(basis);
            header.Children.Add(solventLabel);
            header.Children.Add(solvent);

            var actions = new StackPanel { Margin = new Thickness(6, 0, 4, 0), Width = 110 };
            actions.Children.Add(total);
            actions.Children.Add(ActionButton("Normalize", () => { grid.Normalize(); updateTotal(); }));
            actions.Children.Add(ActionButton("Equalize", () => { grid.Equalize(); updateTotal(); }));
            actions.Children.Add(ActionButton("Clear", () => { grid.Erase(); updateTotal(); }));
            actions.Children.Add(ActionButton("Complete", () => { grid.Complete(); updateTotal(); }));

            var accept = ActionButton("Accept Changes", () =>
            {
                try
                {
                    ms.PropertyPackage.CurrentMaterialStream = ms;
                    flowsheet.RegisterSnapshot(Interfaces.Enums.SnapshotType.ObjectData, ms);

                    CompoundAmounts.Apply(ms, grid.Basis, grid.Amounts, su,
                        solvent.SelectedItem as string ?? "");

                    flowsheet.RequestCalculation(ms);
                    refresh();
                    updateTotal();
                }
                catch (Exception ex)
                {
                    flowsheet.ShowMessage("Could not apply the compound amounts: " + ex.Message,
                        IFlowsheet.MessageType.GeneralError);
                }
            });
            accept.Classes.Add("action");
            actions.Children.Add(accept);

            updateTotal();

            var host = new DockPanel();
            DockPanel.SetDock(header, global::Avalonia.Controls.Dock.Top);
            DockPanel.SetDock(actions, global::Avalonia.Controls.Dock.Right);
            host.Children.Add(header);
            host.Children.Add(actions);
            host.Children.Add(grid);
            return host;
        }

        // ---------------------------------------------------------------------
        // The header the WinForms form carries above its main notebook
        // ---------------------------------------------------------------------

        /// <summary>The Information and Connections notebook at the top of the editor.</summary>
        private static Control BuildObjectTabs(MaterialStream ms, out Action refresh)
        {
            var status = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };
            var linked = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var tag = new TextBox { Text = ms.GraphicObject.Tag, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), MinHeight = 0 };
            tag.LostFocus += (s, e) =>
            {
                ms.GraphicObject.Tag = tag.Text;
                ms.GetFlowsheet().UpdateInterface();
            };

            var active = new CheckBox
            {
                Content = "Active",
                FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
                IsChecked = ms.GraphicObject.Active
            };
            active.IsCheckedChanged += (s, e) =>
            {
                ms.GraphicObject.Active = active.IsChecked.GetValueOrDefault();
                ms.GetFlowsheet().UpdateInterface();
            };

            var info = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
                Margin = new Thickness(6)
            };

            AddInfoRow(info, 0, "Object", tag, active);
            AddInfoRow(info, 1, "Status", status, null);
            AddInfoRow(info, 2, "Linked to", linked, null);

            refresh = () =>
            {
                status.Text = !string.IsNullOrEmpty(ms.ErrorMessage)
                    ? ms.ErrorMessage
                    : (ms.Calculated ? "Calculated" : "Not calculated");

                linked.Text = Upstream(ms) + "  >  " + Downstream(ms);

                if (tag.Text != ms.GraphicObject.Tag) tag.Text = ms.GraphicObject.Tag;
                active.IsChecked = ms.GraphicObject.Active;
            };

            refresh();

            var tabs = new TabControl();
            tabs.Items.Add(Tab("Information", info));
            tabs.Items.Add(Tab("Connections", BuildConnections(ms)));
            return tabs;
        }

        private static string Upstream(MaterialStream ms)
        {
            var connectors = ms.GraphicObject.InputConnectors;
            if (connectors == null || connectors.Count == 0 || !connectors[0].IsAttached) return "-";
            var from = connectors[0].AttachedConnector?.AttachedFrom;
            return from == null ? "-" : from.Tag;
        }

        private static string Downstream(MaterialStream ms)
        {
            var connectors = ms.GraphicObject.OutputConnectors;
            if (connectors == null || connectors.Count == 0 || !connectors[0].IsAttached) return "-";
            var to = connectors[0].AttachedConnector?.AttachedTo;
            return to == null ? "-" : to.Tag;
        }

        private static void AddInfoRow(Grid host, int row, string caption, Control editor, Control trailing)
        {
            var label = new TextBlock
            {
                Text = caption,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 8, 5)
            };
            Grid.SetRow(label, row);
            Grid.SetColumn(label, 0);
            host.Children.Add(label);

            editor.Margin = new Thickness(0, 5, 0, 5);
            Grid.SetRow(editor, row);
            Grid.SetColumn(editor, 1);
            host.Children.Add(editor);

            if (trailing == null) return;

            trailing.Margin = new Thickness(8, 5, 0, 5);
            Grid.SetRow(trailing, row);
            Grid.SetColumn(trailing, 2);
            host.Children.Add(trailing);
        }

        /// <summary>Upstream and downstream of the stream, as the WinForms Connections tab lists them.</summary>
        private static Control BuildConnections(MaterialStream ms)
        {
            return new ScrollViewer { Content = AvaloniaTabBuilders.BuildConnections(ms) };
        }

        /// <summary>The Property Package Settings group between the two notebooks.</summary>
        private static Control BuildPropertyPackageGroup(MaterialStream ms)
        {
            var flowsheet = ms.GetFlowsheet();
            var packages = flowsheet.PropertyPackages.Values.ToList();
            var names = packages.Select(x => x.Tag).ToList();

            var picker = new ComboBox
            {
                ItemsSource = names,
                SelectedIndex = names.IndexOf(ms.PropertyPackage == null ? "" : ms.PropertyPackage.Tag),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11)
            };
            picker.SelectionChanged += (s, e) =>
            {
                if (picker.SelectedIndex < 0 || picker.SelectedIndex >= packages.Count) return;
                ms.PropertyPackage = (DWSIM.Thermodynamics.PropertyPackages.PropertyPackage)packages[picker.SelectedIndex];
            };

            var configure = new Button { Content = "Configure", FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Margin = new Thickness(6, 0, 0, 0) };
            configure.Classes.Add("panel");
            configure.IsVisible = ConfigurePropertyPackage != null;
            configure.Click += (s, e) =>
            {
                if (ConfigurePropertyPackage == null || ms.PropertyPackage == null) return;
                ConfigurePropertyPackage(flowsheet, ms.PropertyPackage);
            };

            var label = new TextBlock
            {
                Text = "Property Package",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var row = new DockPanel { Margin = new Thickness(6, 4, 6, 6) };
            DockPanel.SetDock(label, global::Avalonia.Controls.Dock.Left);
            DockPanel.SetDock(configure, global::Avalonia.Controls.Dock.Right);
            row.Children.Add(label);
            row.Children.Add(configure);
            row.Children.Add(picker);

            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = "Property Package Settings",
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(6, 4, 0, 0)
            });
            content.Children.Add(row);

            var group = new Border { Margin = new Thickness(0, 4, 0, 4), Child = content };
            group.Classes.Add("group");
            return group;
        }

        private sealed class PhaseTotalRow
        {
            public string Caption { get; set; } = "";
            public string Value { get; set; } = "";
        }

        /// <summary>The single row under the compound grid, headerless as in the WinForms editor.</summary>
        private static DataGrid BuildPhaseTotalGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.None,
                GridLinesVisibility = DataGridGridLinesVisibility.None,
                Height = DWSIM.UI.Shared.Avalonia.UiScale.Size(26)
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Binding = new global::Avalonia.Data.Binding(nameof(PhaseTotalRow.Caption)),
                Width = new DataGridLength(60, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Binding = new global::Avalonia.Data.Binding(nameof(PhaseTotalRow.Value)),
                Width = new DataGridLength(40, DataGridLengthUnitType.Star)
            });

            return grid;
        }

        private static Button ActionButton(string caption, Action action)
        {
            var button = new Button
            {
                Content = caption,
                Margin = new Thickness(0, 0, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11)
            };
            button.Classes.Add("panel");
            button.Click += (s, e) => action();
            return button;
        }

        /// <summary>A picker; a width of zero lets it fill the space it is given.</summary>
        private static ComboBox Combo(string[] items, int selected, double width)
        {
            var combo = new ComboBox
            {
                ItemsSource = items.ToList(),
                SelectedIndex = selected >= 0 && selected < items.Length ? selected : 0,
                FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            if (width > 0) combo.Width = width;
            else combo.HorizontalAlignment = HorizontalAlignment.Stretch;

            return combo;
        }

        private static IPhase PhaseOf(MaterialStream ms, int index)
        {
            return ms.Phases.ContainsKey(index) ? ms.Phases[index] : null;
        }

        private static TabItem Tab(string header, Control content)
        {
            return new TabItem { Header = header, Content = content };
        }

    }

}
