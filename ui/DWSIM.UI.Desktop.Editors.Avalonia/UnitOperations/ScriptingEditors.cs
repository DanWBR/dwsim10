using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
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
    public static class ScriptUOEditor
    {

        private sealed class NumberRow : INotifyPropertyChanged
        {
            private readonly Dictionary<string, double> _values;
            private readonly string _key;
            private readonly string _nf;
            private readonly bool _readOnly;

            public NumberRow(Dictionary<string, double> values, string key, string nf, bool readOnly)
            {
                _values = values;
                _key = key;
                _nf = nf;
                _readOnly = readOnly;
            }

            public string Name { get { return _key; } }

            public string Value
            {
                get { return _values[_key].ToString(_nf, CultureInfo.CurrentCulture); }
                set
                {
                    if (_readOnly) return;
                    if (!UnitOpEditorRows.TryParse(value, out var v)) return;
                    _values[_key] = v;
                    Raise("Value");
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise(string name)
            {
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        private sealed class TextRow : INotifyPropertyChanged
        {
            private readonly Dictionary<string, string> _values;
            private readonly string _key;

            public TextRow(Dictionary<string, string> values, string key)
            {
                _values = values;
                _key = key;
            }

            public string Name { get { return _key; } }

            public string Value
            {
                get { return _values[_key]; }
                set { _values[_key] = value ?? ""; Raise("Value"); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise(string name)
            {
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public static Control Build(PythonScriptUO uo)
        {
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
                    ("Input (Numbers)", Numbers(uo, uo.InputVariables, readOnly: false)),
                    ("Input (Text)", Texts(uo, uo.InputStringVariables)),
                    ("Output", Numbers(uo, uo.OutputVariables, readOnly: true))
                });
        }

        private static Control Numbers(PythonScriptUO uo, Dictionary<string, double> values, bool readOnly)
        {
            var nf = uo.GetFlowsheet().FlowsheetOptions.NumberFormat;
            var rows = new ObservableCollection<NumberRow>();

            if (values != null)
                foreach (var key in values.Keys.ToList())
                    rows.Add(new NumberRow(values, key, nf, readOnly));

            return Grid(rows, readOnly);
        }

        private static Control Texts(PythonScriptUO uo, Dictionary<string, string> values)
        {
            var rows = new ObservableCollection<TextRow>();

            if (values != null)
                foreach (var key in values.Keys.ToList())
                    rows.Add(new TextRow(values, key));

            return Grid(rows, readOnly: false);
        }

        private static Control Grid(System.Collections.IEnumerable rows, bool readOnly)
        {
            var grid = new DataGrid
            {
                ItemsSource = rows,
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                IsReadOnly = readOnly,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Height = 180
            };

            grid.Columns.Add(GridColumns.Text("Name", "Name", 1.4, readOnly: true));
            grid.Columns.Add(GridColumns.Text("Value", "Value", 1.4, readOnly));

            return grid;
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
