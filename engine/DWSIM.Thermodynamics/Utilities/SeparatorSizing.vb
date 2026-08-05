'    Gas-Liquid Separator Sizing
'    Copyright 2009-2025 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.
'
'    DWSIM is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.
'
'    You should have received a copy of the GNU General Public License
'    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

Namespace Utilities.Sizing

    ''' <summary>Stream conditions and design parameters for a gas-liquid separator.</summary>
    Public Class SeparatorSizingInput

        'stream properties, SI units
        Public Property LiquidDensity As Double
        Public Property VaporDensity As Double
        Public Property InletDensity As Double
        Public Property LiquidVolumetricFlow As Double
        Public Property VaporVolumetricFlow As Double

        'design parameters
        ''' <summary>Length to diameter ratio.</summary>
        Public Property LengthToDiameter As Double = 3.0
        ''' <summary>Nozzle sizing constant, in SI units.</summary>
        Public Property NozzleConstant As Double = 100.0
        ''' <summary>Percentage of the terminal velocity used as the design gas velocity.</summary>
        Public Property GasVelocityPercent As Double = 75.0
        ''' <summary>Maximum liquid nozzle velocity, m/s.</summary>
        Public Property MaxLiquidVelocity As Double = 1.0
        ''' <summary>Liquid residence time, minutes.</summary>
        Public Property ResidenceTime As Double = 5.0
        ''' <summary>Souders-Brown K factor, m/s.</summary>
        Public Property KFactor As Double = 0.1
        ''' <summary>Surge (design margin) factor applied to both flows.</summary>
        Public Property SurgeFactor As Double = 1.0

    End Class

    ''' <summary>Minimum dimensions of the vessel and its nozzles.</summary>
    Public Class SeparatorSizingResults

        ''' <summary>Vessel internal diameter, mm.</summary>
        Public Property Diameter As Double
        ''' <summary>Vessel height (vertical) or length (horizontal), mm.</summary>
        Public Property Length As Double
        ''' <summary>Inlet nozzle diameter, in.</summary>
        Public Property InletNozzle As Double
        ''' <summary>Gas outlet nozzle diameter, in.</summary>
        Public Property GasNozzle As Double
        ''' <summary>Liquid outlet nozzle diameter, in.</summary>
        Public Property LiquidNozzle As Double

    End Class

    ''' <summary>
    ''' Souders-Brown sizing of vertical and horizontal gas-liquid separators. Shared by the
    ''' WinForms and the Avalonia utilities.
    ''' </summary>
    Public Class SeparatorSizing

        Public Shared Function SizeVertical(input As SeparatorSizingInput) As SeparatorSizingResults

            Dim res As New SeparatorSizingResults

            Dim qv = input.VaporVolumetricFlow * input.SurgeFactor
            Dim ql = input.LiquidVolumetricFlow * input.SurgeFactor

            Dim vk = input.KFactor * ((input.LiquidDensity - input.VaporDensity) / input.VaporDensity) ^ 0.5
            Dim vp = input.GasVelocityPercent / 100 * vk
            Dim At = qv / vp

            res.Diameter = (4 * At / Math.PI) ^ 0.5 * 1000
            res.Length = input.LengthToDiameter * res.Diameter

            SizeNozzles(input, qv, ql, res)

            Return res

        End Function

        Public Shared Function SizeHorizontal(input As SeparatorSizingInput) As SeparatorSizingResults

            Dim res As New SeparatorSizingResults

            Dim qv = input.VaporVolumetricFlow * input.SurgeFactor
            Dim ql = input.LiquidVolumetricFlow * input.SurgeFactor

            Dim vk = input.KFactor * ((input.LiquidDensity - input.VaporDensity) / input.VaporDensity) ^ 0.5
            Dim vp = input.GasVelocityPercent / 100 * vk

            SizeNozzles(input, qv, ql, res)

            Dim l_d = input.LengthToDiameter
            Dim tr = input.ResidenceTime

            'find the liquid level fraction x at which the gas area and the liquid holdup
            'call for the same diameter

            Dim x, y, dv, dl As Double

            x = 0.01
            Do
                y = (1 / Math.PI) * Math.Acos(1 - 2 * x) - (2 / Math.PI) * (1 - 2 * x) * (x * (1 - x)) ^ 0.5
                dv = (4 / Math.PI * qv / vp) ^ 0.5 * ((x / y) / l_d) ^ 0.5
                dl = ((4 / (Math.PI * l_d)) * ql * (tr * 60) / (1 - y)) ^ (1 / 3)
                x += 0.0001
            Loop Until Math.Abs(dv - dl) < 0.0001 Or x >= 0.5

            Dim vl1 = ql * tr / (1 / 60)
            Dim vl2 = (1 - y) * Math.PI * dl ^ 3 / 4 * l_d
            If vl2 < vl1 Then
                Do
                    vl2 = (1 - y) * Math.PI * dl ^ 3 / 4 * l_d
                    dl = dl * 1.001
                Loop Until Math.Abs(vl2 - vl1) < 0.001
            End If

            Dim diam = Math.Max(dl, dv)

            res.Diameter = diam * 1000
            res.Length = l_d * diam * 1000

            Return res

        End Function

        ''' <summary>Minimum nozzle diameters, in inches, from the momentum criterion.</summary>
        Private Shared Sub SizeNozzles(input As SeparatorSizingInput, qv As Double, ql As Double,
                                       res As SeparatorSizingResults)

            Dim vmaxbe = input.NozzleConstant / input.InletDensity ^ 0.5
            Dim aminbe = (qv + ql) / vmaxbe
            res.InletNozzle = (4 * aminbe / Math.PI) ^ 0.5 * 39.37

            Dim vmaxbg = input.NozzleConstant / input.VaporDensity ^ 0.5
            Dim aminbg = qv / vmaxbg
            res.GasNozzle = (4 * aminbg / Math.PI) ^ 0.5 * 39.37

            Dim aminbl = ql / input.MaxLiquidVelocity
            res.LiquidNozzle = (4 * aminbl / Math.PI) ^ 0.5 * 39.37

        End Sub

    End Class

End Namespace
