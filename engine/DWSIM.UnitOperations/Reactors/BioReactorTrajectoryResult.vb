'    BioReactor - Trajectory container (time series of states) for charting & export.
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.

Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.Globalization
Imports System.Text

Namespace Reactors

    ''' <summary>
    ''' Time-series of a BioReactor integration run. Used for the Results &amp; Charts
    ''' dialog and CSV export. All concentrations are in g/L, rates in g/L/h, times
    ''' in seconds (converted to hours for display). In EnzymaticHydrolysis mode
    ''' the Cellulose / Hemicellulose / Glucose / Xylose lists replace the growth
    ''' series X / S / P, which are kept empty.
    ''' </summary>
    <Serializable()> Public Class BioReactorTrajectoryResult

        Public Property Mode As String = "Growth"   ' "Growth" or "EnzymaticHydrolysis"

        ' Time axis
        Public Property Times As New List(Of Double)()   ' seconds

        ' --- Growth-kinetics series ---
        Public Property X As New List(Of Double)()       ' g/L biomass
        Public Property S As New List(Of Double)()       ' g/L substrate
        Public Property P As New List(Of Double)()       ' g/L product
        Public Property Mu As New List(Of Double)()      ' 1/h
        Public Property qS As New List(Of Double)()      ' g S / g X / h
        Public Property qP As New List(Of Double)()      ' g P / g X / h
        Public Property OUR As New List(Of Double)()     ' g O2 / L / h
        Public Property CER As New List(Of Double)()     ' g CO2 / L / h
        Public Property RQ As New List(Of Double)()      ' mol CO2 / mol O2

        ' --- Enzymatic-hydrolysis series ---
        Public Property Cellulose As New List(Of Double)()
        Public Property Hemicellulose As New List(Of Double)()
        Public Property Glucose As New List(Of Double)()
        Public Property Xylose As New List(Of Double)()

        Public Function AvailableSeries() As String()
            If Mode = "EnzymaticHydrolysis" Then
                Return New String() {"Cellulose", "Hemicellulose", "Glucose", "Xylose"}
            End If
            Return New String() {"X", "S", "P", "Mu", "qS", "qP", "OUR", "CER", "RQ"}
        End Function

        Public Function GetSeries(name As String) As Double()
            Select Case name
                Case "X" : Return X.ToArray()
                Case "S" : Return S.ToArray()
                Case "P" : Return P.ToArray()
                Case "Mu" : Return Mu.ToArray()
                Case "qS" : Return qS.ToArray()
                Case "qP" : Return qP.ToArray()
                Case "OUR" : Return OUR.ToArray()
                Case "CER" : Return CER.ToArray()
                Case "RQ" : Return RQ.ToArray()
                Case "Cellulose" : Return Cellulose.ToArray()
                Case "Hemicellulose" : Return Hemicellulose.ToArray()
                Case "Glucose" : Return Glucose.ToArray()
                Case "Xylose" : Return Xylose.ToArray()
                Case Else : Return New Double() {}
            End Select
        End Function

        ''' <summary>Return time in hours for charting.</summary>
        Public Function GetTimesHours() As Double()
            Dim n = Times.Count
            Dim a(n - 1) As Double
            For i = 0 To n - 1
                a(i) = Times(i) / 3600.0
            Next
            Return a
        End Function

        Public Function ToCSV() As String
            Dim sb As New StringBuilder()
            Dim keys = AvailableSeries()
            sb.Append("time_h")
            For Each k In keys : sb.Append(",") : sb.Append(k) : Next
            sb.AppendLine()
            Dim ci = CultureInfo.InvariantCulture
            Dim th = GetTimesHours()
            For i = 0 To Times.Count - 1
                sb.Append(th(i).ToString("G10", ci))
                For Each k In keys
                    Dim arr = GetSeries(k)
                    Dim v As Double = If(i < arr.Length, arr(i), Double.NaN)
                    sb.Append(",") : sb.Append(v.ToString("G10", ci))
                Next
                sb.AppendLine()
            Next
            Return sb.ToString()
        End Function

        Public Function ToDataTable() As DataTable
            Dim dt As New DataTable("BioReactorTrajectory")
            dt.Columns.Add("time_h", GetType(Double))
            Dim keys = AvailableSeries()
            For Each k In keys : dt.Columns.Add(k, GetType(Double)) : Next
            Dim th = GetTimesHours()
            For i = 0 To Times.Count - 1
                Dim row = dt.NewRow()
                row("time_h") = th(i)
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
