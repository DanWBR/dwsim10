using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.BaseClasses;
using DWSIM.Thermodynamics.CompoundEditing;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Two compounds side by side, property by property, with the differing rows highlighted. "Take"
/// copies one value from the right-hand compound into the left-hand one (the editor state);
/// "Take all" copies every differing value. Nothing here touches the simulation.
/// </summary>
public sealed class CompoundDiffWindow : Window
{
    private sealed class Row : INotifyPropertyChanged
    {
        public string Key = "";
        public object? FileValue;
        public bool IsCurve;
        public string Group { get; set; } = "";
        public string Property { get; set; } = "";
        public string Left { get; set; } = "";
        public string Right { get; set; } = "";
        public string Detail { get; set; } = "";
        private bool _differs;
        public bool Differs
        {
            get => _differs;
            set { _differs = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Differs))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DiffersText))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanTake))); }
        }
        public string DiffersText => Differs ? "yes" : "";
        public bool CanTake => Differs && !IsCurve;
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly ConstantProperties _editor;
    private readonly ConstantProperties _file;
    private readonly IUnitsOfMeasure _su;
    private readonly string _nf;
    private readonly bool _canTake;
    private readonly List<Row> _rows = new();
    private readonly DataGrid _grid = new();
    private readonly CheckBox _onlyDiff = new() { Content = "Show only differences", IsChecked = true };
    private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap, Opacity = 0.85 };

    private static readonly IBrush Highlight = new SolidColorBrush(Color.FromArgb(70, 255, 196, 0));

    /// <summary>True when at least one value was copied into the editor state.</summary>
    public bool Applied { get; private set; }

    public CompoundDiffWindow(ConstantProperties editorState, ConstantProperties fileState, IUnitsOfMeasure su, string nf,
                              string leftTitle, string rightTitle, bool canTake, bool offerTakeAll)
    {
        _editor = editorState;
        _file = fileState;
        _su = su;
        _nf = nf;
        _canTake = canTake;

        Title = "Compare compound: " + leftTitle + " vs " + rightTitle;
        Width = 1000;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        _grid.AutoGenerateColumns = false;
        _grid.IsReadOnly = true;
        _grid.CanUserSortColumns = false;
        _grid.HeadersVisibility = DataGridHeadersVisibility.Column;
        _grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        _grid.Columns.Add(new DataGridTextColumn { Header = "Group", Binding = new global::Avalonia.Data.Binding(nameof(Row.Group)), Width = new DataGridLength(150) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Property", Binding = new global::Avalonia.Data.Binding(nameof(Row.Property)), Width = new DataGridLength(240) });
        _grid.Columns.Add(new DataGridTextColumn { Header = leftTitle, Binding = new global::Avalonia.Data.Binding(nameof(Row.Left)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = rightTitle, Binding = new global::Avalonia.Data.Binding(nameof(Row.Right)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Differs", Binding = new global::Avalonia.Data.Binding(nameof(Row.DiffersText)), Width = new DataGridLength(70) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Detail", Binding = new global::Avalonia.Data.Binding(nameof(Row.Detail)), Width = new DataGridLength(220) });
        if (canTake)
        {
            _grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = "",
                Width = new DataGridLength(80),
                CellTemplate = new FuncDataTemplate<Row>((row, _) =>
                {
                    var b = new Button { Content = "Take", Padding = new Thickness(8, 2), IsVisible = row.CanTake };
                    b.Classes.Add("panel");
                    b.Click += (_, _) => Take(row);
                    return b;
                })
            });
        }
        _grid.LoadingRow += (_, e) => { if (e.Row.DataContext is Row r) e.Row.Background = r.Differs ? Highlight : null; };

        Load();

        _onlyDiff.IsCheckedChanged += (_, _) => Refresh();

        var takeAll = new Button { Content = "Take all from " + rightTitle.ToLowerInvariant(), IsVisible = canTake && offerTakeAll };
        takeAll.Classes.Add("panel");
        takeAll.Click += (_, _) => { foreach (var r in _rows.Where(x => x.CanTake).ToList()) Take(r); };
        var close = new Button { Content = "Close", Width = 90, IsCancel = true, IsDefault = true };
        close.Classes.Add("dialog");
        close.Click += (_, _) => Close();

        var top = new StackPanel { Margin = new Thickness(12, 10, 12, 6), Spacing = 6 };
        top.Children.Add(new TextBlock
        {
            Text = "Highlighted rows differ. " + (canTake ? "Take copies the file's value into the editor; nothing reaches the simulation until you press OK in the editor." : "This compound is read-only."),
            TextWrapping = TextWrapping.Wrap, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Opacity = 0.75
        });
        var topRow = new DockPanel();
        DockPanel.SetDock(_onlyDiff, global::Avalonia.Controls.Dock.Left);
        topRow.Children.Add(_onlyDiff);
        _summary.Margin = new Thickness(16, 0, 0, 0);
        _summary.VerticalAlignment = VerticalAlignment.Center;
        topRow.Children.Add(_summary);
        top.Children.Add(topRow);

        var bottom = new DockPanel { Margin = new Thickness(12, 6, 16, 12) };
        var rightButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { close } };
        DockPanel.SetDock(rightButtons, global::Avalonia.Controls.Dock.Right);
        bottom.Children.Add(rightButtons);
        bottom.Children.Add(takeAll);

        var root = new DockPanel();
        DockPanel.SetDock(top, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(top);
        root.Children.Add(bottom);
        root.Children.Add(new Border { Child = _grid, Margin = new Thickness(12, 0, 12, 0) });
        Content = root;
    }

    private void Load()
    {
        _rows.Clear();
        foreach (var e in CompoundDiff.Compare(_editor, _file))
        {
            var groupName = e.Block != null ? e.Block.DisplayName : GroupName(e.Group);
            _rows.Add(new Row
            {
                Key = e.Key,
                FileValue = e.ValueB,
                IsCurve = e.IsCurve,
                Group = groupName,
                Property = e.Block != null && !e.IsCurve ? ShortBlockName(e.DisplayName, e.Block) : e.DisplayName,
                Left = e.IsCurve ? "" : CompoundDiff.FormatValue(e.Key, e.ValueA, _su, _nf),
                Right = e.IsCurve ? "" : CompoundDiff.FormatValue(e.Key, e.ValueB, _su, _nf),
                Differs = e.Differs,
                Detail = e.Detail
            });
        }
        Refresh();
    }

    private void Refresh()
    {
        var shown = _onlyDiff.IsChecked == true ? _rows.Where(r => r.Differs).ToList() : _rows.ToList();
        _grid.ItemsSource = null;
        _grid.ItemsSource = shown;
        var n = _rows.Count(r => r.Differs);
        _summary.Text = n == 0 ? "No differences left." : $"{n} propert{(n == 1 ? "y" : "ies")} differ.";
    }

    private void Take(Row row)
    {
        try
        {
            CompoundPropertyDescriptors.ByKey(row.Key).SetValue(_editor, row.FileValue);
            Applied = true;
            // re-compare: taking one value can change a curve entry as well
            Load();
        }
        catch (Exception ex)
        {
            _summary.Text = "Could not take '" + row.Property + "': " + ex.Message;
        }
    }

    private static string GroupName(CompoundPropertyGroup g)
    {
        var info = CompoundPropertyDescriptors.Groups.FirstOrDefault(x => x.Group == g);
        return info?.DisplayName ?? g.ToString();
    }

    private static string ShortBlockName(string displayName, TemperatureDependentBlock b)
        => displayName.StartsWith(b.DisplayName, StringComparison.OrdinalIgnoreCase) ? displayName.Substring(b.DisplayName.Length).Trim() : displayName;
}
