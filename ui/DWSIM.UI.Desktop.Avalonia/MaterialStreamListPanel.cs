using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.Interfaces.Enums.GraphicObjects;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// The material streams of the simulation side by side, as the Windows panel shows them: one
/// column per stream, one row per property, the property name and its unit on the left, and the
/// properties that define a feed stream open for editing.
/// </summary>
public sealed class MaterialStreamListPanel : DockPanel
{

    /// <summary>
    /// One property across every stream. The values are read once per refresh, so the grid holds
    /// plain strings and the cells are built from them.
    /// </summary>
    private sealed class PropertyRow
    {
        // the two leftmost columns are bound by name, so they have to be properties
        public string Property { get; set; } = "";
        public string Units { get; set; } = "";

        /// <summary>The name the stream reads and writes the property by, PROP_MS_x.</summary>
        public string Key = "";

        public string[] Values = Array.Empty<string>();
        public bool[] Editable = Array.Empty<bool>();
    }

    private static readonly string[] OrderByOptions =
    {
        "Name (Asc)", "Name (Desc)", "Type",
        "Temperature (Asc)", "Temperature (Desc)",
        "Pressure (Asc)", "Pressure (Desc)",
        "Mass Flow (Asc)", "Mass Flow (Desc)",
        "Density (Asc)", "Density (Desc)"
    };

    private static readonly IBrush EditableBrush = new SolidColorBrush(Color.FromRgb(0, 0, 200));

    private readonly DataGrid _grid;
    private readonly ComboBox _cbOrderBy;
    private readonly TextBlock _lblLastUpdate;

    private IFlowsheet? _flowsheet;

    private readonly ObservableCollection<PropertyRow> _rows = new();
    private List<ISimulationObject> _streams = new();

    /// <summary>Keeps a commit from being reapplied while the grid is being rebuilt.</summary>
    private bool _updating;

    public MaterialStreamListPanel()
    {
        _lblLastUpdate = new TextBlock
        {
            Text = "Updated on: --",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
            Margin = new Thickness(8, 0, 4, 0)
        };

        var lblOrderBy = new TextBlock
        {
            Text = "Order By",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
            Margin = new Thickness(4, 0)
        };

        _cbOrderBy = new ComboBox
        {
            ItemsSource = OrderByOptions,
            SelectedIndex = 0,
            FontSize = 11,
            MinWidth = 150,
            VerticalAlignment = VerticalAlignment.Center
        };
        _cbOrderBy.SelectionChanged += (_, _) => { if (!_updating) UpdateList(); };

        var toolbar = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = 2,
            Margin = new Thickness(4)
        };

        toolbar.Children.Add(lblOrderBy);
        toolbar.Children.Add(_cbOrderBy);
        toolbar.Children.Add(ToolButton("Refresh", (_, _) => UpdateList()));
        toolbar.Children.Add(ToolButton("Copy Data to Clipboard", async (_, _) => await CopyAsync()));
        toolbar.Children.Add(ToolButton("Export to Spreadsheet", async (_, _) => await ExportAsync()));
        toolbar.Children.Add(_lblLastUpdate);

        SetDock(toolbar, global::Avalonia.Controls.Dock.Top);
        Children.Add(toolbar);

        _grid = new DataGrid
        {
            ItemsSource = _rows,
            AutoGenerateColumns = false,
            CanUserSortColumns = false,
            CanUserResizeColumns = true,
            IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            FontSize = 11,
            Margin = new Thickness(4)
        };

