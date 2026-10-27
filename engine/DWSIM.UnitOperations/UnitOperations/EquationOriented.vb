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

        ''' <summary>Equations of its own by split ratios: each outlet at the inlet's temperature and pressure, with its share of the flows.</summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Return OperationMode = OpMode.SplitRatios
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
            ' SR1 and SR2, the ratios of the first two outlets, when the last takes the rest
            If Not EORest() OrElse n < 2 Then Return New String() {}
            Return Enumerable.Range(1, n - 1).Select(Function(o) "SR" & o).ToArray()
        End Function

        Private Function EORatio(context As IEquationOrientedContext, x As Double(), o As Integer, ports As Integer(), rest As Boolean) As Double
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
                r(row) = (x(s) - x(inlet)) / context.TemperatureTolerance
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
            Dim k = 0
            For o = 0 To ports.Length - 1
                Dim s = EOOutletOffset(context, ports(o))
                Dim ratio = EORatio(context, x, o, ports, rest)
                Dim ft = context.FlowTolerance(s)
                add(k, s, 1.0 / tt)
                add(k, inlet, -1.0 / tt)
                add(k + 1, s + 1, 1.0 / tp)
                add(k + 1, inlet + 1, -1.0 / tp)
                For i = 0 To c - 1
                    Dim row = k + 2 + i
                    add(row, s + 2 + i, 1.0 / ft)
                    add(row, inlet + 2 + i, -ratio / ft)
                    If rest AndAlso o = ports.Length - 1 Then
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
            Return "outlet " & tag & ": " & If(j = 0, "temperature", If(j = 1, "pressure", "flow " & (j - 2)))
        End Function

    End Class

    Partial Public Class Vessel

        ''' <summary>
        ''' Equations of its own in the legacy mode, adiabatic or at a set temperature, at the pressure of its inlets, with
        ''' a vapor and a liquid outlet: the two phases of the PT flash of the inlets' mixture, at a common temperature and
        ''' pressure, and the energy balance (or the set temperature, whose duty is a result).
        ''' </summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                If CalculationMode <> CalculationModes.Legacy OrElse OverrideP OrElse CalculateRigorousHeatBalance Then Return False
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

End Namespace
