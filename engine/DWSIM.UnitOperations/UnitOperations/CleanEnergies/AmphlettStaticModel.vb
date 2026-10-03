Imports System.Globalization
Imports System.Text

Namespace UnitOperations.Auxiliary

    ''' <summary>
    ''' Amphlett static polarisation model of a PEM fuel cell stack. Port of opem.Static.Amphlett
    ''' (OPEM, MIT licence): same equations, constants, current sweep and summary values.
    ''' </summary>
    Public NotInheritable Class AmphlettStaticModel

        Private Const xi1 As Double = -0.948
        Private Const xi3 As Double = 0.000076
        Private Const xi4 As Double = -0.000193
        Private Const HHV As Double = 1.482
        Private Const uF As Double = 0.95
        Private Const Eth As Double = 1.23
        Private Const R As Double = 8314.47
        Private Const F As Double = 96484600.0

        ''' <summary>Results of the current sweep, one entry per current.</summary>
        Public NotInheritable Class Result

            ''' <summary>Stack current, A.</summary>
            Public ReadOnly I As New List(Of Double)
            ''' <summary>Stack voltage, V.</summary>
            Public ReadOnly V As New List(Of Double)
            ''' <summary>Electric power of the stack, W.</summary>
            Public ReadOnly P As New List(Of Double)
            ''' <summary>Thermal power of the stack, W.</summary>
            Public ReadOnly Ph As New List(Of Double)
            ''' <summary>Cell efficiency (fraction).</summary>
            Public ReadOnly EFF As New List(Of Double)
            ''' <summary>Activation loss of one cell, V.</summary>
            Public ReadOnly EtaActivation As New List(Of Double)
            ''' <summary>Ohmic loss of one cell, V.</summary>
            Public ReadOnly EtaOhmic As New List(Of Double)
            ''' <summary>Concentration loss of one cell, V.</summary>
            Public ReadOnly EtaConcentration As New List(Of Double)
            ''' <summary>Stack voltage of the linear approximation, V.</summary>
            Public ReadOnly VE As New List(Of Double)

            ''' <summary>Number of cells in the stack.</summary>
            Public Property N As Double
            ''' <summary>Membrane water content actually used (OPEM moves values outside 14 to 23 to 23).</summary>
            Public Property Lambda As Double
            ''' <summary>Nernst voltage of one cell, V.</summary>
            Public Property Enernst As Double
            ''' <summary>Intercept of the linear approximation of the stack voltage, V.</summary>
            Public Property V0 As Double
            ''' <summary>Slope of the linear approximation of the stack voltage, V/A.</summary>
            Public Property K As Double
            ''' <summary>Maximum electric power, W.</summary>
            Public Property Pmax As Double
            ''' <summary>Stack voltage at maximum power, V.</summary>
            Public Property VAtPmax As Double
            ''' <summary>Efficiency at maximum power.</summary>
            Public Property EffAtPmax As Double
            ''' <summary>Integral of the stack voltage over the current range (OPEM "Ptotal(Elec)"), W.</summary>
            Public Property TotalElectric As Double
            ''' <summary>Integral of the thermal voltage over the current range (OPEM "Ptotal(Thermal)"), W.</summary>
            Public Property TotalThermal As Double
            ''' <summary>Maximum power of the linear approximation, W.</summary>
            Public Property PmaxLinear As Double
            ''' <summary>Stack voltage at the maximum power of the linear approximation, V.</summary>
            Public Property VAtPmaxLinear As Double
            ''' <summary>First current with a negative cell voltage, A; NaN when there is none.</summary>
            Public Property NegativeVoltageCurrent As Double = Double.NaN

        End Class

        ''' <summary>
        ''' Runs the sweep from <paramref name="iStart"/> up to, and excluding, the smaller of
        ''' <paramref name="iStop"/> and JMax * A.
        ''' </summary>
        ''' <param name="T">Cell temperature, K.</param>
        ''' <param name="PH2">Hydrogen partial pressure, atm.</param>
        ''' <param name="PO2">Oxygen partial pressure, atm.</param>
        ''' <param name="A">Active area, cm2.</param>
        ''' <param name="l">Membrane thickness, cm.</param>
        ''' <param name="N">Number of cells.</param>
        ''' <param name="Relec">Electronic resistance, ohm.</param>
        ''' <param name="JMax">Maximum current density, A/cm2.</param>
        Public Shared Function Run(T As Double, PH2 As Double, PO2 As Double, iStart As Double, iStop As Double, iStep As Double,
                                   A As Double, l As Double, lambda As Double, N As Double, Relec As Double, JMax As Double) As Result

            ' OPEM filter_lambda: a value outside 14 to 23 becomes 23
            If lambda > 23 OrElse lambda < 14 Then lambda = 23

            Dim iEnd As Double = Math.Min(JMax * A, iStop)
            iStep = Math.Abs(iStep)
            If iStart > iEnd Then
                Dim tmp As Double = iStart
                iStart = iEnd
                iEnd = tmp
            End If
            If iStep = 0 Then Throw New ArgumentException("The current step must not be zero.")
            Dim digits As Integer = Decimals(iStep)

            Dim res As New Result With {.N = N, .Lambda = lambda}

            res.Enernst = 1.229 - 0.00085 * (T - 298.15) + 0.00004308 * T * (Math.Log(PH2) + 0.5 * Math.Log(PO2))
            Dim B As Double = R * T / (2 * F)
            Dim CO2 As Double = PO2 / (5080000.0 * Math.Exp(-498 / T))
            Dim CH2 As Double = PH2 / (1090000.0 * Math.Exp(77 / T))
            Dim xi2 As Double = 0.00286 + 0.0002 * Math.Log(A) + 0.000043 * Math.Log(CH2)

            Dim i As Double = iStart
            Do While i < iEnd
                ' set explicitly: a VB local declared in a loop keeps its value from the previous pass
                Dim act As Double = 0, ohm As Double = 0, conc As Double = 0
                If i <> 0 Then
                    Dim J As Double = i / A
                    act = -(xi1 + xi2 * T + xi3 * T * Math.Log(CO2) + xi4 * T * Math.Log(i))
                    Dim rho As Double = 181.6 * (1 + 0.03 * J + 0.062 * (T / 303) ^ 2 * J ^ 2.5) /
                                        ((lambda - 0.634 - 3 * J) * Math.Exp(4.18 * (T - 303) / T))
                    ohm = i * (rho * l / A + Relec)
                    conc = -B * Math.Log(1 - J / JMax)
                End If
                Dim vcell As Double = res.Enernst - (act + ohm + conc)
                If vcell < 0 AndAlso Double.IsNaN(res.NegativeVoltageCurrent) Then res.NegativeVoltageCurrent = i
                res.I.Add(i)
                res.EtaActivation.Add(act)
                res.EtaOhmic.Add(ohm)
                res.EtaConcentration.Add(conc)
                res.V.Add(N * vcell)
                res.EFF.Add(uF * vcell / HHV)
                res.P.Add(N * vcell * i)
                res.Ph.Add(i * (N * Eth - N * vcell))
                i = Math.Round(i + iStep, digits)
            Loop

            Dim count As Integer = res.I.Count
            If count = 0 Then Return res

            ' linear approximation V = V0 + K I (OPEM estimate_coef)
            Dim mx As Double = res.I.Average(), my As Double = res.V.Average()
            Dim sxx As Double = 0, sxy As Double = 0
            For k As Integer = 0 To count - 1
                sxx += res.I(k) * res.I(k)
                sxy += res.I(k) * res.V(k)
            Next
            sxx -= count * mx * mx
            sxy -= count * mx * my
            If sxx <> 0 Then
                res.K = sxy / sxx
                res.V0 = my - res.K * mx
            End If
            For Each x As Double In res.I
                res.VE.Add(res.V0 + res.K * x)
            Next
            res.PmaxLinear = If(res.K <> 0, Math.Abs(res.V0 ^ 2 / (4 * res.K)), Double.NaN)
            res.VAtPmaxLinear = Math.Abs(res.V0 / 2)

            ' maximum power (first occurrence, as OPEM)
            Dim imax As Integer = res.P.IndexOf(res.P.Max())
            res.Pmax = res.P(imax)
            res.VAtPmax = res.V(imax)
            res.EffAtPmax = res.EFF(imax)

            res.TotalElectric = Simpson(res.V, iStep)
            res.TotalThermal = Simpson(res.V.Select(Function(v) N * Eth - v).ToList(), iStep)

            Return res

        End Function

        ''' <summary>OPEM integrate: Simpson weights 1, 4, 2, 4, ..., 1.</summary>
        Private Shared Function Simpson(y As List(Of Double), h As Double) As Double
            Dim total As Double = y(0) + y(y.Count - 1)
            For k As Integer = 1 To y.Count - 2
                total += If(k Mod 2 = 0, 2, 4) * y(k)
            Next
            Return total * (h / 3.0)
        End Function

        ''' <summary>Decimal places of the step, as OPEM get_precision.</summary>
        Private Shared Function Decimals(x As Double) As Integer
            Dim s As String = x.ToString("R", CultureInfo.InvariantCulture)
            Dim dot As Integer = s.IndexOf("."c)
            If dot < 0 OrElse s.Contains("E") Then Return 0
            Return s.Length - dot - 1
        End Function

        Private Shared Function Num(x As Double) As String
            Return x.ToString("R", CultureInfo.InvariantCulture)
        End Function

        Private Shared Function Html(s As String) As String
            Return Net.WebUtility.HtmlEncode(s)
        End Function

        Private Shared Function SummaryRows(res As Result) As List(Of String())
            Return New List(Of String()) From {
                New String() {"Pmax", "Maximum power", Num(res.Pmax), "W"},
                New String() {"VFC|Pmax", "Stack voltage at maximum power", Num(res.VAtPmax), "V"},
                New String() {"Efficiency|Pmax", "Cell efficiency at maximum power", Num(res.EffAtPmax), ""},
                New String() {"Ptotal(Elec)", "Total electrical power", Num(res.TotalElectric), "W"},
                New String() {"Ptotal(Thermal)", "Total thermal power", Num(res.TotalThermal), "W"},
                New String() {"V0", "Intercept of the linear approximation", Num(res.V0), "V"},
                New String() {"K", "Slope of the linear approximation", Num(res.K), "V/A"},
                New String() {"Pmax(L-Approx)", "Maximum power of the linear approximation", Num(res.PmaxLinear), "W"},
                New String() {"VFC|Pmax(L-Approx)", "Stack voltage at maximum power of the linear approximation", Num(res.VAtPmaxLinear), "V"}}
        End Function

        Private Shared Function Warning(res As Result) As String
            If Double.IsNaN(res.NegativeVoltageCurrent) Then Return ""
            Return "Warning: currents from " & Num(res.NegativeVoltageCurrent) & " A on give a negative cell voltage; check the inputs."
        End Function

        ''' <summary>One row per current, with the columns of the OPEM CSV report.</summary>
        Public Shared Function CsvReport(res As Result) As String
            Dim sb As New StringBuilder()
            sb.Append("I (A),Enernst (V),Eta Activation (V),Eta Concentration (V),Eta Ohmic (V),Loss (V),PEM Efficiency (),")
            sb.Append("Power (W),Power-Stack (W),Power-Thermal (W),VStack (V),Vcell (V)").Append(vbLf)
            For k As Integer = 0 To res.I.Count - 1
                Dim vcell As Double = res.V(k) / res.N
                Dim loss As Double = res.EtaActivation(k) + res.EtaOhmic(k) + res.EtaConcentration(k)
                sb.Append(String.Join(",", {res.I(k), res.Enernst, res.EtaActivation(k), res.EtaConcentration(k), res.EtaOhmic(k), loss,
                                             res.EFF(k), vcell * res.I(k), res.P(k), res.Ph(k), res.V(k), vcell}.Select(Function(x) Num(x))))
                sb.Append(vbLf)
            Next
            Return sb.ToString()
        End Function

        Private Shared Function Line(name As String, value As String, units As String, description As String) As String
            Dim s As String = name & " : " & value
            If units <> "" Then s &= " " & units
            If description <> "" Then s &= " (" & description & ")"
            Return s
        End Function

        ''' <summary>Plain-text report: inputs and summary.</summary>
        Public Shared Function TextReport(res As Result, inputs As IEnumerable(Of PEMFuelCellModelParameter)) As String
            Dim sb As New StringBuilder()
            sb.Append("Amphlett static model").Append(vbLf).Append(vbLf)
            sb.Append("Simulation inputs").Append(vbLf).Append(vbLf)
            For Each p In inputs
                sb.Append(Line(p.Name, Num(p.Value), p.Units, p.Description)).Append(vbLf)
            Next
            sb.Append(vbLf).Append("Results").Append(vbLf).Append(vbLf)
            sb.Append(String.Format("Points : {0}, from {1} A to {2} A", res.I.Count, Num(res.I.First()), Num(res.I.Last()))).Append(vbLf)
            sb.Append(String.Format("Enernst : {0} V", Num(res.Enernst))).Append(vbLf)
            For Each row In SummaryRows(res)
                sb.Append(Line(row(0), row(2), row(3), row(1))).Append(vbLf)
            Next
            Dim w As String = Warning(res)
            If w <> "" Then sb.Append(vbLf).Append(w).Append(vbLf)
            Return sb.ToString()
        End Function

        ''' <summary>HTML report: inputs, summary and the table of the sweep.</summary>
        Public Shared Function HtmlReport(res As Result, inputs As IEnumerable(Of PEMFuelCellModelParameter)) As String
            Dim sb As New StringBuilder()
            sb.Append("<!DOCTYPE html><html><head><meta charset=""utf-8""><title>Amphlett static model</title>")
            sb.Append("<style>body{font-family:sans-serif;margin:16px}table{border-collapse:collapse;margin-bottom:16px}")
            sb.Append("th,td{border:1px solid #999;padding:3px 8px;text-align:right}th{background:#eee}")
            sb.Append("td.l,th.l{text-align:left}.w{color:#b00}</style></head><body>")
            sb.Append("<h2>Amphlett static model</h2>")
            Dim w As String = Warning(res)
            If w <> "" Then sb.Append("<p class=""w"">").Append(Html(w)).Append("</p>")
            sb.Append("<h3>Inputs</h3><table><tr><th class=""l"">Name</th><th class=""l"">Description</th><th>Value</th><th class=""l"">Units</th></tr>")
            For Each p In inputs
                sb.Append("<tr><td class=""l"">").Append(Html(p.Name)).Append("</td><td class=""l"">").Append(Html(p.Description))
                sb.Append("</td><td>").Append(Num(p.Value)).Append("</td><td class=""l"">").Append(Html(p.Units)).Append("</td></tr>")
            Next
            sb.Append("</table><h3>Summary</h3><table><tr><th class=""l"">Name</th><th class=""l"">Description</th><th>Value</th><th class=""l"">Units</th></tr>")
            For Each row In SummaryRows(res)
                sb.Append("<tr><td class=""l"">").Append(Html(row(0))).Append("</td><td class=""l"">").Append(Html(row(1)))
                sb.Append("</td><td>").Append(row(2)).Append("</td><td class=""l"">").Append(Html(row(3))).Append("</td></tr>")
            Next
            sb.Append("</table><h3>Sweep</h3><table><tr><th>I (A)</th><th>VStack (V)</th><th>Power-Stack (W)</th><th>Power-Thermal (W)</th>")
            sb.Append("<th>Efficiency</th><th>Eta Activation (V)</th><th>Eta Ohmic (V)</th><th>Eta Concentration (V)</th></tr>")
            For k As Integer = 0 To res.I.Count - 1
                sb.Append("<tr>")
                For Each x In {res.I(k), res.V(k), res.P(k), res.Ph(k), res.EFF(k), res.EtaActivation(k), res.EtaOhmic(k), res.EtaConcentration(k)}
                    sb.Append("<td>").Append(x.ToString("G6", CultureInfo.InvariantCulture)).Append("</td>")
                Next
                sb.Append("</tr>")
            Next
            sb.Append("</table></body></html>")
            Return sb.ToString()
        End Function

    End Class

End Namespace
