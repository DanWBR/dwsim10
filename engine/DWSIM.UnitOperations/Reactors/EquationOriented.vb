'    Reactors - equations for the equation-oriented solver
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
Imports DWSIM.Interfaces.Enums

Namespace Reactors

    Partial Public Class Reactor_Conversion

        ''' <summary>
        ''' Equations of its own for reactions in the mixture, adiabatic (one group of parallel reactions), isothermal or at
        ''' a set outlet temperature, with a vapor and a liquid outlet: the inlet's moles are reacted group by group as the
        ''' unit does, each conversion read at the temperature the unit reads it; the products split by the PT flash at the
        ''' inlet's pressure less the drop; and the energy balance with the heats of reaction, or the temperature. A
        ''' conversion the reactants cannot sustain is not limited here as the unit's own solver limits it.
        ''' </summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Try
                    Dim g = GraphicObject
                    If Not (g.OutputConnectors(0).IsAttached AndAlso g.OutputConnectors(1).IsAttached) Then Return False
                    Select Case ReactorOperationMode
                        Case OperationMode.Isothermic, OperationMode.OutletTemperature
                        Case OperationMode.Adiabatic
                            If GetInletEnergyStream(1) IsNot Nothing Then Return False
                        Case Else
                            Return False
                    End Select
                    Dim groups = EOGroups()
                    If groups.Count = 0 Then Return False
                    ' an adiabatic second group reads its conversions at the temperature the first leaves
                    If ReactorOperationMode = OperationMode.Adiabatic AndAlso groups.Count > 1 Then Return False
                    For Each grp In groups
                        For Each id In grp
                            Dim rxn = FlowSheet.Reactions(id)
                            If rxn.ReactionPhase <> ReactionPhase.Mixture OrElse Not rxn.Components.ContainsKey(rxn.BaseReactant) Then Return False
                        Next
                    Next
                    Return True
                Catch ex As Exception
                    Return False
                End Try
            End Get
        End Property

        ' the active conversion reactions of the set, by rank, as InitVars orders them
        Private Function EOGroups() As List(Of List(Of String))
            Dim rset = FlowSheet.ReactionSets(ReactionSetID)
            Dim active = rset.Reactions.Values.Where(Function(r) r.IsActive AndAlso FlowSheet.Reactions(r.ReactionID).ReactionType = ReactionType.Conversion).ToList()
            Dim groups As New List(Of List(Of String))
            If active.Count = 0 Then Return groups
            For rank = 0 To active.Max(Function(r) r.Rank)
                Dim grp = active.Where(Function(r) r.Rank = rank).Select(Function(r) r.ReactionID).ToList()
                If grp.Count > 0 Then groups.Add(grp)
            Next
            Return groups
        End Function

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Return {"PROP_CR_0"}
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return 2 * context.CompoundCount + 4
        End Function

        Public Overrides Function EOVariables(context As IEquationOrientedContext) As Integer()
            If Not EOHasEquations Then Return MyBase.EOVariables(context)
            Return EOStreamVariables(context, {EOInletOffset(context, 0), EOOutletOffset(context, 0), EOOutletOffset(context, 1)}, EOParameters())
        End Function

        ' the temperature the conversions are read at, and whether it is the inlet's
        Private Function EOConversionTemperature(x As Double(), inlet As Integer, ByRef fromInlet As Boolean) As Double
            fromInlet = ReactorOperationMode <> OperationMode.OutletTemperature
            Return If(fromInlet, x(inlet), OutletTemperature)
        End Function

        ' a reaction's conversion (fraction) at T
        Private Function EOConversion(id As String, T As Double) As Double
            Dim rxn = FlowSheet.Reactions(id)
            SetExpressionVariable(GetExpressionContext(rxn.ID), "T", T)
            Return GetCompiledExpression(rxn.ID, rxn.Expression).Evaluate() / 100.0
        End Function

        ''' <summary>
        ''' The moles after the reactions, the heat they release (the heats of reaction times the moles of the base
        ''' reactants converted, kW), and their derivatives with respect to the inlet's moles and temperature.
        ''' </summary>
        Private Function EOReacted(context As IEquationOrientedContext, x As Double(), inlet As Integer,
                                   ByRef dzdn As Double(,), ByRef dzdT As Double(), ByRef DHr As Double, ByRef dHdn As Double(), ByRef dHdT As Double) As Double()
            Dim c = context.CompoundCount
            Dim names = context.CompoundNames.ToList()
            Dim z(c - 1) As Double
            Array.Copy(x, inlet + 2, z, 0, c)
            dzdn = New Double(c - 1, c - 1) {}
            For i = 0 To c - 1
                dzdn(i, i) = 1.0
            Next
            dzdT = New Double(c - 1) {}
            dHdn = New Double(c - 1) {}
            DHr = 0.0
            dHdT = 0.0
            Dim fromInlet As Boolean
            Dim T = EOConversionTemperature(x, inlet, fromInlet)
            For Each grp In EOGroups()
                ' the group's changes, from the moles before it
                Dim dn(c - 1), ddn_dT(c - 1) As Double
                Dim D(c - 1, c - 1) As Double
                For Each id In grp
                    Dim rxn = FlowSheet.Reactions(id)
                    Dim conv = EOConversion(id, T)
                    Dim dconv = If(fromInlet, (EOConversion(id, T + 0.01) - EOConversion(id, T - 0.01)) / 0.02, 0.0)
                    Dim b = names.IndexOf(rxn.BaseReactant)
                    Dim nb = z(b)
                    Dim scb = rxn.Components(rxn.BaseReactant).StoichCoeff
                    For Each sb In rxn.Components.Values
                        Dim j = names.IndexOf(sb.CompName)
                        Dim f = -sb.StoichCoeff / scb
                        dn(j) += f * conv * nb
                        D(j, b) += f * conv
                        ddn_dT(j) += f * dconv * nb
                    Next
                    ' heat: ReactionHeat (kJ/kmol of base reactant) times the base reactant converted
                    Dim h = rxn.ReactionHeat / 1000.0
                    DHr += h * conv * nb
                    For k = 0 To c - 1
                        dHdn(k) += h * conv * dzdn(b, k)
                    Next
                    dHdT += h * (dconv * nb + conv * dzdT(b))
                Next
                ' z + dn, with the derivatives carried through
                Dim zn(c - 1), Tn(c - 1) As Double
                Dim Jn(c - 1, c - 1) As Double
                For j = 0 To c - 1
                    zn(j) = Math.Max(z(j) + dn(j), 0.0)
                    Tn(j) = dzdT(j) + ddn_dT(j)
                    For m = 0 To c - 1
                        Tn(j) += D(j, m) * dzdT(m)
                        For k = 0 To c - 1
                            Jn(j, k) += If(j = m, 1.0, 0.0) * dzdn(m, k) + D(j, m) * dzdn(m, k)
                        Next
                    Next
                Next
                z = zn : dzdT = Tn : dzdn = Jn
            Next
            Return z
        End Function

        Public Overrides Sub EOResiduals(context As IEquationOrientedContext, x As Double(), r As Double(), row As Integer)
            If Not EOHasEquations Then
                MyBase.EOResiduals(context, x, r, row)
                Return
            End If
            Dim c = context.CompoundCount
            Dim inlet = EOInletOffset(context, 0), vapor = EOOutletOffset(context, 0), liquid = EOOutletOffset(context, 1)
            Dim dzdn As Double(,) = Nothing, dzdT As Double() = Nothing, dHdn As Double() = Nothing
            Dim DHr, dHdT As Double
            Dim z = EOReacted(context, x, inlet, dzdn, dzdT, DHr, dHdn, dHdT)
            Dim P = x(inlet + 1) - EOParameterValue(context, x, "PROP_CR_0", DeltaP.GetValueOrDefault())
            Dim T = x(vapor)
            Dim tf = context.FlowTolerance(inlet), te = context.EnergyTolerance(inlet)
            Dim split = context.Flash(vapor, T, P, z, False)
            For i = 0 To c - 1
                r(row + i) = (x(vapor + 2 + i) - split.V(i)) / tf
                r(row + c + i) = (x(liquid + 2 + i) - split.L(i)) / tf
            Next
            r(row + 2 * c) = (x(liquid) - T) / context.TemperatureTolerance
            r(row + 2 * c + 1) = (x(vapor + 1) - P) / context.PressureTolerance
            r(row + 2 * c + 2) = (x(liquid + 1) - P) / context.PressureTolerance
            Select Case ReactorOperationMode
                Case OperationMode.Adiabatic
                    r(row + 2 * c + 3) = (split.H - context.Enthalpy(inlet, x) + DHr) / te
                Case OperationMode.Isothermic
                    r(row + 2 * c + 3) = (T - x(inlet)) / context.TemperatureTolerance
                Case Else
                    r(row + 2 * c + 3) = (T - OutletTemperature) / context.TemperatureTolerance
            End Select
        End Sub

        Public Overrides Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean
            If Not EOHasEquations Then Return MyBase.EOJacobian(context, x, add)
            Dim c = context.CompoundCount
            Dim inlet = EOInletOffset(context, 0), vapor = EOOutletOffset(context, 0), liquid = EOOutletOffset(context, 1)
            Dim dzdn As Double(,) = Nothing, dzdT As Double() = Nothing, dHdn As Double() = Nothing
            Dim DHr, dHdT As Double
            Dim z = EOReacted(context, x, inlet, dzdn, dzdT, DHr, dHdn, dHdT)
            Dim P = x(inlet + 1) - EOParameterValue(context, x, "PROP_CR_0", DeltaP.GetValueOrDefault())
            Dim kdP = context.ParameterIndex(Me, "PROP_CR_0")
            Dim T = x(vapor)
            Dim tf = context.FlowTolerance(inlet), te = context.EnergyTolerance(inlet)
            Dim e = context.Flash(vapor, T, P, z, True)
            If e Is Nothing Then Throw New NotSupportedException(GraphicObject.Tag & ": no products")
            ' through P = P_in - dP
            Dim addP = Sub(row As Integer, v As Double)
                           add(row, inlet + 1, v)
                           If kdP >= 0 Then add(row, kdP, -v)
                       End Sub
            For i = 0 To c - 1
                ' vapor: n_v - V(T, P, z(n_in, T_in))
                add(i, vapor + 2 + i, 1.0 / tf)
                add(i, vapor, -e.VdT(i) / tf)
                addP(i, -e.VdP(i) / tf)
                ' liquid: n_l - (z - V)
                add(c + i, liquid + 2 + i, 1.0 / tf)
                add(c + i, vapor, e.VdT(i) / tf)
                addP(c + i, e.VdP(i) / tf)
                Dim vT = 0.0
                For j = 0 To c - 1
                    vT += e.Vdn(i, j) * dzdT(j)
                Next
                add(i, inlet, -vT / tf)
                add(c + i, inlet, -(dzdT(i) - vT) / tf)
                For k = 0 To c - 1
                    Dim vk = 0.0
                    For j = 0 To c - 1
                        vk += e.Vdn(i, j) * dzdn(j, k)
                    Next
                    add(i, inlet + 2 + k, -vk / tf)
                    add(c + i, inlet + 2 + k, -(dzdn(i, k) - vk) / tf)
                Next
            Next
            Dim tt = context.TemperatureTolerance, tp = context.PressureTolerance
            add(2 * c, liquid, 1.0 / tt)
            add(2 * c, vapor, -1.0 / tt)
            add(2 * c + 1, vapor + 1, 1.0 / tp)
            addP(2 * c + 1, -1.0 / tp)
            add(2 * c + 2, liquid + 1, 1.0 / tp)
            addP(2 * c + 2, -1.0 / tp)
            Dim kE = 2 * c + 3
            Select Case ReactorOperationMode
                Case OperationMode.Adiabatic
                    ' H(T, P, z) - H_in + DHr
                    add(kE, vapor, e.HdT / te)
                    addP(kE, e.HdP / te)
                    Dim hin = context.EnthalpyDerivatives(inlet, x)
                    For j = 0 To c + 1
                        add(kE, inlet + j, -hin(j) / te)
                    Next
                    Dim hT = dHdT
                    For j = 0 To c - 1
                        hT += e.Hdn(j) * dzdT(j)
                    Next
                    add(kE, inlet, hT / te)
                    For k = 0 To c - 1
                        Dim hk = dHdn(k)
                        For j = 0 To c - 1
                            hk += e.Hdn(j) * dzdn(j, k)
                        Next
                        add(kE, inlet + 2 + k, hk / te)
                    Next
                Case OperationMode.Isothermic
                    add(kE, vapor, 1.0 / tt)
                    add(kE, inlet, -1.0 / tt)
                Case Else
                    add(kE, vapor, 1.0 / tt)
            End Select
            Return True
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
                Case Else : Return If(ReactorOperationMode = OperationMode.Adiabatic, "energy balance", "temperature")
            End Select
        End Function

    End Class

End Namespace
