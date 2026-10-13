using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DWSIM.Interfaces;
using DWSIM.UI.Shared.Avalonia;
using CapeOpenUO = DWSIM.UnitOperations.UnitOperations.CapeOpenUO;
using FlowsheetUO = DWSIM.UnitOperations.UnitOperations.Flowsheet;
using PythonScriptUO = DWSIM.UnitOperations.UnitOperations.CustomUO;
using ExcelParameter = DWSIM.UnitOperations.UnitOperations.Auxiliary.ExcelParameter;
using SpreadsheetCalculationEngine = DWSIM.UnitOperations.UnitOperations.Auxiliary.SpreadsheetCalculationEngine;
using SpreadsheetUO = DWSIM.UnitOperations.UnitOperations.ExcelUO;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Spreadsheet unit operation editor: which workbook drives it, what recalculates it, and the
    /// input and output variables the sheet exchanges with the flowsheet.
    /// </summary>
    public static class SpreadsheetUOEditor
    {

        private sealed class ParameterRow : INotifyPropertyChanged
        {
            private readonly ExcelParameter _parameter;
            private readonly string _nf;
            private readonly bool _readOnly;

            public ParameterRow(ExcelParameter parameter, string nf, bool readOnly)
            {
                _parameter = parameter;
                _nf = nf;
                _readOnly = readOnly;
            }

            public string Name { get { return _parameter.Name; } }
            public string Unit { get { return _parameter.Unit; } }

            public string Value
            {
                get { return _parameter.Value.ToString(_nf, CultureInfo.CurrentCulture); }
                set
                {
                    if (_readOnly) return;
                    if (!UnitOpEditorRows.TryParse(value, out var v)) return;
                    _parameter.Value = v;
                    Raise("Value");
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise(string name)
            {
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        // same order as SpreadsheetCalculationEngine
        private static readonly List<string> Engines = new List<string>
        {
            "Automatic (Excel when installed)",
            "Excel",
            "Internal (.xlsx, .xlsm)"
        };

        public static Control Build(SpreadsheetUO spreadsheet)
        {
            // the variables come from the workbook, which is read once and again after a new file is picked
            try { spreadsheet.ReadExcelParams(); }
            catch (Exception ex)
            {
                spreadsheet.GetFlowsheet()?.ShowMessage(ex.Message, IFlowsheet.MessageType.GeneralError);
            }

            return UnitOpEditor.Build(spreadsheet,
                input: panel => BuildFile(spreadsheet, panel),
                extras: new[]
                {
                    ("Input Variables", Grid(spreadsheet, spreadsheet.InputParams, readOnly: false)),
                    ("Output Variables", Grid(spreadsheet, spreadsheet.OutputParams, readOnly: true))
                });
        }

        private static void BuildFile(SpreadsheetUO spreadsheet, AvaloniaEditorPanel panel)
        {
            var flowsheet = spreadsheet.GetFlowsheet();

            ComboBox embedded = null;
            TextBox external = null;
            Button browse = null;

            void Apply()
            {
                if (embedded != null) embedded.IsEnabled = spreadsheet.FileIsEmbedded;
                if (external != null) external.IsEnabled = !spreadsheet.FileIsEmbedded;
                if (browse != null) browse.IsEnabled = !spreadsheet.FileIsEmbedded;
            }

            panel.CreateAndAddCheckBoxRow("Use Embedded File", spreadsheet.FileIsEmbedded, (cb, e) =>
            {
                spreadsheet.FileIsEmbedded = cb.IsChecked.GetValueOrDefault();
                spreadsheet.ParamsLoaded = false;
                Apply();
            });

            var files = new List<string>();
            try { files.AddRange(flowsheet.FileDatabaseProvider.GetFiles()); }
            catch (Exception) { }

            embedded = panel.CreateAndAddDropDownRow("Embedded File", files,
                Math.Max(0, files.IndexOf(spreadsheet.EmbeddedFileName ?? "")), (dd, e) =>
                {
                    if (dd.SelectedIndex < 0 || dd.SelectedIndex >= files.Count) return;
                    spreadsheet.EmbeddedFileName = files[dd.SelectedIndex];
                    spreadsheet.ParamsLoaded = false;
                });

            // the path is picked from a dialog, never typed, as the Windows editor has it
            external = panel.CreateAndAddStringEditorRow("Spreadsheet File", spreadsheet.Filename, null);
            external.IsReadOnly = true;

            browse = panel.CreateAndAddButtonRow("Browse...", null, (btn, e) =>
                EditorFilePicker.Open(panel, "Select the Spreadsheet",
                    new[] { "*.xlsx", "*.xlsm", "*.xls", "*.ods" },
                    path =>
                    {
                        spreadsheet.Filename = path;
                        spreadsheet.ParamsLoaded = false;
                        external.Text = path;
                    }, flowsheet));

            panel.CreateAndAddButtonRow("Open Spreadsheet in the Default Application", null,
                (btn, e) => OpenWorkbook(spreadsheet));

            // a new file has to be read before its variables show up
            panel.CreateAndAddButtonRow("Reload Variables from File", null, (btn, e) =>
            {
                spreadsheet.ParamsLoaded = false;
                flowsheet.UpdateOpenEditForms();
            });

            panel.CreateAndAddDropDownRow("Calculation Engine", Engines, (int)spreadsheet.CalculationEngine, (dd, e) =>
            {
                if (dd.SelectedIndex < 0 || dd.SelectedIndex >= Engines.Count) return;
                spreadsheet.CalculationEngine = (SpreadsheetCalculationEngine)dd.SelectedIndex;
            });

            panel.CreateAndAddDescriptionRow("Automatic uses Excel when it is installed (Windows) and the internal " +
                "engine otherwise. The internal engine reads .xlsx and .xlsm workbooks and implements the common " +
                "worksheet functions; it does not run macros, add-ins or iterative calculation.");

            Apply();
        }

        /// <summary>
        /// Opens the workbook in the application the system associates with it. An embedded workbook
        /// is copied to a temporary file first, and changes made to that copy stay there.
        /// </summary>
        private static void OpenWorkbook(SpreadsheetUO spreadsheet)
        {
            var flowsheet = spreadsheet.GetFlowsheet();
            try
            {
                string path;
                if (spreadsheet.FileIsEmbedded)
                {
                    var name = spreadsheet.EmbeddedFileName;
                    if (string.IsNullOrEmpty(name) || !flowsheet.FileDatabaseProvider.CheckIfExists(name))
                    {
                        flowsheet.ShowMessage("No embedded spreadsheet is selected.", IFlowsheet.MessageType.GeneralError);
                        return;
                    }
                    path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                        Guid.NewGuid().ToString("N").Substring(0, 8) + "_" + System.IO.Path.GetFileName(name));
                    flowsheet.FileDatabaseProvider.ExportFile(name, path);
                    flowsheet.ShowMessage("The embedded spreadsheet was copied to " + path +
                        ". Changes made to that copy do not go back into the flowsheet.", IFlowsheet.MessageType.Information);
                }
                else
                {
                    path = spreadsheet.Filename;
                    if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                    {
                        flowsheet.ShowMessage("The spreadsheet file '" + path + "' does not exist.", IFlowsheet.MessageType.GeneralError);
                        return;
                    }
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                spreadsheet.ParamsLoaded = false;
            }
            catch (Exception ex)
            {
                flowsheet?.ShowMessage("The spreadsheet could not be opened: " + ex.Message, IFlowsheet.MessageType.GeneralError);
            }
        }

        private static Control Grid(SpreadsheetUO spreadsheet,
                                    Dictionary<string, ExcelParameter> parameters, bool readOnly)
        {
            var nf = spreadsheet.GetFlowsheet().FlowsheetOptions.NumberFormat;
            var rows = new ObservableCollection<ParameterRow>();

            if (parameters != null)
                foreach (var parameter in parameters.Values)
                    rows.Add(new ParameterRow(parameter, nf, readOnly));

            var grid = new DataGrid
            {
                ItemsSource = rows,
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                IsReadOnly = readOnly,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Height = 200
            };

            grid.Columns.Add(GridColumns.Text("Name", "Name", 1.6, readOnly: true));
            grid.Columns.Add(GridColumns.Text("Value", "Value", 1.0, readOnly));
            grid.Columns.Add(GridColumns.Text("Unit", "Unit", 0.8, readOnly: true));

            return grid;
        }

    }

    /// <summary>
    /// Sub-flowsheet editor: the simulation it runs and the variables mapped in and out of it.
    /// </summary>
    public static class FlowsheetUOEditor
    {

        private sealed class LinkRow
        {
            public string Name { get; set; } = "";
            public string Variable { get; set; } = "";
            public string Value { get; set; } = "";
            public string Unit { get; set; } = "";
        }

        public static Control Build(FlowsheetUO flowsheet)
        {
            return UnitOpEditor.Build(flowsheet,
                input: panel => BuildFile(flowsheet, panel),
                propertyPackage: false,
                extras: new[]
                {
                    ("Input Variables", Grid(flowsheet, flowsheet.InputParams)),
                    ("Output Variables", Grid(flowsheet, flowsheet.OutputParams))
                });
        }

        private static void BuildFile(FlowsheetUO uo, AvaloniaEditorPanel panel)
        {
            var parent = uo.GetFlowsheet();

            ComboBox embedded = null;
            TextBox external = null;

            void Apply()
            {
                if (embedded != null) embedded.IsEnabled = uo.FileIsEmbedded;
                if (external != null) external.IsEnabled = !uo.FileIsEmbedded;
            }

            panel.CreateAndAddCheckBoxRow("Use Embedded File", uo.FileIsEmbedded, (cb, e) =>
            {
                uo.FileIsEmbedded = cb.IsChecked.GetValueOrDefault();
                Apply();
            });

            var files = new List<string>();
            try { files.AddRange(parent.FileDatabaseProvider.GetFiles()); }
            catch (Exception) { }

            embedded = panel.CreateAndAddDropDownRow("Embedded File", files,
                Math.Max(0, files.IndexOf(uo.EmbeddedFileName ?? "")), (dd, e) =>
                {
                    if (dd.SelectedIndex < 0 || dd.SelectedIndex >= files.Count) return;
                    uo.EmbeddedFileName = files[dd.SelectedIndex];
                });

            external = panel.CreateAndAddStringEditorRow("Simulation File", uo.SimulationFile, null);
            external.IsReadOnly = true;

            panel.CreateAndAddButtonRow("Browse...", null, (btn, e) =>
                EditorFilePicker.Open(panel, "Select the Simulation File",
                    new[] { "*.dwxml", "*.dwxmz" },
                    path =>
                    {
                        uo.SimulationFile = path;
                        external.Text = path;
                    }, parent));

            panel.CreateAndAddCheckBoxRow("Initialize on Load", uo.InitializeOnLoad,
                (cb, e) => uo.InitializeOnLoad = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddCheckBoxRow("Redirect Output", uo.RedirectOutput,
                (cb, e) => uo.RedirectOutput = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddCheckBoxRow("Update Process Data on Save", uo.UpdateOnSave,
                (cb, e) => uo.UpdateOnSave = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddTwoLabelsRow("Initialized", uo.Initialized ? "Yes" : "No");

            Apply();
        }

        /// <summary>
        /// The mapped variables. They point at objects inside the sub-flowsheet, so the rows only
        /// fill in once it has been loaded.
        /// </summary>
        private static Control Grid(FlowsheetUO uo,
            Dictionary<string, DWSIM.UnitOperations.UnitOperations.Auxiliary.FlowsheetUOParameter> parameters)
        {
            var parent = uo.GetFlowsheet();
            var su = parent.FlowsheetOptions.SelectedUnitSystem;
            var nf = parent.FlowsheetOptions.NumberFormat;

            var rows = new ObservableCollection<LinkRow>();

            if (uo.Fsheet != null && parameters != null)
            {
                foreach (var entry in parameters)
                {
                    var parameter = entry.Value;
                    if (!uo.Fsheet.SimulationObjects.ContainsKey(parameter.ObjectID)) continue;

                    var obj = uo.Fsheet.SimulationObjects[parameter.ObjectID];

                    var value = "";
                    try
                    {
                        value = Convert.ToDouble(obj.GetPropertyValue(parameter.ObjectProperty, su))
                                       .ToString(nf, CultureInfo.CurrentCulture);
                    }
                    catch (Exception)
                    {
                    }

                    rows.Add(new LinkRow
                    {
                        Name = entry.Key,
                        Variable = obj.GraphicObject.Tag + ", " + parameter.ObjectProperty,
                        Value = value,
                        Unit = obj.GetPropertyUnit(parameter.ObjectProperty, su)
                    });
                }
            }

            var grid = new DataGrid
            {
                ItemsSource = rows,
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Height = 200
            };

            grid.Columns.Add(GridColumns.Text("Name", "Name", 1.0, readOnly: true));
            grid.Columns.Add(GridColumns.Text("Variable", "Variable", 2.0, readOnly: true));
            grid.Columns.Add(GridColumns.Text("Value", "Value", 1.0, readOnly: true));
            grid.Columns.Add(GridColumns.Text("Unit", "Unit", 0.8, readOnly: true));

            return grid;
        }

    }

    /// <summary>
    /// Script unit operation editor: the engine that runs the script and the variables it reads
    /// and writes. The script itself is edited in the window the object opens.
    /// </summary>
    /// <remarks>
    /// The input variables (numbers and text) can be added, removed, renamed and edited, as in the
    /// Windows editor. The output variables are what the last run of the script produced, so they
    /// are read-only there too.
    /// </remarks>
    public static class ScriptUOEditor
    {

        /// <summary>The names the script host defines itself, which an input variable would hide.</summary>
        private static readonly HashSet<string> ReservedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Flowsheet", "flowsheet", "Me", "me", "This", "this",
            "ims1", "ims2", "ims3", "ims4", "ims5", "ims6",
            "oms1", "oms2", "oms3", "oms4", "oms5", "oms6",
            "ies1", "oes1"
        };

        private static readonly System.Text.RegularExpressions.Regex Identifier =
            new System.Text.RegularExpressions.Regex("^[A-Za-z_][A-Za-z0-9_]*$");

        /// <summary>One variable of a list: its name and its value, as the grid edits them.</summary>
        private sealed class VariableRow : INotifyPropertyChanged
        {
            private readonly VariableList _list;

            public VariableRow(VariableList list, string key)
            {
                _list = list;
                Key = key;
            }

            internal string Key;

            public string Name
            {
                get { return Key; }
                set { _list.Rename(this, value); }
            }

            public string Value
            {
                get { return _list.Format(Key); }
                set { _list.SetValue(this, value); }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            internal void Raise(string name)
            {
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
            }

            /// <summary>
            /// Shows the stored value again after an edit was refused. Deferred, so the grid reads it
            /// once it has finished writing the text the user typed.
            /// </summary>
            internal void Revert(string name)
            {
                Dispatcher.UIThread.Post(() => Raise(name));
            }
        }

        /// <summary>
        /// The rows of one dictionary of the script, numbers or text, and the edits made to it.
        /// </summary>
        private abstract class VariableList
        {
            protected readonly PythonScriptUO Uo;
            public readonly ObservableCollection<VariableRow> Rows = new ObservableCollection<VariableRow>();
            public readonly TextBlock Message = new TextBlock
            {
                Foreground = Brushes.Red,
                TextWrapping = TextWrapping.Wrap,
                IsVisible = false,
                Margin = new Thickness(2, 4, 0, 0)
            };

            /// <summary>The other list of inputs, whose names share the script scope with this one.</summary>
            public VariableList Other;

            /// <summary>Runs after a change is stored: solves the object and refreshes the editors.</summary>
            public Action Changed;

            protected VariableList(PythonScriptUO uo)
            {
                Uo = uo;
            }

            public abstract string Kind { get; }
            public abstract IEnumerable<string> Keys { get; }
            public abstract bool Contains(string key);
            public abstract string Format(string key);
            protected abstract bool TryStore(string key, string text);
            protected abstract void RenameKey(string oldKey, string newKey);
            protected abstract void AddKey(string key);
            protected abstract void RemoveKey(string key);

            public void Load()
            {
                Rows.Clear();
                foreach (var key in Keys.ToList()) Rows.Add(new VariableRow(this, key));
            }

            public void Show(string message)
            {
                Message.Text = message ?? "";
                Message.IsVisible = !string.IsNullOrEmpty(message);
            }

            private void Snapshot()
            {
                Uo.GetFlowsheet()?.RegisterSnapshot(DWSIM.Interfaces.Enums.SnapshotType.ObjectData, Uo);
            }

            public void SetValue(VariableRow row, string text)
            {
                if (!Contains(row.Key)) return;
                if (Format(row.Key) == (text ?? "")) { Show(null); return; }

                Snapshot();
                if (!TryStore(row.Key, text))
                {
                    Show("'" + text + "' is not a number. The value of " + row.Key + " was kept.");
                    row.Revert("Value");
                    return;
                }

                Show(null);
                row.Raise("Value");
                Changed?.Invoke();
            }

            public void Rename(VariableRow row, string text)
            {
                var name = (text ?? "").Trim();
                if (name == row.Key) { Show(null); return; }

                var problem = Check(name);
                if (problem != null)
                {
                    Show(problem + " The name " + row.Key + " was kept.");
                    row.Revert("Name");
                    return;
                }

                Snapshot();
                RenameKey(row.Key, name);
                row.Key = name;
                Show(null);
                row.Raise("Name");
                row.Raise("Value");
                Changed?.Invoke();
            }

            /// <summary>Why a name cannot be used, or null when it can.</summary>
            private string Check(string name)
            {
                if (name.Length == 0)
                    return "The name cannot be empty.";
                if (!Identifier.IsMatch(name))
                    return "'" + name + "' is not a valid name. Use letters, digits and underscores, starting with a letter or an underscore.";
                if (ReservedNames.Contains(name))
                    return "'" + name + "' is a name the script already uses for the flowsheet or a connected stream.";
                if (Contains(name))
                    return "There is already a " + Kind + " input variable named '" + name + "'.";
                if (Other != null && Other.Contains(name))
                    return "There is already a " + Other.Kind + " input variable named '" + name + "'.";
                return null;
            }

            /// <summary>Adds a variable with the next free name, at the end of the list.</summary>
            public VariableRow Add()
            {
                var i = Rows.Count + 1;
                string name;
                do { name = "var" + i++; }
                while (Contains(name) || (Other != null && Other.Contains(name)));

                Snapshot();
                AddKey(name);
                var row = new VariableRow(this, name);
                Rows.Add(row);
                Show(null);
                return row;
            }

            public void Remove(VariableRow row)
            {
                if (row == null || !Contains(row.Key)) return;
                Snapshot();
                RemoveKey(row.Key);
                Rows.Remove(row);
                Show(null);
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Renames a key in place: the dictionary is rebuilt in the same order, so the renamed
        /// variable keeps its position and the script object keeps the same dictionary.
        /// </summary>
        private static void RenameInPlace<T>(Dictionary<string, T> values, string oldKey, string newKey)
        {
            var pairs = values.ToList();
            values.Clear();
            foreach (var p in pairs)
                values.Add(p.Key == oldKey ? newKey : p.Key, p.Value);
        }

        private sealed class NumberList : VariableList
        {
            private readonly Func<Dictionary<string, double>> _values;
            private readonly string _nf;

            public NumberList(PythonScriptUO uo, Func<Dictionary<string, double>> values) : base(uo)
            {
                _values = values;
                _nf = uo.GetFlowsheet().FlowsheetOptions.NumberFormat;
            }

            private Dictionary<string, double> Values { get { return _values() ?? new Dictionary<string, double>(); } }

            public override string Kind { get { return "number"; } }
            public override IEnumerable<string> Keys { get { return Values.Keys; } }
            public override bool Contains(string key) { return Values.ContainsKey(key); }

            public override string Format(string key)
            {
                return Values.TryGetValue(key, out var v) ? v.ToString(_nf, CultureInfo.CurrentCulture) : "";
            }

            protected override bool TryStore(string key, string text)
            {
                if (!UnitOpEditorRows.TryParse(text, out var v)) return false;
                Values[key] = v;
                return true;
            }

            protected override void RenameKey(string oldKey, string newKey) { RenameInPlace(Values, oldKey, newKey); }
            protected override void AddKey(string key) { Values.Add(key, 0.0); }
            protected override void RemoveKey(string key) { Values.Remove(key); }
        }

        private sealed class TextList : VariableList
        {
            public TextList(PythonScriptUO uo) : base(uo) { }

            private Dictionary<string, string> Values
            {
                get
                {
                    if (Uo.InputStringVariables == null) Uo.InputStringVariables = new Dictionary<string, string>();
                    return Uo.InputStringVariables;
                }
            }

            public override string Kind { get { return "text"; } }
            public override IEnumerable<string> Keys { get { return Values.Keys; } }
            public override bool Contains(string key) { return Values.ContainsKey(key); }
            public override string Format(string key) { return Values.TryGetValue(key, out var v) ? v ?? "" : ""; }

            protected override bool TryStore(string key, string text)
            {
                Values[key] = text ?? "";
                return true;
            }

            protected override void RenameKey(string oldKey, string newKey) { RenameInPlace(Values, oldKey, newKey); }
            protected override void AddKey(string key) { Values.Add(key, ""); }
            protected override void RemoveKey(string key) { Values.Remove(key); }
        }

        public static Control Build(PythonScriptUO uo)
        {
            if (uo.InputVariables == null) uo.InputVariables = new Dictionary<string, double>();
            if (uo.OutputVariables == null) uo.OutputVariables = new Dictionary<string, double>();

            var numbers = new NumberList(uo, () => uo.InputVariables);
            var texts = new TextList(uo);
            numbers.Other = texts;
            texts.Other = numbers;

            // an edited variable solves the object and refreshes the open editors, as the other
            // inputs of the editor do. Posted, so the grid finishes its commit before the editor
            // that holds it is rebuilt.
            Action changed = () => Dispatcher.UIThread.Post(() =>
            {
                var fs = uo.GetFlowsheet();
                if (fs == null) return;
                fs.RequestCalculation(uo);
                fs.UpdateInterface();
            });
            numbers.Changed = changed;
            texts.Changed = changed;

            var outputs = new NumberList(uo, () => uo.OutputVariables);

            return UnitOpEditor.Build(uo,
                input: panel =>
                {
                    panel.CreateAndAddDropDownRow("Execution Engine",
                        new List<string> { "IronPython", "Python.NET" }, (int)uo.ExecutionEngine,
                        (dd, e) =>
                        {
                            if (dd.SelectedIndex < 0) return;
                            uo.ExecutionEngine = (PythonScriptUO.PythonExecutionEngine)dd.SelectedIndex;
                        });

                    panel.CreateAndAddButtonRow("Edit Script", null, (btn, e) => ScriptEditorWindow.Show(uo));
                },
                extras: new[]
                {
                    ("Input (Numbers)", Editable(numbers, hint: true)),
                    ("Input (Text)", Editable(texts, hint: false)),
                    ("Output", ReadOnly(outputs))
                });
        }

        /// <summary>
        /// A list of input variables: the grid, the buttons that add and remove a variable, and the
        /// line that says why an edit was refused.
        /// </summary>
        private static Control Editable(VariableList list, bool hint)
        {
            list.Load();
            var grid = Grid(list.Rows, readOnly: false);

            var add = new Button { Content = "Add", MinWidth = 80 };
            var remove = new Button { Content = "Remove", MinWidth = 80, Margin = new Thickness(6, 0, 0, 0) };
            ToolTip.SetTip(add, "Adds a " + list.Kind + " input variable.");
            ToolTip.SetTip(remove, "Removes the selected variable.");

            void Refresh() => remove.IsEnabled = grid.SelectedItem is VariableRow;

            add.Click += (s, e) =>
            {
                var row = list.Add();
                grid.SelectedItem = row;
                grid.ScrollIntoView(row, grid.Columns[0]);
                grid.CurrentColumn = grid.Columns[0];
                grid.Focus();
                // the name is what the user types next
                Dispatcher.UIThread.Post(() => grid.BeginEdit(), DispatcherPriority.Background);
            };

            remove.Click += (s, e) =>
            {
                var row = grid.SelectedItem as VariableRow;
                if (row == null) return;
                var index = list.Rows.IndexOf(row);
                list.Remove(row);
                if (list.Rows.Count > 0)
                    grid.SelectedItem = list.Rows[Math.Min(index, list.Rows.Count - 1)];
                Refresh();
            };

            grid.SelectionChanged += (s, e) => Refresh();

            // Delete on a selected row, outside a cell being edited, removes the variable. Handled
            // here so the key does not reach the flowsheet, which deletes the selected object.
            grid.KeyDown += (s, e) =>
            {
                if (e.Source is TextBox) return;
                if (e.Key == Key.Delete)
                {
                    remove.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                }
                else if (e.Key == Key.Back)
                {
                    e.Handled = true;
                }
            };

            Refresh();

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 6, 0, 0)
            };
            buttons.Children.Add(add);
            buttons.Children.Add(remove);

            var stack = new StackPanel();
            stack.Children.Add(grid);
            stack.Children.Add(list.Message);
            stack.Children.Add(buttons);
            if (hint)
                stack.Children.Add(new TextBlock
                {
                    Text = "To edit a name or a value, double-click the cell, or select it and type. " +
                           "Press Enter or leave the cell to apply the change.",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.7,
                    Margin = new Thickness(2, 4, 0, 0)
                });
            return stack;
        }

        private static Control ReadOnly(VariableList list)
        {
            list.Load();
            var grid = Grid(list.Rows, readOnly: true);
            grid.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Delete || e.Key == Key.Back) e.Handled = true;
            };
            return grid;
        }

        private static DataGrid Grid(ObservableCollection<VariableRow> rows, bool readOnly)
        {
            var grid = new DataGrid
            {
                ItemsSource = rows,
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                CanUserReorderColumns = false,
                IsReadOnly = readOnly,
                SelectionMode = DataGridSelectionMode.Single,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Height = 180
            };

            grid.Columns.Add(GridColumns.Text("Name", "Name", 1.4, readOnly));
            grid.Columns.Add(GridColumns.Text("Value", "Value", 1.4, readOnly));

            if (!readOnly) TypeToEdit(grid);

            return grid;
        }

        /// <summary>
        /// Starts editing the current cell when the user types on it, as the Windows grid does; the
        /// grid on its own only edits on a double-click, a second click or F2. The typed text
        /// replaces the cell, and Enter or leaving the cell applies it.
        /// </summary>
        private static void TypeToEdit(DataGrid grid)
        {
            string pending = null;

            grid.PreparingCellForEdit += (s, e) =>
            {
                if (pending == null) return;
                var text = pending;
                pending = null;
                if (e.EditingElement is TextBox tb)
                {
                    tb.Text = text;
                    tb.CaretIndex = text.Length;
                    tb.ClearSelection();
                }
            };

            grid.AddHandler(InputElement.TextInputEvent, (s, e) =>
            {
                if (e.Handled || string.IsNullOrEmpty(e.Text)) return;
                if (e.Source is TextBox) return;
                if (grid.SelectedItem == null || grid.CurrentColumn == null || grid.CurrentColumn.IsReadOnly) return;
                if (char.IsControl(e.Text[0])) return;

                pending = e.Text;
                if (grid.BeginEdit()) e.Handled = true;
                pending = null;
            }, RoutingStrategies.Bubble);
        }

    }

    /// <summary>
    /// The script editor window for the Script unit operation. The Windows edition opens its own
    /// Scintilla form; the cross-platform build edits the script text here, in a plain code box that
    /// writes straight back to the object so nothing is lost when the window closes.
    /// </summary>
    internal static class ScriptEditorWindow
    {

        internal static void Show(PythonScriptUO uo)
        {
            var editor = new TextBox
            {
                Text = uo.ScriptText ?? "",
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Cascadia Mono,Consolas,Menlo,DejaVu Sans Mono,Courier New,monospace"),
                FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(13),
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(editor, ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollBarVisibility(editor, ScrollBarVisibility.Auto);

            editor.TextChanged += (s, e) => uo.ScriptText = editor.Text ?? "";

            var close = new Button { Content = "Close", Margin = new Thickness(4, 6, 4, 0) };

            var bar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            bar.Children.Add(close);

            var root = new DockPanel { Margin = new Thickness(6) };
            DockPanel.SetDock(bar, Dock.Bottom);
            root.Children.Add(bar);
            root.Children.Add(editor);

            var tag = uo.GraphicObject != null ? uo.GraphicObject.Tag : "Script";
            var window = AvaloniaCommon.GetDefaultEditorForm(tag + ": Edit Script", 820, 620, root, scrollable: false);
            close.Click += (s, e) => window.Close();
            window.Show();
        }

    }

    /// <summary>
    /// CAPE-OPEN unit operation editor: what the hosted object reports about itself, and the
    /// button that opens its own editor, which only exists on Windows.
    /// </summary>
    public static class CapeOpenUOEditor
    {

        public static Control Build(CapeOpenUO uo)
        {
            return UnitOpEditor.Build(uo,
                input: panel =>
                {
                    var info = uo._seluo;

                    panel.CreateAndAddTwoLabelsRow("CAPE-OPEN Object", info == null ? "N/A" : info.Name);
                    panel.CreateAndAddTwoLabelsRow("Description", info == null ? "N/A" : info.Description);
                    panel.CreateAndAddTwoLabelsRow("Object / CAPE-OPEN Version",
                        info == null ? "N/A" : info.Version + " / " + info.CapeVersion);

                    // the hosted object brings its own dialog, which is a COM window
                    var editor = panel.CreateAndAddButtonRow("Open CAPE-OPEN Object Editor", null,
                        (btn, e) =>
                        {
                            try
                            {
                                uo.Edit();
                                uo.UpdateConnectorPositions();
                                uo.GetFlowsheet().UpdateInterface();
                            }
                            catch (Exception ex)
                            {
                                uo.GetFlowsheet().ShowMessage(ex.Message, IFlowsheet.MessageType.GeneralError);
                            }
                        });

                    editor.IsEnabled = Environment.OSVersion.Platform == PlatformID.Win32NT;

                    if (!editor.IsEnabled)
                        panel.CreateAndAddDescriptionRow(
                            "The hosted object's editor is only available on Windows.");

                    panel.CreateAndAddButtonRow("Restore Parameters", null, (btn, e) =>
                    {
                        try { uo.RestoreParams(); }
                        catch (Exception ex)
                        {
                            uo.GetFlowsheet().ShowMessage(ex.Message, IFlowsheet.MessageType.GeneralError);
                        }
                    });
                });
        }

    }


    /// <summary>The open dialog these editors share for the files they point at.</summary>
}
