# 17 — Dynamic Simulation (Tank Level Control)

Loads a pre-configured dynamic flowsheet (a stirred tank with a PID level
controller), applies a step-change disturbance on the inlet flow at t = 60 s,
and logs the monitored-variable time series to a CSV file.

**Pre-requisite:** the flowsheet file must already have a dynamics schedule
configured in DWSIM's Dynamics Manager. The Fluent API runs the integration
headlessly — it does not build schedules from scratch.

=== "C#"

    ```csharp
    using System;
    using System.IO;
    using System.Linq;
    using DWSIM.Automation.FluentAPI;
    using DWSIM.Automation.DynamicRunner;
    using DWSIM.Interfaces;

    Flowsheet.RegisterAssemblyResolver();
    var fs = Flowsheet.Load(@"C:\simulations\tank_control.dwxmz");

    var epoch = new DateTime();

    var result = fs.RunDynamics("Default Schedule")
        .WithRealTime(false)
        .OnPreStep((s, e) =>
        {
            // step-change: double the inlet flow at t = 60 s
            double t = (e.tstamp - epoch).TotalSeconds;
            if (t >= 60 && t < 61)
            {
                var inlet = (IMaterialStream)e.flowsheet.GetObject("Inlet");
                inlet.SetMassFlow(2.5);   // kg/s (SI)
            }
        })
        .OnPostStep((s, e) =>
        {
            double t = (e.tstamp - epoch).TotalSeconds;
            Console.Write($"\rt = {t,6:F1} s");
        })
        .Execute();

    Console.WriteLine();

    if (!result.Completed)
    {
        Console.WriteLine($"Integration failed: {result.Error!.Message}");
        return;
    }

    // print final-value summary
    foreach (var (name, series) in result.MonitoredVariables)
        Console.WriteLine($"{name,-30} final = {series.Last().Value:G6}  ({series.Count} pts)");

    // export to CSV
    using var csv = new StreamWriter("dynamics_out.csv");
    var headers = result.MonitoredVariables.Keys.ToList();
    csv.WriteLine("t_s," + string.Join(",", headers));
    int n = result.MonitoredVariables.Values.First().Count;
    for (int i = 0; i < n; i++)
    {
        double t = result.MonitoredVariables.Values.First()[i].TimeSeconds;
        var vals = headers.Select(h => result.MonitoredVariables[h][i].Value.ToString("G6"));
        csv.WriteLine($"{t:F2},{string.Join(",", vals)}");
    }
    ```

=== "Python"

    ```python
    import sys, clr, csv as csvmod
    sys.path.append(r"C:\path\to\DWSIM\bin\x64\Debug")
    clr.AddReference("DWSIM.Automation.FluentAPI")
    clr.AddReference("DWSIM.Automation.DynamicRunner")

    from System import DateTime
    from DWSIM.Automation.FluentAPI import Flowsheet
    from DWSIM.Automation.DynamicRunner import Runner

    Flowsheet.RegisterAssemblyResolver()
    fs = Flowsheet.Load(r"C:\simulations\tank_control.dwxmz")

    epoch = DateTime()

    def on_pre(sender, e):
        t = (e.tstamp - epoch).TotalSeconds
        if 60 <= t < 61:
            inlet = e.flowsheet.GetObject("Inlet")
            inlet.SetMassFlow(2.5)

    def on_post(sender, e):
        t = (e.tstamp - epoch).TotalSeconds
        print(f"\rt = {t:6.1f} s", end="")

    result = (fs.RunDynamics("Default Schedule")
                .WithRealTime(False)
                .OnPreStep(Runner.IntegratorPreStepEventHandler(on_pre))
                .OnPostStep(Runner.IntegratorPostStepEventHandler(on_post))
                .Execute())

    print()

    if not result.Completed:
        print(f"Integration failed: {result.Error.Message}")
    else:
        for name, series in result.MonitoredVariables:
            print(f"{name}: {series.Count} pts, final = {list(series)[-1].Value:.4g}")

        # export CSV
        headers = list(result.MonitoredVariables.Keys)
        with open("dynamics_out.csv", "w", newline="") as f:
            w = csvmod.writer(f)
            w.writerow(["t_s"] + headers)
            series0 = list(result.MonitoredVariables[headers[0]])
            for i, pt in enumerate(series0):
                row = [f"{pt.TimeSeconds:.2f}"]
                for h in headers:
                    row.append(f"{list(result.MonitoredVariables[h])[i].Value:.6g}")
                w.writerow(row)
    ```

=== "VB.NET"

    ```vbnet
    Imports System
    Imports System.IO
    Imports System.Linq
    Imports DWSIM.Automation.FluentAPI
    Imports DWSIM.Automation.DynamicRunner
    Imports DWSIM.Interfaces

    Flowsheet.RegisterAssemblyResolver()
    Dim fs = Flowsheet.Load("C:\simulations\tank_control.dwxmz")
    Dim epoch As New DateTime()

    Dim result = fs.RunDynamics("Default Schedule") _
        .WithRealTime(False) _
        .OnPreStep(Sub(s, e)
                       Dim t = (e.tstamp - epoch).TotalSeconds
                       If t >= 60 AndAlso t < 61 Then
                           Dim inlet = CType(e.flowsheet.GetObject("Inlet"), IMaterialStream)
                           inlet.SetMassFlow(2.5)
                       End If
                   End Sub) _
        .OnPostStep(Sub(s, e)
                        Console.Write($"t = {(e.tstamp - epoch).TotalSeconds:F1} s")
                    End Sub) _
        .Execute()

    If Not result.Completed Then
        Console.WriteLine($"Integration failed: {result.Error.Message}")
    Else
        For Each kv In result.MonitoredVariables
            Dim last = kv.Value.Last()
            Console.WriteLine($"{kv.Key}: final = {last.Value:G6} ({kv.Value.Count} pts)")
        Next
    End If
    ```

The CSV output has one column per monitored variable and one row per
integration step, with simulation time (seconds) in the first column.
