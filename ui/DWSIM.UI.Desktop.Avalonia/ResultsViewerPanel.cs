using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// The results of one object at a time: the objects of the simulation on the left, and on the
/// right its properties as a grid, with the written report beside it.
/// </summary>
public sealed class ResultsViewerPanel : DockPanel
{

    /// <summary>One property of the selected object, read once per refresh.</summary>
    private sealed class ResultRow
    {
        public string Property { get; set; } = "";
        public string Value { get; set; } = "";
        public string Units { get; set; } = "";
    }

    private static readonly string[] ShowOptions = { "Results", "Inputs", "All Properties" };

    private static readonly PropertyType[] ShowTypes =
    {
        PropertyType.RO, PropertyType.WR, PropertyType.ALL
    };

    private readonly ComboBox _cbShow;
    private readonly ListBox _lbObjects;
    private readonly DataGrid _grid;
    private readonly TextBox _txtResults;
    private readonly TextBlock _lblLastCalc;

    private readonly ObservableCollection<ResultRow> _rows = new();

    private IFlowsheet? _flowsheet;

    public ResultsViewerPanel()
    {
        _lblLastCalc = new TextBlock
        {
            Text = "Last successful flowsheet calculation: --",
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 4, 0)
        };

        // wraps, so the timestamp moves to a second line instead of being cut off when the
        // panel is narrow
        var toolbar = new WrapPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Margin = new Thickness(4)
        };

        toolbar.Children.Add(new TextBlock
        {
            Text = "Select Object / View Results",
            FontWeight = FontWeight.SemiBold,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0)
        });

        _cbShow = new ComboBox
        {
            ItemsSource = ShowOptions,
            SelectedIndex = 0,
            FontSize = 11,
            MinWidth = 130,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };
        _cbShow.SelectionChanged += (_, _) => ShowSelected();

        toolbar.Children.Add(new TextBlock
        {
            Text = "Show",
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        });
        toolbar.Children.Add(_cbShow);

        toolbar.Children.Add(ToolButton("Refresh", (_, _) => { UpdateList(); ShowSelected(); }));
        toolbar.Children.Add(ToolButton("Copy Data to Clipboard", async (_, _) => await CopyAsync()));
        toolbar.Children.Add(ToolButton("Export to Spreadsheet", async (_, _) => await ExportAsync()));
        toolbar.Children.Add(_lblLastCalc);

        SetDock(toolbar, global::Avalonia.Controls.Dock.Top);
        Children.Add(toolbar);

        _lbObjects = new ListBox
        {
            Width = 250,
            FontSize = 11
        };
        _lbObjects.SelectionChanged += OnSelectionChanged;

        SetDock(_lbObjects, global::Avalonia.Controls.Dock.Left);
        Children.Add(_lbObjects);

        _grid = new DataGrid
        {
            ItemsSource = _rows,
            AutoGenerateColumns = false,
            CanUserSortColumns = false,
            IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            FontSize = 11
        };

        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Property",
            Binding = new Binding("Property"),
            IsReadOnly = true,
            Width = new DataGridLength(2.2, DataGridLengthUnitType.Star)
        });

        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding("Value"),
            IsReadOnly = true,
            Width = new DataGridLength(1.4, DataGridLengthUnitType.Star)
        });

        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Units",
            Binding = new Binding("Units"),
            IsReadOnly = true,
            Width = new DataGridLength(1.0, DataGridLengthUnitType.Star)
        });

        // the written report says things a flat property list cannot, so it stays alongside
        _txtResults = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
            FontSize = 11,
            TextWrapping = TextWrapping.NoWrap
        };

        var tabs = new TabControl { Margin = new Thickness(4) };

        tabs.Items.Add(new TabItem { Header = "Properties", Content = _grid });
        tabs.Items.Add(new TabItem
        {
            Header = "Report",
            Content = new ScrollViewer
            {
                HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = _txtResults
            }
        });

        Children.Add(tabs);
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

    public void SetFlowsheet(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
    }

    /// <summary>Rebuilds the object list, keeping the object the user was looking at.</summary>
    public void UpdateList()
    {
        if (_flowsheet == null) return;

        _lblLastCalc.Text = "Last successful flowsheet calculation: " + DateTime.Now.ToString("HH:mm:ss");

        var selected = (_lbObjects.SelectedItem as ListBoxItem)?.Tag as string;

        _lbObjects.SelectionChanged -= OnSelectionChanged;
        _lbObjects.Items.Clear();

        var objects = _flowsheet.SimulationObjects.Values
            .Where(x => x.GraphicObject != null)
            .OrderBy(x => x.GetDisplayName())
            .ThenBy(x => x.GraphicObject.Tag);

        foreach (var obj in objects)
        {
            _lbObjects.Items.Add(new ListBoxItem
            {
                Content = obj.GraphicObject.Tag + "  (" + obj.GetDisplayName() + ")",
                Tag = obj.Name
            });
        }

        _lbObjects.SelectionChanged += OnSelectionChanged;

        if (selected == null) return;

        foreach (var item in _lbObjects.Items.OfType<ListBoxItem>())
        {
            if ((item.Tag as string) == selected)
            {
                _lbObjects.SelectedItem = item;
                break;
            }
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ShowSelected();
    }

    /// <summary>Reads the properties and the report of the object the list points at.</summary>
    private void ShowSelected()
    {
        _rows.Clear();
        _txtResults.Text = "";

        if (_flowsheet == null) return;
        if (_lbObjects.SelectedItem is not ListBoxItem item) return;

        var key = item.Tag as string;
        if (key == null || !_flowsheet.SimulationObjects.ContainsKey(key)) return;

        var obj = _flowsheet.SimulationObjects[key];
        var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
        var nf = _flowsheet.FlowsheetOptions.NumberFormat;

        var show = ShowTypes[Math.Max(0, _cbShow.SelectedIndex)];

        foreach (var property in obj.GetProperties(show))
        {
            var row = new ResultRow { Property = _flowsheet.GetTranslatedString(property) };

            try
            {
                row.Units = obj.GetPropertyUnit(property, su);

                var value = obj.GetPropertyValue(property, su);
                var text = value?.ToString() ?? "";

                if (double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var d))
                    row.Value = double.IsNaN(d) || double.IsInfinity(d) ? "" : d.ToString(nf);
                else
                    row.Value = text;
            }
            catch (Exception)
            {
                row.Value = "";
            }

            _rows.Add(row);
        }

        try
        {
            _txtResults.Text = obj.GetReport(su, CultureInfo.CurrentCulture, nf);
        }
        catch (Exception ex)
        {
            _txtResults.Text = "Could not write the report: " + ex.Message;
        }
    }

    // -------------------------------------------------------------------------
    // Copy and export
    // -------------------------------------------------------------------------

    private string BuildTable(char separator)
    {
        var sb = new StringBuilder();

        sb.Append("Property").Append(separator).Append("Value").Append(separator).Append("Units");
        sb.AppendLine();

        foreach (var row in _rows)
        {
            sb.Append(row.Property).Append(separator)
              .Append(row.Value).Append(separator)
              .Append(row.Units);
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
            Title = "Export Results",
            SuggestedFileName = "results.csv",
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
            _flowsheet?.ShowMessage("Results exported to " + Path.GetFileName(path) + ".",
                IFlowsheet.MessageType.Information);
        }
        catch (Exception ex)
        {
            _flowsheet?.ShowMessage("Could not export the results: " + ex.Message,
                IFlowsheet.MessageType.GeneralError);
        }
    }

}
