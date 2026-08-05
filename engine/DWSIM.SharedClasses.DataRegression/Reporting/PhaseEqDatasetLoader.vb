Imports System.Globalization
Imports DWSIM.PhaseEquilibriumData.Core

Namespace Global.DWSIM.SharedClasses.DataRegression.Reporting

    ''' <summary>
    ''' Converts a ThermoML/PhaseEq dataset into RegressionDataPoint rows in
    ''' the user's display units (tUnit / pUnit, supplied by the caller). The
    ''' source dataset uses K for temperature and kPa for pressure.
    ''' </summary>
    Public Module PhaseEqDatasetLoader

        Public Function ToRegressionPoints(ds As PhaseEquilibriumDataset,
                                           tUnit As String, pUnit As String,
                                           swap As Boolean) As List(Of RegressionDataPoint)
            Dim rows As New List(Of RegressionDataPoint)
            If ds Is Nothing Then Return rows

            ' Find isobaric/isothermal constraint values (K and kPa).
            Dim constT As Double? = Nothing
            Dim constP As Double? = Nothing
            For Each c In ds.Constraints
                If c.Kind = ConstraintKind.Temperature Then constT = c.Value
                If c.Kind = ConstraintKind.Pressure Then constP = c.Value
            Next

            For Each dpt In ds.Points
                Dim tK As Double = If(constT.HasValue, constT.Value, 0.0)
                Dim pKPa As Double = If(constP.HasValue, constP.Value, 0.0)
                Dim x1 As Double? = Nothing
                Dim y1 As Double? = Nothing

                For Each kv In dpt.Values
                    Dim k = kv.Key
                    If k.IndexOf("Temperature", StringComparison.OrdinalIgnoreCase) >= 0 Then
                        tK = kv.Value
                    ElseIf k.IndexOf("Pressure", StringComparison.OrdinalIgnoreCase) >= 0 Then
                        ' Binary VLE datasets often report the equilibrium pressure as "Vapor pressure"
                        ' or "Bubble point pressure"; treat any Pressure-substring variable as the
                        ' system pressure for the point.
                        pKPa = kv.Value
                    ElseIf k.IndexOf("Mole fraction", StringComparison.OrdinalIgnoreCase) >= 0 _
                        OrElse k.IndexOf("Mass fraction", StringComparison.OrdinalIgnoreCase) >= 0 Then
                        ' Composition variables are tagged "... | ord=N | phase=X" by the parser.
                        ' Keep only component 1 (first in ComponentOrder); legacy untagged keys
                        ' from pre-ord-suffix parsers are treated as component 1 / liquid.
                        Dim isComp1 As Boolean = k.IndexOf("ord=1", StringComparison.OrdinalIgnoreCase) >= 0 _
                            OrElse k.IndexOf("ord=", StringComparison.OrdinalIgnoreCase) < 0
                        If Not isComp1 Then Continue For
                        Dim isVapor As Boolean = k.IndexOf("Gas", StringComparison.OrdinalIgnoreCase) >= 0 _
                            OrElse k.IndexOf("Vapor", StringComparison.OrdinalIgnoreCase) >= 0 _
                            OrElse k.IndexOf("Vapour", StringComparison.OrdinalIgnoreCase) >= 0
                        If isVapor Then
                            If Not y1.HasValue Then y1 = kv.Value
                        Else
                            If Not x1.HasValue Then x1 = kv.Value
                        End If
                    End If
                Next

                Dim xVal As Double = If(x1.HasValue, x1.Value, 0.0)
                Dim yVal As Double = If(y1.HasValue, y1.Value, 0.0)
                ' A point with neither a T nor a P value has nothing to regress against - skip it.
                If tK <= 0.0 AndAlso pKPa <= 0.0 Then Continue For

                Dim tOut = SystemsOfUnits.Converter.ConvertFromSI(tUnit, tK)
                Dim pOut = SystemsOfUnits.Converter.ConvertFromSI(pUnit, pKPa * 1000.0)

                If swap Then
                    rows.Add(New RegressionDataPoint With {
                        .Use = True, .X1 = 1.0 - xVal, .Y1 = 1.0 - yVal, .T = tOut, .P = pOut})
                Else
                    rows.Add(New RegressionDataPoint With {
                        .Use = True, .X1 = xVal, .Y1 = yVal, .T = tOut, .P = pOut})
                End If
            Next

            Return rows
        End Function

        ''' <summary>
        ''' Maps the regression utility's DataType to a phase-equilibrium type
        ''' filter for the ThermoML index pre-search. Null = no pre-filter (let
        ''' the picker dialog show all matches).
        ''' </summary>
        Public Function TypeFilterFor(dt As Models.DataType) As EquilibriumType?
            Select Case dt
                Case Models.DataType.Txy : Return EquilibriumType.VLE_Isobaric
                Case Models.DataType.Pxy : Return EquilibriumType.VLE_Isothermal
                Case Models.DataType.TPxy : Return Nothing
                Case Models.DataType.Txx, Models.DataType.Pxx, Models.DataType.TPxx : Return EquilibriumType.LLE
                Case Models.DataType.TTxSE, Models.DataType.TTxSS : Return EquilibriumType.SLE
                Case Else : Return Nothing
            End Select
        End Function

    End Module

End Namespace
