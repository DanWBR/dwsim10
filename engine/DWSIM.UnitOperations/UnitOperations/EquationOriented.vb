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
    ''' The equations of a heater or a cooler: the compound flows go through, the outlet pressure is the inlet's less the
    ''' pressure drop, and the specification: the outlet temperature (set, or the inlet's plus a change), the duty, which
    ''' the efficiency scales into the outlet's enthalpy as the unit does (a cooler's removed), or the outlet's vapor
    ''' fraction: the outlet's extended vapor fraction when it has one (a condensate of one compound or near an azeotrope),
    ''' else its temperature at that vapor fraction. The duty is otherwise a result. Codes: 0 the pressure drop, 1 the
    ''' efficiency, 2 the outlet temperature, 3 the duty, 4 the vapor fraction, 5 the change.
    ''' </summary>
    Friend Module HeaterCoolerEquations

        ''' <summary>Which specification: 0 the outlet temperature, 1 the temperature change, 2 the duty.</summary>
        Friend Enum Spec
            OutletTemperature
            TemperatureChange
            Duty
            VaporFraction
        End Enum

        Friend Function Count(ctx As IEquationOrientedContext) As Integer
            Return ctx.CompoundCount + 2
        End Function

        Friend Function Parameters(prefix As String, kind As Spec) As String()
            Select Case kind
                Case Spec.OutletTemperature : Return {prefix & "0", prefix & "2"}
                Case Spec.TemperatureChange : Return {prefix & "0", prefix & "5"}
                Case Spec.VaporFraction : Return {prefix & "0", prefix & "4"}
                Case Else : Return {prefix & "0", prefix & "3", prefix & "1"}
            End Select
        End Function

        ''' <summary>value: the outlet temperature, the change or the duty; sign: +1 for heat added, -1 for heat removed.</summary>
        Friend Sub Residuals(ctx As IEquationOrientedContext, x As Double(), r As Double(), row As Integer, inlet As Integer, outlet As Integer,
                             kind As Spec, value As Double, dP As Double, efficiency As Double, sign As Double)
            Dim c = ctx.CompoundCount
            Dim ft = ctx.FlowTolerance(outlet)
            For i = 0 To c - 1
                r(row + i) = (x(outlet + 2 + i) - x(inlet + 2 + i)) / ft
            Next
            r(row + c) = (x(outlet + 1) - (x(inlet + 1) - dP)) / ctx.PressureTolerance
            Select Case kind
                Case Spec.OutletTemperature
                    r(row + c + 1) = (x(outlet) - value) / ctx.TemperatureTolerance
                Case Spec.TemperatureChange
                    r(row + c + 1) = (x(outlet) - x(inlet) - value) / ctx.TemperatureTolerance
                Case Spec.VaporFraction
                    Dim kb = ctx.VaporFractionIndex(outlet)
                    If kb >= 0 Then
                        r(row + c + 1) = (x(kb) - value) / ctx.VaporFractionTolerance
                    Else
                        r(row + c + 1) = (x(outlet) - ctx.SaturationTemperature(outlet, x, value)) / ctx.TemperatureTolerance
                    End If
                Case Else
                    r(row + c + 1) = (ctx.Enthalpy(outlet, x) - ctx.Enthalpy(inlet, x) - sign * value * efficiency / 100.0) / ctx.EnergyTolerance(outlet)
            End Select
        End Sub

        ''' <summary>
        ''' kV, kdP and kEff: the variables of the specification, the pressure drop and the efficiency, or -1. False at a
        ''' set vapor fraction on an outlet without its own (the saturation temperature has no derivatives here): the solver
        ''' differences the block.
        ''' </summary>
        Friend Function Jacobian(ctx As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double), inlet As Integer, outlet As Integer,
                            kind As Spec, kV As Integer, kdP As Integer, kEff As Integer, value As Double, efficiency As Double, sign As Double) As Boolean
            Dim c = ctx.CompoundCount
            Dim kb = If(kind = Spec.VaporFraction, ctx.VaporFractionIndex(outlet), -1)
            If kind = Spec.VaporFraction AndAlso kb < 0 Then Return False
            Dim ft = ctx.FlowTolerance(outlet)
            For i = 0 To c - 1
                add(i, outlet + 2 + i, 1.0 / ft)
                add(i, inlet + 2 + i, -1.0 / ft)
            Next
            add(c, outlet + 1, 1.0 / ctx.PressureTolerance)
            add(c, inlet + 1, -1.0 / ctx.PressureTolerance)
            If kdP >= 0 Then add(c, kdP, 1.0 / ctx.PressureTolerance)
            Dim tt = ctx.TemperatureTolerance
            Select Case kind
                Case Spec.OutletTemperature
                    add(c + 1, outlet, 1.0 / tt)
                    If kV >= 0 Then add(c + 1, kV, -1.0 / tt)
                Case Spec.TemperatureChange
                    add(c + 1, outlet, 1.0 / tt)
                    add(c + 1, inlet, -1.0 / tt)
                    If kV >= 0 Then add(c + 1, kV, -1.0 / tt)
                Case Spec.VaporFraction
                    Dim tb = ctx.VaporFractionTolerance
                    add(c + 1, kb, 1.0 / tb)
                    If kV >= 0 Then add(c + 1, kV, -1.0 / tb)
                Case Else
                    Dim te = ctx.EnergyTolerance(outlet)
                    Dim hout = ctx.EnthalpyDerivatives(outlet, x), hin = ctx.EnthalpyDerivatives(inlet, x)
                    For j = 0 To c + 1
                        add(c + 1, outlet + j, hout(j) / te)
                        add(c + 1, inlet + j, -hin(j) / te)
                    Next
                    If kV >= 0 Then add(c + 1, kV, -sign * efficiency / 100.0 / te)
                    If kEff >= 0 Then add(c + 1, kEff, -sign * value / 100.0 / te)
            End Select
            Return True
        End Function

        Friend Function Name(ctx As IEquationOrientedContext, k As Integer, kind As Spec) As String
            Dim c = ctx.CompoundCount
            If k < c Then Return "flow " & k
            If k = c Then Return "pressure"
            Select Case kind
                Case Spec.OutletTemperature : Return "outlet temperature"
                Case Spec.TemperatureChange : Return "temperature change"
                Case Spec.VaporFraction : Return "outlet vapor fraction"
                Case Else : Return "energy balance"
            End Select
        End Function

    End Module

    Partial Public Class Heater

        ''' <summary>A saturated inlet keeps the equations: its equations read the inlet's flows, pressure, temperature and enthalpy.</summary>
        Public Overrides ReadOnly Property EOTakesSaturatedInlets As Boolean
            Get
                Return EOHasEquations
            End Get
        End Property

        ''' <summary>
        ''' Equations of its own at a set outlet temperature, a set temperature change, a set heat added or a set outlet
        ''' vapor fraction; with an energy stream the heater is a black box.
        ''' </summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Return CalcMode = CalculationMode.OutletTemperature OrElse CalcMode = CalculationMode.TemperatureChange OrElse CalcMode = CalculationMode.HeatAdded OrElse
                    CalcMode = CalculationMode.OutletVaporFraction
            End Get
        End Property

        ''' <summary>
        ''' At a set vapor fraction the equations read the streams' flows and pressures and the outlet's vapor fraction (a
        ''' condenser's saturated outlet keeps them), at a set duty their enthalpies, at the state a vapor fraction gives too.
        ''' </summary>
        Public Overrides ReadOnly Property EOTakesSaturatedStreams As Boolean
            Get
                Return CalcMode = CalculationMode.OutletVaporFraction OrElse CalcMode = CalculationMode.HeatAdded
            End Get
        End Property

        Private ReadOnly Property EOSpec As HeaterCoolerEquations.Spec
            Get
                Select Case CalcMode
                    Case CalculationMode.OutletTemperature : Return HeaterCoolerEquations.Spec.OutletTemperature
                    Case CalculationMode.TemperatureChange : Return HeaterCoolerEquations.Spec.TemperatureChange
                    Case CalculationMode.OutletVaporFraction : Return HeaterCoolerEquations.Spec.VaporFraction
                    Case Else : Return HeaterCoolerEquations.Spec.Duty
                End Select
            End Get
        End Property

        ' the specification's value and its code
        Private Function EOValue(context As IEquationOrientedContext, x As Double(), ByRef code As String) As Double
            Select Case EOSpec
                Case HeaterCoolerEquations.Spec.VaporFraction
                    code = "PROP_HT_4" : Return EOParameterValue(context, x, code, OutletVaporFraction.GetValueOrDefault())
                Case HeaterCoolerEquations.Spec.OutletTemperature
                    code = "PROP_HT_2" : Return EOParameterValue(context, x, code, OutletTemperature.GetValueOrDefault())
                Case HeaterCoolerEquations.Spec.TemperatureChange
                    code = "PROP_HT_5" : Return EOParameterValue(context, x, code, DeltaT.GetValueOrDefault())
                Case Else
                    code = "PROP_HT_3" : Return EOParameterValue(context, x, code, DeltaQ.GetValueOrDefault())
            End Select
        End Function

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Return HeaterCoolerEquations.Parameters("PROP_HT_", EOSpec)
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return HeaterCoolerEquations.Count(context)
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
            Dim code As String = Nothing
            Dim v = EOValue(context, x, code)
            HeaterCoolerEquations.Residuals(context, x, r, row, EOInletOffset(context, 0), EOOutletOffset(context, 0), EOSpec, v,
                                            EOParameterValue(context, x, "PROP_HT_0", DeltaP.GetValueOrDefault()),
                                            EOParameterValue(context, x, "PROP_HT_1", Eficiencia.GetValueOrDefault()), 1.0)
        End Sub

        Public Overrides Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean
            If Not EOHasEquations Then Return MyBase.EOJacobian(context, x, add)
            Dim code As String = Nothing
            Dim v = EOValue(context, x, code)
            Return HeaterCoolerEquations.Jacobian(context, x, add, EOInletOffset(context, 0), EOOutletOffset(context, 0), EOSpec,
                                                  context.ParameterIndex(Me, code), context.ParameterIndex(Me, "PROP_HT_0"),
                                                  If(EOSpec = HeaterCoolerEquations.Spec.Duty, context.ParameterIndex(Me, "PROP_HT_1"), -1),
                                                  v, EOParameterValue(context, x, "PROP_HT_1", Eficiencia.GetValueOrDefault()), 1.0)
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Return HeaterCoolerEquations.Name(context, k, EOSpec)
        End Function

    End Class

    Partial Public Class Cooler

        ''' <summary>A saturated inlet keeps the equations: its equations read the inlet's flows, pressure, temperature and enthalpy.</summary>
        Public Overrides ReadOnly Property EOTakesSaturatedInlets As Boolean
            Get
                Return EOHasEquations
            End Get
        End Property

        ''' <summary>
        ''' Equations of its own at a set outlet temperature, a set temperature change, a set heat removed or a set outlet
        ''' vapor fraction; with an energy stream the cooler is a black box.
        ''' </summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Return CalcMode = CalculationMode.OutletTemperature OrElse CalcMode = CalculationMode.TemperatureChange OrElse CalcMode = CalculationMode.HeatRemoved OrElse
                    CalcMode = CalculationMode.OutletVaporFraction
            End Get
        End Property

        ''' <summary>
        ''' At a set vapor fraction the equations read the streams' flows and pressures and the outlet's vapor fraction (a
        ''' condenser's saturated outlet keeps them), at a set duty their enthalpies, at the state a vapor fraction gives too.
        ''' </summary>
        Public Overrides ReadOnly Property EOTakesSaturatedStreams As Boolean
            Get
                Return CalcMode = CalculationMode.OutletVaporFraction OrElse CalcMode = CalculationMode.HeatRemoved
            End Get
        End Property

        Private ReadOnly Property EOSpec As HeaterCoolerEquations.Spec
            Get
                Select Case CalcMode
                    Case CalculationMode.OutletTemperature : Return HeaterCoolerEquations.Spec.OutletTemperature
                    Case CalculationMode.TemperatureChange : Return HeaterCoolerEquations.Spec.TemperatureChange
                    Case CalculationMode.OutletVaporFraction : Return HeaterCoolerEquations.Spec.VaporFraction
                    Case Else : Return HeaterCoolerEquations.Spec.Duty
                End Select
            End Get
        End Property

        ' the specification's value and its code
        Private Function EOValue(context As IEquationOrientedContext, x As Double(), ByRef code As String) As Double
            Select Case EOSpec
                Case HeaterCoolerEquations.Spec.VaporFraction
                    code = "PROP_CL_4" : Return EOParameterValue(context, x, code, OutletVaporFraction.GetValueOrDefault())
                Case HeaterCoolerEquations.Spec.OutletTemperature
                    code = "PROP_CL_2" : Return EOParameterValue(context, x, code, OutletTemperature.GetValueOrDefault())
                Case HeaterCoolerEquations.Spec.TemperatureChange
                    code = "PROP_CL_5" : Return EOParameterValue(context, x, code, DeltaT.GetValueOrDefault())
                Case Else
                    code = "PROP_CL_3" : Return EOParameterValue(context, x, code, DeltaQ.GetValueOrDefault())
            End Select
        End Function

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Return HeaterCoolerEquations.Parameters("PROP_CL_", EOSpec)
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return HeaterCoolerEquations.Count(context)
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
            Dim code As String = Nothing
            Dim v = EOValue(context, x, code)
            HeaterCoolerEquations.Residuals(context, x, r, row, EOInletOffset(context, 0), EOOutletOffset(context, 0), EOSpec, v,
                                            EOParameterValue(context, x, "PROP_CL_0", DeltaP.GetValueOrDefault()),
                                            EOParameterValue(context, x, "PROP_CL_1", Eficiencia.GetValueOrDefault()), -1.0)
        End Sub

        Public Overrides Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean
            If Not EOHasEquations Then Return MyBase.EOJacobian(context, x, add)
            Dim code As String = Nothing
            Dim v = EOValue(context, x, code)
            Return HeaterCoolerEquations.Jacobian(context, x, add, EOInletOffset(context, 0), EOOutletOffset(context, 0), EOSpec,
                                                  context.ParameterIndex(Me, code), context.ParameterIndex(Me, "PROP_CL_0"),
                                                  If(EOSpec = HeaterCoolerEquations.Spec.Duty, context.ParameterIndex(Me, "PROP_CL_1"), -1),
                                                  v, EOParameterValue(context, x, "PROP_CL_1", Eficiencia.GetValueOrDefault()), -1.0)
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Return HeaterCoolerEquations.Name(context, k, EOSpec)
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

        ''' <summary>The equations read the streams' flows, pressures and enthalpies: a saturated stream keeps them.</summary>
        Public Overrides ReadOnly Property EOTakesSaturatedStreams As Boolean
            Get
                Return EOHasEquations
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

    ''' <summary>
    ''' The pressure a block takes from its inlets by its rule (the lowest, the highest or the average of those that carry
    ''' flow), and its derivatives. Inlets tied at the extreme have none there: the first of them takes it, in the order the
    ''' solver lists them.
    ''' </summary>
    Friend Module InletPressureRule

        Private Function Live(ctx As IEquationOrientedContext, x As Double(), inlets As Integer()) As Integer()
            Dim c = ctx.CompoundCount
            Return inlets.Where(Function(o) Enumerable.Range(0, c).Sum(Function(i) x(o + 2 + i)) > 0.0).ToArray()
        End Function

        Friend Function Pressure(ctx As IEquationOrientedContext, x As Double(), inlets As Integer(), rule As String) As Double
            Dim l = Live(ctx, x, inlets)
            If l.Length = 0 Then l = inlets
            Dim p = l.Select(Function(o) x(o + 1)).ToArray()
            Select Case rule
                Case "Maximum" : Return p.Max()
                Case "Average" : Return p.Average()
                Case Else : Return p.Min()
            End Select
        End Function

        ''' <summary>d(the block's pressure)/d(each inlet's pressure): (offset of the inlet, weight).</summary>
        Friend Function Weights(ctx As IEquationOrientedContext, x As Double(), inlets As Integer(), rule As String) As List(Of KeyValuePair(Of Integer, Double))
            Dim l = Live(ctx, x, inlets)
            If l.Length = 0 Then l = inlets
            Select Case rule
                Case "Average" : Return l.Select(Function(o) New KeyValuePair(Of Integer, Double)(o, 1.0 / l.Length)).ToList()
                Case "Maximum" : Return New List(Of KeyValuePair(Of Integer, Double)) From {New KeyValuePair(Of Integer, Double)(l.OrderByDescending(Function(o) x(o + 1)).First(), 1.0)}
                Case Else : Return New List(Of KeyValuePair(Of Integer, Double)) From {New KeyValuePair(Of Integer, Double)(l.OrderBy(Function(o) x(o + 1)).First(), 1.0)}
            End Select
        End Function

        ''' <summary>Whether the pressure the rule takes is tied between two inlets.</summary>
        Friend Function Tied(ctx As IEquationOrientedContext, x As Double(), inlets As Integer(), rule As String) As Boolean
            If rule = "Average" Then Return False
            Dim p = Live(ctx, x, inlets).Select(Function(o) x(o + 1)).ToArray()
            If p.Length < 2 Then Return False
            Dim e = If(rule = "Maximum", p.Max(), p.Min())
            Return p.Count(Function(v) Math.Abs(v - e) <= 0.000000001 * Math.Max(Math.Abs(e), 1.0)) > 1
        End Function

    End Module

    Partial Public Class Mixer

        ''' <summary>Equations of its own (flows, pressure by its rule, energy) when no energy stream enters it.</summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Return Not GraphicObject.InputConnectors.Any(Function(k) k.IsAttached AndAlso k.AttachedConnector.AttachedFrom.ObjectType = Enums.GraphicObjects.ObjectType.EnergyStream)
            End Get
        End Property

        ''' <summary>The equations read the streams' flows, pressures and enthalpies: a saturated stream keeps them.</summary>
        Public Overrides ReadOnly Property EOTakesSaturatedStreams As Boolean
            Get
                Return EOHasEquations
            End Get
        End Property

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Return New String() {}
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return context.CompoundCount + 2
        End Function

        Public Overrides Function EOVariables(context As IEquationOrientedContext) As Integer()
            If Not EOHasEquations Then Return MyBase.EOVariables(context)
            Return EOStreamVariables(context, context.InletOffsets(Me).Concat({EOOutletOffset(context, 0)}).ToArray(), Nothing)
        End Function

        Public Overrides Sub EOResiduals(context As IEquationOrientedContext, x As Double(), r As Double(), row As Integer)
            If Not EOHasEquations Then
                MyBase.EOResiduals(context, x, r, row)
                Return
            End If
            Dim c = context.CompoundCount
            Dim inlets = context.InletOffsets(Me), outlet = EOOutletOffset(context, 0)
            Dim ft = context.FlowTolerance(outlet)
            For i = 0 To c - 1
                Dim sum = 0.0
                For Each o In inlets
                    sum += x(o + 2 + i)
                Next
                r(row + i) = (x(outlet + 2 + i) - sum) / ft
            Next
            r(row + c) = (x(outlet + 1) - InletPressureRule.Pressure(context, x, inlets, PressureCalculation.ToString())) / context.PressureTolerance
            Dim hin = 0.0
            For Each o In inlets
                hin += context.Enthalpy(o, x)
            Next
            r(row + c + 1) = (context.Enthalpy(outlet, x) - hin) / context.EnergyTolerance(outlet)
        End Sub

        Public Overrides Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean
            If Not EOHasEquations Then Return MyBase.EOJacobian(context, x, add)
            Dim c = context.CompoundCount
            Dim inlets = context.InletOffsets(Me), outlet = EOOutletOffset(context, 0)
            Dim ft = context.FlowTolerance(outlet)
            For i = 0 To c - 1
                add(i, outlet + 2 + i, 1.0 / ft)
                For Each o In inlets
                    add(i, o + 2 + i, -1.0 / ft)
                Next
            Next
            add(c, outlet + 1, 1.0 / context.PressureTolerance)
            For Each w In InletPressureRule.Weights(context, x, inlets, PressureCalculation.ToString())
                add(c, w.Key + 1, -w.Value / context.PressureTolerance)
            Next
            Dim te = context.EnergyTolerance(outlet)
            Dim hout = context.EnthalpyDerivatives(outlet, x)
            For j = 0 To c + 1
                add(c + 1, outlet + j, hout(j) / te)
            Next
            For Each o In inlets
                If Enumerable.Range(0, c).Sum(Function(i) x(o + 2 + i)) <= 0.0 Then Continue For
                Dim hin = context.EnthalpyDerivatives(o, x)
                For j = 0 To c + 1
                    add(c + 1, o + j, -hin(j) / te)
                Next
            Next
            Return True
        End Function

        Public Overrides Function EOIsSmooth(context As IEquationOrientedContext, x As Double(), k As Integer) As Boolean
            If Not EOHasEquations Then Return MyBase.EOIsSmooth(context, x, k)
            Return k <> context.CompoundCount OrElse Not InletPressureRule.Tied(context, x, context.InletOffsets(Me), PressureCalculation.ToString())
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Dim c = context.CompoundCount
            Return If(k < c, "flow balance " & k, If(k = c, "pressure", "energy balance"))
        End Function

    End Class

    Partial Public Class Splitter

        ''' <summary>
        ''' Equations of its own by split ratios, or by the molar or mass flow of the first outlets (two or three, on the
        ''' first connectors) with the last taking the rest: each outlet at the inlet's temperature and pressure, with its
        ''' share of the flows. With a set flow the share is that flow over the inlet's. An outlet with an extended vapor
        ''' fraction, of an inlet with one, takes the inlet's vapor fraction in place of its temperature (a stream of one
        ''' compound in two phases has the same temperature at any vapor fraction).
        ''' </summary>
        Public Overrides ReadOnly Property EOTakesSaturatedStreams As Boolean
            Get
                Return EOHasEquations
            End Get
        End Property

        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                If OperationMode = OpMode.SplitRatios Then Return True
                If Not EOFlowSpec Then Return False
                Dim n = EOOutletPorts().Length
                Return EORest() AndAlso (n = 2 OrElse n = 3)
            End Get
        End Property

        Private ReadOnly Property EOFlowSpec As Boolean
            Get
                Return OperationMode = OpMode.StreamMoleFlowSpec OrElse OperationMode = OpMode.StreamMassFlowSpec
            End Get
        End Property

        ' the connected outlets, by connector
        Private Function EOOutletPorts() As Integer()
            Return Enumerable.Range(0, GraphicObject.OutputConnectors.Count).Where(Function(o) GraphicObject.OutputConnectors(o).IsAttached).ToArray()
        End Function

        ''' <summary>The outlets are the first connectors: the last takes what the others leave, as the unit does.</summary>
        Private Function EORest() As Boolean
            Dim ports = EOOutletPorts()
            Return Enumerable.Range(0, ports.Length).All(Function(o) ports(o) = o)
        End Function

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Dim n = EOOutletPorts().Length
            If EOFlowSpec Then Return Enumerable.Range(1, n - 1).Select(Function(o) "PROP_SP_" & o).ToArray()
            ' SR1 and SR2, the ratios of the first two outlets, when the last takes the rest
            If Not EORest() OrElse n < 2 Then Return New String() {}
            Return Enumerable.Range(1, n - 1).Select(Function(o) "SR" & o).ToArray()
        End Function

        ' the weight of each compound's moles in the flow a set flow is given in: 1 for a molar flow, the molar mass for a mass flow
        Private Function EOFlowWeights(context As IEquationOrientedContext) As Double()
            Dim c = context.CompoundCount
            If OperationMode = OpMode.StreamMassFlowSpec Then Return context.MolecularWeights.Select(Function(m) m / 1000.0).ToArray()
            Return Enumerable.Repeat(1.0, c).ToArray()
        End Function

        ' the set flow of outlet o (0 or 1) and its code
        Private Function EOSetFlow(context As IEquationOrientedContext, x As Double(), o As Integer) As Double
            Return EOParameterValue(context, x, "PROP_SP_" & (o + 1), If(o = 0, StreamFlowSpec, Stream2FlowSpec))
        End Function

        ' the inlet's flow in the set flow's basis
        Private Function EOInletFlow(context As IEquationOrientedContext, x As Double(), inlet As Integer) As Double
            Dim w = EOFlowWeights(context), f = 0.0
            For i = 0 To w.Length - 1
                f += w(i) * x(inlet + 2 + i)
            Next
            Return f
        End Function

        Private Function EORatio(context As IEquationOrientedContext, x As Double(), o As Integer, ports As Integer(), rest As Boolean) As Double
            If EOFlowSpec Then
                Dim f = EOInletFlow(context, x, EOInletOffset(context, 0))
                If f <= 0.0 Then Return If(o = ports.Length - 1, 1.0, 0.0)
                If o < ports.Length - 1 Then Return EOSetFlow(context, x, o) / f
                Dim taken = 0.0
                For k = 0 To ports.Length - 2
                    taken += EOSetFlow(context, x, k)
                Next
                Return 1.0 - taken / f
            End If
            If Not rest OrElse o < ports.Length - 1 Then Return EOParameterValue(context, x, "SR" & (o + 1), Convert.ToDouble(Ratios(ports(o))))
            Dim sum = 0.0
            For k = 0 To ports.Length - 2
                sum += EOParameterValue(context, x, "SR" & (k + 1), Convert.ToDouble(Ratios(ports(k))))
            Next
            Return 1.0 - sum
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return EOOutletPorts().Length * (context.CompoundCount + 2)
        End Function

        Public Overrides Function EOVariables(context As IEquationOrientedContext) As Integer()
            If Not EOHasEquations Then Return MyBase.EOVariables(context)
            Dim offsets = {EOInletOffset(context, 0)}.Concat(EOOutletPorts().Select(Function(o) EOOutletOffset(context, o))).ToArray()
            Return EOStreamVariables(context, offsets, EOParameters())
        End Function

        Public Overrides Sub EOResiduals(context As IEquationOrientedContext, x As Double(), r As Double(), row As Integer)
            If Not EOHasEquations Then
                MyBase.EOResiduals(context, x, r, row)
                Return
            End If
            Dim c = context.CompoundCount
            Dim inlet = EOInletOffset(context, 0)
            Dim ports = EOOutletPorts(), rest = EORest()
            For o = 0 To ports.Length - 1
                Dim s = EOOutletOffset(context, ports(o))
                Dim ratio = EORatio(context, x, o, ports, rest)
                Dim ft = context.FlowTolerance(s)
                Dim kbo = context.VaporFractionIndex(s), kbi = context.VaporFractionIndex(inlet)
                If kbo >= 0 AndAlso kbi >= 0 Then
                    r(row) = (x(kbo) - x(kbi)) / context.VaporFractionTolerance
                Else
                    r(row) = (x(s) - x(inlet)) / context.TemperatureTolerance
                End If
                r(row + 1) = (x(s + 1) - x(inlet + 1)) / context.PressureTolerance
                For i = 0 To c - 1
                    r(row + 2 + i) = (x(s + 2 + i) - ratio * x(inlet + 2 + i)) / ft
                Next
                row += c + 2
            Next
        End Sub

        Public Overrides Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean
            If Not EOHasEquations Then Return MyBase.EOJacobian(context, x, add)
            Dim c = context.CompoundCount
            Dim inlet = EOInletOffset(context, 0)
            Dim ports = EOOutletPorts(), rest = EORest()
            Dim tt = context.TemperatureTolerance, tp = context.PressureTolerance
            ' with set flows, each share moves with the inlet's flow and with its set flow
            Dim w = EOFlowWeights(context)
            Dim f = If(EOFlowSpec, EOInletFlow(context, x, inlet), 0.0)
            Dim k = 0
            For o = 0 To ports.Length - 1
                Dim s = EOOutletOffset(context, ports(o))
                Dim ratio = EORatio(context, x, o, ports, rest)
                Dim ft = context.FlowTolerance(s)
                Dim kbo = context.VaporFractionIndex(s), kbi = context.VaporFractionIndex(inlet)
                If kbo >= 0 AndAlso kbi >= 0 Then
                    add(k, kbo, 1.0 / context.VaporFractionTolerance)
                    add(k, kbi, -1.0 / context.VaporFractionTolerance)
                Else
                    add(k, s, 1.0 / tt)
                    add(k, inlet, -1.0 / tt)
                End If
                add(k + 1, s + 1, 1.0 / tp)
                add(k + 1, inlet + 1, -1.0 / tp)
                Dim last = o = ports.Length - 1
                For i = 0 To c - 1
                    Dim row = k + 2 + i
                    add(row, s + 2 + i, 1.0 / ft)
                    add(row, inlet + 2 + i, -ratio / ft)
                    If EOFlowSpec Then
                        If f <= 0.0 Then Continue For
                        ' the set flows this share is made of: its own, or all of them, taken from the rest
                        Dim taken = 0.0
                        If last Then
                            For m = 0 To ports.Length - 2
                                taken += EOSetFlow(context, x, m)
                            Next
                        Else
                            taken = EOSetFlow(context, x, o)
                        End If
                        Dim sgn = If(last, -1.0, 1.0)
                        ' d(ratio)/d(n_j) = -sgn taken w_j / f^2
                        For j = 0 To c - 1
                            add(row, inlet + 2 + j, sgn * taken * w(j) / (f * f) * x(inlet + 2 + i) / ft)
                        Next
                        For m = 0 To ports.Length - 2
                            If Not last AndAlso m <> o Then Continue For
                            Dim p = context.ParameterIndex(Me, "PROP_SP_" & (m + 1))
                            If p >= 0 Then add(row, p, -sgn * x(inlet + 2 + i) / f / ft)
                        Next
                    ElseIf rest AndAlso last Then
                        For m = 0 To ports.Length - 2
                            Dim p = context.ParameterIndex(Me, "SR" & (m + 1))
                            If p >= 0 Then add(row, p, x(inlet + 2 + i) / ft)
                        Next
                    Else
                        Dim p = context.ParameterIndex(Me, "SR" & (o + 1))
                        If p >= 0 Then add(row, p, -x(inlet + 2 + i) / ft)
                    End If
                Next
                k += c + 2
            Next
            Return True
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Dim c = context.CompoundCount
            Dim o = k \ (c + 2), j = k Mod (c + 2)
            Dim tag = GraphicObject.OutputConnectors(EOOutletPorts()(o)).AttachedConnector.AttachedTo.Tag
            Dim saturated = context.VaporFractionIndex(EOOutletOffset(context, EOOutletPorts()(o))) >= 0 AndAlso context.VaporFractionIndex(EOInletOffset(context, 0)) >= 0
            Return "outlet " & tag & ": " & If(j = 0, If(saturated, "vapor fraction", "temperature"), If(j = 1, "pressure", "flow " & (j - 2)))
        End Function

    End Class

    Partial Public Class Vessel

        ''' <summary>A saturated inlet keeps the equations: its equations read the inlets' flows, pressures and enthalpies (the flash is of their mixture).</summary>
        Public Overrides ReadOnly Property EOTakesSaturatedInlets As Boolean
            Get
                Return EOHasEquations
            End Get
        End Property

        ''' <summary>
        ''' Equations of its own in the legacy mode, adiabatic or at a set temperature, at the pressure of its inlets, with
        ''' a vapor and a liquid outlet: the two phases of the PT flash of the inlets' mixture, at a common temperature and
        ''' pressure, and the energy balance (or the set temperature, whose duty is a result). The adiabatic mode is the
        ''' same flash with no heat: its energy streams are left out of its calculation.
        ''' </summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                If OverrideP OrElse CalculateRigorousHeatBalance Then Return False
                If CalculationMode = CalculationModes.Adiabatic Then
                    Return GraphicObject.OutputConnectors(0).IsAttached AndAlso GraphicObject.OutputConnectors(1).IsAttached
                End If
                If CalculationMode <> CalculationModes.Legacy Then Return False
                Dim g = GraphicObject
                ' an energy inlet only carries the duty of a vessel at a set temperature
                If Not OverrideT AndAlso g.InputConnectors.Count > 6 AndAlso g.InputConnectors(6).IsAttached Then Return False
                If g.OutputConnectors.Count > 2 AndAlso g.OutputConnectors(2).IsAttached Then Return False
                Return g.OutputConnectors(0).IsAttached AndAlso g.OutputConnectors(1).IsAttached
            End Get
        End Property

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Return If(OverrideT, New String() {"PROP_SV_0"}, New String() {})
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return 2 * context.CompoundCount + 4
        End Function

        Public Overrides Function EOVariables(context As IEquationOrientedContext) As Integer()
            If Not EOHasEquations Then Return MyBase.EOVariables(context)
            Dim offsets = context.InletOffsets(Me).Concat({EOOutletOffset(context, 0), EOOutletOffset(context, 1)}).ToArray()
            Return EOStreamVariables(context, offsets, EOParameters())
        End Function

        ' the inlets' mixture, and the tolerances of the vessel's balances (those of all its inlets together)
        Private Function EOFeed(context As IEquationOrientedContext, x As Double(), inlets As Integer(), ByRef flowTol As Double, ByRef energyTol As Double) As Double()
            Dim c = context.CompoundCount
            Dim z(c - 1) As Double
            flowTol = 0.0
            energyTol = 0.0
            For Each o In inlets
                For i = 0 To c - 1
                    z(i) += x(o + 2 + i)
                Next
                flowTol += context.FlowTolerance(o)
                energyTol += context.EnergyTolerance(o)
            Next
            Return z
        End Function

        Public Overrides Sub EOResiduals(context As IEquationOrientedContext, x As Double(), r As Double(), row As Integer)
            If Not EOHasEquations Then
                MyBase.EOResiduals(context, x, r, row)
                Return
            End If
            Dim c = context.CompoundCount
            Dim inlets = context.InletOffsets(Me)
            Dim vapor = EOOutletOffset(context, 0), liquid = EOOutletOffset(context, 1)
            Dim tf, te As Double
            Dim z = EOFeed(context, x, inlets, tf, te)
            Dim P = InletPressureRule.Pressure(context, x, inlets, PressureCalculation.ToString())
            Dim T = x(vapor)
            Dim split = context.Flash(vapor, T, P, z, False)
            For i = 0 To c - 1
                r(row + i) = (x(vapor + 2 + i) - split.V(i)) / tf
                r(row + c + i) = (x(liquid + 2 + i) - split.L(i)) / tf
            Next
            r(row + 2 * c) = (x(liquid) - T) / context.TemperatureTolerance
            r(row + 2 * c + 1) = (x(vapor + 1) - P) / context.PressureTolerance
            r(row + 2 * c + 2) = (x(liquid + 1) - P) / context.PressureTolerance
            If OverrideT Then
                r(row + 2 * c + 3) = (T - EOParameterValue(context, x, "PROP_SV_0", FlashTemperature)) / context.TemperatureTolerance
                Return
            End If
            Dim hin = 0.0
            For Each o In inlets
                hin += context.Enthalpy(o, x)
            Next
            r(row + 2 * c + 3) = (split.H - hin) / te
        End Sub

        Public Overrides Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean
            If Not EOHasEquations Then Return MyBase.EOJacobian(context, x, add)
            Dim c = context.CompoundCount
            Dim inlets = context.InletOffsets(Me)
            Dim vapor = EOOutletOffset(context, 0), liquid = EOOutletOffset(context, 1)
            Dim tf, te As Double
            Dim z = EOFeed(context, x, inlets, tf, te)
            Dim rule = PressureCalculation.ToString()
            Dim P = InletPressureRule.Pressure(context, x, inlets, rule)
            Dim T = x(vapor)
            Dim e = context.Flash(vapor, T, P, z, True)
            If e Is Nothing Then Throw New NotSupportedException(GraphicObject.Tag & ": no feed")
            Dim weights = InletPressureRule.Weights(context, x, inlets, rule)
            For i = 0 To c - 1
                ' vapor: n_v - V(T, P, z)
                add(i, vapor + 2 + i, 1.0 / tf)
                add(i, vapor, -e.VdT(i) / tf)
                For Each w In weights
                    add(i, w.Key + 1, -e.VdP(i) * w.Value / tf)
                Next
                ' liquid: n_l - (z - V)
                add(c + i, liquid + 2 + i, 1.0 / tf)
                add(c + i, vapor, e.VdT(i) / tf)
                For Each w In weights
                    add(c + i, w.Key + 1, e.VdP(i) * w.Value / tf)
                Next
                For Each o In inlets
                    For j = 0 To c - 1
                        add(i, o + 2 + j, -e.Vdn(i, j) / tf)
                        add(c + i, o + 2 + j, -(If(i = j, 1.0, 0.0) - e.Vdn(i, j)) / tf)
                    Next
                Next
            Next
            Dim tt = context.TemperatureTolerance, tp = context.PressureTolerance
            add(2 * c, liquid, 1.0 / tt)
            add(2 * c, vapor, -1.0 / tt)
            add(2 * c + 1, vapor + 1, 1.0 / tp)
            add(2 * c + 2, liquid + 1, 1.0 / tp)
            For Each w In weights
                add(2 * c + 1, w.Key + 1, -w.Value / tp)
                add(2 * c + 2, w.Key + 1, -w.Value / tp)
            Next
            Dim k = 2 * c + 3
            If OverrideT Then
                add(k, vapor, 1.0 / tt)
                Dim kT = context.ParameterIndex(Me, "PROP_SV_0")
                If kT >= 0 Then add(k, kT, -1.0 / tt)
                Return True
            End If
            ' energy: H(T, P, z) less the inlets' enthalpies
            add(k, vapor, e.HdT / te)
            For Each w In weights
                add(k, w.Key + 1, e.HdP * w.Value / te)
            Next
            For Each o In inlets
                For j = 0 To c - 1
                    add(k, o + 2 + j, e.Hdn(j) / te)
                Next
                If Enumerable.Range(0, c).Sum(Function(i) x(o + 2 + i)) <= 0.0 Then Continue For
                Dim hin = context.EnthalpyDerivatives(o, x)
                For j = 0 To c + 1
                    add(k, o + j, -hin(j) / te)
                Next
            Next
            Return True
        End Function

        ' every row but the liquid temperature reads the block's pressure
        Public Overrides Function EOIsSmooth(context As IEquationOrientedContext, x As Double(), k As Integer) As Boolean
            If Not EOHasEquations Then Return MyBase.EOIsSmooth(context, x, k)
            Return k = 2 * context.CompoundCount OrElse Not InletPressureRule.Tied(context, x, context.InletOffsets(Me), PressureCalculation.ToString())
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Dim c = context.CompoundCount
            If k < c Then Return "vapor flow " & k
            If k < 2 * c Then Return "liquid flow " & (k - c)
            Select Case k - 2 * c
                Case 0 : Return "liquid temperature"
                Case 1 : Return "vapor pressure"
                Case 2 : Return "liquid pressure"
                Case Else : Return If(OverrideT, "set temperature", "energy balance")
            End Select
        End Function

    End Class

    Partial Public Class Pump

        ''' <summary>
        ''' Equations of its own at a set pressure increase or outlet pressure: the compound flows go through, the outlet
        ''' pressure, and the energy the liquid takes, the pressure rise over the inlet's liquid density and the efficiency
        ''' times the mass flow. In the other modes the pump is a black box.
        ''' </summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Return CalcMode = CalculationMode.Delta_P OrElse CalcMode = CalculationMode.OutletPressure
            End Get
        End Property

        ' the pressure increase in that mode, the outlet pressure in the other
        Private ReadOnly Property EOPressureCode As String
            Get
                Return If(CalcMode = CalculationMode.Delta_P, "PROP_PU_0", "PROP_PU_5")
            End Get
        End Property

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Return {EOPressureCode, "PROP_PU_1"}
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return context.CompoundCount + 2
        End Function

        Public Overrides Function EOVariables(context As IEquationOrientedContext) As Integer()
            If Not EOHasEquations Then Return MyBase.EOVariables(context)
            Return EOStreamVariables(context, {EOInletOffset(context, 0), EOOutletOffset(context, 0)}, EOParameters())
        End Function

        ''' <summary>The equations read the streams' enthalpies and the inlet's liquid density, at the state a vapor fraction gives too.</summary>
        Public Overrides ReadOnly Property EOTakesSaturatedStreams As Boolean
            Get
                Return EOHasEquations
            End Get
        End Property

        Private Shared ReadOnly EOLiquidDensity As Func(Of IMaterialStream, Double) = Function(ms) ms.Phases(1).Properties.density.GetValueOrDefault()

        ' the outlet pressure the specification gives, and its derivative with respect to the inlet pressure
        Private Function EOOutletPressure(context As IEquationOrientedContext, x As Double(), inlet As Integer, ByRef dPin As Double) As Double
            If CalcMode = CalculationMode.Delta_P Then
                dPin = 1.0
                Return x(inlet + 1) + EOParameterValue(context, x, "PROP_PU_0", DeltaP.GetValueOrDefault())
            End If
            dPin = 0.0
            Return EOParameterValue(context, x, "PROP_PU_5", Pout)
        End Function

        ' the energy the pump gives the liquid, kW, as its calculation does
        Private Function EOWork(context As IEquationOrientedContext, x As Double(), inlet As Integer, P2 As Double, ByRef rho As Double, ByRef W As Double, ByRef eff As Double) As Double
            Dim c = context.CompoundCount
            Dim mw = context.MolecularWeights
            W = 0.0
            For i = 0 To c - 1
                W += x(inlet + 2 + i) * mw(i) / 1000.0
            Next
            eff = EOParameterValue(context, x, "PROP_PU_1", Eficiencia.GetValueOrDefault()) / 100.0
            rho = context.StreamProperty(inlet, x, "liquid density", EOLiquidDensity)
            ' no liquid: the pump passes the stream as it is
            If rho <= 0.0 Then Return 0.0
            Return (P2 - x(inlet + 1)) / rho / 1000.0 / eff * W
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
            Dim dPin As Double
            Dim P2 = EOOutletPressure(context, x, inlet, dPin)
            r(row + c) = (x(outlet + 1) - P2) / context.PressureTolerance
            Dim rho, W, eff As Double
            Dim work = EOWork(context, x, inlet, P2, rho, W, eff)
            r(row + c + 1) = (context.Enthalpy(outlet, x) - context.Enthalpy(inlet, x) - work) / context.EnergyTolerance(outlet)
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
            Dim dPin As Double
            Dim P2 = EOOutletPressure(context, x, inlet, dPin)
            Dim kP = context.ParameterIndex(Me, EOPressureCode)
            add(c, outlet + 1, 1.0 / tp)
            If dPin <> 0.0 Then add(c, inlet + 1, -dPin / tp)
            If kP >= 0 Then add(c, kP, -1.0 / tp)
            ' energy: H_out - H_in - W (P2 - P1) / (rho 1000 eff)
            Dim te = context.EnergyTolerance(outlet)
            Dim hout = context.EnthalpyDerivatives(outlet, x), hin = context.EnthalpyDerivatives(inlet, x)
            For j = 0 To c + 1
                add(c + 1, outlet + j, hout(j) / te)
                add(c + 1, inlet + j, -hin(j) / te)
            Next
            Dim rho, W, eff As Double
            Dim work = EOWork(context, x, inlet, P2, rho, W, eff)
            If rho > 0.0 Then
                Dim mw = context.MolecularWeights
                Dim per = 1.0 / rho / 1000.0 / eff
                Dim drho = context.StreamPropertyDerivatives(inlet, x, "liquid density", EOLiquidDensity)
                ' through the density
                For j = 0 To c + 1
                    add(c + 1, inlet + j, work / rho * drho(j) / te)
                Next
                ' through the mass flow and the pressure rise
                For i = 0 To c - 1
                    add(c + 1, inlet + 2 + i, -(P2 - x(inlet + 1)) * per * mw(i) / 1000.0 / te)
                Next
                add(c + 1, inlet + 1, -W * per * (dPin - 1.0) / te)
                If kP >= 0 Then add(c + 1, kP, -W * per / te)
                Dim kE = context.ParameterIndex(Me, "PROP_PU_1")
                If kE >= 0 Then add(c + 1, kE, work / (eff * 100.0) / te)
            End If
            Return True
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Dim c = context.CompoundCount
            Return If(k < c, "flow " & k, If(k = c, "pressure", "energy balance"))
        End Function

    End Class

    Partial Public Class Compressor

        ''' <summary>
        ''' Equations of its own on the adiabatic path at a set pressure increase, outlet pressure or pressure ratio: the
        ''' compound flows go through, the outlet pressure, and the outlet enthalpy the inlet's plus the isentropic rise over
        ''' the efficiency. The isentropic rise ends where the inlet's moles at the outlet pressure have the inlet's entropy.
        ''' In the other modes, and on the polytropic path, the compressor is a black box.
        ''' </summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Return ProcessPath = ProcessPathType.Adiabatic AndAlso
                    (CalcMode = CalculationMode.Delta_P OrElse CalcMode = CalculationMode.OutletPressure OrElse CalcMode = CalculationMode.PressureRatio)
            End Get
        End Property

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Select Case CalcMode
                Case CalculationMode.Delta_P : Return {"PROP_CO_0", "PROP_CO_1"}
                Case CalculationMode.OutletPressure : Return {"PROP_CO_4", "PROP_CO_1"}
                Case Else : Return {"PROP_CO_1"}
            End Select
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return context.CompoundCount + 2
        End Function

        Public Overrides Function EOVariables(context As IEquationOrientedContext) As Integer()
            If Not EOHasEquations Then Return MyBase.EOVariables(context)
            Return EOStreamVariables(context, {EOInletOffset(context, 0), EOOutletOffset(context, 0)}, EOParameters())
        End Function

        ' the outlet pressure the specification gives, its derivative with respect to the inlet pressure, and the variable
        ' of the specification when the solver moves it
        Private Function EOOutletPressure(context As IEquationOrientedContext, x As Double(), inlet As Integer, ByRef dPin As Double, ByRef kP As Integer) As Double
            Select Case CalcMode
                Case CalculationMode.Delta_P
                    dPin = 1.0
                    kP = context.ParameterIndex(Me, "PROP_CO_0")
                    Return x(inlet + 1) + EOParameterValue(context, x, "PROP_CO_0", DeltaP)
                Case CalculationMode.OutletPressure
                    dPin = 0.0
                    kP = context.ParameterIndex(Me, "PROP_CO_4")
                    Return EOParameterValue(context, x, "PROP_CO_4", POut)
                Case Else
                    dPin = PressureRatio
                    kP = -1
                    Return x(inlet + 1) * PressureRatio
            End Select
        End Function

        ' the moles of the inlet, as the outlet carries them
        Private Function EOMoles(context As IEquationOrientedContext, x As Double(), inlet As Integer) As Double()
            Dim n(context.CompoundCount - 1) As Double
            Array.Copy(x, inlet + 2, n, 0, n.Length)
            Return n
        End Function

        ''' <summary>
        ''' The isentropic outlet temperature: the inlet's moles at P2 with the inlet's entropy, by secants from the
        ''' ideal-gas estimate.
        ''' </summary>
        Private Function EOIsentropicTemperature(context As IEquationOrientedContext, x As Double(), inlet As Integer, P2 As Double, n As Double()) As Double
            Dim T1 = x(inlet), P1 = x(inlet + 1)
            Dim S1 = context.Flash(inlet, T1, P1, n, False).S
            Dim Ta = T1 * Math.Pow(Math.Max(P2, 1.0) / Math.Max(P1, 1.0), 0.2857)
            Dim fa = context.Flash(inlet, Ta, P2, n, False).S - S1
            Dim Tb = Ta + 1.0
            Dim fb = context.Flash(inlet, Tb, P2, n, False).S - S1
            For it = 1 To 60
                If fb = fa Then Exit For
                Dim step1 = -fb * (Tb - Ta) / (fb - fa)
                step1 = Math.Max(-50.0, Math.Min(50.0, step1))
                Ta = Tb : fa = fb
                Tb = Math.Max(Tb + step1, 10.0)
                fb = context.Flash(inlet, Tb, P2, n, False).S - S1
                If Math.Abs(Tb - Ta) < 0.0000001 Then Exit For
            Next
            Return Tb
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
            Dim dPin As Double, kP As Integer
            Dim P2 = EOOutletPressure(context, x, inlet, dPin, kP)
            r(row + c) = (x(outlet + 1) - P2) / context.PressureTolerance
            Dim n = EOMoles(context, x, inlet)
            Dim te = context.EnergyTolerance(outlet)
            Dim H1 = context.Enthalpy(inlet, x)
            If n.Sum() <= 0.0 Then
                r(row + c + 1) = (context.Enthalpy(outlet, x) - H1) / te
                Return
            End If
            Dim eff = EOParameterValue(context, x, "PROP_CO_1", AdiabaticEfficiency) / 100.0
            Dim Ts = EOIsentropicTemperature(context, x, inlet, P2, n)
            Dim Hs = context.Flash(inlet, Ts, P2, n, False).H
            r(row + c + 1) = (context.Enthalpy(outlet, x) - H1 - (Hs - H1) / eff) / te
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
            Dim dPin As Double, kP As Integer
            Dim P2 = EOOutletPressure(context, x, inlet, dPin, kP)
            add(c, outlet + 1, 1.0 / tp)
            If dPin <> 0.0 Then add(c, inlet + 1, -dPin / tp)
            If kP >= 0 Then add(c, kP, -1.0 / tp)

            Dim te = context.EnergyTolerance(outlet)
            Dim hout = context.EnthalpyDerivatives(outlet, x)
            For j = 0 To c + 1
                add(c + 1, outlet + j, hout(j) / te)
            Next
            Dim n = EOMoles(context, x, inlet)
            Dim hin = context.EnthalpyDerivatives(inlet, x)
            If n.Sum() <= 0.0 Then
                For j = 0 To c + 1
                    add(c + 1, inlet + j, -hin(j) / te)
                Next
                Return True
            End If
            Dim eff = EOParameterValue(context, x, "PROP_CO_1", AdiabaticEfficiency) / 100.0
            ' H_out - (1 - 1/eff) H_in - H_s / eff
            For j = 0 To c + 1
                add(c + 1, inlet + j, -(1.0 - 1.0 / eff) * hin(j) / te)
            Next
            ' H_s = H(T_s, P2, n) with S(T_s, P2, n) = S(T1, P1, n): T_s moves with the inlet and with P2
            Dim Ts = EOIsentropicTemperature(context, x, inlet, P2, n)
            Dim e1 = context.Flash(inlet, x(inlet), x(inlet + 1), n, True)
            Dim es = context.Flash(inlet, Ts, P2, n, True)
            If e1 Is Nothing OrElse es Is Nothing Then Throw New NotSupportedException(GraphicObject.Tag & ": no flow")
            Dim dHsdP2 = es.HdP - es.HdT * es.SdP / es.SdT
            add(c + 1, inlet, -es.HdT * e1.SdT / es.SdT / eff / te)
            add(c + 1, inlet + 1, -(es.HdT * e1.SdP / es.SdT + dHsdP2 * dPin) / eff / te)
            For i = 0 To c - 1
                add(c + 1, inlet + 2 + i, -(es.Hdn(i) + es.HdT * (e1.Sdn(i) - es.Sdn(i)) / es.SdT) / eff / te)
            Next
            If kP >= 0 Then add(c + 1, kP, -dHsdP2 / eff / te)
            Dim kE = context.ParameterIndex(Me, "PROP_CO_1")
            If kE >= 0 Then
                Dim H1 = context.Enthalpy(inlet, x)
                add(c + 1, kE, (es.H - H1) / (eff * eff) / 100.0 / te)
            End If
            Return True
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Dim c = context.CompoundCount
            Return If(k < c, "flow " & k, If(k = c, "pressure", "energy balance"))
        End Function

    End Class

    Partial Public Class HeatExchanger

        ''' <summary>A saturated inlet keeps the equations: its equations read the inlets' flows, pressures, temperatures and enthalpies.</summary>
        Public Overrides ReadOnly Property EOTakesSaturatedInlets As Boolean
            Get
                Return EOHasEquations
            End Get
        End Property

        ''' <summary>
        ''' Equations of its own with a set outlet temperature on one side, a set heat load, a set U and A, or a set vapor
        ''' fraction at one outlet: each side's compound flows go through with its pressure drop, the heat the hot side gives
        ''' is what the cold side takes plus the heat loss, and the specification. With U and A, the heat load is U A F LMTD,
        ''' the point where the unit's own iteration stops. A vapor fraction sets the outlet's temperature where that
        ''' outlet's compounds, at its pressure, flash to it (the bubble point at 0, the dew point at 1), a condition smooth
        ''' in the pressure and the flows where the vapor fraction itself is flat. The cold side is the inlet at the lower
        ''' temperature, as the unit decides. The rating, pinch and efficiency modes take the exchanger as a black box.
        ''' </summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Select Case CalcMode
                    Case HeatExchangerCalcMode.CalcTempHotOut, HeatExchangerCalcMode.CalcTempColdOut,
                         HeatExchangerCalcMode.CalcBothTemp, HeatExchangerCalcMode.CalcBothTemp_UA,
                         HeatExchangerCalcMode.OutletVaporFraction1, HeatExchangerCalcMode.OutletVaporFraction2
                        Dim g = GraphicObject
                        Return g.InputConnectors(0).IsAttached AndAlso g.InputConnectors(1).IsAttached AndAlso
                               g.OutputConnectors(0).IsAttached AndAlso g.OutputConnectors(1).IsAttached
                    Case Else
                        Return False
                End Select
            End Get
        End Property

        ''' <summary>
        ''' At a set heat load or a set U and A the equations read the streams' enthalpies, at the state a vapor fraction gives
        ''' too, and their temperatures, which follow a saturated stream's vapor fraction: a condensing or boiling side keeps
        ''' them. A set outlet temperature or vapor fraction would fix what the vapor fraction leaves open.
        ''' </summary>
        Public Overrides ReadOnly Property EOTakesSaturatedStreams As Boolean
            Get
                Return EOHasEquations AndAlso (CalcMode = HeatExchangerCalcMode.CalcBothTemp OrElse CalcMode = HeatExchangerCalcMode.CalcBothTemp_UA)
            End Get
        End Property

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Dim common = {"PROP_HX_28", "PROP_HX_32", "PROP_HX_33"}
            Select Case CalcMode
                Case HeatExchangerCalcMode.CalcTempHotOut : Return common.Concat({"PROP_HX_3"}).ToArray()
                Case HeatExchangerCalcMode.CalcTempColdOut : Return common.Concat({"PROP_HX_4"}).ToArray()
                Case HeatExchangerCalcMode.CalcBothTemp : Return common.Concat({"PROP_HX_2"}).ToArray()
                Case HeatExchangerCalcMode.OutletVaporFraction1 : Return common.Concat({"PROP_HX_30"}).ToArray()
                Case HeatExchangerCalcMode.OutletVaporFraction2 : Return common.Concat({"PROP_HX_31"}).ToArray()
                Case Else : Return common.Concat({"PROP_HX_0", "PROP_HX_1"}).ToArray()
            End Select
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return 2 * (context.CompoundCount + 2)
        End Function

        Public Overrides Function EOVariables(context As IEquationOrientedContext) As Integer()
            If Not EOHasEquations Then Return MyBase.EOVariables(context)
            Return EOStreamVariables(context, {EOInletOffset(context, 0), EOInletOffset(context, 1), EOOutletOffset(context, 0), EOOutletOffset(context, 1)}, EOParameters())
        End Function

        ' the sides: inlet and outlet offsets of the cold and the hot fluid
        Private Sub EOSides(context As IEquationOrientedContext, x As Double(), ByRef ci As Integer, ByRef co As Integer, ByRef hi As Integer, ByRef ho As Integer)
            Dim i0 = EOInletOffset(context, 0), i1 = EOInletOffset(context, 1)
            Dim o0 = EOOutletOffset(context, 0), o1 = EOOutletOffset(context, 1)
            If x(i0) < x(i1) Then
                ci = i0 : co = o0 : hi = i1 : ho = o1
            Else
                ci = i1 : co = o1 : hi = i0 : ho = o0
            End If
        End Sub

        ''' <summary>The log mean temperature difference times the correction factor, and its derivatives with respect to Th1, Tc1, Th2 and Tc2.</summary>
        Private Function EOLMTD(Th1 As Double, Tc1 As Double, Th2 As Double, Tc2 As Double, ByRef d As Double()) As Double
            Dim a, b As Double
            Dim da(3), db(3) As Double
            If FlowDir = FlowDirection.CoCurrent Then
                a = Th1 - Tc1 : da(0) = 1 : da(1) = -1
                b = Th2 - Tc2 : db(2) = 1 : db(3) = -1
            Else
                a = Th1 - Tc2 : da(0) = 1 : da(3) = -1
                b = Th2 - Tc1 : db(2) = 1 : db(1) = -1
            End If
            ' the unit's own floor on the differences
            If a < 0.001 Then a = 0.001 : da = New Double(3) {}
            If b < 0.001 Then b = 0.001 : db = New Double(3) {}
            Dim L, La, Lb As Double
            If Math.Abs(a / b - 1.0) < 0.0001 Then
                L = (a + b) / 2 : La = 0.5 : Lb = 0.5
            Else
                Dim lr = Math.Log(a / b)
                L = (a - b) / lr
                La = 1.0 / lr - (a - b) / (a * lr * lr)
                Lb = -1.0 / lr + (a - b) / (b * lr * lr)
            End If
            d = New Double(3) {}
            For k = 0 To 3
                d(k) = (La * da(k) + Lb * db(k)) * CorrectionFactorLMTD
            Next
            Return L * CorrectionFactorLMTD
        End Function

        ' the vapor fraction mode's outlet (that of inlet 0 or 1), its specification and the specification's code
        Private Function EOVaporFractionOutlet(context As IEquationOrientedContext, x As Double(), ByRef vf As Double, ByRef code As String) As Integer
            Dim first = CalcMode = HeatExchangerCalcMode.OutletVaporFraction1
            code = If(first, "PROP_HX_30", "PROP_HX_31")
            vf = EOParameterValue(context, x, code, If(first, OutletVaporFraction1, OutletVaporFraction2))
            Return EOOutletOffset(context, If(first, 0, 1))
        End Function

        ' the outlet's temperature at the vapor fraction, flashed by the package of the side's inlet; read by name and state
        Private Function EOVaporFractionRead(vf As Double, ByRef name As String) As Func(Of IMaterialStream, Double)
            Dim pp = GetInletMaterialStream(If(CalcMode = HeatExchangerCalcMode.OutletVaporFraction1, 0, 1)).PropertyPackage
            name = Me.Name & " vapor fraction temperature " & vf.ToString("R", Globalization.CultureInfo.InvariantCulture)
            Return Function(ms)
                       pp.CurrentMaterialStream = ms
                       Dim res = pp.CalculateEquilibrium2(Enums.FlashCalculationType.PressureVaporFraction, ms.GetPressure(), vf, ms.GetTemperature())
                       Return res.CalculatedTemperature.GetValueOrDefault(Double.NaN)
                   End Function
        End Function

        ''' <summary>The temperature at which the outlet's compounds, at its pressure, flash to the vapor fraction, as the unit flashes them.</summary>
        Private Function EOVaporFractionTemperature(context As IEquationOrientedContext, x As Double(), offset As Integer, vf As Double) As Double
            Dim name As String = Nothing
            Dim read = EOVaporFractionRead(vf, name)
            Return context.StreamProperty(offset, x, name, read)
        End Function

        Public Overrides Sub EOResiduals(context As IEquationOrientedContext, x As Double(), r As Double(), row As Integer)
            If Not EOHasEquations Then
                MyBase.EOResiduals(context, x, r, row)
                Return
            End If
            Dim c = context.CompoundCount
            Dim ci, co, hi, ho As Integer
            EOSides(context, x, ci, co, hi, ho)
            Dim k = row
            For Each side In {New Integer() {ci, co}, New Integer() {hi, ho}}
                Dim ft = context.FlowTolerance(side(1))
                For i = 0 To c - 1
                    r(k) = (x(side(1) + 2 + i) - x(side(0) + 2 + i)) / ft
                    k += 1
                Next
            Next
            Dim tp = context.PressureTolerance
            r(k) = (x(co + 1) - (x(ci + 1) - EOParameterValue(context, x, "PROP_HX_32", ColdSidePressureDrop))) / tp : k += 1
            r(k) = (x(ho + 1) - (x(hi + 1) - EOParameterValue(context, x, "PROP_HX_33", HotSidePressureDrop))) / tp : k += 1
            Dim te = context.EnergyTolerance(co) + context.EnergyTolerance(ho)
            Dim dHc = context.Enthalpy(co, x) - context.Enthalpy(ci, x)
            Dim dHh = context.Enthalpy(ho, x) - context.Enthalpy(hi, x)
            Dim loss = EOParameterValue(context, x, "PROP_HX_28", HeatLoss)
            Select Case CalcMode
                Case HeatExchangerCalcMode.CalcTempHotOut
                    r(k) = (x(co) - EOParameterValue(context, x, "PROP_HX_3", TempColdOut.GetValueOrDefault())) / context.TemperatureTolerance
                    r(k + 1) = (dHc + dHh + loss) / te
                Case HeatExchangerCalcMode.CalcTempColdOut
                    r(k) = (x(ho) - EOParameterValue(context, x, "PROP_HX_4", TempHotOut.GetValueOrDefault())) / context.TemperatureTolerance
                    r(k + 1) = (dHc + dHh + loss) / te
                Case HeatExchangerCalcMode.CalcBothTemp
                    Dim q = EOParameterValue(context, x, "PROP_HX_2", Me.Q.GetValueOrDefault())
                    r(k) = (dHc - q) / te
                    r(k + 1) = (dHh + q + loss) / te
                Case HeatExchangerCalcMode.OutletVaporFraction1, HeatExchangerCalcMode.OutletVaporFraction2
                    Dim vf As Double, code As String = Nothing
                    Dim so = EOVaporFractionOutlet(context, x, vf, code)
                    r(k) = (x(so) - EOVaporFractionTemperature(context, x, so, vf)) / context.TemperatureTolerance
                    r(k + 1) = (dHc + dHh + loss) / te
                Case Else
                    Dim d As Double() = Nothing
                    Dim L = EOLMTD(x(hi), x(ci), x(ho), x(co), d)
                    Dim UA = EOParameterValue(context, x, "PROP_HX_0", OverallCoefficient.GetValueOrDefault()) * EOParameterValue(context, x, "PROP_HX_1", Area.GetValueOrDefault())
                    r(k) = (dHc - UA * L / 1000.0) / te
                    r(k + 1) = (dHc + dHh + loss) / te
            End Select
        End Sub

        Public Overrides Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean
            If Not EOHasEquations Then Return MyBase.EOJacobian(context, x, add)
            Dim c = context.CompoundCount
            Dim ci, co, hi, ho As Integer
            EOSides(context, x, ci, co, hi, ho)
            Dim k = 0
            For Each side In {New Integer() {ci, co}, New Integer() {hi, ho}}
                Dim ft = context.FlowTolerance(side(1))
                For i = 0 To c - 1
                    add(k, side(1) + 2 + i, 1.0 / ft)
                    add(k, side(0) + 2 + i, -1.0 / ft)
                    k += 1
                Next
            Next
            Dim tp = context.PressureTolerance
            add(k, co + 1, 1.0 / tp) : add(k, ci + 1, -1.0 / tp)
            Dim kp = context.ParameterIndex(Me, "PROP_HX_32")
            If kp >= 0 Then add(k, kp, 1.0 / tp)
            k += 1
            add(k, ho + 1, 1.0 / tp) : add(k, hi + 1, -1.0 / tp)
            kp = context.ParameterIndex(Me, "PROP_HX_33")
            If kp >= 0 Then add(k, kp, 1.0 / tp)
            k += 1
            Dim te = context.EnergyTolerance(co) + context.EnergyTolerance(ho)
            Dim hco = context.EnthalpyDerivatives(co, x), hci = context.EnthalpyDerivatives(ci, x)
            Dim hho = context.EnthalpyDerivatives(ho, x), hhi = context.EnthalpyDerivatives(hi, x)
            ' sign times d(dHc) and d(dHh) into a row
            Dim addCold = Sub(row As Integer, sign As Double)
                              For j = 0 To c + 1
                                  add(row, co + j, sign * hco(j) / te)
                                  add(row, ci + j, -sign * hci(j) / te)
                              Next
                          End Sub
            Dim addHot = Sub(row As Integer, sign As Double)
                             For j = 0 To c + 1
                                 add(row, ho + j, sign * hho(j) / te)
                                 add(row, hi + j, -sign * hhi(j) / te)
                             Next
                         End Sub
            Dim kl = context.ParameterIndex(Me, "PROP_HX_28")
            Select Case CalcMode
                Case HeatExchangerCalcMode.CalcTempHotOut, HeatExchangerCalcMode.CalcTempColdOut
                    Dim tt = context.TemperatureTolerance
                    Dim code = If(CalcMode = HeatExchangerCalcMode.CalcTempHotOut, "PROP_HX_3", "PROP_HX_4")
                    add(k, If(CalcMode = HeatExchangerCalcMode.CalcTempHotOut, co, ho), 1.0 / tt)
                    Dim ks = context.ParameterIndex(Me, code)
                    If ks >= 0 Then add(k, ks, -1.0 / tt)
                    addCold(k + 1, 1.0) : addHot(k + 1, 1.0)
                Case HeatExchangerCalcMode.CalcBothTemp
                    addCold(k, 1.0)
                    addHot(k + 1, 1.0)
                    Dim kq = context.ParameterIndex(Me, "PROP_HX_2")
                    If kq >= 0 Then add(k, kq, -1.0 / te) : add(k + 1, kq, 1.0 / te)
                Case HeatExchangerCalcMode.OutletVaporFraction1, HeatExchangerCalcMode.OutletVaporFraction2
                    Dim tt = context.TemperatureTolerance
                    Dim vf As Double, code As String = Nothing, name As String = Nothing
                    Dim so = EOVaporFractionOutlet(context, x, vf, code)
                    add(k, so, 1.0 / tt)
                    ' the flash temperature by central differences on the outlet's pressure and flows; the outlet's own
                    ' temperature is only the flash's first estimate
                    Dim read = EOVaporFractionRead(vf, name)
                    Dim d = context.StreamPropertyDerivatives(so, x, name, read)
                    For j = 1 To c + 1
                        add(k, so + j, -d(j) / tt)
                    Next
                    Dim kv = context.ParameterIndex(Me, code)
                    If kv >= 0 Then
                        Dim lo = Math.Max(vf - 0.0001, 0.0), up = Math.Min(vf + 0.0001, 1.0)
                        Dim dT = (EOVaporFractionTemperature(context, x, so, up) - EOVaporFractionTemperature(context, x, so, lo)) / (up - lo)
                        add(k, kv, -dT / tt)
                    End If
                    addCold(k + 1, 1.0) : addHot(k + 1, 1.0)
                Case Else
                    Dim d As Double() = Nothing
                    Dim U = EOParameterValue(context, x, "PROP_HX_0", OverallCoefficient.GetValueOrDefault())
                    Dim A = EOParameterValue(context, x, "PROP_HX_1", Area.GetValueOrDefault())
                    Dim L = EOLMTD(x(hi), x(ci), x(ho), x(co), d)
                    addCold(k, 1.0)
                    Dim temps = {hi, ci, ho, co}
                    For m = 0 To 3
                        add(k, temps(m), -U * A * d(m) / 1000.0 / te)
                    Next
                    Dim kU = context.ParameterIndex(Me, "PROP_HX_0"), kA = context.ParameterIndex(Me, "PROP_HX_1")
                    If kU >= 0 Then add(k, kU, -A * L / 1000.0 / te)
                    If kA >= 0 Then add(k, kA, -U * L / 1000.0 / te)
                    addCold(k + 1, 1.0) : addHot(k + 1, 1.0)
            End Select
            If kl >= 0 Then add(k + 1, kl, 1.0 / te)
            Return True
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Dim c = context.CompoundCount
            If k < c Then Return "cold side flow " & k
            If k < 2 * c Then Return "hot side flow " & (k - c)
            Select Case k - 2 * c
                Case 0 : Return "cold side pressure"
                Case 1 : Return "hot side pressure"
                Case 2
                    Select Case CalcMode
                        Case HeatExchangerCalcMode.CalcTempHotOut : Return "cold outlet temperature"
                        Case HeatExchangerCalcMode.CalcTempColdOut : Return "hot outlet temperature"
                        Case HeatExchangerCalcMode.CalcBothTemp : Return "heat the cold side takes"
                        Case HeatExchangerCalcMode.OutletVaporFraction1, HeatExchangerCalcMode.OutletVaporFraction2
                            Return "outlet temperature at its vapor fraction"
                        Case Else : Return "heat load = U A LMTD"
                    End Select
                Case Else
                    Return If(CalcMode = HeatExchangerCalcMode.CalcBothTemp, "heat the hot side gives", "energy balance")
            End Select
        End Function

    End Class


    Partial Public Class ComponentSeparator

        ''' <summary>A saturated inlet keeps the equations: its equations read the inlet's flows, pressure and temperature.</summary>
        Public Overrides ReadOnly Property EOTakesSaturatedInlets As Boolean
            Get
                Return EOHasEquations
            End Get
        End Property

        ''' <summary>
        ''' Equations of its own when every compound has a specification: both outlets at the inlet's temperature and
        ''' pressure, the specified outlet with each compound's set flow (molar, or mass over the molar mass) or set share
        ''' of the inlet's, and the other with the rest. The energy imbalance is a result.
        ''' </summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Dim g = GraphicObject
                If Not (g.InputConnectors(0).IsAttached AndAlso g.OutputConnectors(0).IsAttached AndAlso g.OutputConnectors(1).IsAttached) Then Return False
                If SpecifiedStreamIndex > 1 Then Return False
                Dim inlet = TryCast(GetInletMaterialStream(0), DWSIM.Thermodynamics.Streams.MaterialStream)
                If inlet Is Nothing Then Return False
                Return inlet.Phases(0).Compounds.Keys.All(Function(k) ComponentSepSpecs.ContainsKey(k))
            End Get
        End Property

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Return New String() {}
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return 2 * (context.CompoundCount + 2)
        End Function

        Public Overrides Function EOVariables(context As IEquationOrientedContext) As Integer()
            If Not EOHasEquations Then Return MyBase.EOVariables(context)
            Return EOStreamVariables(context, {EOInletOffset(context, 0), EOOutletOffset(context, 0), EOOutletOffset(context, 1)}, Nothing)
        End Function

        ' the specified outlet's flow of compound i: a set flow (mol/s) plus a share of the inlet's
        Private Sub EOSpec(context As IEquationOrientedContext, i As Integer, ByRef setFlow As Double, ByRef share As Double)
            Dim cs = ComponentSepSpecs(context.CompoundNames(i))
            setFlow = 0.0 : share = 0.0
            Select Case cs.SepSpec
                Case Auxiliary.SeparationSpec.MolarFlow
                    setFlow = SharedClasses.SystemsOfUnits.Converter.ConvertToSI(cs.SpecUnit, cs.SpecValue)
                Case Auxiliary.SeparationSpec.MassFlow
                    setFlow = SharedClasses.SystemsOfUnits.Converter.ConvertToSI(cs.SpecUnit, cs.SpecValue) / context.MolecularWeights(i) * 1000.0
                Case Else
                    ' a share of the compound's mass or moles is the same share of the other
                    share = cs.SpecValue / 100.0
            End Select
        End Sub

        Public Overrides Sub EOResiduals(context As IEquationOrientedContext, x As Double(), r As Double(), row As Integer)
            If Not EOHasEquations Then
                MyBase.EOResiduals(context, x, r, row)
                Return
            End If
            Dim c = context.CompoundCount
            Dim inlet = EOInletOffset(context, 0)
            For o = 0 To 1
                Dim s = EOOutletOffset(context, o)
                Dim specified = o = SpecifiedStreamIndex
                Dim ft = context.FlowTolerance(s)
                r(row) = (x(s) - x(inlet)) / context.TemperatureTolerance
                r(row + 1) = (x(s + 1) - x(inlet + 1)) / context.PressureTolerance
                For i = 0 To c - 1
                    Dim setFlow, share As Double
                    EOSpec(context, i, setFlow, share)
                    Dim taken = setFlow + share * x(inlet + 2 + i)
                    r(row + 2 + i) = (x(s + 2 + i) - If(specified, taken, x(inlet + 2 + i) - taken)) / ft
                Next
                row += c + 2
            Next
        End Sub

        Public Overrides Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean
            If Not EOHasEquations Then Return MyBase.EOJacobian(context, x, add)
            Dim c = context.CompoundCount
            Dim inlet = EOInletOffset(context, 0)
            Dim k = 0
            For o = 0 To 1
                Dim s = EOOutletOffset(context, o)
                Dim specified = o = SpecifiedStreamIndex
                Dim ft = context.FlowTolerance(s)
                add(k, s, 1.0 / context.TemperatureTolerance)
                add(k, inlet, -1.0 / context.TemperatureTolerance)
                add(k + 1, s + 1, 1.0 / context.PressureTolerance)
                add(k + 1, inlet + 1, -1.0 / context.PressureTolerance)
                For i = 0 To c - 1
                    Dim setFlow, share As Double
                    EOSpec(context, i, setFlow, share)
                    add(k + 2 + i, s + 2 + i, 1.0 / ft)
                    add(k + 2 + i, inlet + 2 + i, -If(specified, share, 1.0 - share) / ft)
                Next
                k += c + 2
            Next
            Return True
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Dim c = context.CompoundCount
            Dim o = k \ (c + 2), j = k Mod (c + 2)
            Dim tag = GraphicObject.OutputConnectors(o).AttachedConnector.AttachedTo.Tag
            Return "outlet " & tag & ": " & If(j = 0, "temperature", If(j = 1, "pressure", "flow " & (j - 2)))
        End Function

    End Class


    Partial Public Class Expander

        ''' <summary>
        ''' Equations of its own on the adiabatic path at a set pressure drop, outlet pressure or pressure ratio: the compound
        ''' flows go through, the outlet pressure, and the outlet enthalpy the inlet's less the efficiency times the
        ''' isentropic drop. The isentropic drop ends where the inlet's moles at the outlet pressure have the inlet's
        ''' entropy. The power is the enthalpy the fluid gives up. In the other modes, and on the polytropic path, the
        ''' expander is a black box.
        ''' </summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Return ProcessPath = ProcessPathType.Adiabatic AndAlso
                    (CalcMode = CalculationMode.Delta_P OrElse CalcMode = CalculationMode.OutletPressure OrElse CalcMode = CalculationMode.PressureRatio)
            End Get
        End Property

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Select Case CalcMode
                Case CalculationMode.Delta_P : Return {"PROP_TU_0", "PROP_TU_1"}
                Case CalculationMode.OutletPressure : Return {"PROP_TU_4", "PROP_TU_1"}
                Case Else : Return {"PROP_TU_1"}
            End Select
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return context.CompoundCount + 2
        End Function

        Public Overrides Function EOVariables(context As IEquationOrientedContext) As Integer()
            If Not EOHasEquations Then Return MyBase.EOVariables(context)
            Return EOStreamVariables(context, {EOInletOffset(context, 0), EOOutletOffset(context, 0)}, EOParameters())
        End Function

        ' the outlet pressure the specification gives, its derivatives with respect to the inlet pressure and to the
        ' specification, and the variable of the specification when the solver moves it
        Private Function EOOutletPressure(context As IEquationOrientedContext, x As Double(), inlet As Integer, ByRef dPin As Double, ByRef dSpec As Double, ByRef kP As Integer) As Double
            Select Case CalcMode
                Case CalculationMode.Delta_P
                    dPin = 1.0 : dSpec = -1.0
                    kP = context.ParameterIndex(Me, "PROP_TU_0")
                    Return x(inlet + 1) - EOParameterValue(context, x, "PROP_TU_0", DeltaP)
                Case CalculationMode.OutletPressure
                    dPin = 0.0 : dSpec = 1.0
                    kP = context.ParameterIndex(Me, "PROP_TU_4")
                    Return EOParameterValue(context, x, "PROP_TU_4", POut)
                Case Else
                    dPin = PressureRatio : dSpec = 0.0
                    kP = -1
                    Return x(inlet + 1) * PressureRatio
            End Select
        End Function

        Private Function EOMoles(context As IEquationOrientedContext, x As Double(), inlet As Integer) As Double()
            Dim n(context.CompoundCount - 1) As Double
            Array.Copy(x, inlet + 2, n, 0, n.Length)
            Return n
        End Function

        ''' <summary>The isentropic outlet temperature: the inlet's moles at P2 with the inlet's entropy, by secants from the ideal-gas estimate.</summary>
        Private Function EOIsentropicTemperature(context As IEquationOrientedContext, x As Double(), inlet As Integer, P2 As Double, n As Double()) As Double
            Dim T1 = x(inlet), P1 = x(inlet + 1)
            Dim S1 = context.Flash(inlet, T1, P1, n, False).S
            Dim Ta = T1 * Math.Pow(Math.Max(P2, 1.0) / Math.Max(P1, 1.0), 0.2857)
            Dim fa = context.Flash(inlet, Ta, P2, n, False).S - S1
            Dim Tb = Ta + 1.0
            Dim fb = context.Flash(inlet, Tb, P2, n, False).S - S1
            For it = 1 To 60
                If fb = fa Then Exit For
                Dim step1 = -fb * (Tb - Ta) / (fb - fa)
                step1 = Math.Max(-50.0, Math.Min(50.0, step1))
                Ta = Tb : fa = fb
                Tb = Math.Max(Tb + step1, 10.0)
                fb = context.Flash(inlet, Tb, P2, n, False).S - S1
                If Math.Abs(Tb - Ta) < 0.0000001 Then Exit For
            Next
            Return Tb
        End Function

        ''' <summary>
        ''' The isentropic outlet's enthalpy flow (kW) as the unit finds it: the package's PS flash at P2 from the inlet's
        ''' entropy. The final pass of the units recalculates the expander this way; in two phases that flash closes looser
        ''' than the isentropic temperature found by secants, and the two would leave the outlet some hundredths of a
        ''' kelvin apart.
        ''' </summary>
        Private Function EOIsentropicEnthalpy(context As IEquationOrientedContext, x As Double(), inlet As Integer, P2 As Double) As Double
            Dim pp = GetInletMaterialStream(0).PropertyPackage
            Dim name = Me.Name & " isentropic enthalpy " & P2.ToString("R", Globalization.CultureInfo.InvariantCulture)
            Return context.StreamProperty(inlet, x, name,
                Function(ms)
                    pp.CurrentMaterialStream = ms
                    Dim S1 = ms.Phases(0).Properties.entropy.GetValueOrDefault()
                    Dim res = pp.CalculateEquilibrium2(Enums.FlashCalculationType.PressureEntropy, P2, S1, ms.GetTemperature())
                    Return res.CalculatedEnthalpy.GetValueOrDefault(Double.NaN) * ms.GetMassFlow()
                End Function)
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
            Dim dPin, dSpec As Double, kP As Integer
            Dim P2 = EOOutletPressure(context, x, inlet, dPin, dSpec, kP)
            r(row + c) = (x(outlet + 1) - P2) / context.PressureTolerance
            Dim n = EOMoles(context, x, inlet)
            Dim te = context.EnergyTolerance(outlet)
            Dim H1 = context.Enthalpy(inlet, x)
            If n.Sum() <= 0.0 Then
                r(row + c + 1) = (context.Enthalpy(outlet, x) - H1) / te
                Return
            End If
            Dim eff = EOParameterValue(context, x, "PROP_TU_1", AdiabaticEfficiency) / 100.0
            Dim Hs = EOIsentropicEnthalpy(context, x, inlet, P2)
            r(row + c + 1) = (context.Enthalpy(outlet, x) - H1 + eff * (H1 - Hs)) / te
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
            Dim dPin, dSpec As Double, kP As Integer
            Dim P2 = EOOutletPressure(context, x, inlet, dPin, dSpec, kP)
            add(c, outlet + 1, 1.0 / tp)
            If dPin <> 0.0 Then add(c, inlet + 1, -dPin / tp)
            If kP >= 0 Then add(c, kP, -dSpec / tp)

            Dim te = context.EnergyTolerance(outlet)
            Dim hout = context.EnthalpyDerivatives(outlet, x)
            For j = 0 To c + 1
                add(c + 1, outlet + j, hout(j) / te)
            Next
            Dim n = EOMoles(context, x, inlet)
            Dim hin = context.EnthalpyDerivatives(inlet, x)
            If n.Sum() <= 0.0 Then
                For j = 0 To c + 1
                    add(c + 1, inlet + j, -hin(j) / te)
                Next
                Return True
            End If
            Dim eff = EOParameterValue(context, x, "PROP_TU_1", AdiabaticEfficiency) / 100.0
            ' H_out - (1 - eff) H_in - eff H_s
            For j = 0 To c + 1
                add(c + 1, inlet + j, -(1.0 - eff) * hin(j) / te)
            Next
            ' H_s = H(T_s, P2, n) with S(T_s, P2, n) = S(T1, P1, n): T_s moves with the inlet and with P2 (the derivatives
            ' along the system's own isentropic path)
            Dim Ts = EOIsentropicTemperature(context, x, inlet, P2, n)
            Dim e1 = context.Flash(inlet, x(inlet), x(inlet + 1), n, True)
            Dim es = context.Flash(inlet, Ts, P2, n, True)
            If e1 Is Nothing OrElse es Is Nothing Then Throw New NotSupportedException(GraphicObject.Tag & ": no flow")
            Dim dHsdP2 = es.HdP - es.HdT * es.SdP / es.SdT
            add(c + 1, inlet, -eff * es.HdT * e1.SdT / es.SdT / te)
            add(c + 1, inlet + 1, -eff * (es.HdT * e1.SdP / es.SdT + dHsdP2 * dPin) / te)
            For i = 0 To c - 1
                add(c + 1, inlet + 2 + i, -eff * (es.Hdn(i) + es.HdT * (e1.Sdn(i) - es.Sdn(i)) / es.SdT) / te)
            Next
            If kP >= 0 Then add(c + 1, kP, -eff * dHsdP2 * dSpec / te)
            Dim kE = context.ParameterIndex(Me, "PROP_TU_1")
            If kE >= 0 Then
                Dim H1 = context.Enthalpy(inlet, x)
                add(c + 1, kE, (H1 - es.H) / 100.0 / te)
            End If
            Return True
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Dim c = context.CompoundCount
            Return If(k < c, "flow " & k, If(k = c, "pressure", "energy balance"))
        End Function

    End Class

End Namespace
