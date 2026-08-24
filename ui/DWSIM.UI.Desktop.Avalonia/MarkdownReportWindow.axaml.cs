using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Object list and property list on the left, the value of the selected property on the right.
/// </summary>
public partial class MarkdownReportWindow : Window
{
    private readonly IFlowsheet? _flowsheet;

    private TextBox? _content;

    /// <summary>Parameterless constructor required by Avalonia XAML parser.</summary>
    public MarkdownReportWindow()
    {
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
    }

    public MarkdownReportWindow(IFlowsheet flowsheet, string windowTitle)
    {
        _flowsheet = flowsheet;
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
        Title = windowTitle;

        ObjectList.SelectionChanged += OnObjectSelected;
        PropertyList.SelectionChanged += OnPropertySelected;

        Opened += (_, _) =>
        {
            InitContentView();
            PopulateObjects();
        };
    }

    private void InitContentView()
    {
        _content = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(12),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Watermark = "Select an object and property to view its value."
        };

        ContentHost.Child = _content;
    }

    // -----------------------------------------------------------------
    // Object / property population
    // -----------------------------------------------------------------

    private void PopulateObjects()
    {
        if (_flowsheet == null) return;
        ObjectList.Items.Clear();
        foreach (var obj in _flowsheet.SimulationObjects.Values
            .Where(o => o.GraphicObject != null)
            .OrderBy(o => o.GraphicObject.Tag))
        {
            ObjectList.Items.Add(new ListBoxItem
            {
                Content = obj.GraphicObject.Tag,
                Tag = obj.Name
            });
        }
    }

    private void OnObjectSelected(object? sender, SelectionChangedEventArgs e)
    {
        PropertyList.Items.Clear();
        if (ObjectList.SelectedItem is not ListBoxItem selected) return;
        var key = selected.Tag as string;
        if (key == null || _flowsheet == null || !_flowsheet.SimulationObjects.ContainsKey(key)) return;

        var obj = _flowsheet.SimulationObjects[key];
        var props = obj.GetProperties(Interfaces.Enums.PropertyType.ALL);
        foreach (var prop in props)
        {
            PropertyList.Items.Add(new ListBoxItem
            {
                Content = _flowsheet.GetTranslatedString(prop),
                Tag = prop
            });
        }
    }

    private void OnPropertySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ObjectList.SelectedItem is not ListBoxItem objItem) return;
        if (PropertyList.SelectedItem is not ListBoxItem propItem) return;

        var objKey = objItem.Tag as string;
        var propKey = propItem.Tag as string;
        if (objKey == null || propKey == null || _flowsheet == null) return;
        if (!_flowsheet.SimulationObjects.ContainsKey(objKey)) return;

        var obj = _flowsheet.SimulationObjects[objKey];
        try
        {
            var val = obj.GetPropertyValue(propKey);
            SetContent(val?.ToString() ?? "");
        }
        catch (Exception ex)
        {
            SetContent($"Error: {ex.Message}");
        }
    }

    public void SetContent(string markdown)
    {
        if (_content != null) _content.Text = markdown;
    }
}
