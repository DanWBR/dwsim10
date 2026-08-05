Imports DWSIM.Interfaces

Public Class Evaluation

    Public Shared Function GetScore(flowsheet As IFlowsheet) As Double

        Dim equipments = flowsheet.SimulationObjects.Values.Where(Function(o) TypeOf o Is IUnitOperation And TypeOf o IsNot IIndicator)

        Dim eb, totalEC, totalEG, totalMF, totalMP As Double
        Dim nf, np As Integer

        totalEC = 0
        totalEG = 0

        For Each eq In equipments
            eb = eq.GetPowerGeneratedOrConsumed()
            If eb < 0 Then totalEC += eb
            If eb > 0 Then totalEG += eb
        Next

        Dim streams = flowsheet.SimulationObjects.Values.Where(Function(o) TypeOf o Is IMaterialStream).Select(Function(o) DirectCast(o, IMaterialStream))

        Dim dpFeeds As New List(Of Double), dpProducts As New List(Of Double)
        Dim dp As Double, vec As Double()

        nf = 0
        np = 0
        For Each s In streams
            Dim t = s.GetTemperature()
            Dim p = s.GetPressure()
            Dim mf = s.GetMassFlow()
            If Not DirectCast(s, ISimulationObject).GraphicObject.InputConnectors(0).IsAttached Then
                vec = s.GetOverallComposition()
                dp = MathNet.Numerics.Statistics.Statistics.StandardDeviation(vec)
                dpFeeds.Add(dp)
                totalMF += mf
                nf += 1
            End If
            If Not DirectCast(s, ISimulationObject).GraphicObject.OutputConnectors(0).IsAttached Then
                vec = s.GetOverallComposition()
                dp = MathNet.Numerics.Statistics.Statistics.StandardDeviation(vec)
                dpProducts.Add(dp)
                totalMP += mf
                np += 1
            End If
        Next

        Dim nr As Integer = 0
        Dim conversions As New Dictionary(Of String, Double)
        Dim counters As New Dictionary(Of String, Integer)

        For Each kr In flowsheet.FlowsheetOptions.Metadata.KeyReactants
            If Not conversions.ContainsKey(kr) Then conversions(kr) = 0.0
            If Not counters.ContainsKey(kr) Then counters(kr) = 0.0
            Dim reactors As IEnumerable(Of IReactor) = flowsheet.SimulationObjects.Values.Where(Function(o) TypeOf o Is IReactor).Select(Function(o) DirectCast(o, IReactor))
            For Each r In reactors
                If r.ComponentConversions.ContainsKey(kr) Then
                    conversions(kr) += r.ComponentConversions(kr)
                    counters(kr) += 1
                End If
            Next
            conversions(kr) /= counters(kr)
            If Double.IsNaN(conversions(kr)) Then conversions(kr) = 0.0
        Next

        Dim hxs As IEnumerable(Of IHeatExchanger) = flowsheet.SimulationObjects.Values.Where(Function(o) TypeOf o Is IHeatExchanger).Select(Function(o) DirectCast(o, IHeatExchanger))
        Dim totalhxarea, totalhxeff As Double
        For Each hx In hxs
            totalhxarea += hx.Area
            totalhxeff += hx.Efficiency
        Next

        Dim totalScore, wEG, wEC, wdp, wC, wHX, wEN As Double

        wEG = 0.1
        wEC = 0.1
        wdp = 1000
        wC = 1
        wHX = 1
        wEN = 1

        If (dpProducts.Count > 0 And dpFeeds.Count > 0) Then
            totalScore = wEG * totalEG / totalMF + wEC * totalEC / totalMF + wdp * (dpProducts.Average() - dpFeeds.Average())
        Else
            totalScore = wEG * totalEG / totalMF + wEC * totalEC / totalMF
        End If

        If nr > 0 Then totalScore += wC * conversions.Values.Average()

        If hxs.Count > 0 Then totalScore += wHX * (totalhxarea / hxs.Count + totalhxeff / hxs.Count)

        totalScore -= wEN * equipments.Count / 100.0

        Return totalScore

    End Function

End Class