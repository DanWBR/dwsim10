'    Black Oil Lab-PVT Calibration
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.

Imports System.Linq
Imports DWSIM.Thermodynamics.PropertyPackages.Auxiliary

Namespace Utilities.BlackOil

    ''' <summary>One measured PVT lab point: pressure/temperature and any subset of Rs, Bo, oil viscosity.</summary>
    Public Class BlackOilLabPoint
        Public Pressure As Double        ' Pa
        Public Temperature As Double     ' K
        Public Rs As Double = Double.NaN         ' solution GOR, m3/m3 STD (NaN = not measured)
        Public Bo As Double = Double.NaN         ' oil formation volume factor, m3/m3 (NaN = not measured)
        Public OilViscosity As Double = Double.NaN ' oil viscosity, Pa.s (NaN = not measured)
    End Class

    ''' <summary>Fitted black-oil correction multipliers plus a short diagnostic report.</summary>
    Public Class BlackOilCalibrationResult
        Public RsMult As Double = 1.0
        Public BoMult As Double = 1.0
        Public PbMult As Double = 1.0
        Public OilViscMult As Double = 1.0
        Public RsPoints As Integer
        Public BoPoints As Integer
        Public ViscPoints As Integer
        Public PbSet As Boolean
        Public Report As String = ""
    End Class

    ''' <summary>
    ''' Fits the black-oil correction multipliers (Rs, Bo, Pb, oil viscosity) so the Standing / Beggs-Robinson
    ''' correlations best match a set of measured PVT lab points. Each multiplier is the mean of the
    ''' measured/correlated ratios over the points that report that quantity; the bubble-point multiplier is
    ''' the single measured/correlated ratio at the reservoir temperature. Multipliers stay at 1 where there
    ''' is no data. The result is applied by writing it onto the compound's BO_*Mult properties.
    ''' </summary>
    Public Module BlackOilCalibration

        Public Function Calibrate(sgo As Double, sgg As Double, gor As Double, bsw As Double,
                                  points As IEnumerable(Of BlackOilLabPoint),
                                  measuredPb As Double, reservoirT As Double) As BlackOilCalibrationResult

            Dim bp As New BlackOilProperties
            Dim res As New BlackOilCalibrationResult

            Dim rsR As New List(Of Double)
            Dim boR As New List(Of Double)
            Dim muR As New List(Of Double)

            If points IsNot Nothing Then
                For Each pt In points
                    If pt Is Nothing OrElse pt.Pressure <= 0 OrElse pt.Temperature <= 0 Then Continue For
                    If Not Double.IsNaN(pt.Rs) AndAlso pt.Rs > 0 Then
                        Dim rc = bp.SolutionGOR(pt.Temperature, pt.Pressure, sgo, sgg)
                        If rc > 0.000000001 Then rsR.Add(pt.Rs / rc)
                    End If
                    If Not Double.IsNaN(pt.Bo) AndAlso pt.Bo > 0 Then
                        Dim boc = bp.OilFVF(pt.Temperature, pt.Pressure, sgo, sgg, gor)
                        If boc > 0.000000001 Then boR.Add(pt.Bo / boc)
                    End If
                    If Not Double.IsNaN(pt.OilViscosity) AndAlso pt.OilViscosity > 0 Then
                        Dim muc = bp.LiquidViscosity(pt.Temperature, pt.Pressure, sgo, sgg, gor, bsw, 0, 0, 0, 0)
                        If muc > 0.000000000001 Then muR.Add(pt.OilViscosity / muc)
                    End If
                Next
            End If

            If rsR.Count > 0 Then
                res.RsMult = rsR.Average() : res.RsPoints = rsR.Count
            End If
            If boR.Count > 0 Then
                res.BoMult = boR.Average() : res.BoPoints = boR.Count
            End If
            If muR.Count > 0 Then
                res.OilViscMult = muR.Average() : res.ViscPoints = muR.Count
            End If
            If measuredPb > 0 AndAlso reservoirT > 0 Then
                Dim pbc = bp.BubblePointStanding(reservoirT, sgo, sgg, gor)
                If pbc > 0.000000001 Then
                    res.PbMult = measuredPb / pbc : res.PbSet = True
                End If
            End If

            Dim sb As New System.Text.StringBuilder
            sb.AppendLine("Black-oil lab-PVT calibration")
            sb.AppendLine("  Rs   multiplier = " & res.RsMult.ToString("0.0000") & "  (" & res.RsPoints & " point(s))")
            sb.AppendLine("  Bo   multiplier = " & res.BoMult.ToString("0.0000") & "  (" & res.BoPoints & " point(s))")
            sb.AppendLine("  Pb   multiplier = " & res.PbMult.ToString("0.0000") & (If(res.PbSet, "  (measured)", "  (not set)")))
            sb.AppendLine("  Visc multiplier = " & res.OilViscMult.ToString("0.0000") & "  (" & res.ViscPoints & " point(s))")
            res.Report = sb.ToString()

            Return res

        End Function

        ''' <summary>Writes the fitted multipliers onto a black-oil compound's BO_*Mult properties.</summary>
        Public Sub Apply(comp As Interfaces.ICompoundConstantProperties, result As BlackOilCalibrationResult)
            If comp Is Nothing OrElse result Is Nothing Then Return
            comp.BO_RsMult = result.RsMult
            comp.BO_BoMult = result.BoMult
            comp.BO_PbMult = result.PbMult
            comp.BO_OilViscMult = result.OilViscMult
        End Sub

    End Module

End Namespace
