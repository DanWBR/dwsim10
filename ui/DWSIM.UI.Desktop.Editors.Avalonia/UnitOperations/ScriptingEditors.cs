using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using DWSIM.Interfaces;
using DWSIM.SharedClassesCSharp.FilePicker;
using DWSIM.UI.Shared.Avalonia;
using CapeOpenUO = DWSIM.UnitOperations.UnitOperations.CapeOpenUO;
using FlowsheetUO = DWSIM.UnitOperations.UnitOperations.Flowsheet;
using PythonScriptUO = DWSIM.UnitOperations.UnitOperations.CustomUO;

namespace DWSIM.UI.Desktop.Editors
{

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
            {
                var picked = FileRows.Pick("Simulation Files", new[] { "*.dwxml", "*.dwxmz" });
                if (picked == null) return;

                uo.SimulationFile = picked;
                external.Text = picked;
            });

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

                    panel.CreateAndAddButtonRow("Edit Script", null, (btn, e) => uo.DisplayScriptEditorForm());
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
    internal static class FileRows
    {

        internal static string Pick(string description, string[] patterns)
        {
            try
            {
                var picker = FilePickerService.GetInstance().GetFilePicker();
                var handler = picker.ShowOpenDialog(new List<FilePickerAllowedType>
                {
                    new FilePickerAllowedType(description, patterns)
                });

                return handler == null ? null : handler.Filename;
            }
            catch (Exception)
            {
                return null;
            }
        }

    }

}
