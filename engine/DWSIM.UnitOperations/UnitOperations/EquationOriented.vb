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

        ''' <summary>A saturated inlet keeps the equations: its equations read the inlets' flows, pressures, temperatures and enthalpies (the flash is of their mixture).</summary>
        Public Overrides ReadOnly Property EOTakesSaturatedInlets As Boolean
            Get
                Return EOHasEquations
            End Get
        End Property

        ''' <summary>The mode the vessel runs in: a set temperature or pressure turns it to the legacy mode, as Calculate does.</summary>
        Private ReadOnly Property EOMode As CalculationModes
            Get
                Return If(OverrideP OrElse OverrideT, CalculationModes.Legacy, CalculationMode)
            End Get
        End Property

        ''' <summary>The vessel's pressure is a variable of its own: the isothermal heating or cooling mode moves it to hold the temperature.</summary>
        Private ReadOnly Property EOFreePressure As Boolean
            Get
                Return EOMode = CalculationModes.HeatingCoolingIsothermic
            End Get
        End Property

        ''' <summary>
        ''' Equations of its own with a vapor and a liquid outlet: the two phases of the PT flash of the inlets' mixture, at a
        ''' common temperature and pressure. The pressure is the inlets' by the vessel's rule, or the one set; the temperature
        ''' comes from the energy balance with the heat the vessel takes in (its energy stream, its heating or cooling
        ''' amount, the heat its thermal profile gives), or is the one set, or, with only the pressure set, the inlets'
        ''' temperatures averaged by mass, as the vessel takes it. In the isothermal heating or cooling mode the temperature
        ''' is the inlets' mixture's at their pressure, and the pressure the one where the heat leaves it there. A second
        ''' liquid outlet (three phases), and a rigorous heat balance against the surroundings, take it as a black box.
        ''' </summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Dim g = GraphicObject
                If Not (g.OutputConnectors(0).IsAttached AndAlso g.OutputConnectors(1).IsAttached) Then Return False
                If g.OutputConnectors.Count > 2 AndAlso g.OutputConnectors(2).IsAttached Then Return False
                If CalculateRigorousHeatBalance Then
                    ' the heat given, added before the mode's own; the adiabatic mode reads its single inlet over it
                    If ThermalProperties.TipoPerfil <> Auxiliary.Pipe.ThermalEditorDefinitions.ThermalProfileType.Definir_Q Then Return False
                    If EOMode = CalculationModes.Adiabatic Then Return False
                End If
                Return True
            End Get
        End Property

        ''' <summary>The heat the vessel takes in (kW) besides its inlets', in the mode it runs in.</summary>
        Private Function EODuty() As Double
            Dim q = If(CalculateRigorousHeatBalance, ThermalProperties.Calor_trocado, 0.0)
            Dim es = GetInletEnergyStream(6), esout = GetOutletEnergyStream(4)
            Select Case EOMode
                Case CalculationModes.Legacy
                    Dim d = 0.0
                    If es IsNot Nothing Then d = es.EnergyFlow.GetValueOrDefault()
                    If esout IsNot Nothing Then d = -esout.EnergyFlow.GetValueOrDefault()
                    q += d
                Case CalculationModes.HeatingCoolingIsobaric, CalculationModes.HeatingCoolingIsothermic
                    If HeatingCoolingAmount.GetValueOrDefault() > 0 Then
                        q += HeatingCoolingAmount.GetValueOrDefault()
                    ElseIf es IsNot Nothing Then
                        q += es.EnergyFlow.GetValueOrDefault()
                    ElseIf HeatingCoolingAmount.GetValueOrDefault() < 0 Then
                        q += HeatingCoolingAmount.GetValueOrDefault()
                    End If
            End Select
            Return q
        End Function

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Dim list As New List(Of String)
            If OverrideT Then list.Add("PROP_SV_0")
            If OverrideP Then list.Add("PROP_SV_1")
            Return list.ToArray()
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

        ''' <summary>The vessel's pressure and the weights of the variables it moves with: the inlets' by the rule, the one set, or the vapor outlet's own.</summary>
        Private Function EOPressure(context As IEquationOrientedContext, x As Double(), inlets As Integer(), ByRef weights As Dictionary(Of Integer, Double)) As Double
            Dim vapor = EOOutletOffset(context, 0)
            weights = New Dictionary(Of Integer, Double)
            If EOFreePressure Then
                weights(vapor + 1) = 1.0
                Return x(vapor + 1)
            End If
            If OverrideP Then
                Dim k = context.ParameterIndex(Me, "PROP_SV_1")
                If k >= 0 Then weights(k) = 1.0
                Return EOParameterValue(context, x, "PROP_SV_1", FlashPressure)
            End If
            Dim rule = PressureCalculation.ToString()
            For Each w In InletPressureRule.Weights(context, x, inlets, rule)
                weights(w.Key + 1) = w.Value
            Next
            Return InletPressureRule.Pressure(context, x, inlets, rule)
        End Function

        ''' <summary>The inlets' temperatures averaged by their mass flows, as the vessel takes its temperature with only the pressure set.</summary>
        Private Function EOMassMeanTemperature(context As IEquationOrientedContext, x As Double(), inlets As Integer()) As Double
            Dim mw = context.MolecularWeights
            Dim c = context.CompoundCount
            Dim W = 0.0, WT = 0.0
            For Each o In inlets
                Dim wo = 0.0
                For i = 0 To c - 1
                    wo += x(o + 2 + i) * mw(i) / 1000.0
                Next
                W += wo
                WT += wo * x(o)
            Next
            Return If(W > 0.0, WT / W, 273.15)
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
            Dim weights As Dictionary(Of Integer, Double) = Nothing
            Dim P = EOPressure(context, x, inlets, weights)
            Dim T = x(vapor)
            Dim split = context.Flash(vapor, T, P, z, False)
            For i = 0 To c - 1
                r(row + i) = (x(vapor + 2 + i) - split.V(i)) / tf
                r(row + c + i) = (x(liquid + 2 + i) - split.L(i)) / tf
            Next
            Dim tt = context.TemperatureTolerance, tp = context.PressureTolerance
            r(row + 2 * c) = (x(liquid) - T) / tt
            Dim hin = 0.0
            For Each o In inlets
                hin += context.Enthalpy(o, x)
            Next
            If EOFreePressure Then
                ' the liquid at the vessel's pressure; that pressure where the heat leaves the mixture's temperature
                Dim rule = PressureCalculation.ToString()
                Dim Pin = InletPressureRule.Pressure(context, x, inlets, rule)
                r(row + 2 * c + 1) = (x(liquid + 1) - P) / tp
                r(row + 2 * c + 2) = (split.H - hin - EODuty()) / te
                Dim mix = context.Flash(vapor, T, Pin, z, False)
                r(row + 2 * c + 3) = (mix.H - hin - If(CalculateRigorousHeatBalance, ThermalProperties.Calor_trocado, 0.0)) / te
                Return
            End If
            r(row + 2 * c + 1) = (x(vapor + 1) - P) / tp
            r(row + 2 * c + 2) = (x(liquid + 1) - P) / tp
            If OverrideT Then
                r(row + 2 * c + 3) = (T - EOParameterValue(context, x, "PROP_SV_0", FlashTemperature)) / tt
            ElseIf OverrideP Then
                r(row + 2 * c + 3) = (T - EOMassMeanTemperature(context, x, inlets)) / tt
            Else
                r(row + 2 * c + 3) = (split.H - hin - EODuty()) / te
            End If
        End Sub

        Public Overrides Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean
            If Not EOHasEquations Then Return MyBase.EOJacobian(context, x, add)
            Dim c = context.CompoundCount
            Dim inlets = context.InletOffsets(Me)
            Dim vapor = EOOutletOffset(context, 0), liquid = EOOutletOffset(context, 1)
            Dim tf, te As Double
            Dim z = EOFeed(context, x, inlets, tf, te)
            Dim weights As Dictionary(Of Integer, Double) = Nothing
            Dim P = EOPressure(context, x, inlets, weights)
            Dim T = x(vapor)
            Dim e = context.Flash(vapor, T, P, z, True)
            If e Is Nothing Then Throw New NotSupportedException(GraphicObject.Tag & ": no feed")
            For i = 0 To c - 1
                ' vapor: n_v - V(T, P, z)
                add(i, vapor + 2 + i, 1.0 / tf)
                add(i, vapor, -e.VdT(i) / tf)
                For Each w In weights
                    add(i, w.Key, -e.VdP(i) * w.Value / tf)
                Next
                ' liquid: n_l - (z - V)
                add(c + i, liquid + 2 + i, 1.0 / tf)
                add(c + i, vapor, e.VdT(i) / tf)
                For Each w In weights
                    add(c + i, w.Key, e.VdP(i) * w.Value / tf)
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
            ' minus the inlets' enthalpies, into a row
            Dim addInlets = Sub(row As Integer)
                                For Each o In inlets
                                    If Enumerable.Range(0, c).Sum(Function(i) x(o + 2 + i)) <= 0.0 Then Continue For
                                    Dim hin = context.EnthalpyDerivatives(o, x)
                                    For j = 0 To c + 1
                                        add(row, o + j, -hin(j) / te)
                                    Next
                                Next
                            End Sub
            ' H(T, P, z) of a flash with its pressure's weights, into a row
            Dim addFlash = Sub(row As Integer, f As IEquationOrientedFlash, pw As IEnumerable(Of KeyValuePair(Of Integer, Double)))
                               add(row, vapor, f.HdT / te)
                               For Each w In pw
                                   add(row, w.Key, f.HdP * w.Value / te)
                               Next
                               For Each o In inlets
                                   For j = 0 To c - 1
                                       add(row, o + 2 + j, f.Hdn(j) / te)
                                   Next
                               Next
                           End Sub
            If EOFreePressure Then
                add(2 * c + 1, liquid + 1, 1.0 / tp)
                add(2 * c + 1, vapor + 1, -1.0 / tp)
                addFlash(2 * c + 2, e, weights)
                addInlets(2 * c + 2)
                Dim rule = PressureCalculation.ToString()
                Dim Pin = InletPressureRule.Pressure(context, x, inlets, rule)
                Dim mix = context.Flash(vapor, T, Pin, z, True)
                If mix Is Nothing Then Throw New NotSupportedException(GraphicObject.Tag & ": no feed")
                addFlash(2 * c + 3, mix, InletPressureRule.Weights(context, x, inlets, rule).Select(Function(w) New KeyValuePair(Of Integer, Double)(w.Key + 1, w.Value)))
                addInlets(2 * c + 3)
                Return True
            End If
            add(2 * c + 1, vapor + 1, 1.0 / tp)
            add(2 * c + 2, liquid + 1, 1.0 / tp)
            For Each w In weights
                add(2 * c + 1, w.Key, -w.Value / tp)
                add(2 * c + 2, w.Key, -w.Value / tp)
            Next
            Dim k = 2 * c + 3
            If OverrideT Then
                add(k, vapor, 1.0 / tt)
                Dim kT = context.ParameterIndex(Me, "PROP_SV_0")
                If kT >= 0 Then add(k, kT, -1.0 / tt)
            ElseIf OverrideP Then
                ' T less the inlets' temperatures averaged by mass
                add(k, vapor, 1.0 / tt)
                Dim mw = context.MolecularWeights
                Dim W = 0.0
                For Each o In inlets
                    For i = 0 To c - 1
                        W += x(o + 2 + i) * mw(i) / 1000.0
                    Next
                Next
                If W > 0.0 Then
                    Dim Tm = EOMassMeanTemperature(context, x, inlets)
                    For Each o In inlets
                        Dim wo = 0.0
                        For i = 0 To c - 1
                            wo += x(o + 2 + i) * mw(i) / 1000.0
                        Next
                        add(k, o, -wo / W / tt)
                        For i = 0 To c - 1
                            add(k, o + 2 + i, -mw(i) / 1000.0 * (x(o) - Tm) / W / tt)
                        Next
                    Next
                End If
            Else
                addFlash(k, e, weights)
                addInlets(k)
            End If
            Return True
        End Function

        ' every row but the liquid temperature reads the block's pressure, which ties the inlets' by the rule
        Public Overrides Function EOIsSmooth(context As IEquationOrientedContext, x As Double(), k As Integer) As Boolean
            If Not EOHasEquations Then Return MyBase.EOIsSmooth(context, x, k)
            If OverrideP Then Return True
            If EOFreePressure Then Return k <> 2 * context.CompoundCount + 3 OrElse Not InletPressureRule.Tied(context, x, context.InletOffsets(Me), PressureCalculation.ToString())
            Return k = 2 * context.CompoundCount OrElse Not InletPressureRule.Tied(context, x, context.InletOffsets(Me), PressureCalculation.ToString())
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Dim c = context.CompoundCount
            If k < c Then Return "vapor flow " & k
            If k < 2 * c Then Return "liquid flow " & (k - c)
            If EOFreePressure Then
                Select Case k - 2 * c
                    Case 0 : Return "liquid temperature"
                    Case 1 : Return "liquid pressure"
                    Case 2 : Return "energy balance at the vessel's pressure"
                    Case Else : Return "temperature of the inlets' mixture"
                End Select
            End If
            Select Case k - 2 * c
                Case 0 : Return "liquid temperature"
                Case 1 : Return "vapor pressure"
                Case 2 : Return "liquid pressure"
                Case Else : Return If(OverrideT, "set temperature", If(OverrideP, "inlets' temperature by mass", "energy balance"))
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
        ''' temperature, as the unit decides. The area mode is the mode of its set outlet temperature, its area a result. At a
        ''' thermal efficiency (counter-current) the cold side takes that share of the largest heat the exchanger can move:
        ''' the lesser of the cold side's at the hot inlet's temperature and the hot side's at the cold inlet's, each at its
        ''' outlet pressure (co-current: where the outlets meet, at the inlet pressures). At a pinch the smallest difference between the two sides' temperature profiles, 26 points each
        ''' by PH flashes along the heat as the unit makes them, is the minimum approach. In co-current the largest heat is
        ''' where the two outlets meet at one temperature. In shell and tube rating each side's outlet pressure is its inlet's
        ''' less the pressure drop of the side it flows in, and the heat load is U A F LMTD (at most the largest heat), U and
        ''' the drops from the unit's correlations at each side's properties at its mean temperature, differenced; the
        ''' fouling factor mode sets both outlet temperatures, its pressures as the rating's.
        ''' </summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Select Case CalcMode
                    Case HeatExchangerCalcMode.CalcTempHotOut, HeatExchangerCalcMode.CalcTempColdOut,
                         HeatExchangerCalcMode.CalcBothTemp, HeatExchangerCalcMode.CalcBothTemp_UA,
                         HeatExchangerCalcMode.OutletVaporFraction1, HeatExchangerCalcMode.OutletVaporFraction2,
                         HeatExchangerCalcMode.CalcArea, HeatExchangerCalcMode.PinchPoint, HeatExchangerCalcMode.ThermalEfficiency,
                         HeatExchangerCalcMode.ShellandTube_Rating, HeatExchangerCalcMode.ShellandTube_CalcFoulingFactor
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

        ''' <summary>The mode the equations write: the area mode is that of the outlet temperature it sets.</summary>
        Private ReadOnly Property EOCalcMode As HeatExchangerCalcMode
            Get
                If CalcMode <> HeatExchangerCalcMode.CalcArea Then Return CalcMode
                Return If(DefinedTemperature = SpecifiedTemperature.Cold_Fluid, HeatExchangerCalcMode.CalcTempHotOut, HeatExchangerCalcMode.CalcTempColdOut)
            End Get
        End Property

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            If CalcMode = HeatExchangerCalcMode.ShellandTube_Rating Then Return {"PROP_HX_28"}
            If CalcMode = HeatExchangerCalcMode.ShellandTube_CalcFoulingFactor Then Return {"PROP_HX_3", "PROP_HX_4"}
            Dim common = {"PROP_HX_28", "PROP_HX_32", "PROP_HX_33"}
            Select Case EOCalcMode
                Case HeatExchangerCalcMode.ThermalEfficiency : Return common.Concat({"PROP_HX_25"}).ToArray()
                Case HeatExchangerCalcMode.PinchPoint : Return common.Concat({"PROP_HX_27"}).ToArray()
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

        Private Shared Function EOSideFlow(x As Double(), o As Integer, c As Integer) As Double
            Dim s = 0.0
            For i = 0 To c - 1
                s += x(o + 2 + i)
            Next
            Return s
        End Function

        ''' <summary>
        ''' The largest heat (kW) a counter-current exchanger can move, as the unit takes it: the lesser of the cold side's
        ''' enthalpy rise to the hot inlet's temperature and the hot side's fall to the cold inlet's, each at its outlet
        ''' pressure; and its derivatives by variable.
        ''' </summary>
        Private Function EOMaxHeat(context As IEquationOrientedContext, x As Double(), ci As Integer, co As Integer, hi As Integer, ho As Integer, ByRef d As Dictionary(Of Integer, Double)) As Double
            If FlowDir = FlowDirection.CoCurrent Then Return EOMaxHeatCoCurrent(context, x, ci, hi, d)
            Dim c = context.CompoundCount
            Dim nc = Enumerable.Range(0, c).Select(Function(i) x(ci + 2 + i)).ToArray()
            Dim nh = Enumerable.Range(0, c).Select(Function(i) x(hi + 2 + i)).ToArray()
            Dim fc = context.Flash(ci, x(hi), x(co + 1), nc, True)
            Dim fh = context.Flash(hi, x(ci), x(ho + 1), nh, True)
            Dim qc = fc.H - context.Enthalpy(ci, x)
            Dim qh = context.Enthalpy(hi, x) - fh.H
            Dim dd As New Dictionary(Of Integer, Double)
            d = dd
            Dim put = Sub(m As Integer, v As Double)
                          If dd.ContainsKey(m) Then dd(m) += v Else dd(m) = v
                      End Sub
            If qc <= qh Then
                put(hi, fc.HdT)
                put(co + 1, fc.HdP)
                For i = 0 To c - 1
                    put(ci + 2 + i, fc.Hdn(i))
                Next
                Dim h1 = context.EnthalpyDerivatives(ci, x)
                For j = 0 To c + 1
                    put(ci + j, -h1(j))
                Next
                Return qc
            End If
            Dim h2 = context.EnthalpyDerivatives(hi, x)
            For j = 0 To c + 1
                put(hi + j, h2(j))
            Next
            put(ci, -fh.HdT)
            put(ho + 1, -fh.HdP)
            For i = 0 To c - 1
                put(hi + 2 + i, -fh.Hdn(i))
            Next
            Return qh
        End Function

        Private ReadOnly Property EOShellAndTube As Boolean
            Get
                Return CalcMode = HeatExchangerCalcMode.ShellandTube_Rating OrElse CalcMode = HeatExchangerCalcMode.ShellandTube_CalcFoulingFactor
            End Get
        End Property

        ''' <summary>The density, heat capacity, conductivity and viscosity of a side's inlet fluid at the temperature Tm, as the rating takes them, and its mass flow.</summary>
        Private Function EOMeanProperties(context As IEquationOrientedContext, x As Double(), inlet As Integer, Tm As Double) As Double()
            Dim name = Me.Name & " mean properties " & Tm.ToString("R", Globalization.CultureInfo.InvariantCulture)
            Return context.StreamProperties(inlet, x, name,
                Function(ms)
                    Dim pr = STMeanProperties(DirectCast(ms, DWSIM.Thermodynamics.Streams.MaterialStream), Tm, Nothing)
                    Return {pr(0), pr(1), pr(2), pr(3), ms.GetMassFlow()}
                End Function)
        End Function

        ''' <summary>
        ''' The shell and tube rating at x, as one pass of the unit's iteration makes it: the heat load U A F LMTD (kW) and
        ''' the cold and hot sides' pressure drops (Pa), from each side's properties at its mean temperature.
        ''' </summary>
        Private Function EOShellTube(context As IEquationOrientedContext, x As Double(), ci As Integer, co As Integer, hi As Integer, ho As Integer) As Double()
            Dim Tc1 = x(ci), Tc2 = x(co), Th1 = x(hi), Th2 = x(ho)
            Dim pc = EOMeanProperties(context, x, ci, (Tc1 + Tc2) / 2)
            Dim ph = EOMeanProperties(context, x, hi, (Th1 + Th2) / 2)
            Dim terms = STCorrelations(pc(0), pc(1), pc(2), pc(3), ph(0), ph(1), ph(2), ph(3), pc(4), ph(4), Nothing)
            Dim R, P As Double
            Dim F = STCorrectionFactor(Tc1, Tc2, Th1, Th2, 1.0, R, P)
            Dim a, b As Double
            If FlowDir = FlowDirection.CoCurrent Then
                a = Th1 - Tc1 : b = Th2 - Tc2
            Else
                a = Th1 - Tc2 : b = Th2 - Tc1
            End If
            Dim lm = If(Math.Abs(a - b) < 0.000000001 * Math.Max(Math.Abs(a), 1.0), a, (a - b) / Math.Log(a / b)) * CorrectionFactorLMTD
            Dim U = 1.0 / (terms.f1 + terms.f2 + terms.f3 + terms.f4 + terms.f5)
            Dim shellCold = STProperties.Shell_Fluid = 0
            Return {U * terms.A * F * lm / 1000.0, If(shellCold, terms.dps, terms.dpt), If(shellCold, terms.dpt, terms.dps)}
        End Function

        ''' <summary>
        ''' The largest heat (kW) a co-current exchanger can move, as the unit takes it: the heat at which the two outlets
        ''' meet at one temperature T*, each side at its inlet pressure. T* is the root of what the hot side gives less what
        ''' the cold side takes, both by PT flashes; the derivatives come through T* by the implicit function theorem.
        ''' </summary>
        Private Function EOMaxHeatCoCurrent(context As IEquationOrientedContext, x As Double(), ci As Integer, hi As Integer, ByRef d As Dictionary(Of Integer, Double)) As Double
            Dim c = context.CompoundCount
            Dim nc = Enumerable.Range(0, c).Select(Function(i) x(ci + 2 + i)).ToArray()
            Dim nh = Enumerable.Range(0, c).Select(Function(i) x(hi + 2 + i)).ToArray()
            Dim Pc = x(ci + 1), Ph = x(hi + 1)
            Dim Hc1 = context.Enthalpy(ci, x), Hh1 = context.Enthalpy(hi, x)
            Dim g = Function(T As Double) (Hh1 - context.Flash(hi, T, Ph, nh, False).H) - (context.Flash(ci, T, Pc, nc, False).H - Hc1)
            Dim Ts = MathNet.Numerics.RootFinding.Brent.FindRoot(g, x(ci), x(hi), 0.000001, 200)
            Dim fc = context.Flash(ci, Ts, Pc, nc, True)
            Dim fh = context.Flash(hi, Ts, Ph, nh, True)
            Dim dd As New Dictionary(Of Integer, Double)
            d = dd
            Dim put = Sub(m As Integer, v As Double)
                          If dd.ContainsKey(m) Then dd(m) += v Else dd(m) = v
                      End Sub
            ' g's derivatives at T* by variable, and T*'s
            Dim dg As New Dictionary(Of Integer, Double)
            Dim putg = Sub(m As Integer, v As Double)
                           If dg.ContainsKey(m) Then dg(m) += v Else dg(m) = v
                       End Sub
            Dim dHh1 = context.EnthalpyDerivatives(hi, x), dHc1 = context.EnthalpyDerivatives(ci, x)
            For j = 0 To c + 1
                putg(hi + j, dHh1(j))
                putg(ci + j, dHc1(j))
            Next
            putg(hi + 1, -fh.HdP)
            putg(ci + 1, -fc.HdP)
            For i = 0 To c - 1
                putg(hi + 2 + i, -fh.Hdn(i))
                putg(ci + 2 + i, -fc.Hdn(i))
            Next
            Dim dgdT = -(fh.HdT + fc.HdT)
            ' Q = Hc(T*) - Hc1
            For j = 0 To c + 1
                put(ci + j, -dHc1(j))
            Next
            put(ci + 1, fc.HdP)
            For i = 0 To c - 1
                put(ci + 2 + i, fc.Hdn(i))
            Next
            If dgdT <> 0.0 Then
                For Each kv In dg
                    put(kv.Key, -fc.HdT * kv.Value / dgdT)
                Next
            End If
            Return fc.H - Hc1
        End Function

        ''' <summary>
        ''' The smallest temperature difference between the two sides along the heat, as the unit's pinch mode takes it:
        ''' each side's temperature at 26 points of its enthalpy and pressure from inlet to outlet (PH flashes of the side's
        ''' inlet), the hot side's reversed in counter-current unless the pinch is taken at the outlets.
        ''' </summary>
        Private Function EOPinch(context As IEquationOrientedContext, x As Double(), ci As Integer, co As Integer, hi As Integer, ho As Integer) As Double
            Const steps As Integer = 25
            Dim q = context.Enthalpy(co, x) - context.Enthalpy(ci, x)
            Dim tc = EOProfile(context, x, ci, q, x(ci + 1) - x(co + 1), steps)
            Dim th = EOProfile(context, x, hi, -q, x(hi + 1) - x(ho + 1), steps)
            If Not PinchPointAtOutlets AndAlso FlowDir = FlowDirection.CounterCurrent Then th = th.Reverse().ToArray()
            Dim dmin = Double.MaxValue
            For i = 0 To steps
                dmin = Math.Min(dmin, Math.Abs(th(i) - tc(i)))
            Next
            Return dmin
        End Function

        ''' <summary>A side's temperatures at steps + 1 points as its inlet takes the heat q (kW) and loses the pressure dp, by PH flashes of the inlet.</summary>
        Private Function EOProfile(context As IEquationOrientedContext, x As Double(), inlet As Integer, q As Double, dp As Double, steps As Integer) As Double()
            Dim inv = Globalization.CultureInfo.InvariantCulture
            Dim name = Me.Name & " profile " & q.ToString("R", inv) & " " & dp.ToString("R", inv)
            Return context.StreamProperties(inlet, x, name,
                Function(ms)
                    Dim pp = ms.PropertyPackage
                    Dim W = ms.GetMassFlow()
                    Dim H1 = ms.GetMassEnthalpy(), P1 = ms.GetPressure()
                    Dim T = ms.GetTemperature()
                    Dim res(steps) As Double
                    For i = 0 To steps
                        Dim f = CDbl(i) / steps
                        pp.CurrentMaterialStream = ms
                        Dim fl = pp.CalculateEquilibrium2(Enums.FlashCalculationType.PressureEnthalpy, P1 - f * dp, H1 + f * q / W, T)
                        Dim tv As Object = fl.CalculatedTemperature
                        res(i) = If(tv Is Nothing, Double.NaN, CDbl(tv))
                        T = res(i)
                    Next
                    Return res
                End Function)
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
            Dim stq As Double() = Nothing
            If EOShellAndTube Then
                ' the drop of the side each fluid flows in
                stq = EOShellTube(context, x, ci, co, hi, ho)
                r(k) = (x(co + 1) - (x(ci + 1) - stq(1))) / tp : k += 1
                r(k) = (x(ho + 1) - (x(hi + 1) - stq(2))) / tp : k += 1
            Else
                r(k) = (x(co + 1) - (x(ci + 1) - EOParameterValue(context, x, "PROP_HX_32", ColdSidePressureDrop))) / tp : k += 1
                r(k) = (x(ho + 1) - (x(hi + 1) - EOParameterValue(context, x, "PROP_HX_33", HotSidePressureDrop))) / tp : k += 1
            End If
            Dim te = context.EnergyTolerance(co) + context.EnergyTolerance(ho)
            Dim dHc = context.Enthalpy(co, x) - context.Enthalpy(ci, x)
            Dim dHh = context.Enthalpy(ho, x) - context.Enthalpy(hi, x)
            Dim loss = EOParameterValue(context, x, "PROP_HX_28", HeatLoss)
            Select Case EOCalcMode
                Case HeatExchangerCalcMode.ShellandTube_Rating
                    ' the load at most the largest heat, as the unit caps it; the heat loss on the shell side's fluid
                    Dim dqx As Dictionary(Of Integer, Double) = Nothing
                    Dim q = Math.Min(stq(0), EOMaxHeat(context, x, ci, co, hi, ho, dqx))
                    r(k) = (dHc - (q - If(STProperties.Shell_Fluid = 0, loss, 0.0))) / te
                    r(k + 1) = (dHc + dHh + loss) / te
                Case HeatExchangerCalcMode.ShellandTube_CalcFoulingFactor
                    r(k) = (x(co) - EOParameterValue(context, x, "PROP_HX_3", TempColdOut.GetValueOrDefault())) / context.TemperatureTolerance
                    r(k + 1) = (x(ho) - EOParameterValue(context, x, "PROP_HX_4", TempHotOut.GetValueOrDefault())) / context.TemperatureTolerance
                Case HeatExchangerCalcMode.ThermalEfficiency
                    Dim eff = EOParameterValue(context, x, "PROP_HX_25", ThermalEfficiency) / 100.0
                    Dim dqx As Dictionary(Of Integer, Double) = Nothing
                    r(k) = (dHc - eff * EOMaxHeat(context, x, ci, co, hi, ho, dqx)) / te
                    r(k + 1) = (dHc + dHh + loss) / te
                Case HeatExchangerCalcMode.PinchPoint
                    r(k) = (EOPinch(context, x, ci, co, hi, ho) - EOParameterValue(context, x, "PROP_HX_27", MITA)) / context.TemperatureTolerance
                    r(k + 1) = (dHc + dHh + loss) / te
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
            Dim stq As Double() = Nothing
            Dim dst As New Dictionary(Of Integer, Double())
            If EOShellAndTube Then
                ' the rating's load and drops by central differences on the inlets' states and the outlets' temperatures
                stq = EOShellTube(context, x, ci, co, hi, ho)
                Dim xt = CType(x.Clone(), Double())
                Dim cols As New List(Of Integer)
                For j = 0 To c + 1
                    cols.Add(ci + j) : cols.Add(hi + j)
                Next
                cols.Add(co) : cols.Add(ho)
                For Each m In cols
                    Dim o = If(m >= hi AndAlso m <= hi + c + 1, hi, ci)
                    Dim j = m - o
                    If m = co OrElse m = ho Then j = 0
                    Dim h = If(j = 0, 0.01, If(j = 1, 0.0001 * Math.Max(x(m), 10000.0), 0.0001 * Math.Max(Math.Abs(x(m)), 0.000001 * EOSideFlow(x, o, c))))
                    Dim hb = If(j < 2, h, Math.Min(h, Math.Max(x(m), 0.0)))
                    xt(m) = x(m) + h
                    Dim up = EOShellTube(context, xt, ci, co, hi, ho)
                    xt(m) = x(m) - hb
                    Dim dn = EOShellTube(context, xt, ci, co, hi, ho)
                    xt(m) = x(m)
                    dst(m) = {(up(0) - dn(0)) / (h + hb), (up(1) - dn(1)) / (h + hb), (up(2) - dn(2)) / (h + hb)}
                Next
                add(k, co + 1, 1.0 / tp) : add(k, ci + 1, -1.0 / tp)
                For Each kv In dst
                    If kv.Value(1) <> 0.0 Then add(k, kv.Key, kv.Value(1) / tp)
                Next
                k += 1
                add(k, ho + 1, 1.0 / tp) : add(k, hi + 1, -1.0 / tp)
                For Each kv In dst
                    If kv.Value(2) <> 0.0 Then add(k, kv.Key, kv.Value(2) / tp)
                Next
                k += 1
            Else
                add(k, co + 1, 1.0 / tp) : add(k, ci + 1, -1.0 / tp)
                Dim kp = context.ParameterIndex(Me, "PROP_HX_32")
                If kp >= 0 Then add(k, kp, 1.0 / tp)
                k += 1
                add(k, ho + 1, 1.0 / tp) : add(k, hi + 1, -1.0 / tp)
                kp = context.ParameterIndex(Me, "PROP_HX_33")
                If kp >= 0 Then add(k, kp, 1.0 / tp)
                k += 1
            End If
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
            Select Case EOCalcMode
                Case HeatExchangerCalcMode.ShellandTube_Rating
                    Dim dq As Dictionary(Of Integer, Double) = Nothing
                    Dim qmax = EOMaxHeat(context, x, ci, co, hi, ho, dq)
                    addCold(k, 1.0)
                    If stq(0) <= qmax Then
                        For Each kv In dst
                            If kv.Value(0) <> 0.0 Then add(k, kv.Key, -kv.Value(0) / te)
                        Next
                    Else
                        For Each kv In dq
                            add(k, kv.Key, -kv.Value / te)
                        Next
                    End If
                    If kl >= 0 AndAlso STProperties.Shell_Fluid = 0 Then add(k, kl, 1.0 / te)
                    addCold(k + 1, 1.0) : addHot(k + 1, 1.0)
                Case HeatExchangerCalcMode.ShellandTube_CalcFoulingFactor
                    Dim tt = context.TemperatureTolerance
                    add(k, co, 1.0 / tt)
                    add(k + 1, ho, 1.0 / tt)
                    Dim k3 = context.ParameterIndex(Me, "PROP_HX_3"), k4 = context.ParameterIndex(Me, "PROP_HX_4")
                    If k3 >= 0 Then add(k, k3, -1.0 / tt)
                    If k4 >= 0 Then add(k + 1, k4, -1.0 / tt)
                    Return True
                Case HeatExchangerCalcMode.ThermalEfficiency
                    Dim eff = EOParameterValue(context, x, "PROP_HX_25", ThermalEfficiency) / 100.0
                    Dim dq As Dictionary(Of Integer, Double) = Nothing
                    Dim qmax = EOMaxHeat(context, x, ci, co, hi, ho, dq)
                    addCold(k, 1.0)
                    For Each kv In dq
                        add(k, kv.Key, -eff * kv.Value / te)
                    Next
                    Dim ke = context.ParameterIndex(Me, "PROP_HX_25")
                    If ke >= 0 Then add(k, ke, -qmax / 100.0 / te)
                    addCold(k + 1, 1.0) : addHot(k + 1, 1.0)
                Case HeatExchangerCalcMode.PinchPoint
                    ' the pinch by central differences on the four streams' variables
                    Dim tt = context.TemperatureTolerance
                    Dim xt = CType(x.Clone(), Double())
                    For Each o In {ci, co, hi, ho}
                        For j = 0 To c + 1
                            Dim m = o + j
                            Dim h = If(j = 0, 0.05, If(j = 1, 0.001 * Math.Max(x(m), 10000.0), 0.0001 * Math.Max(Math.Abs(x(m)), 0.000001 * EOSideFlow(x, o, c))))
                            Dim hb = If(j < 2, h, Math.Min(h, Math.Max(x(m), 0.0)))
                            xt(m) = x(m) + h
                            Dim up = EOPinch(context, xt, ci, co, hi, ho)
                            xt(m) = x(m) - hb
                            Dim down = EOPinch(context, xt, ci, co, hi, ho)
                            xt(m) = x(m)
                            Dim dv = (up - down) / (h + hb)
                            If dv <> 0.0 AndAlso Not Double.IsNaN(dv) Then add(k, m, dv / tt)
                        Next
                    Next
                    Dim km = context.ParameterIndex(Me, "PROP_HX_27")
                    If km >= 0 Then add(k, km, -1.0 / tt)
                    addCold(k + 1, 1.0) : addHot(k + 1, 1.0)
                Case HeatExchangerCalcMode.CalcTempHotOut, HeatExchangerCalcMode.CalcTempColdOut
                    Dim tt = context.TemperatureTolerance
                    Dim code = If(EOCalcMode = HeatExchangerCalcMode.CalcTempHotOut, "PROP_HX_3", "PROP_HX_4")
                    add(k, If(EOCalcMode = HeatExchangerCalcMode.CalcTempHotOut, co, ho), 1.0 / tt)
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
                Case 0 : Return If(EOShellAndTube, "cold side pressure at its rated drop", "cold side pressure")
                Case 1 : Return If(EOShellAndTube, "hot side pressure at its rated drop", "hot side pressure")
                Case 2
                    Select Case EOCalcMode
                        Case HeatExchangerCalcMode.ShellandTube_Rating : Return "heat load = U A F LMTD of the rating"
                        Case HeatExchangerCalcMode.ShellandTube_CalcFoulingFactor : Return "cold outlet temperature"

                        Case HeatExchangerCalcMode.ThermalEfficiency : Return "heat load at the thermal efficiency"
                        Case HeatExchangerCalcMode.PinchPoint : Return "minimum internal temperature approach"
                        Case HeatExchangerCalcMode.CalcTempHotOut : Return "cold outlet temperature"
                        Case HeatExchangerCalcMode.CalcTempColdOut : Return "hot outlet temperature"
                        Case HeatExchangerCalcMode.CalcBothTemp : Return "heat the cold side takes"
                        Case HeatExchangerCalcMode.OutletVaporFraction1, HeatExchangerCalcMode.OutletVaporFraction2
                            Return "outlet temperature at its vapor fraction"
                        Case Else : Return "heat load = U A LMTD"
                    End Select
                Case Else
                    If CalcMode = HeatExchangerCalcMode.ShellandTube_CalcFoulingFactor Then Return "hot outlet temperature"
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

    Partial Public Class Pipe

        ''' <summary>
        ''' Equations of its own when the length is given, at equilibrium, with the heat given or an overall coefficient
        ''' against the surroundings (or no heat balance): one cell per increment of the hydraulic profile, whose ends are
        ''' internal states of the system (T, P and the compound flows), the last one the outlet. Each cell carries the
        ''' compounds through, drops the pressure by the mean of its own correlation's drop at its two ends (the trapezoidal
        ''' rule; the pipe takes the drop at the increment's inlet), or by a gas pipeline equation's drop from its inlet, which
        ''' is the increment's already, and holds the energy balance from the inlet to its end
        ''' with the heat of the cells up to it, U A times the log mean of the differences to the surroundings, or the heat
        ''' spread evenly. The drops come from the state put on a stream; their derivatives from central differences on it.
        ''' </summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Try
                    Dim g = GraphicObject
                    If Not (g.InputConnectors(0).IsAttached AndAlso g.OutputConnectors(0).IsAttached) Then Return False
                    If Specification <> Specmode.Length Then Return False
                    If Profile Is Nothing OrElse Profile.Status <> Auxiliary.Pipe.PipeEditorStatus.OK OrElse Profile.Sections.Count = 0 Then Return False
                    If Not CalculateEquilibrium Then Return False
                    If CalculateHeatBalance Then
                        Select Case ThermalProfile.TipoPerfil
                            Case Auxiliary.Pipe.ThermalEditorDefinitions.ThermalProfileType.Definir_Q
                            Case Auxiliary.Pipe.ThermalEditorDefinitions.ThermalProfileType.Definir_CGTC
                                If ThermalProfile.IncludeSolarRadiation Then Return False
                            Case Else
                                Return False
                        End Select
                    End If
                    Dim n = EOPipeCells().Count
                    Return n >= 1 AndAlso n <= 200
                Catch ex As Exception
                    Return False
                End Try
            End Get
        End Property

        ''' <summary>One increment of the hydraulic profile: its section, length, elevation and where it starts along the pipe.</summary>
        Private Structure EOPipeCell
            Public Section As Auxiliary.Pipe.PipeSection
            Public Length, Elevation, Start As Double
        End Structure

        ''' <summary>The increments in the order the pipe takes them; a fitting is 0.1 m long and level, as the pipe takes it.</summary>
        Private Function EOPipeCells() As List(Of EOPipeCell)
            Dim list As New List(Of EOPipeCell)
            Dim pos = 0.0
            For Each sec In Profile.Sections.Values
                Dim straight = IsStraightSection(sec)
                Dim len = If(straight, sec.Comprimento / sec.Incrementos, 0.1)
                Dim el = If(straight, sec.Elevacao / sec.Incrementos, 0.0)
                For q = 1 To sec.Quantidade
                    For j = 1 To sec.Incrementos
                        list.Add(New EOPipeCell With {.Section = sec, .Length = len, .Elevation = el, .Start = pos})
                        pos += If(straight, sec.Comprimento, 0.1) / sec.Incrementos
                    Next
                Next
            Next
            Return list
        End Function

        Private Function EOPipeName(sec As Auxiliary.Pipe.PipeSection) As String
            Return "Pipe drop " & Name & " " & sec.Indice
        End Function

        ''' <summary>The pressure drop (Pa) of one increment of this section at the state of a stream, as Calculate works it out.</summary>
        Private Function EOPipeDrop(ms As IMaterialStream, sec As Auxiliary.Pipe.PipeSection, len As Double, el As Double) As Double
            Dim oms = CType(ms, Thermodynamics.Streams.MaterialStream)
            Dim Qlin = oms.OverallLiquid.Properties.volumetric_flow.GetValueOrDefault
            Dim Qsin = oms.Solid.Properties.volumetric_flow.GetValueOrDefault
            Dim rho_l = oms.OverallLiquid.Properties.density.GetValueOrDefault
            If Double.IsNaN(rho_l) Then rho_l = 0.0
            Dim eta_l As Double
            If IncludeEmulsion AndAlso oms.Liquid1.Properties.volumetric_flow.GetValueOrDefault > 0.0 AndAlso oms.Liquid2.Properties.volumetric_flow.GetValueOrDefault > 0.0 Then
                eta_l = EmulsionViscosity(oms)
            Else
                eta_l = oms.OverallLiquid.Properties.viscosity.GetValueOrDefault
            End If
            If SlurryViscosityMode = 1 AndAlso Qlin > 0.0 Then
                Dim phi = Qsin / Qlin
                eta_l *= 1.0 + 3.0 * phi / (1.0 - phi / 0.52)
            End If
            Dim tens = oms.Phases(0).Properties.surfaceTension.GetValueOrDefault
            If Double.IsNaN(tens) Then tens = 0.0
            Dim Qvin = oms.Phases(2).Properties.volumetric_flow.GetValueOrDefault
            Dim rho_v = oms.Phases(2).Properties.density.GetValueOrDefault
            Dim eta_v = oms.Phases(2).Properties.viscosity.GetValueOrDefault
            Dim w_v = oms.Phases(2).Properties.massflow.GetValueOrDefault
            Dim z = oms.Phases(2).Properties.compressibilityFactor.GetValueOrDefault
            If Qvin + Qlin = 0.0 Then Return 0.0
            Dim fpp As FlowPackages.FPBaseClass
            Select Case SelectedFlowPackage
                Case FlowPackage.Lockhart_Martinelli : fpp = New FlowPackages.LockhartMartinelli
                Case FlowPackage.Petalas_Aziz : fpp = New FlowPackages.PetalasAziz
                Case Else : fpp = New FlowPackages.BeggsBrill
            End Select
            Dim D = sec.DI * 0.0254
            Dim T = oms.Phases(0).Properties.temperature.GetValueOrDefault
            Dim P = oms.Phases(0).Properties.pressure.GetValueOrDefault
            Dim MWv = oms.Phases(2).Properties.molecularWeight.GetValueOrDefault
            Dim resv As Object()
            If IsStraightSection(sec) Then
                If IsGasPipelineEquation() Then
                    resv = GasPipelineDeltaP(D, len, el, w_v, MWv, T, z, rho_v, P, PipelineEfficiency)
                Else
                    resv = fpp.CalculateDeltaP(D, len, el, GetRugosity(sec.Material, sec), Qvin * 24 * 3600, Qlin * 24 * 3600, eta_v * 1000, eta_l * 1000, rho_v, rho_l, tens)
                End If
            ElseIf sec.TipoSegmento.Contains("[27]") Then
                ' a fitting with its drop set, in Pa
                Return sec.DI
            Else
                Dim resf = Kfit(sec.TipoSegmento)
                If resf(1) = 1.0 Then
                    Dim L_eq = resf(0) * 0.0254 * sec.DI
                    If IsGasPipelineEquation() Then
                        resv = GasPipelineDeltaP(D, L_eq, 0, w_v, MWv, T, z, rho_v, P, PipelineEfficiency)
                    Else
                        resv = fpp.CalculateDeltaP(D, L_eq, 0, GetRugosity(sec.Material, sec), Qvin * 24 * 3600, Qlin * 24 * 3600, eta_v * 1000, eta_l * 1000, rho_v, rho_l, tens)
                    End If
                Else
                    Dim area = D ^ 2 * Math.PI / 4
                    Dim Ql = Qlin + Qsin
                    Dim rho_mix = Ql / (Qvin + Ql) * rho_l + Qvin / (Qvin + Ql) * rho_v
                    Return resf(0) * rho_mix * (Ql / area + Qvin / area) ^ 2 / 2
                End If
            End If
            Return CDbl(resv(4))
        End Function

        ''' <summary>The heat (kW) cell k takes in, from its two ends' temperatures.</summary>
        Private Function EOPipeHeat(cell As EOPipeCell, cells As Integer, Tin As Double, Tout As Double) As Double
            If Not CalculateHeatBalance Then Return 0.0
            If ThermalProfile.TipoPerfil = Auxiliary.Pipe.ThermalEditorDefinitions.ThermalProfileType.Definir_Q Then Return ThermalProfile.Calor_trocado / cells
            Dim Text, dText_dL, U As Double
            If ThermalProfile.UseUserDefinedU Then
                Text = MathNet.Numerics.Interpolate.Linear(ThermalProfile.UserDefinedU_Length, ThermalProfile.UserDefinedU_Temp).Interpolate(cell.Start)
                dText_dL = 0.0
                U = MathNet.Numerics.Interpolate.Step(ThermalProfile.UserDefinedU_Length, ThermalProfile.UserDefinedU_U).Interpolate(cell.Start)
            Else
                Text = ThermalProfile.Temp_amb_definir
                dText_dL = ThermalProfile.AmbientTemperatureGradient
                U = ThermalProfile.CGTC_Definido
            End If
            Dim Tamb = If(UseGlobalWeather, FlowSheet.FlowsheetOptions.CurrentWeather.Temperature_C + 273.15, Text + dText_dL * cell.Start)
            If U = 0.0 Then Return 0.0
            Dim A = Math.PI * (cell.Section.DE * 0.0254) * cell.Length
            Return LogMeanDeltaT(Tin, Tout, Tamb) * U / 1000 * A
        End Function

        ''' <summary>The offset of the state at boundary k: the inlet at 0, the internal states, the outlet at the last.</summary>
        Private Function EOPipeBoundary(context As IEquationOrientedContext, k As Integer, cells As Integer) As Integer
            If k = 0 Then Return EOInletOffset(context, 0)
            If k = cells Then Return EOOutletOffset(context, 0)
            Return context.InternalStateOffset(Me, k - 1)
        End Function

        ''' <summary>The energy balances' tolerance over the unit's: see the PFR's, whose flashes they share.</summary>
        Private Const EOPipeEnergyTolerance As Double = 10.0

        Public Overrides Function EOInternalStateCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return 0
            Return EOPipeCells().Count - 1
        End Function

        ''' <summary>The pipe's last results at the start of the next increment, or a line from the inlet to the outlet.</summary>
        Public Overrides Function EOInternalStateGuess(context As IEquationOrientedContext, k As Integer) As Double()
            Dim names = context.CompoundNames
            Dim c = names.Length
            Dim g(c + 1) As Double
            Dim inlet = GetInletMaterialStream(0), outlet = GetOutletMaterialStream(0)
            For i = 0 To c - 1
                g(2 + i) = inlet.Phases(0).Compounds(names(i)).MolarFlow.GetValueOrDefault()
            Next
            Dim results = Profile.Sections.Values.SelectMany(Function(s) s.Results).ToList()
            Dim n = EOPipeCells().Count
            If results.Count = n AndAlso results(k + 1).Temperature_Initial.HasValue AndAlso results(k + 1).Pressure_Initial.HasValue Then
                g(0) = results(k + 1).Temperature_Initial.Value
                g(1) = results(k + 1).Pressure_Initial.Value
                Return g
            End If
            Dim z = (k + 1.0) / n
            Dim outOk = outlet.GetTemperature() > 0.0 AndAlso outlet.GetPressure() > 0.0
            Dim wz = If(outOk, z, 0.0)
            g(0) = inlet.GetTemperature() + wz * (outlet.GetTemperature() - inlet.GetTemperature())
            g(1) = inlet.GetPressure() + wz * (outlet.GetPressure() - inlet.GetPressure())
            Return g
        End Function

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Return {}
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return EOPipeCells().Count * (context.CompoundCount + 2)
        End Function

        Public Overrides Function EOVariables(context As IEquationOrientedContext) As Integer()
            If Not EOHasEquations Then Return MyBase.EOVariables(context)
            Dim n = EOPipeCells().Count
            Dim offsets = Enumerable.Range(0, n + 1).Select(Function(k) EOPipeBoundary(context, k, n)).ToArray()
            Return EOStreamVariables(context, offsets, Nothing)
        End Function

        Public Overrides Sub EOResiduals(context As IEquationOrientedContext, x As Double(), r As Double(), row As Integer)
            If Not EOHasEquations Then
                MyBase.EOResiduals(context, x, r, row)
                Return
            End If
            Dim c = context.CompoundCount
            Dim cells = EOPipeCells()
            For k = 1 To cells.Count
                Dim res = EOPipeCellResiduals(context, x, cells, k)
                Array.Copy(res, 0, r, row + (k - 1) * (c + 2), c + 2)
            Next
        End Sub

        ''' <summary>The equations of cell k (from boundary k-1 to k), each over its tolerance.</summary>
        Private Function EOPipeCellResiduals(context As IEquationOrientedContext, x As Double(), cells As List(Of EOPipeCell), k As Integer) As Double()
            Dim c = context.CompoundCount
            Dim n = cells.Count
            Dim cell = cells(k - 1)
            Dim inlet = EOPipeBoundary(context, 0, n)
            Dim a = EOPipeBoundary(context, k - 1, n), b = EOPipeBoundary(context, k, n)
            Dim outlet = EOOutletOffset(context, 0)
            Dim ft = context.FlowTolerance(outlet)
            Dim r(c + 1) As Double
            For i = 0 To c - 1
                r(i) = (x(b + 2 + i) - x(a + 2 + i)) / ft
            Next
            Dim name = EOPipeName(cell.Section)
            Dim drop As Func(Of IMaterialStream, Double) = Function(ms) EOPipeDrop(ms, cell.Section, cell.Length, cell.Elevation)
            Dim dpa = context.StreamProperty(a, x, name, drop)
            ' a gas pipeline equation is already the drop over the increment from its inlet
            Dim dpb = If(IsGasPipelineEquation(), dpa, context.StreamProperty(b, x, name, drop))
            r(c) = (x(b + 1) - x(a + 1) + (dpa + dpb) / 2.0) / context.PressureTolerance
            Dim q = 0.0
            For j = 1 To k
                q += EOPipeHeat(cells(j - 1), n, x(EOPipeBoundary(context, j - 1, n)), x(EOPipeBoundary(context, j, n)))
            Next
            r(c + 1) = (context.Enthalpy(b, x) - context.Enthalpy(inlet, x) - q) / (EOPipeEnergyTolerance * context.EnergyTolerance(outlet))
            Return r
        End Function

        Public Overrides Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean
            If Not EOHasEquations Then Return MyBase.EOJacobian(context, x, add)
            Dim c = context.CompoundCount
            Dim cells = EOPipeCells()
            Dim n = cells.Count
            Dim inlet = EOPipeBoundary(context, 0, n)
            Dim outlet = EOOutletOffset(context, 0)
            Dim ft = context.FlowTolerance(outlet), tp = context.PressureTolerance
            Dim tq = EOPipeEnergyTolerance * context.EnergyTolerance(outlet)
            Dim h0 = context.EnthalpyDerivatives(inlet, x)
            ' each cell's heat against the temperatures at its two ends
            Dim dq(n - 1, 1) As Double
            For j = 1 To n
                Dim ta = EOPipeBoundary(context, j - 1, n), tb = EOPipeBoundary(context, j, n)
                Dim cell = cells(j - 1)
                Dim h = 0.001
                dq(j - 1, 0) = (EOPipeHeat(cell, n, x(ta) + h, x(tb)) - EOPipeHeat(cell, n, x(ta) - h, x(tb))) / (2 * h)
                dq(j - 1, 1) = (EOPipeHeat(cell, n, x(ta), x(tb) + h) - EOPipeHeat(cell, n, x(ta), x(tb) - h)) / (2 * h)
            Next
            For k = 1 To n
                Dim cell = cells(k - 1)
                Dim a = EOPipeBoundary(context, k - 1, n), b = EOPipeBoundary(context, k, n)
                Dim e0 = (k - 1) * (c + 2)
                For i = 0 To c - 1
                    add(e0 + i, b + 2 + i, 1.0 / ft)
                    add(e0 + i, a + 2 + i, -1.0 / ft)
                Next
                Dim name = EOPipeName(cell.Section)
                Dim drop As Func(Of IMaterialStream, Double) = Function(ms) EOPipeDrop(ms, cell.Section, cell.Length, cell.Elevation)
                Dim da = context.StreamPropertyDerivatives(a, x, name, drop)
                add(e0 + c, b + 1, 1.0 / tp)
                add(e0 + c, a + 1, -1.0 / tp)
                If IsGasPipelineEquation() Then
                    For j = 0 To c + 1
                        If da(j) <> 0.0 Then add(e0 + c, a + j, da(j) / tp)
                    Next
                Else
                    Dim db = context.StreamPropertyDerivatives(b, x, name, drop)
                    For j = 0 To c + 1
                        If da(j) <> 0.0 Then add(e0 + c, a + j, da(j) / 2.0 / tp)
                        If db(j) <> 0.0 Then add(e0 + c, b + j, db(j) / 2.0 / tp)
                    Next
                End If
                Dim hb = context.EnthalpyDerivatives(b, x)
                For j = 0 To c + 1
                    add(e0 + c + 1, b + j, hb(j) / tq)
                    add(e0 + c + 1, inlet + j, -h0(j) / tq)
                Next
                For j = 1 To k
                    add(e0 + c + 1, EOPipeBoundary(context, j - 1, n), -dq(j - 1, 0) / tq)
                    add(e0 + c + 1, EOPipeBoundary(context, j, n), -dq(j - 1, 1) / tq)
                Next
            Next
            Return True
        End Function

        ''' <summary>The cells solved one after the other from the inlet x holds, each from the state before it, as the PFR's.</summary>
        Public Overrides Function EOInitialize(context As IEquationOrientedContext, x As Double()) As Boolean
            If Not EOHasEquations Then Return False
            Dim c = context.CompoundCount
            Dim cells = EOPipeCells()
            Dim n = cells.Count
            For k = 1 To n
                Dim a = EOPipeBoundary(context, k - 1, n), b = EOPipeBoundary(context, k, n)
                Dim kept = Enumerable.Range(0, c + 2).Select(Function(j) x(b + j)).ToArray()
                For j = 0 To c + 1
                    x(b + j) = x(a + j)
                Next
                Dim cell = k
                If Not EOSolveState(x, b, c + 2, Function(xv) EOPipeCellResiduals(context, xv, cells, cell)) Then
                    For j = 0 To c + 1
                        x(b + j) = kept(j)
                    Next
                    Exit For
                End If
            Next
            Return True
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Dim c = context.CompoundCount
            Dim cell = k \ (c + 2) + 1, j = k Mod (c + 2)
            Dim what = If(j < c, "flow of compound " & j, If(j = c, "pressure", "energy balance from the inlet"))
            Return $"increment {cell} of {EOPipeCells().Count}: {what}"
        End Function

    End Class

End Namespace
