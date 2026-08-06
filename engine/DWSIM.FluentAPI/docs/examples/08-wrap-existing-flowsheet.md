# 08 — Wrap an Existing Flowsheet

Use the Fluent API on an `IFlowsheet` that is already alive — for instance,
the flowsheet of an open DWSIM editing session, an extender plugin, or the
AI-assistant host. `Flowsheet.Wrap(existing)` reuses the same instance, so
edits land on the live document.

=== "Python (extender script)"

    ```python
    # Inside a DWSIM Python extender — the host passes an IFlowsheet to your callback.
    from DWSIM.Automation.FluentAPI import Flowsheet, Q

    def run(flowsheet):
        fs = Flowsheet.Wrap(flowsheet)

        fs.AddHeater("H-NEW") \
          .WithOutletTemperature(Q.Kelvin(350)) \
          .WithPressureDrop(Q.Bar(0.5)) \
          .ConnectFeed(fs.MaterialStream("inlet")) \
          .ConnectProduct(fs.MaterialStream("outlet"))

        fs.Solve()
    ```

=== "C# (extender plugin)"

    ```csharp
    using DWSIM.Automation.FluentAPI;
    using DWSIM.Interfaces;

    public class MyExtender
    {
        public void Run(IFlowsheet flowsheet)
        {
            var fs = Flowsheet.Wrap(flowsheet);

            fs.AddHeater("H-NEW")
              .WithOutletTemperature(350.Kelvin())
              .WithPressureDrop(0.5.Bar())
              .ConnectFeed(fs.MaterialStream("inlet"))
              .ConnectProduct(fs.MaterialStream("outlet"));

            fs.Solve();
        }
    }
    ```

=== "VB.NET (extender plugin)"

    ```vbnet
    Imports DWSIM.Automation.FluentAPI
    Imports DWSIM.Interfaces

    Public Class MyExtender
        Public Sub Run(flowsheet As IFlowsheet)
            Dim fs = Flowsheet.Wrap(flowsheet)

            fs.AddHeater("H-NEW") _
              .WithOutletTemperature(350.0.Kelvin()) _
              .WithPressureDrop(0.5.Bar()) _
              .ConnectFeed(fs.MaterialStream("inlet")) _
              .ConnectProduct(fs.MaterialStream("outlet"))

            fs.Solve()
        End Sub
    End Class
    ```

`Flowsheet.MaterialStream(tag)` and `Flowsheet.EnergyStream(tag)` look up
existing streams by their tag — handy when scripting against a flowsheet
you didn't build yourself. `fs.Inner` exposes the underlying `IFlowsheet`
when the fluent surface isn't enough.
