using System;
using Avalonia.Threading;
using DWSIM.Interfaces;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Concrete IFlowsheet implementation for the Avalonia host.
/// Extends FlowsheetBase with UI callbacks wired to Avalonia Dispatcher
/// and canvas invalidation, without any Eto.Forms dependency.
/// </summary>
internal sealed class AvaloniaFlowsheet : DWSIM.FlowsheetBase.FlowsheetBase
{
    private Action<string, IFlowsheet.MessageType>? _listener;

    /// <summary>Called when the engine emits a log/status message.</summary>
    public Action<string>? OnMessage { get; set; }

    /// <summary>
    /// The same message with what the log panel needs to show it: how serious it is and the
    /// exception behind it, when the engine recorded one.
    /// </summary>
    public Action<string, IFlowsheet.MessageType, string>? OnLogMessage { get; set; }

    /// <summary>Called when the engine asks the UI to refresh (UpdateInterface/UpdateInformation).</summary>
    public Action? OnUpdateInterface { get; set; }

    /// <summary>Called when the engine asks the UI to refresh open property editors (e.g. after a solve).</summary>
    public Action? OnUpdateOpenEditForms { get; set; }

    /// <summary>Called when the engine asks the UI to close open property editors (e.g. on flowsheet swap).</summary>
    public Action? OnCloseOpenEditForms { get; set; }

    public override bool SupressMessages { get; set; }

    public AvaloniaFlowsheet()
    {
        // The solver checks this flag before every solve; must be true to allow calculation.
        DWSIM.GlobalSettings.Settings.CalculatorActivated = true;
    }

    public override IFlowsheet GetNewInstance() => new AvaloniaFlowsheet();

    /// <summary>
    /// Deep copy through the XML round-trip, matching the Eto host. The dynamics integrator
    /// takes one of these at the start of a run to resolve event-list property values.
    /// </summary>
    public override IFlowsheet Clone()
    {
        var fs = new AvaloniaFlowsheet();
        fs.Initialize();
        fs.LoadFromXML(SaveToXML());
        return fs;
    }

    public override void DisplayForm(object form)
    {
        // Engine asks the host to show a sub-form (e.g. column convergence inspector).
        // Avalonia host has no direct mapping; surface the request via the message channel
        // so it shows up in the log instead of being silently dropped.
        if (form is null) return;
        ShowMessage($"[engine] DisplayForm requested ({form.GetType().Name}); only available in Classic UI.",
            IFlowsheet.MessageType.Information);
    }

    public override void ShowDebugInfo(string text, int level)
    {
        if (string.IsNullOrEmpty(text)) return;
        ShowMessage($"[debug:{level}] {text}", IFlowsheet.MessageType.Information);
    }

    public override void ShowMessage(string text, IFlowsheet.MessageType mtype, string exceptionID = "")
    {
        // The engine sets SupressMessages for long unattended runs (the dynamics integrator,
        // the optimizer). Honouring it here matters: the solver emits a message per object per
        // step, and posting every one of those to the log saturates the UI thread.
        if (SupressMessages && mtype != IFlowsheet.MessageType.GeneralError) return;

        OnMessage?.Invoke(text);
        OnLogMessage?.Invoke(text, mtype, exceptionID);
        _listener?.Invoke(text, mtype);
    }

    public override void UpdateOpenEditForms() =>
        Dispatcher.UIThread.Post(() => OnUpdateOpenEditForms?.Invoke());

    public override void CloseOpenEditForms() =>
        Dispatcher.UIThread.Post(() => OnCloseOpenEditForms?.Invoke());

    public override void RunCodeOnUIThread(Action act) =>
        Dispatcher.UIThread.Post(act);

    public override void SetMessageListener(Action<string, IFlowsheet.MessageType> act) =>
        _listener = act;

    public override void UpdateInformation() =>
        Dispatcher.UIThread.Post(() => OnUpdateInterface?.Invoke());

    public override void UpdateInterface() =>
        Dispatcher.UIThread.Post(() => OnUpdateInterface?.Invoke());

    public override object GetApplicationObject() => null!;
}
