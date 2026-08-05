'    CFB Fast Pyrolysis - Axial trajectory container (1-D PFR profile).
'    Holds z-indexed arrays of temperature, cumulative vapor residence time,
'    and per-species mass fractions (9 species from the reduced Ranzi scheme).
'    Used for the Results &amp; Charts dialog and CSV export.
'    Copyright 2026 Daniel Wagner O. de Medeiros

Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.Globalization
Imports System.Text
Imports DWSIM.UnitOperations.Reactors.CFBPyrolysis

Namespace Reactors

    ''' <summary>
    ''' Axial profile of a CFB fast-pyrolysis riser integration. Each list has the same
    ''' length N (number of axial cells + 1 for the inlet sample). Mass fractions are on
    ''' a total-reacting-mass basis (solid + vapor phase lumped). Temperatures are in K,
    ''' positions in meters, cumulative vapor residence time in seconds.
    ''' </summary>
    <Serializable()> Public Class CFBPyrolysisTrajectoryResult

        Public Property Z_m As New List(Of Double)()
        Public Property T_K As New List(Of Double)()
        Public Property VaporResidenceTime_s As New List(Of Double)()
        Public Property SolidVelocity_ms As New List(Of Double)()
        Public Property GasVelocity_ms As New List(Of Double)()
        Public Property SolidsHoldup As New List(Of Double)()

        ''' <summary>Species-by-species axial mass-fraction tracks (keyed by species name).</summary>
        Public Property Species As New Dictionary(Of String, List(Of Double))()

        ' ---------- Summary results at the outlet -----------
        Public Property OutletYield_Oil As Double = 0.0
        Public Property OutletYield_Gas As Double = 0.0
        Public Property OutletYield_Char As Double = 0.0
        Public Property OutletYield_UnreactedSolid As Double = 0.0
        Public Property OutletTemperature_K As Double = 0.0
        Public Property OutletVaporResidenceTime_s As Double = 0.0
        Public Property RequiredSandCirculation_kgps As Double = 0.0
        Public Property SandInletTemperature_K As Double = 0.0
        Public Property SandOutletTemperature_K As Double = 0.0
        Public Property NetPyrolysisDuty_kW As Double = 0.0

        ''' <summary>True when the char-combustor loop was active for this run.</summary>
        Public Property InternalCharCombustor As Boolean = False
        Public Property CharCombustorDuty_kW As Double = 0.0
        Public Property CharCombustorAirFlow_kgps As Double = 0.0
        Public Property CharCombustorFlueT_K As Double = 0.0

        ''' <summary>Names of all series available for charting (position plus species plus
        ''' auxiliary hydrodynamic/thermal variables).</summary>
        Public Function AvailableSeries() As String()
            Dim names As New List(Of String) From {
                "T_K", "VaporResidenceTime_s", "SolidVelocity_ms", "GasVelocity_ms", "SolidsHoldup"}
            For Each k In Species.Keys : names.Add(k) : Next
            Return names.ToArray()
        End Function

        Public Function GetSeries(name As String) As Double()
            Select Case name
                Case "T_K" : Return T_K.ToArray()
                Case "VaporResidenceTime_s" : Return VaporResidenceTime_s.ToArray()
                Case "SolidVelocity_ms" : Return SolidVelocity_ms.ToArray()
                Case "GasVelocity_ms" : Return GasVelocity_ms.ToArray()
                Case "SolidsHoldup" : Return SolidsHoldup.ToArray()
                Case Else
                    If Species.ContainsKey(name) Then Return Species(name).ToArray()
                    Return New Double() {}
            End Select
        End Function

        Public Function ToCSV() As String
            Dim sb As New StringBuilder()
            Dim ci = CultureInfo.InvariantCulture
            Dim keys = AvailableSeries()
            sb.Append("z_m")
            For Each k In keys : sb.Append(",") : sb.Append(k) : Next
            sb.AppendLine()
            For i = 0 To Z_m.Count - 1
                sb.Append(Z_m(i).ToString("G10", ci))
                For Each k In keys
                    Dim arr = GetSeries(k)
                    Dim v As Double = If(i < arr.Length, arr(i), Double.NaN)
                    sb.Append(",") : sb.Append(v.ToString("G10", ci))
                Next
                sb.AppendLine()
            Next
            sb.AppendLine()
            sb.AppendLine("# Summary")
            sb.AppendLine("# OutletTemperature_K," & OutletTemperature_K.ToString("G10", ci))
            sb.AppendLine("# OutletVaporResidenceTime_s," & OutletVaporResidenceTime_s.ToString("G10", ci))
            sb.AppendLine("# OutletYield_Oil," & OutletYield_Oil.ToString("G10", ci))
            sb.AppendLine("# OutletYield_Gas," & OutletYield_Gas.ToString("G10", ci))
            sb.AppendLine("# OutletYield_Char," & OutletYield_Char.ToString("G10", ci))
            sb.AppendLine("# OutletYield_UnreactedSolid," & OutletYield_UnreactedSolid.ToString("G10", ci))
            sb.AppendLine("# RequiredSandCirculation_kgps," & RequiredSandCirculation_kgps.ToString("G10", ci))
            sb.AppendLine("# SandInletTemperature_K," & SandInletTemperature_K.ToString("G10", ci))
            sb.AppendLine("# SandOutletTemperature_K," & SandOutletTemperature_K.ToString("G10", ci))
            sb.AppendLine("# NetPyrolysisDuty_kW," & NetPyrolysisDuty_kW.ToString("G10", ci))
            If InternalCharCombustor Then
                sb.AppendLine("# CharCombustorDuty_kW," & CharCombustorDuty_kW.ToString("G10", ci))
                sb.AppendLine("# CharCombustorAirFlow_kgps," & CharCombustorAirFlow_kgps.ToString("G10", ci))
                sb.AppendLine("# CharCombustorFlueT_K," & CharCombustorFlueT_K.ToString("G10", ci))
            End If
            Return sb.ToString()
        End Function

        Public Function ToDataTable() As DataTable
            Dim dt As New DataTable("CFBPyrolysisProfile")
            dt.Columns.Add("z_m", GetType(Double))
            Dim keys = AvailableSeries()
            For Each k In keys : dt.Columns.Add(k, GetType(Double)) : Next
            For i = 0 To Z_m.Count - 1
                Dim row = dt.NewRow()
                row("z_m") = Z_m(i)
                For Each k In keys
                    Dim arr = GetSeries(k)
                    If i < arr.Length Then
                        row(k) = arr(i)
                    Else
                        row(k) = DBNull.Value
                    End If
                Next
                dt.Rows.Add(row)
            Next
            Return dt
        End Function

    End Class

End Namespace
