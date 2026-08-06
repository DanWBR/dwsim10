using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Script manager, as the Windows FormScript works: the scripts of the simulation on the left,
/// the editor and its output on the right, and the event each script is linked to below the
/// toolbar. Scripts live in the flowsheet, so they are saved with it.
/// </summary>
public partial class ScriptEditorWindow : Window
{

    /// <summary>Runs the given script; the host owns the interpreter and the output.</summary>
    public event EventHandler<IScript>? RunRequested;

    /// <summary>Runs the given script off the UI thread.</summary>
    public event EventHandler<IScript>? RunAsyncRequested;

    public event EventHandler? StopRequested;

    private IFlowsheet? _flowsheet;
    private IScript? _current;
    private bool _loading;

    private readonly ObservableCollection<ScriptItem> _items = new();

    /// <summary>A script in the list. The label follows the title as it is typed.</summary>
    private sealed class ScriptItem : System.ComponentModel.INotifyPropertyChanged
    {
        public ScriptItem(IScript script) { Script = script; }

        public IScript Script { get; }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Script.Title) ? "(untitled)" : Script.Title;
        }

        public void Refresh()
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(""));
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    // -------------------------------------------------------------------------
    // The linked event lists, in the order the Windows editor shows them
    // -------------------------------------------------------------------------

    private static readonly string[] SimulationEvents =
    {
        "Simulation Opened", "Simulation Saved", "Simulation Closed",
        "1 min. Timer", "5 min. Timer", "15 min. Timer", "30 min. Timer", "60 min. Timer"
    };

    private static readonly Scripts.EventType[] SimulationEventTypes =
    {
        Scripts.EventType.SimulationOpened, Scripts.EventType.SimulationSaved,
        Scripts.EventType.SimulationClosed, Scripts.EventType.SimulationTimer1,
        Scripts.EventType.SimulationTimer5, Scripts.EventType.SimulationTimer15,
        Scripts.EventType.SimulationTimer30, Scripts.EventType.SimulationTimer60
    };

    private static readonly string[] SolverEvents =
    {
        "Solver Started", "Solver Finished", "Recycle Loop"
    };

    private static readonly Scripts.EventType[] SolverEventTypes =
    {
        Scripts.EventType.SolverStarted, Scripts.EventType.SolverFinished,
        Scripts.EventType.SolverRecycleLoop
    };

    private static readonly string[] IntegratorEvents =
    {
        "Integrator Started", "Integrator Finished", "Integrator Error",
        "Integrator Post-Step", "Integrator Pre-Step"
    };

    private static readonly Scripts.EventType[] IntegratorEventTypes =
    {
        Scripts.EventType.IntegratorStarted, Scripts.EventType.IntegratorFinished,
        Scripts.EventType.IntegratorError, Scripts.EventType.IntegratorStep,
        Scripts.EventType.IntegratorPreStep
    };

    private static readonly string[] ObjectEvents =
    {
        "Object Calculation Started", "Object Calculation Finished", "Object Calculation Error"
    };

    private static readonly Scripts.EventType[] ObjectEventTypes =
    {
        Scripts.EventType.ObjectCalculationStarted, Scripts.EventType.ObjectCalculationFinished,
        Scripts.EventType.ObjectCalculationError
    };

    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------

    public ScriptEditorWindow()
    {
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
        ConfigureEditor();
        WireControls();
    }

    private void ConfigureEditor()
    {
        ScriptEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Python");
        ScriptEditor.Options.EnableHyperlinks = false;
        ScriptEditor.Options.EnableEmailHyperlinks = false;
        ScriptEditor.Options.ShowBoxForControlCharacters = true;
        ScriptEditor.Options.ConvertTabsToSpaces = true;
        ScriptEditor.Options.IndentationSize = 4;

        ScriptEditor.TextChanged += (_, _) =>
        {
            if (_loading || _current == null) return;
            _current.ScriptText = ScriptEditor.Text;
        };
    }

    /// <summary>
    /// Hands the manager the simulation whose scripts it edits. Without one it still works as a
    /// scratch editor, but nothing is stored.
    /// </summary>
    public void SetFlowsheet(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;

        ReloadScripts();
        ReloadLinkedObjects();

        if (_items.Count == 0) NewScript();
        else ScriptList.SelectedIndex = 0;
    }

    private void ReloadScripts()
    {
        _items.Clear();

        if (_flowsheet == null) return;

        foreach (var script in _flowsheet.Scripts.Values.OrderBy(x => x.Title))
            _items.Add(new ScriptItem(script));
    }

    // -------------------------------------------------------------------------
    // Wiring
    // -------------------------------------------------------------------------

    private void WireControls()
    {
        ScriptList.ItemsSource = _items;
        ScriptList.SelectionChanged += (_, _) => LoadSelected();

        CbLanguage.ItemsSource = new[] { "Python", "C#", "Visual Basic" };
        CbLanguage.SelectedIndex = 0;
        CbLanguage.SelectionChanged += (_, _) => ApplyHighlighting();

        CbInterpreter.ItemsSource = new[] { "IronPython", "Python.NET" };
        CbInterpreter.SelectedIndex = 0;
        CbInterpreter.SelectionChanged += (_, _) =>
        {
            if (_loading || _current == null) return;
            _current.PythonInterpreter = CbInterpreter.SelectedIndex == 1
                ? Scripts.Interpreter.Python_NET
                : Scripts.Interpreter.IronPython;
        };

        CbFont.ItemsSource = new[]
        {
            "Cascadia Code", "Consolas", "Courier New", "Lucida Console", "Menlo", "monospace"
        };
        CbFont.SelectedIndex = 0;
        CbFont.SelectionChanged += (_, _) =>
        {
            if (CbFont.SelectedItem is string family)
                ScriptEditor.FontFamily = new global::Avalonia.Media.FontFamily(family);
        };

        CbFontSize.ItemsSource = new[] { "9", "10", "11", "12", "13", "14", "16", "18", "20" };
        CbFontSize.SelectedIndex = 4;
        CbFontSize.SelectionChanged += (_, _) =>
        {
            if (CbFontSize.SelectedItem is string size && double.TryParse(size, out var value))
                ScriptEditor.FontSize = value;
        };

        BtnNew.Click += (_, _) => NewScript();
        BtnDuplicate.Click += (_, _) => DuplicateScript();
        BtnDelete.Click += async (_, _) => await DeleteScriptAsync();

        TbTitle.TextChanged += (_, _) =>
        {
            if (_loading || _current == null) return;
            _current.Title = TbTitle.Text ?? "";
            (ScriptList.SelectedItem as ScriptItem)?.Refresh();
        };

        ChkLinked.IsCheckedChanged += (_, _) =>
        {
            if (_loading || _current == null) return;
            _current.Linked = ChkLinked.IsChecked.GetValueOrDefault();
        };

        CbLinkedObject.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            ReloadLinkedEvents();
            StoreLink();
        };

        CbLinkedEvent.SelectionChanged += (_, _) => { if (!_loading) StoreLink(); };

        BtnRun.Click += (_, _) =>
        {
            if (_current == null) return;
            SetRunning(true);
            SetStatus("Running...");
            RunRequested?.Invoke(this, _current);
        };

        BtnRunAsync.Click += (_, _) =>
        {
            if (_current == null) return;
            SetRunning(true);
            SetStatus("Running (async)...");
            RunAsyncRequested?.Invoke(this, _current);
        };

        BtnStop.Click += (_, _) =>
        {
            SetRunning(false);
            SetStatus("Stopped.");
            StopRequested?.Invoke(this, EventArgs.Empty);
        };

        BtnClear.Click += (_, _) => OutputBox.Text = string.Empty;

        BtnUndo.Click += (_, _) => ScriptEditor.Undo();
        BtnRedo.Click += (_, _) => ScriptEditor.Redo();

        BtnComment.Click += (_, _) => CommentSelection(true);
        BtnUncomment.Click += (_, _) => CommentSelection(false);
        BtnIndent.Click += (_, _) => IndentSelection(true);
        BtnUnindent.Click += (_, _) => IndentSelection(false);

        BtnSnippet.Click += (_, _) => ShowSnippets();
        BtnApiHelp.Click += (_, _) => OpenApiHelp();
    }

    private void ApplyHighlighting()
    {
        ScriptEditor.SyntaxHighlighting = CbLanguage.SelectedItem?.ToString() switch
        {
            "Python"       => HighlightingManager.Instance.GetDefinition("Python"),
            "C#"           => HighlightingManager.Instance.GetDefinition("C#"),
            "Visual Basic" => HighlightingManager.Instance.GetDefinition("VB"),
            _              => null
        };
    }

    // -------------------------------------------------------------------------
    // The script list
    // -------------------------------------------------------------------------

    private void NewScript()
    {
        var script = new DWSIM.FlowsheetSolver.Script
        {
            ID = Guid.NewGuid().ToString(),
            Title = "Script" + (_items.Count + 1)
        };

        // the flowsheet owns the scripts, which is what gets them saved with the simulation
        _flowsheet?.Scripts.Add(script.ID, script);

        var item = new ScriptItem(script);
        _items.Add(item);
        ScriptList.SelectedItem = item;
    }

    private void DuplicateScript()
    {
        if (_current == null) return;

        var script = new DWSIM.FlowsheetSolver.Script
        {
            ID = Guid.NewGuid().ToString(),
            Title = _current.Title + " (copy)",
            ScriptText = _current.ScriptText,
            PythonInterpreter = _current.PythonInterpreter,
            LinkedObjectType = _current.LinkedObjectType,
            LinkedObjectName = _current.LinkedObjectName,
            LinkedEventType = _current.LinkedEventType
        };

        _flowsheet?.Scripts.Add(script.ID, script);

        var item = new ScriptItem(script);
        _items.Add(item);
        ScriptList.SelectedItem = item;
    }

    private async System.Threading.Tasks.Task DeleteScriptAsync()
    {
        if (ScriptList.SelectedItem is not ScriptItem item) return;

        if (!await ConfirmAsync("Remove Script",
                $"Remove the script '{item.Script.Title}'?")) return;

        _flowsheet?.Scripts.Remove(item.Script.ID);
        _items.Remove(item);

        if (_items.Count > 0) ScriptList.SelectedIndex = 0;
        else { _current = null; _loading = true; ScriptEditor.Text = ""; TbTitle.Text = ""; _loading = false; }
    }

    private void LoadSelected()
    {
        if (ScriptList.SelectedItem is not ScriptItem item) return;

        _loading = true;

        _current = item.Script;

        ScriptEditor.Text = _current.ScriptText ?? "";
        TbTitle.Text = _current.Title ?? "";
        CbInterpreter.SelectedIndex = _current.PythonInterpreter == Scripts.Interpreter.Python_NET ? 1 : 0;
        ChkLinked.IsChecked = _current.Linked;

        SelectLinkedObject();
        ReloadLinkedEvents();
        SelectLinkedEvent();

        _loading = false;
    }

    // -------------------------------------------------------------------------
    // Linked event
    // -------------------------------------------------------------------------

    private void ReloadLinkedObjects()
    {
        var entries = new List<string> { "Simulation", "Solver", "Integrator" };

        if (_flowsheet != null)
            entries.AddRange(_flowsheet.SimulationObjects.Values
                .Where(x => x.GraphicObject != null)
                .Select(x => x.GraphicObject.Tag)
                .OrderBy(x => x));

        CbLinkedObject.ItemsSource = entries;
        if (CbLinkedObject.SelectedIndex < 0) CbLinkedObject.SelectedIndex = 0;
    }

    private void SelectLinkedObject()
    {
        if (_current == null) return;

        if (_current.LinkedObjectType == Scripts.ObjectType.FlowsheetObject &&
            !string.IsNullOrEmpty(_current.LinkedObjectName) &&
            _flowsheet != null &&
            _flowsheet.SimulationObjects.ContainsKey(_current.LinkedObjectName))
        {
            var tag = _flowsheet.SimulationObjects[_current.LinkedObjectName].GraphicObject.Tag;
            CbLinkedObject.SelectedItem = tag;
            return;
        }

        CbLinkedObject.SelectedIndex = _current.LinkedObjectType switch
        {
            Scripts.ObjectType.Simulation => 0,
            Scripts.ObjectType.Solver     => 1,
            Scripts.ObjectType.Integrator => 2,
            _                             => 0
        };
    }

    private void ReloadLinkedEvents()
    {
        var previous = CbLinkedEvent.SelectedIndex;

        CbLinkedEvent.ItemsSource = CbLinkedObject.SelectedIndex switch
        {
            0 => SimulationEvents,
            1 => SolverEvents,
            2 => IntegratorEvents,
            _ => ObjectEvents
        };

        var count = ((string[])CbLinkedEvent.ItemsSource!).Length;
        CbLinkedEvent.SelectedIndex = previous >= 0 && previous < count ? previous : 0;
    }

    private void SelectLinkedEvent()
    {
        if (_current == null) return;

        var types = CbLinkedObject.SelectedIndex switch
        {
            0 => SimulationEventTypes,
            1 => SolverEventTypes,
            2 => IntegratorEventTypes,
            _ => ObjectEventTypes
        };

        var index = Array.IndexOf(types, _current.LinkedEventType);
        CbLinkedEvent.SelectedIndex = index >= 0 ? index : 0;
    }

    /// <summary>Writes the picked object and event into the script, as the Windows editor does.</summary>
    private void StoreLink()
    {
        if (_current == null) return;

        var eventIndex = Math.Max(0, CbLinkedEvent.SelectedIndex);

        switch (CbLinkedObject.SelectedIndex)
        {
            case 0:
                _current.LinkedObjectType = Scripts.ObjectType.Simulation;
                _current.LinkedObjectName = "";
                _current.LinkedEventType = At(SimulationEventTypes, eventIndex);
                break;

            case 1:
                _current.LinkedObjectType = Scripts.ObjectType.Solver;
                _current.LinkedObjectName = "";
                _current.LinkedEventType = At(SolverEventTypes, eventIndex);
                break;

            case 2:
                _current.LinkedObjectType = Scripts.ObjectType.Integrator;
                _current.LinkedObjectName = "";
                _current.LinkedEventType = At(IntegratorEventTypes, eventIndex);
                break;

            default:
                _current.LinkedObjectType = Scripts.ObjectType.FlowsheetObject;

                if (_flowsheet != null && CbLinkedObject.SelectedItem is string tag)
                {
                    var target = _flowsheet.SimulationObjects.Values
                        .FirstOrDefault(x => x.GraphicObject != null && x.GraphicObject.Tag == tag);

                    if (target != null) _current.LinkedObjectName = target.Name;
                }

                _current.LinkedEventType = At(ObjectEventTypes, eventIndex);
                break;
        }
    }

    private static Scripts.EventType At(Scripts.EventType[] types, int index)
    {
        return index >= 0 && index < types.Length ? types[index] : types[0];
    }

    // -------------------------------------------------------------------------
    // Editing helpers
    // -------------------------------------------------------------------------

    private string CommentToken()
    {
        return CbLanguage.SelectedItem?.ToString() switch
        {
            "C#"           => "//",
            "Visual Basic" => "'",
            _              => "#"
        };
    }

    /// <summary>Comments or uncomments the selected lines, or the caret line when nothing is selected.</summary>
    private void CommentSelection(bool comment)
    {
        var token = CommentToken();
        var document = ScriptEditor.Document;

        var start = ScriptEditor.SelectionLength > 0 ? ScriptEditor.SelectionStart : ScriptEditor.CaretOffset;
        var end = ScriptEditor.SelectionLength > 0
            ? ScriptEditor.SelectionStart + ScriptEditor.SelectionLength
            : ScriptEditor.CaretOffset;

        var first = document.GetLineByOffset(start).LineNumber;
        var last = document.GetLineByOffset(end).LineNumber;

        document.BeginUpdate();

        for (int number = first; number <= last; number++)
        {
            var line = document.GetLineByNumber(number);
            var text = document.GetText(line.Offset, line.Length);

            if (comment)
            {
                document.Insert(line.Offset, token);
            }
            else
            {
                var trimmed = text.TrimStart();
                if (!trimmed.StartsWith(token, StringComparison.Ordinal)) continue;

                var indent = text.Length - trimmed.Length;
                document.Remove(line.Offset + indent, token.Length);
            }
        }

        document.EndUpdate();
    }

    /// <summary>Adds or removes one indentation level on the selected lines.</summary>
    private void IndentSelection(bool indent)
    {
        var pad = new string(' ', ScriptEditor.Options.IndentationSize);
        var document = ScriptEditor.Document;

        var start = ScriptEditor.SelectionLength > 0 ? ScriptEditor.SelectionStart : ScriptEditor.CaretOffset;
        var end = ScriptEditor.SelectionLength > 0
            ? ScriptEditor.SelectionStart + ScriptEditor.SelectionLength
            : ScriptEditor.CaretOffset;

        var first = document.GetLineByOffset(start).LineNumber;
        var last = document.GetLineByOffset(end).LineNumber;

        document.BeginUpdate();

        for (int number = first; number <= last; number++)
        {
            var line = document.GetLineByNumber(number);

            if (indent)
            {
                document.Insert(line.Offset, pad);
                continue;
            }

            var text = document.GetText(line.Offset, line.Length);

            var removable = 0;
            while (removable < pad.Length && removable < text.Length && text[removable] == ' ') removable++;

            if (removable == 0 && text.StartsWith("\t", StringComparison.Ordinal)) removable = 1;
            if (removable > 0) document.Remove(line.Offset, removable);
        }

        document.EndUpdate();
    }

    private void Insert(string text)
    {
        ScriptEditor.Document.Insert(ScriptEditor.CaretOffset, text);
        ScriptEditor.Focus();
    }

    // -------------------------------------------------------------------------
    // Snippets
    // -------------------------------------------------------------------------

    /// <summary>
    /// The snippet menu the Windows editor builds: the general calls plus a reader and a writer
    /// for every property of every object on the flowsheet.
    /// </summary>
    private void ShowSnippets()
    {
        var menu = new ContextMenu();
        var items = new List<Control>();

        items.Add(Snippet("Get Flowsheet Object",
            "obj = Flowsheet.GetFlowsheetSimulationObject('<tag>')\n"));
        items.Add(Snippet("Solve Flowsheet", "Flowsheet.RequestCalculation()\n"));
        items.Add(Snippet("Show Message", "Flowsheet.ShowMessage('message', MessageType.Information)\n"));
        items.Add(Snippet("Iterate over Objects",
            "for obj in Flowsheet.SimulationObjects.Values:\n" +
            "    Flowsheet.ShowMessage(obj.GraphicObject.Tag, MessageType.Information)\n"));

        if (_flowsheet != null)
        {
            var objects = _flowsheet.SimulationObjects.Values
                .Where(x => x.GraphicObject != null)
                .OrderBy(x => x.GraphicObject.Tag)
                .ToList();

            if (objects.Count > 0)
            {
                items.Add(new Separator());
                items.Add(PropertyMenu("Get Property", objects, write: false));
                items.Add(PropertyMenu("Set Property", objects, write: true));
            }
        }

        menu.ItemsSource = items;
        menu.PlacementTarget = BtnSnippet;
        menu.Open(BtnSnippet);
    }

    private MenuItem Snippet(string header, string text)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => Insert(text);
        return item;
    }

    private MenuItem PropertyMenu(string header, List<ISimulationObject> objects, bool write)
    {
        var root = new MenuItem { Header = header };
        var groups = new List<Control>();

        foreach (var simobj in objects)
        {
            var tag = simobj.GraphicObject.Tag;
            var group = new MenuItem { Header = tag };

            var properties = new List<Control>();

            try
            {
                var names = simobj.GetProperties(write ? PropertyType.WR : PropertyType.ALL)
                            ?? Array.Empty<string>();

                foreach (var property in names.OrderBy(x => x))
                {
                    var captured = property;
                    var entry = new MenuItem { Header = captured };

                    entry.Click += (_, _) => Insert(write
                        ? $"obj = Flowsheet.GetFlowsheetSimulationObject('{tag}')\n" +
                          $"obj.SetPropertyValue('{captured}', value)\n"
                        : $"obj = Flowsheet.GetFlowsheetSimulationObject('{tag}')\n" +
                          $"value = obj.GetPropertyValue('{captured}')\n");

                    properties.Add(entry);
                }
            }
            catch (Exception)
            {
            }

            group.ItemsSource = properties;
            groups.Add(group);
        }

        root.ItemsSource = groups;
        return root;
    }

    private void OpenApiHelp()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://dwsim.org/api_help/html/R_Project_DWSIM_Class_Library_Documentation.htm",
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
        }
    }

    // -------------------------------------------------------------------------
    // Output and status
    // -------------------------------------------------------------------------

    public void AppendOutput(string text)
    {
        OutputBox.Text = (OutputBox.Text ?? string.Empty) + text + "\n";
        OutputBox.CaretIndex = (OutputBox.Text ?? string.Empty).Length;
    }

    public void NotifyRunCompleted()
    {
        SetRunning(false);
        SetStatus("Done.");
    }

    private void SetRunning(bool running)
    {
        BtnRun.IsEnabled = !running;
        BtnRunAsync.IsEnabled = !running;
        BtnStop.IsEnabled = running;
    }

    private void SetStatus(string text) => StatusLabel.Text = text;

    private async System.Threading.Tasks.Task<bool> ConfirmAsync(string title, string message)
    {
        var result = false;

        var no = new Button { Content = "No", Width = 80, IsCancel = true };
        no.Classes.Add("dialog");
        var yes = new Button { Content = "Yes", Width = 80, IsDefault = true };
        yes.Classes.Add("dialog");

        var buttons = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new global::Avalonia.Thickness(0, 0, 16, 12)
        };
        buttons.Children.Add(no);
        buttons.Children.Add(yes);

        var body = new DockPanel();
        DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
        body.Children.Add(buttons);
        body.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            FontSize = 13,
            Margin = new global::Avalonia.Thickness(20, 20, 20, 0)
        });

        var dialog = new Window
        {
            Title = title,
            Width = 380,
            Height = 160,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = IconHelper.GetWindowIcon(),
            Content = body
        };

        yes.Click += (_, _) => { result = true; dialog.Close(); };
        no.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);

        return result;
    }

}
