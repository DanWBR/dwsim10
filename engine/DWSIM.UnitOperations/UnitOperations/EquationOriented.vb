'    Unit Operations - equations for the equation-oriented solver
'    Copyright 2026 Daniel Wagner O. de Medeiros
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

Imports DWSIM.Interfaces

Namespace UnitOperations

    ''' <summary>
    ''' The equations of a unit that sets its outlet temperature (a heater or a cooler): the compound flows go through,
    ''' the outlet pressure is the inlet's less the pressure drop, and the outlet temperature is the set one. The duty is
    ''' a result.
    ''' </summary>
    Friend Module SetTemperatureEquations

        Friend Function Count(ctx As IEquationOrientedContext) As Integer
            Return ctx.CompoundCount + 2
        End Function

        Friend Sub Residuals(ctx As IEquationOrientedContext, x As Double(), r As Double(), row As Integer, inlet As Integer, outlet As Integer, T As Double, dP As Double)
            Dim c = ctx.CompoundCount
            Dim ft = ctx.FlowTolerance(outlet)
            For i = 0 To c - 1
                r(row + i) = (x(outlet + 2 + i) - x(inlet + 2 + i)) / ft
            Next
            r(row + c) = (x(outlet + 1) - (x(inlet + 1) - dP)) / ctx.PressureTolerance
            r(row + c + 1) = (x(outlet) - T) / ctx.TemperatureTolerance
        End Sub

        ''' <summary>kT and kdP: the variables of the set temperature and of the pressure drop, or -1 when they stay fixed.</summary>
        Friend Sub Jacobian(ctx As IEquationOrientedContext, add As Action(Of Integer, Integer, Double), inlet As Integer, outlet As Integer, kT As Integer, kdP As Integer)
            Dim c = ctx.CompoundCount
            Dim ft = ctx.FlowTolerance(outlet)
            For i = 0 To c - 1
                add(i, outlet + 2 + i, 1.0 / ft)
                add(i, inlet + 2 + i, -1.0 / ft)
            Next
            add(c, outlet + 1, 1.0 / ctx.PressureTolerance)
            add(c, inlet + 1, -1.0 / ctx.PressureTolerance)
            If kdP >= 0 Then add(c, kdP, 1.0 / ctx.PressureTolerance)
            add(c + 1, outlet, 1.0 / ctx.TemperatureTolerance)
            If kT >= 0 Then add(c + 1, kT, -1.0 / ctx.TemperatureTolerance)
        End Sub

        Friend Function Name(ctx As IEquationOrientedContext, k As Integer) As String
            Dim c = ctx.CompoundCount
            Return If(k < c, "flow " & k, If(k = c, "pressure", "outlet temperature"))
        End Function

    End Module

    Partial Public Class Heater

        ''' <summary>Equations of its own at a set outlet temperature; in the other modes the heater is a black box.</summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Return CalcMode = CalculationMode.OutletTemperature
            End Get
        End Property

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Return {"PROP_HT_0", "PROP_HT_2"}
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return SetTemperatureEquations.Count(context)
        End Function

        Public Overrides Function EOVariables(context As IEquationOrientedContext) As Integer()
            If Not EOHasEquations Then Return MyBase.EOVariables(context)
            Return EOStreamVariables(context, {EOInletOffset(context, 0), EOOutletOffset(context, 0)}, EOParameters())
        End Function

        Public Overrides Sub EOResiduals(context As IEquationOrientedContext, x As Double(), r As Double(), row As Integer)
            If Not EOHasEquations Then
                MyBase.EOResiduals(context, x, r, row)
                Return
            End If
            SetTemperatureEquations.Residuals(context, x, r, row, EOInletOffset(context, 0), EOOutletOffset(context, 0),
                                              EOParameterValue(context, x, "PROP_HT_2", OutletTemperature.GetValueOrDefault()),
                                              EOParameterValue(context, x, "PROP_HT_0", DeltaP.GetValueOrDefault()))
        End Sub

        Public Overrides Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean
            If Not EOHasEquations Then Return MyBase.EOJacobian(context, x, add)
            SetTemperatureEquations.Jacobian(context, add, EOInletOffset(context, 0), EOOutletOffset(context, 0),
                                             context.ParameterIndex(Me, "PROP_HT_2"), context.ParameterIndex(Me, "PROP_HT_0"))
            Return True
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Return SetTemperatureEquations.Name(context, k)
        End Function

    End Class

    Partial Public Class Cooler

        ''' <summary>Equations of its own at a set outlet temperature; in the other modes the cooler is a black box.</summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Return CalcMode = CalculationMode.OutletTemperature
            End Get
        End Property

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Return {"PROP_CO_0", "PROP_CO_2"}
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return SetTemperatureEquations.Count(context)
        End Function

        Public Overrides Function EOVariables(context As IEquationOrientedContext) As Integer()
            If Not EOHasEquations Then Return MyBase.EOVariables(context)
            Return EOStreamVariables(context, {EOInletOffset(context, 0), EOOutletOffset(context, 0)}, EOParameters())
        End Function

        Public Overrides Sub EOResiduals(context As IEquationOrientedContext, x As Double(), r As Double(), row As Integer)
            If Not EOHasEquations Then
                MyBase.EOResiduals(context, x, r, row)
                Return
            End If
            SetTemperatureEquations.Residuals(context, x, r, row, EOInletOffset(context, 0), EOOutletOffset(context, 0),
                                              EOParameterValue(context, x, "PROP_CO_2", OutletTemperature.GetValueOrDefault()),
                                              EOParameterValue(context, x, "PROP_CO_0", DeltaP.GetValueOrDefault()))
        End Sub

        Public Overrides Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean
            If Not EOHasEquations Then Return MyBase.EOJacobian(context, x, add)
            SetTemperatureEquations.Jacobian(context, add, EOInletOffset(context, 0), EOOutletOffset(context, 0),
                                             context.ParameterIndex(Me, "PROP_CO_2"), context.ParameterIndex(Me, "PROP_CO_0"))
            Return True
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Return SetTemperatureEquations.Name(context, k)
        End Function

    End Class

    Partial Public Class Valve

        ''' <summary>
        ''' Equations of its own at a set pressure drop or outlet pressure: the compound flows go through, the outlet
        ''' pressure, and the enthalpy flow kept (adiabatic). In the flow coefficient modes the valve is a black box.
        ''' </summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Return CalcMode = CalculationMode.DeltaP OrElse CalcMode = CalculationMode.OutletPressure
            End Get
        End Property

        ' the pressure drop in that mode, the outlet pressure in the other
        Private ReadOnly Property EOPressureCode As String
            Get
                Return If(CalcMode = CalculationMode.DeltaP, "PROP_VA_1", "PROP_VA_2")
            End Get
        End Property

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Return {EOPressureCode}
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return context.CompoundCount + 2
        End Function

        Public Overrides Function EOVariables(context As IEquationOrientedContext) As Integer()
            If Not EOHasEquations Then Return MyBase.EOVariables(context)
            Return EOStreamVariables(context, {EOInletOffset(context, 0), EOOutletOffset(context, 0)}, EOParameters())
        End Function

        Public Overrides Sub EOResiduals(context As IEquationOrientedContext, x As Double(), r As Double(), row As Integer)
            If Not EOHasEquations Then
                MyBase.EOResiduals(context, x, r, row)
                Return
            End If
            Dim c = context.CompoundCount
            Dim inlet = EOInletOffset(context, 0), outlet = EOOutletOffset(context, 0)
            Dim ft = context.FlowTolerance(outlet)
            For i = 0 To c - 1
                r(row + i) = (x(outlet + 2 + i) - x(inlet + 2 + i)) / ft
            Next
            Dim Pout As Double
            If CalcMode = CalculationMode.DeltaP Then
                Pout = x(inlet + 1) - EOParameterValue(context, x, "PROP_VA_1", DeltaP.GetValueOrDefault())
            Else
                Pout = EOParameterValue(context, x, "PROP_VA_2", OutletPressure.GetValueOrDefault())
            End If
            r(row + c) = (x(outlet + 1) - Pout) / context.PressureTolerance
            r(row + c + 1) = (context.Enthalpy(outlet, x) - context.Enthalpy(inlet, x)) / context.EnergyTolerance(outlet)
        End Sub

        Public Overrides Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean
            If Not EOHasEquations Then Return MyBase.EOJacobian(context, x, add)
            Dim c = context.CompoundCount
            Dim inlet = EOInletOffset(context, 0), outlet = EOOutletOffset(context, 0)
            Dim ft = context.FlowTolerance(outlet)
            For i = 0 To c - 1
                add(i, outlet + 2 + i, 1.0 / ft)
                add(i, inlet + 2 + i, -1.0 / ft)
            Next
            Dim tp = context.PressureTolerance
            add(c, outlet + 1, 1.0 / tp)
            Dim k = context.ParameterIndex(Me, EOPressureCode)
            If CalcMode = CalculationMode.DeltaP Then
                add(c, inlet + 1, -1.0 / tp)
                If k >= 0 Then add(c, k, 1.0 / tp)
            ElseIf k >= 0 Then
                add(c, k, -1.0 / tp)
            End If
            Dim te = context.EnergyTolerance(outlet)
            Dim hout = context.EnthalpyDerivatives(outlet, x), hin = context.EnthalpyDerivatives(inlet, x)
            For j = 0 To c + 1
                add(c + 1, outlet + j, hout(j) / te)
                add(c + 1, inlet + j, -hin(j) / te)
            Next
            Return True
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Dim c = context.CompoundCount
            Return If(k < c, "flow " & k, If(k = c, "pressure", "energy balance"))
        End Function

    End Class

End Namespace