        Children.Add(_grid);
    }

    private static Button ToolButton(string text, EventHandler<global::Avalonia.Interactivity.RoutedEventArgs> handler)
    {
        var button = new Button
        {
            Content = text,
            FontSize = 11,
            Padding = new Thickness(8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };
        button.Classes.Add("panel");
        button.Click += handler;
        return button;
    }

    /// <summary>Set or replace the flowsheet reference.</summary>
    public void SetFlowsheet(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
    }

    // -------------------------------------------------------------------------
    // Table
    // -------------------------------------------------------------------------

    /// <summary>Rebuilds the table from the material streams currently on the flowsheet.</summary>
    public void UpdateList()
    {
        if (_flowsheet == null) return;

        _updating = true;

        try
        {
            var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
            var nf = _flowsheet.FlowsheetOptions.NumberFormat;

            _streams = Sort(_flowsheet.SimulationObjects.Values
                .Where(x => x.GraphicObject != null &&
                            x.GraphicObject.ObjectType == ObjectType.MaterialStream)).ToList();

            // the type follows the connections and is otherwise only refreshed while solving; the
            // panel reads it to tell a feed from a result, so it is brought up to date here
            foreach (var stream in _streams)
            {
                try { (stream as IMaterialStream)?.UpdateStreamType(); }
                catch (Exception) { }
            }

            _rows.Clear();
            _grid.Columns.Clear();

            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Property / Streams",
                Binding = new global::Avalonia.Data.Binding("Property"),
                IsReadOnly = true,
                Width = new DataGridLength(240)
            });

            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Units",
                Binding = new global::Avalonia.Data.Binding("Units"),
                IsReadOnly = true,
                Width = new DataGridLength(80)
            });

            // the two leftmost columns stay in place while the streams scroll past them
            _grid.FrozenColumnCount = 2;

            if (_streams.Count == 0)
            {
                _lblLastUpdate.Text = "Updated on: " + DateTime.Now.ToString("HH:mm:ss") +
                                      " (no material streams)";
                return;
            }

            var props = _streams[0].GetProperties(PropertyType.ALL);

            var typeRow = new PropertyRow
            {
                Property = "Type",
                Values = new string[_streams.Count],
                Editable = new bool[_streams.Count]
            };

            for (int j = 0; j < _streams.Count; j++)
                typeRow.Values[j] = (_streams[j] as IMaterialStream)?.StreamType.ToString() ?? "";

            _rows.Add(typeRow);

            foreach (var prop in props)
            {
                var row = new PropertyRow
                {
                    Key = prop,
                    Property = _flowsheet.GetTranslatedString(prop),
                    Values = new string[_streams.Count],
                    Editable = new bool[_streams.Count]
                };

                for (int j = 0; j < _streams.Count; j++)
                {
                    try
                    {
                        row.Units = _streams[j].GetPropertyUnit(prop, su);

                        var value = _streams[j].GetPropertyValue(prop, su);
                        var text = value?.ToString() ?? "";

                        if (double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var d))
                            row.Values[j] = double.IsNaN(d) || double.IsInfinity(d) ? "" : d.ToString(nf);
                        else
                            row.Values[j] = text;
                    }
                    catch (Exception)
                    {
                        row.Values[j] = "";
                    }

                    row.Editable[j] = IsSpecified(_streams[j] as IMaterialStream, prop);
                }

                _rows.Add(row);
            }

            for (int j = 0; j < _streams.Count; j++)
            {
                _grid.Columns.Add(new DataGridTemplateColumn
                {
                    Header = _streams[j].GraphicObject.Tag,
                    Width = new DataGridLength(150),
                    CellTemplate = CellTemplate(j)
                });
            }

            _lblLastUpdate.Text = "Updated on: " + DateTime.Now.ToString("HH:mm:ss");
        }
        finally
        {
            _updating = false;
        }
    }

    private IEnumerable<ISimulationObject> Sort(IEnumerable<ISimulationObject> streams)
    {
        double Density(ISimulationObject x)
        {
            var phase = (x as IMaterialStream)?.Phases[0];
            return phase?.Properties.density.GetValueOrDefault() ?? 0.0;
        }

        return _cbOrderBy.SelectedIndex switch
        {
            1 => streams.OrderByDescending(x => x.GraphicObject.Tag),
            2 => streams.OrderBy(x => (x as IMaterialStream)?.StreamType),
            3 => streams.OrderBy(x => (x as IMaterialStream)?.GetTemperature()),
            4 => streams.OrderByDescending(x => (x as IMaterialStream)?.GetTemperature()),
            5 => streams.OrderBy(x => (x as IMaterialStream)?.GetPressure()),
            6 => streams.OrderByDescending(x => (x as IMaterialStream)?.GetPressure()),
            7 => streams.OrderBy(x => (x as IMaterialStream)?.GetMassFlow()),
            8 => streams.OrderByDescending(x => (x as IMaterialStream)?.GetMassFlow()),
            9 => streams.OrderBy(Density),
            10 => streams.OrderByDescending(Density),
            _ => streams.OrderBy(x => x.GraphicObject.Tag)
        };
    }

    /// <summary>
    /// Whether the property is one the user defines on that stream: only a feed or an incoming
    /// recycle is defined by hand, and only through the two properties its specification names
    /// plus the flow it is defined by.
    /// </summary>
    private static bool IsSpecified(IMaterialStream? stream, string property)
    {
        if (stream == null) return false;
        if (stream.StreamType != StreamType.Feed && stream.StreamType != StreamType.Recycle_In)
            return false;

        var specified = stream.SpecType switch
        {
            StreamSpec.Temperature_and_Pressure => property == "PROP_MS_0" || property == "PROP_MS_1",
            StreamSpec.Pressure_and_Enthalpy => property == "PROP_MS_7" || property == "PROP_MS_1",
            StreamSpec.Pressure_and_Entropy => property == "PROP_MS_8" || property == "PROP_MS_1",
            StreamSpec.Pressure_and_VaporFraction => property == "PROP_MS_27" || property == "PROP_MS_1",
            StreamSpec.Temperature_and_VaporFraction => property == "PROP_MS_27" || property == "PROP_MS_0",
            _ => false
        };

        if (specified) return true;

        return stream.DefinedFlow switch
        {
            FlowSpec.Mass => property == "PROP_MS_2",
            FlowSpec.Mole => property == "PROP_MS_3",
            FlowSpec.Volumetric => property == "PROP_MS_4",
            _ => false
        };
    }

    // -------------------------------------------------------------------------
    // Cells
    // -------------------------------------------------------------------------

    /// <summary>
    /// The cells of one stream. What the user can define is a box; everything else is a result,
    /// and reads as text.
    /// </summary>
    private IDataTemplate CellTemplate(int index)
    {
        return new FuncDataTemplate<PropertyRow>((row, _) =>
        {
            if (row == null || index >= row.Values.Length) return new TextBlock();

            if (!row.Editable[index])
            {
                return new TextBlock
                {
                    Text = row.Values[index],
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0)
                };
            }

            var box = new TextBox
            {
                Text = row.Values[index],
                Foreground = EditableBrush,
                TextAlignment = TextAlignment.Right,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Padding = new Thickness(4, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            // the cell takes the focus for itself once the grid sees the click, so the box keeps
            // the press to itself; the caret is already placed by then
            box.AddHandler(InputElement.PointerPressedEvent, (_, e) => e.Handled = true,
                global::Avalonia.Interactivity.RoutingStrategies.Bubble);

            box.LostFocus += (_, _) => Commit(row, index, box.Text);
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) Commit(row, index, box.Text);
            };

            return box;
        }, supportsRecycling: false);
    }

    /// <summary>Writes the typed value back to the stream and solves, as the Windows panel does.</summary>
    private void Commit(PropertyRow row, int index, string? text)
    {
        if (_updating || _flowsheet == null) return;
        if (index >= _streams.Count) return;
        if (text == row.Values[index]) return;

        if (!double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var value)) return;

        try
        {
            _updating = true;
            _streams[index].SetPropertyValue(row.Key, cv.ConvertToSI(row.Units, value));
        }
        catch (Exception ex)
        {
            _flowsheet.ShowMessage(ex.Message, IFlowsheet.MessageType.GeneralError);
            return;
        }
        finally
        {
            _updating = false;
        }

        _flowsheet.RequestCalculationAndWait();

        UpdateList();
    }

    // -------------------------------------------------------------------------
    // Copy and export
    // -------------------------------------------------------------------------

    private string BuildTable(char separator)
    {
        var sb = new StringBuilder();

        sb.Append("Property / Streams").Append(separator).Append("Units");
        foreach (var stream in _streams) sb.Append(separator).Append(stream.GraphicObject.Tag);
        sb.AppendLine();

        foreach (var row in _rows)
        {
            sb.Append(row.Property).Append(separator).Append(row.Units);
            foreach (var value in row.Values) sb.Append(separator).Append(value);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private async System.Threading.Tasks.Task CopyAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;

        await clipboard.SetTextAsync(BuildTable('\t'));
    }

    private async System.Threading.Tasks.Task ExportAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Material Streams",
            SuggestedFileName = "material streams.csv",
            DefaultExtension = "csv",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Comma-separated values") { Patterns = new[] { "*.csv" } }
            }
        });

        var path = file?.Path?.LocalPath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            File.WriteAllText(path, BuildTable(','), Encoding.UTF8);
            _flowsheet?.ShowMessage("Material streams exported to " + Path.GetFileName(path) + ".",
                IFlowsheet.MessageType.Information);
        }
        catch (Exception ex)
        {
            _flowsheet?.ShowMessage("Could not export the table: " + ex.Message,
                IFlowsheet.MessageType.GeneralError);
        }
    }

}
