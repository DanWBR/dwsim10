using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Platform.Storage;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Desktop.Avalonia.Reactions;
using RxnBaseClasses = DWSIM.Thermodynamics.BaseClasses;

namespace DWSIM.UI.Desktop.Avalonia;

public partial class ReactionManagerWindow : Window
{
    private readonly IFlowsheet _flowsheet;
    private readonly Control? _root;

    private readonly ObservableCollection<SetRow> _sets = new();
    private readonly ObservableCollection<RxnRow> _reactions = new();

    public ReactionManagerWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
        _root = Content as Control;
        BuildColumns();
        WireEvents();
        RefreshSets();
        RefreshReactions();
    }

    /// <summary>
    /// Detaches the manager from its window so another window can host it: the Simulation Settings
    /// window shows it on its Reactions tab, the way the WinForms settings form does.
    /// </summary>
    public static Control CreateEmbeddedContent(IFlowsheet flowsheet)
    {
        var window = new ReactionManagerWindow(flowsheet);
        var content = (Control)window.Content!;
        window.Content = null;
        window.BtnClose.IsVisible = false;
        content.Tag = window;   // the handlers live on the window; keep it alive
        return content;
    }

    private Window DialogOwner()
    {
        if (_root != null && TopLevel.GetTopLevel(_root) is Window host) return host;
        return this;
    }

    // -------------------------------------------------------------------------
    // Columns + wiring
    // -------------------------------------------------------------------------

    private void BuildColumns()
    {
        GridSets.ItemsSource = _sets;
        GridSets.Columns.Add(TextCol("Name", nameof(SetRow.Name), 2));
        GridSets.Columns.Add(TextCol("Description", nameof(SetRow.Description), 3));

        GridReactions.ItemsSource = _reactions;
        GridReactions.Columns.Add(TextCol("Name", nameof(RxnRow.Name), 2));
        GridReactions.Columns.Add(TextCol("Type", nameof(RxnRow.Type), 1.4));
        GridReactions.Columns.Add(TextCol("Equation", nameof(RxnRow.Equation), 3));
    }

    private static DataGridTextColumn TextCol(string header, string prop, double star) => new()
    {
        Header = header,
        Binding = new Binding(prop) { Mode = BindingMode.OneWay },
        IsReadOnly = true,
        Width = new DataGridLength(star, DataGridLengthUnitType.Star)
    };

    private void WireEvents()
    {
        BtnClose.Click += (_, _) => Close();

        BtnSetNew.Click    += async (_, _) => await EditSet(null);
        BtnSetEdit.Click   += async (_, _) => { if (SelectedSet() is { } rs) await EditSet(rs); };
        BtnSetCopy.Click   += (_, _) => CopySet();
        BtnSetRemove.Click += (_, _) => RemoveSet();
        GridSets.DoubleTapped += async (_, _) => { if (SelectedSet() is { } rs) await EditSet(rs); };

        BtnRxnAdd.Click    += (_, _) => ShowAddReactionFlyout();
        BtnRxnEdit.Click   += async (_, _) => { if (SelectedReaction() is { } rxn) await EditReaction(rxn); };
        BtnRxnCopy.Click   += (_, _) => CopyReaction();
        BtnRxnDelete.Click += (_, _) => DeleteReaction();
        BtnRxnExport.Click += async (_, _) => await ExportReactions();
        BtnRxnImport.Click += async (_, _) => await ImportReactions();
        GridReactions.DoubleTapped += async (_, _) => { if (SelectedReaction() is { } rxn) await EditReaction(rxn); };
    }

    // -------------------------------------------------------------------------
    // Refresh
    // -------------------------------------------------------------------------

    private void RefreshSets()
    {
        var sel = SelectedSet()?.ID;
        _sets.Clear();
        foreach (var rs in _flowsheet.ReactionSets.Values)
            _sets.Add(new SetRow { Name = rs.Name, Description = rs.Description ?? "", ID = rs.ID });
        if (sel != null) GridSets.SelectedItem = _sets.FirstOrDefault(r => r.ID == sel);
    }

    private void RefreshReactions()
    {
        var sel = SelectedReaction()?.ID;
        _reactions.Clear();
        foreach (var r in _flowsheet.Reactions.Values)
            _reactions.Add(new RxnRow { Name = r.Name, Type = TypeName(r.ReactionType), Equation = r.Equation ?? "", ID = r.ID });
        if (sel != null) GridReactions.SelectedItem = _reactions.FirstOrDefault(r => r.ID == sel);
    }

    private IReactionSet? SelectedSet()
        => GridSets.SelectedItem is SetRow row && _flowsheet.ReactionSets.TryGetValue(row.ID, out var rs) ? rs : null;

    private IReaction? SelectedReaction()
        => GridReactions.SelectedItem is RxnRow row && _flowsheet.Reactions.TryGetValue(row.ID, out var rxn) ? rxn : null;

    // -------------------------------------------------------------------------
    // Reactions
    // -------------------------------------------------------------------------

    private void ShowAddReactionFlyout()
    {
        var menu = new MenuFlyout();
        foreach (var (label, type) in new[]
        {
            ("Conversion", ReactionType.Conversion),
            ("Equilibrium", ReactionType.Equilibrium),
            ("Kinetic", ReactionType.Kinetic),
            ("Heterogeneous Catalytic", ReactionType.Heterogeneous_Catalytic),
        })
        {
            var item = new MenuItem { Header = label };
            var t = type;
            item.Click += async (_, _) => await NewReaction(t);
            menu.Items.Add(item);
        }
        menu.ShowAt(BtnRxnAdd);
    }

    private async Task NewReaction(ReactionType type)
    {
        var dlg = EditorFor(type, null);
        if (dlg == null) return;
        await dlg.ShowDialog(DialogOwner());
        if (dlg.Saved) { RefreshReactions(); RefreshSets(); }
    }

    private async Task EditReaction(IReaction rxn)
    {
        var dlg = EditorFor(rxn.ReactionType, rxn);
        if (dlg == null) return;
        await dlg.ShowDialog(DialogOwner());
        if (dlg.Saved) { RefreshReactions(); RefreshSets(); }
    }

    private ReactionEditorWindow? EditorFor(ReactionType type, IReaction? existing) => type switch
    {
        ReactionType.Conversion => new ConversionReactionEditor(_flowsheet, existing, "New Reaction"),
        ReactionType.Equilibrium => new EquilibriumReactionEditor(_flowsheet, existing, "New Reaction"),
        ReactionType.Kinetic => new KineticReactionEditor(_flowsheet, existing, "New Reaction"),
        ReactionType.Heterogeneous_Catalytic => new HeterogeneousReactionEditor(_flowsheet, existing, "New Reaction"),
        _ => null
    };

    private void CopyReaction()
    {
        if (SelectedReaction() is not { } rxn) return;
        var clone = new RxnBaseClasses.Reaction();
        ((DWSIM.Interfaces.ICustomXMLSerialization)clone).LoadData(
            ((DWSIM.Interfaces.ICustomXMLSerialization)rxn).SaveData());
        clone.ID = Guid.NewGuid().ToString();
        clone.Name = rxn.Name + "1";
        _flowsheet.RegisterSnapshot(SnapshotType.ReactionSubsystem);
        _flowsheet.AddReaction(clone);
        if (_flowsheet.ReactionSets.ContainsKey("DefaultSet"))
            _flowsheet.AddReactionToSet(clone.ID, "DefaultSet", true, 0);
        RefreshReactions();
    }

    private void DeleteReaction()
    {
        if (SelectedReaction() is not { } rxn) return;
        _flowsheet.RegisterSnapshot(SnapshotType.ReactionSubsystem);
        _flowsheet.Reactions.Remove(rxn.ID);
        foreach (var rs in _flowsheet.ReactionSets.Values)
            if (rs.Reactions.ContainsKey(rxn.ID)) rs.Reactions.Remove(rxn.ID);
        RefreshReactions();
        RefreshSets();
    }

    // -------------------------------------------------------------------------
    // Reaction sets
    // -------------------------------------------------------------------------

    private async Task EditSet(IReactionSet? existing)
    {
        var dlg = new ReactionSetEditorWindow(_flowsheet, existing, "New Reaction Set");
        await dlg.ShowDialog(DialogOwner());
        if (dlg.Saved) RefreshSets();
    }

    private void CopySet()
    {
        if (SelectedSet() is not { } rs) return;
        if (rs.ID == "DefaultSet") return;   // the default set cannot be copied, as in WinForms
        var clone = new RxnBaseClasses.ReactionSet(Guid.NewGuid().ToString(), rs.Name + "1", rs.Description ?? "");
        foreach (var pair in rs.Reactions)
            clone.Reactions[pair.Key] = new RxnBaseClasses.ReactionSetBase(pair.Key, pair.Value.Rank, pair.Value.IsActive);
        _flowsheet.RegisterSnapshot(SnapshotType.ReactionSubsystem);
        _flowsheet.AddReactionSet(clone);
        RefreshSets();
    }

    private void RemoveSet()
    {
        if (SelectedSet() is not { } rs) return;
        if (rs.ID == "DefaultSet") return;   // the default set cannot be removed, as in WinForms
        _flowsheet.RegisterSnapshot(SnapshotType.ReactionSubsystem);
        _flowsheet.ReactionSets.Remove(rs.ID);
        RefreshSets();
    }

    // -------------------------------------------------------------------------
    // Export / import (.dwrxm)
    // -------------------------------------------------------------------------

    private async Task ExportReactions()
    {
        if (SelectedReaction() is not { } rxn) return;
        var top = TopLevel.GetTopLevel(DialogOwner());
        if (top == null) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Reaction",
            SuggestedFileName = rxn.Name + ".dwrxm",
            FileTypeChoices = new[] { new FilePickerFileType("DWSIM Reaction") { Patterns = new[] { "*.dwrxm" } } }
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        var doc = new XDocument(new XElement("Reactions",
            new XElement("Reaction", ((DWSIM.Interfaces.ICustomXMLSerialization)rxn).SaveData().ToArray())));
        doc.Save(path);
    }

    private async Task ImportReactions()
    {
        var top = TopLevel.GetTopLevel(DialogOwner());
        if (top == null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Reactions",
            AllowMultiple = true,
            FileTypeFilter = new[] { new FilePickerFileType("DWSIM Reaction") { Patterns = new[] { "*.dwrxm" } } }
        });
        if (files.Count == 0) return;

        _flowsheet.RegisterSnapshot(SnapshotType.ReactionSubsystem);
        foreach (var f in files)
        {
            var path = f.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) continue;
            try
            {
                var doc = XDocument.Load(path);
                foreach (var el in doc.Descendants("Reaction"))
                {
                    var rxn = new RxnBaseClasses.Reaction();
                    ((DWSIM.Interfaces.ICustomXMLSerialization)rxn).LoadData(el.Elements().ToList());
                    rxn.ID = Guid.NewGuid().ToString();
                    _flowsheet.AddReaction(rxn);
                    if (_flowsheet.ReactionSets.ContainsKey("DefaultSet"))
                        _flowsheet.AddReactionToSet(rxn.ID, "DefaultSet", true, 0);
                }
            }
            catch { /* skip an unreadable file */ }
        }
        RefreshReactions();
        RefreshSets();
    }

    private static string TypeName(ReactionType t) => t switch
    {
        ReactionType.Heterogeneous_Catalytic => "Heterogeneous Catalytic",
        _ => t.ToString()
    };

    // -------------------------------------------------------------------------
    // Row models
    // -------------------------------------------------------------------------

    private sealed class SetRow
    {
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public string ID { get; init; } = "";
    }

    private sealed class RxnRow
    {
        public string Name { get; init; } = "";
        public string Type { get; init; } = "";
        public string Equation { get; init; } = "";
        public string ID { get; init; } = "";
    }
}
