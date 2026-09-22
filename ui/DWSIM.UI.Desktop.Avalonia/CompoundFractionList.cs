using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// A list of compounds with a fraction beside each, built one row at a time: a drop-down of the
/// compounds that may be declared, a percentage, and a button to take the row away.
/// </summary>
/// <remarks>
/// This is what the petroleum tools use for the light ends of an assay and for the defined
/// composition of a reservoir fluid. Typing a compound name was the first shape of it and it was
/// the wrong one: the name has to match the database exactly, and a misspelling was only heard
/// after the fitting had run. The candidate list is asked for each time a row is added, so a
/// filter that depends on another field, the molar weight a plus fraction starts at, follows it.
/// </remarks>
internal sealed class CompoundFractionList : StackPanel
{
    private readonly Func<List<string>> _candidates;
    private readonly Button _add;

    public CompoundFractionList(Func<List<string>> candidates, string addCaption = "Add Compound")
    {
        _candidates = candidates;
        Spacing = 4;

        _add = new Button { Content = addCaption, HorizontalAlignment = HorizontalAlignment.Left };
        _add.Click += (_, _) => AddRow();
        Children.Add(_add);
    }

    /// <summary>What the user filled in, skipping the rows left empty.</summary>
    public IEnumerable<(string Compound, double Percent)> Rows
    {
        get
        {
            foreach (var row in Children.OfType<Grid>())
            {
                var combo = row.Children.OfType<ComboBox>().FirstOrDefault();
                var box = row.Children.OfType<TextBox>().FirstOrDefault();
                if (combo?.SelectedItem == null) continue;

                var text = (box?.Text ?? "").Trim();
                if (!double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var pct) &&
                    !double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out pct))
                {
                    throw new Exception("'" + text + "' on the line for " + combo.SelectedItem + " is not a number.");
                }

                yield return (combo.SelectedItem.ToString() ?? "", pct);
            }
        }
    }

    public void Clear()
    {
        foreach (var row in Children.OfType<Grid>().ToList()) Children.Remove(row);
    }

    public void AddRow(string? compound = null, double percent = 0.0)
    {
        var options = _candidates() ?? new List<string>();

        var combo = new ComboBox
        {
            ItemsSource = options,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = "Select a compound"
        };
        if (compound != null && options.Contains(compound)) combo.SelectedItem = compound;

        var box = new TextBox
        {
            Text = percent.ToString("G6", CultureInfo.CurrentCulture),
            Width = 90,
            TextAlignment = global::Avalonia.Media.TextAlignment.Right
        };

        var remove = new Button { Content = "X", Width = 32 };

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            Margin = new global::Avalonia.Thickness(0, 0, 0, 2)
        };
        Grid.SetColumn(combo, 0);
        Grid.SetColumn(box, 1);
        Grid.SetColumn(remove, 2);
        row.Children.Add(combo);
        row.Children.Add(box);
        row.Children.Add(remove);

        remove.Click += (_, _) => Children.Remove(row);

        // the add button stays at the bottom
        Children.Insert(Children.Count - 1, row);
    }
}
