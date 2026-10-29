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
Imports DWSIM.Thermodynamics
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.Thermodynamics.BaseClasses

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

    ''' <summary>
    ''' The equations of a reactor that brings its reactions to chemical equilibrium (the equilibrium and the Gibbs
    ''' reactors), with a vapor and a liquid outlet. The overall moles z are the two outlets together; they differ from
    ''' the inlet's by a combination of the columns of S (the conservation rows, one per free column of S transposed in
    ''' row echelon form); each column of S is at equilibrium, the sum of its coefficients times the logarithms of the
    ''' bases equal to ln K; the outlets split by the PT flash of z; and the energy balance carries the heat of reaction
    ''' as the coefficients h times the change of z, or the temperature is set.
    ''' </summary>
    Friend Class EOChemicalEquilibrium

        ''' <summary>Stoichiometry over the context's compounds, compounds by columns of equilibrium.</summary>
        Public S As Double(,)
        ''' <summary>ln K of each column at T.</summary>
        Public LnK As Func(Of Double, Double())
        ''' <summary>ln of the basis of each compound for each column (R x C) at T, P, the moles of the reacting phase and whether it is a vapor.</summary>
        Public LnBasis As Func(Of Double, Double, Double(), Boolean, Double(,))
        ''' <summary>The reacting phase: the overall moles, or the phase the flash leaves (the vapor when there is one).</summary>
        Public FlashPhase As Boolean
        ''' <summary>kW per mol/s of change of each compound.</summary>
        Public H As Double()
        Public Mode As OperationMode
        Public Tset As Double
        Public DeltaP As Double
        Public Const TolLn As Double = 0.000001

        Private R, C, nfree As Integer
        Private M As Double(,)

        ''' <summary>The free columns of S transposed in row echelon form give the conservation rows; false when the columns of S are dependent.</summary>
        Public Function Prepare(c As Integer) As Boolean
            Me.C = c
            R = S.GetLength(1)
            ' row echelon form of S transposed (R x C)
            Dim A(R - 1, c - 1) As Double
            For i = 0 To R - 1
                For j = 0 To c - 1
                    A(i, j) = S(j, i)
                Next
            Next
            Dim pivots As New List(Of Integer)
            Dim row = 0
            For col = 0 To c - 1
                If row >= R Then Exit For
                Dim best = row
                For i = row + 1 To R - 1
                    If Math.Abs(A(i, col)) > Math.Abs(A(best, col)) Then best = i
                Next
                If Math.Abs(A(best, col)) < 0.000000001 Then Continue For
                For j = 0 To c - 1
                    Dim t = A(row, j) : A(row, j) = A(best, j) : A(best, j) = t
                Next
                Dim p = A(row, col)
                For j = 0 To c - 1
                    A(row, j) /= p
                Next
                For i = 0 To R - 1
                    If i = row Then Continue For
                    Dim f = A(i, col)
                    If f = 0.0 Then Continue For
                    For j = 0 To c - 1
                        A(i, j) -= f * A(row, j)
                    Next
                Next
                pivots.Add(col)
                row += 1
            Next
            If pivots.Count < R Then Return False
            Dim free = Enumerable.Range(0, c).Where(Function(j) Not pivots.Contains(j)).ToList()
            nfree = free.Count
            ReDim M(nfree - 1, c - 1)
            For k = 0 To nfree - 1
                M(k, free(k)) = 1.0
                For q = 0 To R - 1
                    M(k, pivots(q)) = -A(q, free(k))
                Next
            Next
            Return True
        End Function

        Public Function Count() As Integer
            Return 2 * C + 4
        End Function

        ' the overall moles and the reacting phase's moles with d(phase moles)/d(z), /dT and /dP
        Private Function Phase(ctx As IEquationOrientedContext, vapor As Integer, T As Double, P As Double, z As Double(), derivatives As Boolean,
                               ByRef dw As Double(,), ByRef dwT As Double(), ByRef dwP As Double(), ByRef isVapor As Boolean) As Double()
            dw = New Double(C - 1, C - 1) {}
            dwT = New Double(C - 1) {}
            dwP = New Double(C - 1) {}
            isVapor = True
            If Not FlashPhase Then
                For i = 0 To C - 1
                    dw(i, i) = 1.0
                Next
                Return z
            End If
            Dim fl = ctx.Flash(vapor, T, P, z, derivatives)
            Dim useVapor = fl.V.Sum() > 0.0000000001 * z.Sum()
            isVapor = useVapor
            Dim w = If(useVapor, fl.V, fl.L)
            If derivatives Then
                Dim sg = If(useVapor, 1.0, -1.0)
                For i = 0 To C - 1
                    For k = 0 To C - 1
                        dw(i, k) = If(useVapor, 0.0, If(i = k, 1.0, 0.0)) + sg * fl.Vdn(i, k)
                    Next
                    dwT(i) = sg * fl.VdT(i)
                    dwP(i) = sg * fl.VdP(i)
                Next
            End If
            Return CType(w.Clone(), Double())
        End Function

        Public Sub Residuals(ctx As IEquationOrientedContext, x As Double(), res As Double(), row As Integer, inlet As Integer, vapor As Integer, liquid As Integer)
            Dim z(C - 1) As Double
            For i = 0 To C - 1
                z(i) = x(vapor + 2 + i) + x(liquid + 2 + i)
            Next
            Dim P = x(inlet + 1) - DeltaP
            Dim T = x(vapor)
            Dim tf = ctx.FlowTolerance(inlet)
            Dim split = ctx.Flash(vapor, T, P, z, False)
            Dim k = row
            For i = 0 To C - 1
                res(k) = (x(vapor + 2 + i) - split.V(i)) / tf : k += 1
            Next
            For q = 0 To nfree - 1
                Dim acc = 0.0
                For j = 0 To C - 1
                    acc += M(q, j) * (z(j) - x(inlet + 2 + j))
                Next
                res(k) = acc / tf : k += 1
            Next
            Dim dw As Double(,) = Nothing, dwT As Double() = Nothing, dwP As Double() = Nothing
            Dim isVapor As Boolean
            Dim w = Phase(ctx, vapor, T, P, z, False, dw, dwT, dwP, isVapor)
            Dim lb = LnBasis(T, P, w, isVapor)
            Dim lk = LnK(T)
            For q = 0 To R - 1
                Dim acc = -lk(q)
                For j = 0 To C - 1
                    If S(j, q) <> 0.0 Then acc += S(j, q) * lb(q, j)
                Next
                res(k) = acc / TolLn : k += 1
            Next
            res(k) = (x(liquid) - T) / ctx.TemperatureTolerance : k += 1
            res(k) = (x(vapor + 1) - P) / ctx.PressureTolerance : k += 1
            res(k) = (x(liquid + 1) - P) / ctx.PressureTolerance : k += 1
            Select Case Mode
                Case OperationMode.Adiabatic
                    Dim e = split.H - ctx.Enthalpy(inlet, x)
                    For j = 0 To C - 1
                        e += H(j) * (z(j) - x(inlet + 2 + j))
                    Next
                    res(k) = e / ctx.EnergyTolerance(inlet)
                Case OperationMode.Isothermic
                    res(k) = (T - x(inlet)) / ctx.TemperatureTolerance
                Case Else
                    res(k) = (T - Tset) / ctx.TemperatureTolerance
            End Select
        End Sub

        ''' <summary>kdP: the variable of the pressure drop, or -1.</summary>
        Public Sub Jacobian(ctx As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double), inlet As Integer, vapor As Integer, liquid As Integer, kdP As Integer)
            Dim z(C - 1) As Double
            For i = 0 To C - 1
                z(i) = x(vapor + 2 + i) + x(liquid + 2 + i)
            Next
            Dim P = x(inlet + 1) - DeltaP
            Dim T = x(vapor)
            Dim tf = ctx.FlowTolerance(inlet)
            Dim e = ctx.Flash(vapor, T, P, z, True)
            If e Is Nothing Then Throw New NotSupportedException("no products")
            ' through P = P_in - dP
            Dim addP = Sub(rw As Integer, v As Double)
                           add(rw, inlet + 1, v)
                           If kdP >= 0 Then add(rw, kdP, -v)
                       End Sub
            ' through z = n_v + n_l
            Dim addZ = Sub(rw As Integer, j As Integer, v As Double)
                           add(rw, vapor + 2 + j, v)
                           add(rw, liquid + 2 + j, v)
                       End Sub
            Dim k = 0
            For i = 0 To C - 1
                add(k, vapor + 2 + i, 1.0 / tf)
                For j = 0 To C - 1
                    addZ(k, j, -e.Vdn(i, j) / tf)
                Next
                add(k, vapor, -e.VdT(i) / tf)
                addP(k, -e.VdP(i) / tf)
                k += 1
            Next
            For q = 0 To nfree - 1
                For j = 0 To C - 1
                    If M(q, j) <> 0.0 Then
                        addZ(k, j, M(q, j) / tf)
                        add(k, inlet + 2 + j, -M(q, j) / tf)
                    End If
                Next
                k += 1
            Next
            ' the equilibrium rows: d(ln basis)/d(T, P, phase moles) by central differences on the smooth fugacity
            ' coefficients, carried through the flash to z
            Dim dw As Double(,) = Nothing, dwT As Double() = Nothing, dwP As Double() = Nothing
            Dim isVapor As Boolean
            Dim w = Phase(ctx, vapor, T, P, z, True, dw, dwT, dwP, isVapor)
            Dim hT = 0.001, hP = Math.Max(0.00001 * P, 0.1), sw = Math.Max(w.Sum(), 1.0E-30)
            Dim bT = Diff(Function(d) LnBasis(T + d, P, w, isVapor), hT)
            Dim bP = Diff(Function(d) LnBasis(T, P + d, w, isVapor), hP)
            Dim bw As New List(Of Double(,))
            For mi = 0 To C - 1
                Dim mm = mi
                Dim hw = 0.000001 * sw
                If w(mi) > hw Then
                    bw.Add(Diff(Function(d)
                                     Dim w2 = CType(w.Clone(), Double())
                                     w2(mm) += d
                                     Return LnBasis(T, P, w2, isVapor)
                                 End Function, hw))
                Else
                    Dim w2 = CType(w.Clone(), Double())
                    w2(mm) += hw
                    Dim up = LnBasis(T, P, w2, isVapor), at = LnBasis(T, P, w, isVapor)
                    Dim d0(R - 1, C - 1) As Double
                    For q = 0 To R - 1
                        For j = 0 To C - 1
                            d0(q, j) = (up(q, j) - at(q, j)) / hw
                        Next
                    Next
                    bw.Add(d0)
                End If
            Next
            Dim lk0 = LnK(T + hT), lk1 = LnK(T - hT)
            For q = 0 To R - 1
                Dim gT = -(lk0(q) - lk1(q)) / (2 * hT)
                Dim gP = 0.0
                Dim gw(C - 1) As Double
                For j = 0 To C - 1
                    If S(j, q) = 0.0 Then Continue For
                    gT += S(j, q) * bT(q, j)
                    gP += S(j, q) * bP(q, j)
                    For mi = 0 To C - 1
                        gw(mi) += S(j, q) * bw(mi)(q, j)
                    Next
                Next
                ' w = w(T, P, z)
                For mi = 0 To C - 1
                    gT += gw(mi) * dwT(mi)
                    gP += gw(mi) * dwP(mi)
                Next
                For j = 0 To C - 1
                    Dim gz = 0.0
                    For mi = 0 To C - 1
                        gz += gw(mi) * dw(mi, j)
                    Next
                    If gz <> 0.0 Then addZ(k, j, gz / TolLn)
                Next
                add(k, vapor, gT / TolLn)
                addP(k, gP / TolLn)
                k += 1
            Next
            Dim tt = ctx.TemperatureTolerance, tp = ctx.PressureTolerance
            add(k, liquid, 1.0 / tt) : add(k, vapor, -1.0 / tt) : k += 1
            add(k, vapor + 1, 1.0 / tp) : addP(k, -1.0 / tp) : k += 1
            add(k, liquid + 1, 1.0 / tp) : addP(k, -1.0 / tp) : k += 1
            Select Case Mode
                Case OperationMode.Adiabatic
                    Dim te = ctx.EnergyTolerance(inlet)
                    add(k, vapor, e.HdT / te)
                    addP(k, e.HdP / te)
                    For j = 0 To C - 1
                        addZ(k, j, (e.Hdn(j) + H(j)) / te)
                        add(k, inlet + 2 + j, -H(j) / te)
                    Next
                    Dim hin = ctx.EnthalpyDerivatives(inlet, x)
                    For j = 0 To C + 1
                        add(k, inlet + j, -hin(j) / te)
                    Next
                Case OperationMode.Isothermic
                    add(k, vapor, 1.0 / tt) : add(k, inlet, -1.0 / tt)
                Case Else
                    add(k, vapor, 1.0 / tt)
            End Select
        End Sub

        ' central difference of an (R x C) function of one shift
        Private Function Diff(f As Func(Of Double, Double(,)), h As Double) As Double(,)
            Dim up = f(h), dn = f(-h)
            Dim d(R - 1, C - 1) As Double
            For q = 0 To R - 1
                For j = 0 To C - 1
                    d(q, j) = (up(q, j) - dn(q, j)) / (2 * h)
                Next
            Next
            Return d
        End Function

        Public Function Name(k As Integer) As String
            If k < C Then Return "vapor flow " & k
            If k < C + nfree Then Return "conservation " & (k - C)
            If k < 2 * C Then Return "chemical equilibrium " & (k - C - nfree)
            Select Case k - 2 * C
                Case 0 : Return "liquid temperature"
                Case 1 : Return "vapor pressure"
                Case 2 : Return "liquid pressure"
                Case Else : Return If(Mode = OperationMode.Adiabatic, "energy balance", "temperature")
            End Select
        End Function

    End Class

    ''' <summary>The parts the equilibrium and the Gibbs reactors share in their equation-oriented models.</summary>
    Friend Module EOReactorHelpers

        ''' <summary>Fugacity coefficients with the package pointed at the reactor's inlet stream while it calculates.</summary>
        Friend Function FugacityCoefficients(pp As PropertyPackages.PropertyPackage, inlet As MaterialStream, y As Double(), T As Double, P As Double, vapor As Boolean) As Double()
            Dim keep = pp.CurrentMaterialStream
            Try
                pp.CurrentMaterialStream = inlet
                Return pp.DW_CalcFugCoeff(y, T, P, If(vapor, PropertyPackages.State.Vapor, PropertyPackages.State.Liquid))
            Finally
                pp.CurrentMaterialStream = keep
            End Try
        End Function

        ''' <summary>Whether the context's compounds are the package's, in its order.</summary>
        Friend Function SameCompounds(ctxNames As String(), inlet As MaterialStream) As Boolean
            Return ctxNames.SequenceEqual(inlet.Phases(0).Compounds.Keys)
        End Function

    End Module

    Partial Public Class Reactor_Equilibrium

        ''' <summary>
        ''' Equations of their own for reactions in the vapor or in the liquid with activity, fugacity, mole fraction or
        ''' partial pressure as the basis, adiabatic, isothermal or at a set outlet temperature, with a vapor and a liquid
        ''' outlet: each reaction at its constant at the temperature plus its approach, the bases taken at the overall
        ''' composition as the unit takes them. The unit's own solver stops when the squares of the logarithms of the
        ''' ratios to the constants sum to 1e-3; these equations close them to 1e-6.
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
                    Dim ids = EOReactions()
                    If ids.Count = 0 Then Return False
                    For Each id In ids
                        Dim rx = FlowSheet.Reactions(id)
                        If rx.ReactionPhase <> ReactionPhase.Vapor AndAlso rx.ReactionPhase <> ReactionPhase.Liquid Then Return False
                        Select Case rx.ReactionBasis
                            Case ReactionBasis.Activity, ReactionBasis.Fugacity, ReactionBasis.MolarFrac, ReactionBasis.PartialPress
                            Case Else
                                Return False
                        End Select
                        If Not rx.Components.ContainsKey(rx.BaseReactant) Then Return False
                    Next
                    Return True
                Catch ex As Exception
                    Return False
                End Try
            End Get
        End Property

        ' the active equilibrium reactions of the set
        Private Function EOReactions() As List(Of String)
            Return FlowSheet.ReactionSets(ReactionSetID).Reactions.Values.Where(Function(r) r.IsActive AndAlso FlowSheet.Reactions(r.ReactionID).ReactionType = ReactionType.Equilibrium).Select(Function(r) r.ReactionID).ToList()
        End Function

        Private Function EOModel(context As IEquationOrientedContext, x As Double()) As EOChemicalEquilibrium
            Dim names = context.CompoundNames
            Dim c = names.Length
            Dim pp As PropertyPackages.PropertyPackage = PropertyPackage
            If Not EOReactorHelpers.SameCompounds(names, GetInletMaterialStream(0)) Then Throw New NotSupportedException(GraphicObject.Tag & ": the package lists the compounds in another order")
            Dim ids = EOReactions()
            Dim rxs = ids.Select(Function(id) FlowSheet.Reactions(id)).ToList()
            Dim nr = rxs.Count
            Dim S(c - 1, nr - 1) As Double
            For q = 0 To nr - 1
                For Each sb In rxs(q).Components.Values
                    S(Array.IndexOf(names, sb.CompName), q) = sb.StoichCoeff
                Next
            Next
            ' the heat of reaction per extent: ReactionHeat (kJ/kmol of base reactant) times its coefficient; h = w (S^T S)^-1 S^T
            Dim wr(nr - 1) As Double
            For q = 0 To nr - 1
                wr(q) = rxs(q).ReactionHeat * Math.Abs(rxs(q).Components(rxs(q).BaseReactant).StoichCoeff) / 1000.0
            Next
            Dim inletStream = GetInletMaterialStream(0)
            Dim m As New EOChemicalEquilibrium With {
                .S = S,
                .FlashPhase = False,
                .Mode = ReactorOperationMode,
                .Tset = OutletTemperature,
                .DeltaP = EOParameterValue(context, x, "PROP_EQ_0", DeltaP.GetValueOrDefault()),
                .H = EOReactorHelpers2.ExtentHeat(S, wr),
                .LnK = Function(T)
                           ' a constant from the Gibbs energies reads the package's current stream
                           Dim keep = pp.CurrentMaterialStream
                           Try
                               pp.CurrentMaterialStream = inletStream
                               Return rxs.Select(Function(rx) Math.Log(rx.EvaluateK(T + rx.Approach, pp))).ToArray()
                           Finally
                               pp.CurrentMaterialStream = keep
                           End Try
                       End Function,
                .LnBasis = Function(T, P, w, isVapor)
                               Dim total = w.Sum()
                               Dim y(c - 1) As Double
                               For i = 0 To c - 1
                                   y(i) = If(total > 0.0, w(i) / total, 0.0)
                               Next
                               Dim fv As Double() = Nothing, fl As Double() = Nothing
                               If rxs.Any(Function(rx) rx.ReactionPhase = ReactionPhase.Vapor) Then fv = EOReactorHelpers.FugacityCoefficients(pp, inletStream, y, T, P, True)
                               If rxs.Any(Function(rx) rx.ReactionPhase = ReactionPhase.Liquid) Then fl = EOReactorHelpers.FugacityCoefficients(pp, inletStream, y, T, P, False)
                               Dim b(nr - 1, c - 1) As Double
                               For q = 0 To nr - 1
                                   Dim rx = rxs(q)
                                   Dim vap = rx.ReactionPhase = ReactionPhase.Vapor
                                   For j = 0 To c - 1
                                       If S(j, q) = 0.0 Then Continue For
                                       ' an absent compound counts at a mole fraction of 0.01, as the unit counts it
                                       Dim yj = If(y(j) > 0.0, y(j), 0.01)
                                       Dim phi = If(vap, fv(j), fl(j))
                                       Select Case rx.ReactionBasis
                                           Case ReactionBasis.Activity, ReactionBasis.Fugacity
                                               b(q, j) = Math.Log(phi * yj * If(vap, P / 101325.0, 1.0))
                                           Case ReactionBasis.MolarFrac
                                               b(q, j) = Math.Log(yj)
                                           Case Else
                                               b(q, j) = Math.Log(DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(rx.EquilibriumReactionBasisUnits, yj * phi * P))
                                       End Select
                                   Next
                               Next
                               Return b
                           End Function
            }
            If Not m.Prepare(c) Then Throw New NotSupportedException(GraphicObject.Tag & ": the reactions are not independent")
            Return m
        End Function

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Return {"PROP_EQ_0"}
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return 2 * context.CompoundCount + 4
        End Function

        Public Overrides Function EOVariables(context As IEquationOrientedContext) As Integer()
            If Not EOHasEquations Then Return MyBase.EOVariables(context)
            Return EOStreamVariables(context, {EOInletOffset(context, 0), EOOutletOffset(context, 0), EOOutletOffset(context, 1)}, EOParameters())
        End Function

        Public Overrides Sub EOResiduals(context As IEquationOrientedContext, x As Double(), r As Double(), row As Integer)
            If Not EOHasEquations Then
                MyBase.EOResiduals(context, x, r, row)
                Return
            End If
            EOModel(context, x).Residuals(context, x, r, row, EOInletOffset(context, 0), EOOutletOffset(context, 0), EOOutletOffset(context, 1))
        End Sub

        Public Overrides Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean
            If Not EOHasEquations Then Return MyBase.EOJacobian(context, x, add)
            EOModel(context, x).Jacobian(context, x, add, EOInletOffset(context, 0), EOOutletOffset(context, 0), EOOutletOffset(context, 1), context.ParameterIndex(Me, "PROP_EQ_0"))
            Return True
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Dim c = context.CompoundCount, nfree = c - EOReactions().Count
            If k < c Then Return "vapor flow " & k
            If k < c + nfree Then Return "conservation " & (k - c)
            If k < 2 * c Then Return "chemical equilibrium of " & FlowSheet.Reactions(EOReactions()(k - c - nfree)).Name
            Select Case k - 2 * c
                Case 0 : Return "liquid temperature"
                Case 1 : Return "vapor pressure"
                Case 2 : Return "liquid pressure"
                Case Else : Return If(ReactorOperationMode = OperationMode.Adiabatic, "energy balance", "temperature")
            End Select
        End Function

    End Class

    Friend Module EOReactorHelpers2

        ''' <summary>h = w (S^T S)^-1 S^T: the heat of the extents that make a change of the moles in the span of S.</summary>
        Friend Function ExtentHeat(S As Double(,), w As Double()) As Double()
            Dim c = S.GetLength(0), nr = S.GetLength(1)
            Dim G(nr - 1, nr - 1) As Double
            For a = 0 To nr - 1
                For b = 0 To nr - 1
                    For j = 0 To c - 1
                        G(a, b) += S(j, a) * S(j, b)
                    Next
                Next
            Next
            ' solve G v = w (G symmetric), then h = S v
            Dim v = SolveSmall(G, w)
            Dim h(c - 1) As Double
            For j = 0 To c - 1
                For a = 0 To nr - 1
                    h(j) += S(j, a) * v(a)
                Next
            Next
            Return h
        End Function

        ''' <summary>A basis of the null space of A (rows by columns): one vector per free column of its row echelon form.</summary>
        Friend Function NullSpace(A0 As Double(,)) As List(Of Double())
            Dim rows = A0.GetLength(0), n = A0.GetLength(1)
            Dim A = CType(A0.Clone(), Double(,))
            Dim pivots As New List(Of Integer)
            Dim row = 0
            For col = 0 To n - 1
                If row >= rows Then Exit For
                Dim best = row
                For i = row + 1 To rows - 1
                    If Math.Abs(A(i, col)) > Math.Abs(A(best, col)) Then best = i
                Next
                If Math.Abs(A(best, col)) < 0.000000001 Then Continue For
                For j = 0 To n - 1
                    Dim t = A(row, j) : A(row, j) = A(best, j) : A(best, j) = t
                Next
                Dim p = A(row, col)
                For j = 0 To n - 1
                    A(row, j) /= p
                Next
                For i = 0 To rows - 1
                    If i = row Then Continue For
                    Dim f = A(i, col)
                    If f = 0.0 Then Continue For
                    For j = 0 To n - 1
                        A(i, j) -= f * A(row, j)
                    Next
                Next
                pivots.Add(col)
                row += 1
            Next
            Dim basis As New List(Of Double())
            For f = 0 To n - 1
                If pivots.Contains(f) Then Continue For
                Dim v(n - 1) As Double
                v(f) = 1.0
                For q = 0 To pivots.Count - 1
                    v(pivots(q)) = -A(q, f)
                Next
                basis.Add(v)
            Next
            Return basis
        End Function

        Friend Function SolveSmall(A0 As Double(,), b0 As Double()) As Double()
            Dim n = b0.Length
            Dim A = CType(A0.Clone(), Double(,)), b = CType(b0.Clone(), Double())
            For col = 0 To n - 1
                Dim best = col
                For i = col + 1 To n - 1
                    If Math.Abs(A(i, col)) > Math.Abs(A(best, col)) Then best = i
                Next
                For j = 0 To n - 1
                    Dim t = A(col, j) : A(col, j) = A(best, j) : A(best, j) = t
                Next
                Dim tb = b(col) : b(col) = b(best) : b(best) = tb
                For i = col + 1 To n - 1
                    Dim f = A(i, col) / A(col, col)
                    For j = col To n - 1
                        A(i, j) -= f * A(col, j)
                    Next
                    b(i) -= f * b(col)
                Next
            Next
            Dim xs(n - 1) As Double
            For i = n - 1 To 0 Step -1
                Dim s = b(i)
                For j = i + 1 To n - 1
                    s -= A(i, j) * xs(j)
                Next
                xs(i) = s / A(i, i)
            Next
            Return xs
        End Function

    End Module

    Partial Public Class Reactor_Gibbs

        ''' <summary>
        ''' Equations of their own for the minimum of the Gibbs energy over the reacting compounds, adiabatic, isothermal or
        ''' at a set outlet temperature, reacting in the vapor, in the liquid, or in the phases the flash gives, with a vapor
        ''' and a liquid outlet: the element balances, and the chemical potentials of the unit's Gibbs energy (the energies
        ''' of formation over RT, the logarithms of the fugacities over 1 atm) balanced along a basis of the reactions the
        ''' elements allow. The unit's own minimization stops at its tolerance; these equations close the minimum.
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
                    If ReactivePhaseBehavior = ReactivePhaseType.Solid Then Return False
                    If ComponentIDs Is Nothing OrElse ComponentIDs.Count = 0 Then Return False
                    Return True
                Catch ex As Exception
                    Return False
                End Try
            End Get
        End Property

        Private Function EOModel(context As IEquationOrientedContext, x As Double()) As EOChemicalEquilibrium
            Dim names = context.CompoundNames
            Dim c = names.Length
            Dim pp As PropertyPackages.PropertyPackage = PropertyPackage
            If Not EOReactorHelpers.SameCompounds(names, GetInletMaterialStream(0)) Then Throw New NotSupportedException(GraphicObject.Tag & ": the package lists the compounds in another order")
            ' the element matrix over the reacting compounds, and a basis of the reactions it allows
            Dim ids = ComponentIDs.ToList()
            Dim els = ids.SelectMany(Function(id) FlowSheet.SelectedCompounds(id).Elements.Keys.Cast(Of String)()).Distinct().ToList()
            Dim Ael(els.Count - 1, ids.Count - 1) As Double
            For ei = 0 To els.Count - 1
                For j = 0 To ids.Count - 1
                    Dim elems = FlowSheet.SelectedCompounds(ids(j)).Elements
                    If elems.ContainsKey(els(ei)) Then Ael(ei, j) = Convert.ToDouble(elems(els(ei)))
                Next
            Next
            Dim nullBasis = EOReactorHelpers2.NullSpace(Ael)
            Dim nr = nullBasis.Count
            If nr = 0 Then Throw New NotSupportedException(GraphicObject.Tag & ": the elements allow no reaction")
            Dim S(c - 1, nr - 1) As Double
            For q = 0 To nr - 1
                For j = 0 To ids.Count - 1
                    S(Array.IndexOf(names, ids(j)), q) = nullBasis(q)(j)
                Next
            Next
            ' the heat of reaction from the enthalpies of formation, as the unit takes it
            Dim h(c - 1) As Double
            For Each id In ids
                Dim cp = FlowSheet.SelectedCompounds(id)
                h(Array.IndexOf(names, id)) = cp.IG_Enthalpy_of_Formation_25C * cp.Molar_Weight / 1000.0
            Next
            Dim mw = context.MolecularWeights
            Dim inletStream = GetInletMaterialStream(0)
            Dim vapor = ReactivePhaseBehavior <> ReactivePhaseType.Liquid
            Dim m As New EOChemicalEquilibrium With {
                .S = S,
                .FlashPhase = ReactivePhaseBehavior = ReactivePhaseType.CalculateEquilibrium,
                .Mode = ReactorOperationMode,
                .Tset = OutletTemperature,
                .DeltaP = EOParameterValue(context, x, "PROP_GR_0", DeltaP.GetValueOrDefault()),
                .H = h,
                .LnK = Function(T)
                           ' ln K = -sum of the coefficients times the energies of formation over RT
                           Dim keep = pp.CurrentMaterialStream
                           Dim g(c - 1) As Double
                           Try
                               pp.CurrentMaterialStream = inletStream
                               For j = 0 To c - 1
                                   If ids.Contains(names(j)) Then g(j) = pp.AUX_DELGF_T(298.15, T, names(j)) * mw(j)
                               Next
                           Finally
                               pp.CurrentMaterialStream = keep
                           End Try
                           Dim lk(nr - 1) As Double
                           For q = 0 To nr - 1
                               For j = 0 To c - 1
                                   lk(q) -= S(j, q) * g(j)
                               Next
                           Next
                           Return lk
                       End Function,
                .LnBasis = Function(T, P, w, isVapor)
                               Dim total = w.Sum()
                               Dim y(c - 1) As Double
                               For i = 0 To c - 1
                                   y(i) = If(total > 0.0, w(i) / total, 0.0)
                               Next
                               ' the reacting phase: the vapor of the flash when there is one, else its liquid
                               Dim inVapor = If(ReactivePhaseBehavior = ReactivePhaseType.CalculateEquilibrium, isVapor, vapor)
                               Dim phi = EOReactorHelpers.FugacityCoefficients(pp, inletStream, y, T, P, inVapor)
                               Dim b(nr - 1, c - 1) As Double
                               For q = 0 To nr - 1
                                   For j = 0 To c - 1
                                       If S(j, q) = 0.0 Then Continue For
                                       b(q, j) = Math.Log(Math.Max(y(j), 1.0E-20)) + Math.Log(phi(j) * P / 101325.0)
                                   Next
                               Next
                               Return b
                           End Function
            }
            If Not m.Prepare(c) Then Throw New NotSupportedException(GraphicObject.Tag & ": no basis of reactions")
            Return m
        End Function

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Return {"PROP_GR_0"}
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return 2 * context.CompoundCount + 4
        End Function

        Public Overrides Function EOVariables(context As IEquationOrientedContext) As Integer()
            If Not EOHasEquations Then Return MyBase.EOVariables(context)
            Return EOStreamVariables(context, {EOInletOffset(context, 0), EOOutletOffset(context, 0), EOOutletOffset(context, 1)}, EOParameters())
        End Function

        Public Overrides Sub EOResiduals(context As IEquationOrientedContext, x As Double(), r As Double(), row As Integer)
            If Not EOHasEquations Then
                MyBase.EOResiduals(context, x, r, row)
                Return
            End If
            EOModel(context, x).Residuals(context, x, r, row, EOInletOffset(context, 0), EOOutletOffset(context, 0), EOOutletOffset(context, 1))
        End Sub

        Public Overrides Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean
            If Not EOHasEquations Then Return MyBase.EOJacobian(context, x, add)
            EOModel(context, x).Jacobian(context, x, add, EOInletOffset(context, 0), EOOutletOffset(context, 0), EOOutletOffset(context, 1), context.ParameterIndex(Me, "PROP_GR_0"))
            Return True
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Dim c = context.CompoundCount
            If k < c Then Return "vapor flow " & k
            If k < 2 * c Then Return "conservation or chemical equilibrium " & (k - c)
            Select Case k - 2 * c
                Case 0 : Return "liquid temperature"
                Case 1 : Return "vapor pressure"
                Case 2 : Return "liquid pressure"
                Case Else : Return If(ReactorOperationMode = OperationMode.Adiabatic, "energy balance", "temperature")
            End Select
        End Function

    End Class

    Partial Public Class Reactor_CSTR

        ''' <summary>
        ''' Equations of its own with a single outlet, isothermal, adiabatic or at a set outlet temperature, for kinetic
        ''' and heterogeneous catalytic reactions whose rates are expressions (not scripts), in the mixture, the liquid or
        ''' the vapor: the outlet's moles are the inlet's plus what the reactions make in the reactor at the outlet's
        ''' state (the steady state the unit's own time stepping approaches), the outlet pressure, and the energy balance
        ''' with the heats of reaction, or the temperature. The rates are worked out from the outlet stream put at its
        ''' variables, as the unit works them out from its contents; their derivatives come from central differences on it.
        ''' </summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Try
                    Dim g = GraphicObject
                    If Not g.OutputConnectors(0).IsAttached OrElse g.OutputConnectors(1).IsAttached Then Return False
                    Select Case ReactorOperationMode
                        Case OperationMode.Isothermic, OperationMode.Adiabatic, OperationMode.OutletTemperature
                        Case Else
                            Return False
                    End Select
                    Dim any = False
                    For Each rxnsb In FlowSheet.ReactionSets(ReactionSetID).Reactions.Values
                        If Not rxnsb.IsActive Then Continue For
                        Dim rxn = FlowSheet.Reactions(rxnsb.ReactionID)
                        If rxn.ReactionType <> ReactionType.Kinetic AndAlso rxn.ReactionType <> ReactionType.Heterogeneous_Catalytic Then Continue For
                        If rxn.ReactionKinetics <> ReactionKinetics.Expression Then Return False
                        If rxn.ReactionPhase <> PhaseName.Mixture AndAlso rxn.ReactionPhase <> PhaseName.Liquid AndAlso rxn.ReactionPhase <> PhaseName.Vapor Then Return False
                        If Not rxn.Components.ContainsKey(rxn.BaseReactant) Then Return False
                        any = True
                    Next
                    Return any
                Catch ex As Exception
                    Return False
                End Try
            End Get
        End Property

        ''' <summary>
        ''' What the reactions make in the reactor at the state of a stream (mol/s of each compound) and the heat they
        ''' release (kW, last), as Calculate_Internal_1 works them out for a single outlet.
        ''' </summary>
        Private Function EOSource(ms As IMaterialStream) As Double()
            Dim mstr = CType(ms, MaterialStream)
            Dim names = mstr.Phases(0).Compounds.Keys.ToList()
            Dim nc = names.Count
            Dim src(nc) As Double
            Dim T = mstr.Phases(0).Properties.temperature.GetValueOrDefault()
            Dim Q = mstr.Phases(0).Properties.volumetric_flow.GetValueOrDefault()
            Dim QV = mstr.Phases(2).Properties.volumetric_flow.GetValueOrDefault()
            Dim QL = mstr.Phases(1).Properties.volumetric_flow.GetValueOrDefault()
            For Each rxnsb In FlowSheet.ReactionSets(ReactionSetID).Reactions.Values
                If Not rxnsb.IsActive Then Continue For
                Dim rxn As Reaction = FlowSheet.Reactions(rxnsb.ReactionID)
                If rxn.ReactionType <> ReactionType.Kinetic AndAlso rxn.ReactionType <> ReactionType.Heterogeneous_Catalytic Then Continue For
                Dim scBC = Math.Abs(rxn.Components(rxn.BaseReactant).StoichCoeff)
                Dim convfactors = GetConvFactors(rxn, mstr)
                Dim conc As New Dictionary(Of String, Double)
                Dim Qr As Double
                Select Case rxn.ReactionPhase
                    Case PhaseName.Liquid
                        Qr = QL / Q * Volume
                        For Each comp As Compound In mstr.Phases(1).Compounds.Values
                            conc(comp.Name) = comp.Molarity.GetValueOrDefault()
                        Next
                    Case PhaseName.Vapor
                        Qr = QV / Q * Volume
                        For Each comp As Compound In mstr.Phases(2).Compounds.Values
                            conc(comp.Name) = comp.Molarity.GetValueOrDefault()
                        Next
                    Case Else
                        Qr = If(MixtureUsesHeadspaceInSingleOutlet, Volume + Headspace, Volume)
                        For Each comp As Compound In mstr.Phases(0).Compounds.Values
                            conc(comp.Name) = comp.MolarFlow.GetValueOrDefault() / Q
                        Next
                End Select
                Dim rate, amount As Double
                If rxn.ReactionType = ReactionType.Kinetic Then
                    Dim kxf, kxr As Double
                    If rxn.ReactionKinFwdType = ReactionKineticType.Arrhenius Then
                        kxf = rxn.A_Forward * Math.Exp(-DWSIM.SharedClasses.SystemsOfUnits.Converter.Convert(rxn.E_Forward_Unit, "J/mol", rxn.E_Forward) / (8.314 * T))
                    Else
                        SetExpressionVariable(GetExpressionContext(rxn.ID), "T", T)
                        kxf = GetCompiledExpression(rxn.ID, rxn.ReactionKinFwdExpression).Evaluate()
                    End If
                    If rxn.ReactionKinRevType = ReactionKineticType.Arrhenius Then
                        kxr = rxn.A_Reverse * Math.Exp(-DWSIM.SharedClasses.SystemsOfUnits.Converter.Convert(rxn.E_Reverse_Unit, "J/mol", rxn.E_Reverse) / (8.314 * T))
                    Else
                        SetExpressionVariable(GetExpressionContext(rxn.ID), "T", T)
                        kxr = GetCompiledExpression(rxn.ID, rxn.ReactionKinRevExpression).Evaluate()
                    End If
                    If T < rxn.Tmin Or T > rxn.Tmax Then
                        kxf = 0.0
                        kxr = 0.0
                    End If
                    Dim rxf = 1.0, rxr = 1.0
                    For Each sb As ReactionStoichBase In rxn.Components.Values
                        rxf *= (conc(sb.CompName) * convfactors(sb.CompName)) ^ sb.DirectOrder
                        rxr *= (conc(sb.CompName) * convfactors(sb.CompName)) ^ sb.ReverseOrder
                    Next
                    rate = DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertToSI(rxn.VelUnit, kxf * rxf - kxr * rxr)
                    amount = Qr
                Else
                    If T < rxn.Tmin Or T > rxn.Tmax Then
                        rate = 0.0
                    Else
                        Dim context = GetExpressionContext(rxn.ID)
                        SetExpressionVariable(context, "T", T)
                        Dim ir = 1, ip = 1
                        For Each sb As ReactionStoichBase In rxn.Components.Values
                            If sb.StoichCoeff < 0 Then
                                SetExpressionVariable(context, "R" & ir.ToString(), conc(sb.CompName) * convfactors(sb.CompName))
                                ir += 1
                            ElseIf sb.StoichCoeff > 0 Then
                                SetExpressionVariable(context, "P" & ip.ToString(), conc(sb.CompName) * convfactors(sb.CompName))
                                ip += 1
                            End If
                        Next
                        Dim numval = GetCompiledExpression(rxn.ID, rxn.RateEquationNumerator).Evaluate()
                        Dim denmval = GetCompiledExpression(rxn.ID, rxn.RateEquationDenominator).Evaluate()
                        rate = DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertToSI(rxn.VelUnit, numval / denmval)
                    End If
                    amount = CatalystAmount
                End If
                For Each sb As ReactionStoichBase In rxn.Components.Values
                    src(names.IndexOf(sb.CompName)) += rate * sb.StoichCoeff / scBC * amount
                Next
                ' the heat released: ReactionHeat (kJ/kmol of base reactant) times the base reactant converted
                src(nc) += rxn.ReactionHeat * (rate / scBC * amount) * scBC * 0.001
            Next
            Return src
        End Function

        Private ReadOnly Property EOSourceName As String
            Get
                Return "CSTR source " & Name
            End Get
        End Property

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            Return {"PROP_CS_0"}
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
            Dim src = context.StreamProperties(outlet, x, EOSourceName, AddressOf EOSource)
            Dim ft = context.FlowTolerance(outlet)
            For i = 0 To c - 1
                r(row + i) = (x(outlet + 2 + i) - x(inlet + 2 + i) - src(i)) / ft
            Next
            r(row + c) = (x(outlet + 1) - (x(inlet + 1) - EOParameterValue(context, x, "PROP_CS_0", DeltaP.GetValueOrDefault()))) / context.PressureTolerance
            Select Case ReactorOperationMode
                Case OperationMode.Adiabatic
                    r(row + c + 1) = (context.Enthalpy(outlet, x) - context.Enthalpy(inlet, x) + src(c)) / context.EnergyTolerance(outlet)
                Case OperationMode.Isothermic
                    r(row + c + 1) = (x(outlet) - x(inlet)) / context.TemperatureTolerance
                Case Else
                    r(row + c + 1) = (x(outlet) - OutletTemperature) / context.TemperatureTolerance
            End Select
        End Sub

        Public Overrides Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean
            If Not EOHasEquations Then Return MyBase.EOJacobian(context, x, add)
            Dim c = context.CompoundCount
            Dim inlet = EOInletOffset(context, 0), outlet = EOOutletOffset(context, 0)
            Dim dsrc = context.StreamPropertiesDerivatives(outlet, x, EOSourceName, AddressOf EOSource)
            Dim ft = context.FlowTolerance(outlet)
            For i = 0 To c - 1
                add(i, outlet + 2 + i, 1.0 / ft)
                add(i, inlet + 2 + i, -1.0 / ft)
                For j = 0 To c + 1
                    If dsrc(i, j) <> 0.0 Then add(i, outlet + j, -dsrc(i, j) / ft)
                Next
            Next
            Dim tp = context.PressureTolerance
            add(c, outlet + 1, 1.0 / tp) : add(c, inlet + 1, -1.0 / tp)
            Dim kP = context.ParameterIndex(Me, "PROP_CS_0")
            If kP >= 0 Then add(c, kP, 1.0 / tp)
            Dim tt = context.TemperatureTolerance
            Select Case ReactorOperationMode
                Case OperationMode.Adiabatic
                    Dim te = context.EnergyTolerance(outlet)
                    Dim hout = context.EnthalpyDerivatives(outlet, x), hin = context.EnthalpyDerivatives(inlet, x)
                    For j = 0 To c + 1
                        add(c + 1, outlet + j, (hout(j) + dsrc(c, j)) / te)
                        add(c + 1, inlet + j, -hin(j) / te)
                    Next
                Case OperationMode.Isothermic
                    add(c + 1, outlet, 1.0 / tt) : add(c + 1, inlet, -1.0 / tt)
                Case Else
                    add(c + 1, outlet, 1.0 / tt)
            End Select
            Return True
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Dim c = context.CompoundCount
            If k < c Then Return "balance of compound " & k
            If k = c Then Return "pressure"
            Return If(ReactorOperationMode = OperationMode.Adiabatic, "energy balance", "temperature")
        End Function

    End Class

    Partial Public Class Reactor_PFR

        ''' <summary>
        ''' Equations of its own, isothermal, adiabatic, at a set outlet temperature or with the energy stream's duty spread
        ''' along the length, for kinetic and heterogeneous catalytic reactions whose rates are expressions, in the mixture,
        ''' the liquid or the vapor. The length is cut into cells whose boundaries are internal states of the system (T, P
        ''' and the compound flows), the last one the outlet. Each cell writes the balance of each compound by the
        ''' trapezoidal rule, (n_k - n_k-1) = dV/2 (r(k) + r(k-1)) with the rates per volume of the reactor's own
        ''' integration, the pressure drop of the reactor's own correlation by the same rule, and the energy balance from
        ''' the inlet to the cell's end with the enthalpies of formation, or the temperature. The rates and the pressure drop are worked out from the state
        ''' put on a stream; their derivatives come from central differences on it.
        ''' </summary>
        Public Overrides ReadOnly Property EOHasEquations As Boolean
            Get
                Try
                    Dim g = GraphicObject
                    If Not (g.InputConnectors(0).IsAttached AndAlso g.OutputConnectors(0).IsAttached AndAlso g.InputConnectors(1).IsAttached) Then Return False
                    Select Case ReactorOperationMode
                        Case OperationMode.Isothermic, OperationMode.Adiabatic, OperationMode.OutletTemperature, OperationMode.NonIsothermalNonAdiabatic
                        Case Else
                            Return False
                    End Select
                    If Not String.IsNullOrEmpty(ExternalSolverID) AndAlso FlowSheet.ExternalSolvers.ContainsKey(ExternalSolverID) Then Return False
                    Dim phase As ReactionPhase? = Nothing
                    For Each rxnsb In FlowSheet.ReactionSets(ReactionSetID).Reactions.Values
                        If Not rxnsb.IsActive Then Continue For
                        Dim rxn = FlowSheet.Reactions(rxnsb.ReactionID)
                        If rxn.ReactionType <> ReactionType.Kinetic AndAlso rxn.ReactionType <> ReactionType.Heterogeneous_Catalytic Then Continue For
                        If rxn.ReactionKinetics <> ReactionKinetics.Expression Then Return False
                        If rxn.ReactionPhase <> ReactionPhase.Mixture AndAlso rxn.ReactionPhase <> ReactionPhase.Liquid AndAlso rxn.ReactionPhase <> ReactionPhase.Vapor Then Return False
                        If Not rxn.Components.ContainsKey(rxn.BaseReactant) Then Return False
                        If phase.HasValue AndAlso phase.Value <> rxn.ReactionPhase Then Return False
                        phase = rxn.ReactionPhase
                    Next
                    Return phase.HasValue
                Catch ex As Exception
                    Return False
                End Try
            End Get
        End Property

        ''' <summary>The cells along the length: the reactor's own sections, at most 50.</summary>
        Private ReadOnly Property EOCells As Integer
            Get
                Dim d = If(dV > 0.0, dV, 0.01)
                Return Math.Max(1, Math.Min(50, CInt(Math.Round(1.0 / d))))
            End Get
        End Property

        ''' <summary>The reactions the cells write, active kinetic and heterogeneous catalytic ones.</summary>
        Private Function EOReactions() As List(Of Reaction)
            Dim list As New List(Of Reaction)
            For Each rxnsb In FlowSheet.ReactionSets(ReactionSetID).Reactions.Values
                If Not rxnsb.IsActive Then Continue For
                Dim rxn As Reaction = FlowSheet.Reactions(rxnsb.ReactionID)
                If rxn.ReactionType = ReactionType.Kinetic OrElse rxn.ReactionType = ReactionType.Heterogeneous_Catalytic Then list.Add(rxn)
            Next
            Return list
        End Function

        ''' <summary>The reactor's length and the diameter of each tube, from its volume and the one it sets.</summary>
        Private Sub EOGeometry(ByRef len As Double, ByRef diam As Double)
            If ReactorSizingType = SizingType.Length Then
                len = Length
                diam = (4 * Volume / NumberOfTubes / len / Math.PI) ^ 0.5
            Else
                diam = Diameter
                len = 4 * Volume / NumberOfTubes / Math.PI / diam ^ 2
            End If
        End Sub

        ''' <summary>
        ''' At the state of a stream: what the reactions make per volume of reactor (mol/(m3.s) of each compound), as ODEFunc
        ''' works it out, then the pressure drop of one cell (Pa), as Calculate_Internal works it out for a section.
        ''' </summary>
        Private Function EOCell(ms As IMaterialStream) As Double()
            Dim mstr = CType(ms, MaterialStream)
            Dim names = mstr.Phases(0).Compounds.Keys.ToList()
            Dim nc = names.Count
            Dim res(nc) As Double
            Dim T = mstr.Phases(0).Properties.temperature.GetValueOrDefault()
            Dim rxns = EOReactions()
            Dim hasHetCat = rxns.Any(Function(rx) rx.ReactionType = ReactionType.Heterogeneous_Catalytic)
            Dim Qf As Double
            Select Case rxns(0).ReactionPhase
                Case ReactionPhase.Vapor : Qf = mstr.Phases(2).Properties.volumetric_flow.GetValueOrDefault()
                Case ReactionPhase.Liquid : Qf = mstr.Phases(1).Properties.volumetric_flow.GetValueOrDefault()
                Case Else : Qf = mstr.Phases(0).Properties.volumetric_flow.GetValueOrDefault()
            End Select
            ' the whole stream in one phase, and not the one the reactions name
            If Not Qf > 0.0 Then
                For Each phaseid In New Integer() {2, 3, 4, 5, 6, 7}
                    If mstr.Phases(phaseid).Properties.molarfraction.GetValueOrDefault > 0.9999999 Then Qf = mstr.Phases(0).Properties.volumetric_flow.GetValueOrDefault()
                Next
            End If
            For Each rxn In rxns
                If Not Qf > 0.0 Then Exit For
                Dim scBC = Math.Abs(rxn.Components(rxn.BaseReactant).StoichCoeff)
                Dim convfactors = GetConvFactors(rxn, mstr)
                Dim conc As New Dictionary(Of String, Double)
                For Each sb As ReactionStoichBase In rxn.Components.Values
                    conc(sb.CompName) = Math.Max(mstr.Phases(0).Compounds(sb.CompName).MolarFlow.GetValueOrDefault(), 0.0) / Qf
                Next
                Dim rate As Double
                If rxn.ReactionType = ReactionType.Kinetic Then
                    Dim kxf, kxr As Double
                    If rxn.ReactionKinFwdType = ReactionKineticType.Arrhenius Then
                        kxf = rxn.A_Forward * Math.Exp(-DWSIM.SharedClasses.SystemsOfUnits.Converter.Convert(rxn.E_Forward_Unit, "J/mol", rxn.E_Forward) / (8.314 * T))
                    Else
                        SetExpressionVariable(GetExpressionContext(rxn.ID), "T", T)
                        kxf = GetCompiledExpression(rxn.ID, rxn.ReactionKinFwdExpression).Evaluate()
                    End If
                    If rxn.ReactionKinRevType = ReactionKineticType.Arrhenius Then
                        kxr = rxn.A_Reverse * Math.Exp(-DWSIM.SharedClasses.SystemsOfUnits.Converter.Convert(rxn.E_Reverse_Unit, "J/mol", rxn.E_Reverse) / (8.314 * T))
                    Else
                        SetExpressionVariable(GetExpressionContext(rxn.ID), "T", T)
                        kxr = GetCompiledExpression(rxn.ID, rxn.ReactionKinRevExpression).Evaluate()
                    End If
                    If T < rxn.Tmin Or T > rxn.Tmax Then
                        kxf = 0.0
                        kxr = 0.0
                    End If
                    ' (the integration zeroes both rates where any compound of the reaction is absent, a zero-order product
                    ' at the inlet too; here an absent compound of order zero leaves them, so that the rate is continuous)
                    Dim rxf = 1.0, rxr = 1.0
                    For Each sb As ReactionStoichBase In rxn.Components.Values
                        Dim cvar = conc(sb.CompName) * convfactors(sb.CompName)
                        If sb.DirectOrder <> 0.0 Then rxf *= cvar ^ sb.DirectOrder
                        If sb.ReverseOrder <> 0.0 Then rxr *= cvar ^ sb.ReverseOrder
                    Next
                    rate = DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertToSI(rxn.VelUnit, kxf * rxf - kxr * rxr)
                Else
                    If T < rxn.Tmin Or T > rxn.Tmax Then
                        rate = 0.0
                    Else
                        Dim context = GetExpressionContext(rxn.ID)
                        SetExpressionVariable(context, "T", T)
                        Dim ir = 1, ip = 1, ine = 1
                        For Each sb As ReactionStoichBase In rxn.Components.Values
                            Dim cvar = conc(sb.CompName) * convfactors(sb.CompName)
                            If sb.StoichCoeff < 0 Then
                                SetExpressionVariable(context, "R" & ir.ToString(), cvar)
                                ir += 1
                            ElseIf sb.StoichCoeff > 0 Then
                                SetExpressionVariable(context, "P" & ip.ToString(), cvar)
                                ip += 1
                            Else
                                SetExpressionVariable(context, "N" & ine.ToString(), cvar)
                                ine += 1
                            End If
                        Next
                        Dim numval = GetCompiledExpression(rxn.ID, rxn.RateEquationNumerator).Evaluate()
                        Dim denmval = GetCompiledExpression(rxn.ID, rxn.RateEquationDenominator).Evaluate()
                        rate = DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertToSI(rxn.VelUnit, numval / denmval)
                    End If
                    ' per kg of catalyst, over the volume the gas takes
                    rate *= CatalystLoading / CatalystVoidFraction
                End If
                For Each sb As ReactionStoichBase In rxn.Components.Values
                    res(names.IndexOf(sb.CompName)) += rate * sb.StoichCoeff / scBC
                Next
            Next
            res(nc) = EOCellPressureDrop(mstr, hasHetCat)
            Return res
        End Function

        ''' <summary>The pressure drop of one cell at the state of a stream, as Calculate_Internal takes it for a section.</summary>
        Private Function EOCellPressureDrop(ms As MaterialStream, hasHetCat As Boolean) As Double
            Dim n = EOCells
            If UseUserDefinedPressureDrop Then Return UserDefinedPressureDrop / n
            Dim len, diam As Double
            EOGeometry(len, diam)
            Dim L = len / n
            Dim rho = ms.Phases(0).Properties.density.GetValueOrDefault
            Dim Qlin = (ms.Phases(3).Properties.volumetric_flow.GetValueOrDefault + ms.Phases(4).Properties.volumetric_flow.GetValueOrDefault) / NumberOfTubes
            Dim Qsin = ms.Phases(7).Properties.volumetric_flow.GetValueOrDefault / NumberOfTubes
            Dim Qvin = ms.Phases(2).Properties.volumetric_flow.GetValueOrDefault / NumberOfTubes
            Dim rho_l = ms.Phases(1).Properties.density.GetValueOrDefault
            Dim eta_l = ms.Phases(1).Properties.viscosity.GetValueOrDefault
            Dim tens = ms.Phases(0).Properties.surfaceTension.GetValueOrDefault
            Dim rho_v = ms.Phases(2).Properties.density.GetValueOrDefault
            Dim eta_v = ms.Phases(2).Properties.viscosity.GetValueOrDefault
            Dim xv = ms.Phases(2).Properties.massfraction.GetValueOrDefault
            Dim xl = ms.Phases(1).Properties.massfraction.GetValueOrDefault
            If SlurryViscosityMode = 1 AndAlso Qlin > 0.0 Then
                Dim phi = Qsin / Qlin
                eta_l *= 1.0 + 3.0 * phi / (1.0 - phi / 0.52)
            End If
            Dim eta = eta_l * xl + eta_v * xv
            If CatalystLoading > 0.0 And hasHetCat Then
                ' Ergun, over a bed of catalyst
                Dim vel As Double = (Qlin + Qvin + Qsin) / (Math.PI * diam ^ 2 / 4)
                Dim dp As Double = CatalystParticleDiameter * CatalystParticleSphericity
                Dim ev As Double = CatalystVoidFraction
                Return 150 * eta * L / dp ^ 2 * (1 - ev) ^ 2 / ev ^ 3 * vel + 1.75 * L * rho / dp * (1 - ev) / ev ^ 3 * vel ^ 2
            End If
            ' the friction of Beggs and Brill
            Dim fpp As New FlowPackages.BeggsBrill
            Dim resv = fpp.CalculateDeltaP(diam, L, 0.0#, 0.000045, Qvin * 24 * 3600, (Qlin + Qsin) * 24 * 3600, eta_v * 1000, eta_l * 1000, rho_v, rho_l, tens)
            Return CDbl(resv(2))
        End Function

        ''' <summary>
        ''' The energy balances' tolerance over the unit's: a flash's enthalpy jumps by about the unit's tolerance under
        ''' steps of 1e-5 K (its own convergence), and each point's balance, from the inlet, reads two of them. Ten of
        ''' them leave each point within some 0.01 K, without adding up along the length.
        ''' </summary>
        Private Const EOEnergyTolerance As Double = 10.0

        Private ReadOnly Property EOCellName As String
            Get
                Return "PFR cell " & Name
            End Get
        End Property

        ''' <summary>The offset of the state at boundary k: the inlet at 0, the internal states, the outlet at the last.</summary>
        Private Function EOBoundary(context As IEquationOrientedContext, k As Integer) As Integer
            If k = 0 Then Return EOInletOffset(context, 0)
            If k = EOCells Then Return EOOutletOffset(context, 0)
            Return context.InternalStateOffset(Me, k - 1)
        End Function

        ''' <summary>Each compound's enthalpy of formation (kJ/mol), which the energy balance carries.</summary>
        Private Function EOFormation(context As IEquationOrientedContext) As Double()
            Dim ms = CType(GetInletMaterialStream(0), MaterialStream)
            Return context.CompoundNames.Select(Function(k)
                                                    Dim cp = ms.Phases(0).Compounds(k).ConstantProperties
                                                    Return cp.IG_Enthalpy_of_Formation_25C * cp.Molar_Weight / 1000.0
                                                End Function).ToArray()
        End Function

        Public Overrides Function EOInternalStateCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return 0
            Return EOCells - 1
        End Function

        ''' <summary>The profile of the reactor's last calculation at the boundary's place, or a line from the inlet to the outlet.</summary>
        Public Overrides Function EOInternalStateGuess(context As IEquationOrientedContext, k As Integer) As Double()
            Dim names = context.CompoundNames
            Dim c = names.Length
            Dim z = (k + 1.0) / EOCells
            Dim g(c + 1) As Double
            Dim prof = Profile
            If prof IsNot Nothing AndAlso prof.Count > 1 AndAlso prof.Last().Item1 > 0.0 Then
                Dim pos = z * prof.Last().Item1
                Dim j = 1
                While j < prof.Count - 1 AndAlso prof(j).Item1 < pos
                    j += 1
                End While
                Dim a = prof(j - 1), b = prof(j)
                Dim w = If(b.Item1 > a.Item1, Math.Min(Math.Max((pos - a.Item1) / (b.Item1 - a.Item1), 0.0), 1.0), 1.0)
                g(0) = a.Item2 + w * (b.Item2 - a.Item2)
                g(1) = a.Item3 + w * (b.Item3 - a.Item3)
                Dim complete = True
                For i = 0 To c - 1
                    Dim name = names(i)
                    Dim na = a.Item4.FirstOrDefault(Function(p) p.Compound = name)
                    Dim nb = b.Item4.FirstOrDefault(Function(p) p.Compound = name)
                    If na Is Nothing OrElse nb Is Nothing Then
                        complete = False
                    Else
                        g(2 + i) = na.MolarFlow + w * (nb.MolarFlow - na.MolarFlow)
                    End If
                Next
                If complete AndAlso g.All(Function(v) Not Double.IsNaN(v)) Then Return g
            End If
            Dim inlet = GetInletMaterialStream(0), outlet = GetOutletMaterialStream(0)
            Dim outOk = outlet.GetTemperature() > 0.0 AndAlso outlet.GetPressure() > 0.0
            Dim wz = If(outOk, z, 0.0)
            g(0) = inlet.GetTemperature() + wz * (outlet.GetTemperature() - inlet.GetTemperature())
            g(1) = inlet.GetPressure() + wz * (outlet.GetPressure() - inlet.GetPressure())
            For i = 0 To c - 1
                Dim ni = inlet.Phases(0).Compounds(names(i)).MolarFlow.GetValueOrDefault()
                Dim no = outlet.Phases(0).Compounds(names(i)).MolarFlow.GetValueOrDefault()
                g(2 + i) = ni + wz * (no - ni)
            Next
            Return g
        End Function

        Public Overrides Function EOParameters() As String()
            If Not EOHasEquations Then Return MyBase.EOParameters()
            ' the volume, when the pressure drop does not read the geometry
            Dim list As New List(Of String)
            If UseUserDefinedPressureDrop Then list.Add("PROP_PF_2")
            If ReactorOperationMode = OperationMode.OutletTemperature Then list.Add("PROP_PF_12")
            Return list.ToArray()
        End Function

        Public Overrides Function EOEquationCount(context As IEquationOrientedContext) As Integer
            If Not EOHasEquations Then Return MyBase.EOEquationCount(context)
            Return EOCells * (context.CompoundCount + 2)
        End Function

        Public Overrides Function EOVariables(context As IEquationOrientedContext) As Integer()
            If Not EOHasEquations Then Return MyBase.EOVariables(context)
            Dim offsets = Enumerable.Range(0, EOCells + 1).Select(Function(k) EOBoundary(context, k)).ToArray()
            Return EOStreamVariables(context, offsets, EOParameters())
        End Function

        ''' <summary>The energy stream's duty (kW), spread along the length in the non-isothermal, non-adiabatic mode.</summary>
        Private Function EODuty() As Double
            Dim es = GetInletEnergyStream(1)
            Return If(es Is Nothing, 0.0, es.EnergyFlow.GetValueOrDefault())
        End Function

        Public Overrides Sub EOResiduals(context As IEquationOrientedContext, x As Double(), r As Double(), row As Integer)
            If Not EOHasEquations Then
                MyBase.EOResiduals(context, x, r, row)
                Return
            End If
            Dim c = context.CompoundCount
            For k = 1 To EOCells
                Dim cell = EOCellResiduals(context, x, k)
                Array.Copy(cell, 0, r, row + (k - 1) * (c + 2), c + 2)
            Next
        End Sub

        ''' <summary>The equations of cell k (from boundary k-1 to k), each over its tolerance.</summary>
        Private Function EOCellResiduals(context As IEquationOrientedContext, x As Double(), k As Integer) As Double()
            Dim c = context.CompoundCount
            Dim n = EOCells
            Dim dVc = EOParameterValue(context, x, "PROP_PF_2", Volume) / n
            Dim outlet = EOOutletOffset(context, 0)
            Dim ft = context.FlowTolerance(outlet)
            Dim inlet = EOBoundary(context, 0)
            Dim a = EOBoundary(context, k - 1), b = EOBoundary(context, k)
            Dim prev = context.StreamProperties(a, x, EOCellName, AddressOf EOCell)
            Dim cur = context.StreamProperties(b, x, EOCellName, AddressOf EOCell)
            Dim r(c + 1) As Double
            For i = 0 To c - 1
                r(i) = (x(b + 2 + i) - x(a + 2 + i) - dVc / 2.0 * (cur(i) + prev(i))) / ft
            Next
            r(c) = (x(b + 1) - x(a + 1) + (cur(c) + prev(c)) / 2.0) / context.PressureTolerance
            Dim T0 = x(inlet)
            Select Case ReactorOperationMode
                Case OperationMode.Adiabatic, OperationMode.NonIsothermalNonAdiabatic
                    Dim hf = EOFormation(context)
                    Dim dhf = 0.0
                    For i = 0 To c - 1
                        dhf += hf(i) * (x(b + 2 + i) - x(inlet + 2 + i))
                    Next
                    Dim duty = If(ReactorOperationMode = OperationMode.NonIsothermalNonAdiabatic, EODuty(), 0.0)
                    r(c + 1) = (context.Enthalpy(b, x) - context.Enthalpy(inlet, x) + dhf - duty * k / n) / (EOEnergyTolerance * context.EnergyTolerance(outlet))
                Case OperationMode.Isothermic
                    r(c + 1) = (x(b) - T0) / context.TemperatureTolerance
                Case Else
                    Dim Tout = EOParameterValue(context, x, "PROP_PF_12", OutletTemperature)
                    r(c + 1) = (x(b) - (T0 + (Tout - T0) * k / n)) / context.TemperatureTolerance
            End Select
            Return r
        End Function

        ''' <summary>
        ''' The cells solved one after the other from the inlet x holds, each from the state before it: the implicit
        ''' trapezoidal rule marched along the length, so that the solve starts at the reactor's own profile for its inlet
        ''' (a reaction that runs away moves its front, which a profile kept from another inlet puts elsewhere). A cell
        ''' that does not solve keeps the guess it had, and so do the ones after it.
        ''' </summary>
        Public Overrides Function EOInitialize(context As IEquationOrientedContext, x As Double()) As Boolean
            If Not EOHasEquations Then Return False
            Dim c = context.CompoundCount
            For k = 1 To EOCells
                Dim a = EOBoundary(context, k - 1), b = EOBoundary(context, k)
                Dim kept = Enumerable.Range(0, c + 2).Select(Function(j) x(b + j)).ToArray()
                For j = 0 To c + 1
                    x(b + j) = x(a + j)
                Next
                Dim cell = k
                If Not EOSolveState(x, b, c + 2, Function(xv) EOCellResiduals(context, xv, cell)) Then
                    For j = 0 To c + 1
                        x(b + j) = kept(j)
                    Next
                    Exit For
                End If
            Next
            Return True
        End Function

        Public Overrides Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean
            If Not EOHasEquations Then Return MyBase.EOJacobian(context, x, add)
            Dim c = context.CompoundCount
            Dim n = EOCells
            Dim kV = context.ParameterIndex(Me, "PROP_PF_2")
            Dim kT = context.ParameterIndex(Me, "PROP_PF_12")
            Dim dVc = EOParameterValue(context, x, "PROP_PF_2", Volume) / n
            Dim hf = EOFormation(context)
            Dim outlet = EOOutletOffset(context, 0)
            Dim ft = context.FlowTolerance(outlet), te = context.EnergyTolerance(outlet)
            Dim tp = context.PressureTolerance, tt = context.TemperatureTolerance
            Dim inlet = EOBoundary(context, 0)
            Dim prev = context.StreamProperties(inlet, x, EOCellName, AddressOf EOCell)
            Dim dprev = context.StreamPropertiesDerivatives(inlet, x, EOCellName, AddressOf EOCell)
            Dim h0 As Double() = Nothing
            If ReactorOperationMode = OperationMode.Adiabatic OrElse ReactorOperationMode = OperationMode.NonIsothermalNonAdiabatic Then h0 = context.EnthalpyDerivatives(inlet, x)
            Dim tq = EOEnergyTolerance * te
            For k = 1 To n
                Dim a = EOBoundary(context, k - 1), b = EOBoundary(context, k)
                Dim cur = context.StreamProperties(b, x, EOCellName, AddressOf EOCell)
                Dim dcur = context.StreamPropertiesDerivatives(b, x, EOCellName, AddressOf EOCell)
                Dim e0 = (k - 1) * (c + 2)
                For i = 0 To c - 1
                    add(e0 + i, b + 2 + i, 1.0 / ft)
                    add(e0 + i, a + 2 + i, -1.0 / ft)
                    For j = 0 To c + 1
                        If dcur(i, j) <> 0.0 Then add(e0 + i, b + j, -dVc / 2.0 * dcur(i, j) / ft)
                        If dprev(i, j) <> 0.0 Then add(e0 + i, a + j, -dVc / 2.0 * dprev(i, j) / ft)
                    Next
                    If kV >= 0 Then add(e0 + i, kV, -(cur(i) + prev(i)) / (2.0 * n) / ft)
                Next
                add(e0 + c, b + 1, 1.0 / tp)
                add(e0 + c, a + 1, -1.0 / tp)
                For j = 0 To c + 1
                    If dcur(c, j) <> 0.0 Then add(e0 + c, b + j, dcur(c, j) / 2.0 / tp)
                    If dprev(c, j) <> 0.0 Then add(e0 + c, a + j, dprev(c, j) / 2.0 / tp)
                Next
                Select Case ReactorOperationMode
                    Case OperationMode.Adiabatic, OperationMode.NonIsothermalNonAdiabatic
                        Dim hcur = context.EnthalpyDerivatives(b, x)
                        For j = 0 To c + 1
                            add(e0 + c + 1, b + j, hcur(j) / tq)
                            add(e0 + c + 1, inlet + j, -h0(j) / tq)
                        Next
                        For i = 0 To c - 1
                            add(e0 + c + 1, b + 2 + i, hf(i) / tq)
                            add(e0 + c + 1, inlet + 2 + i, -hf(i) / tq)
                        Next
                    Case OperationMode.Isothermic
                        add(e0 + c + 1, b, 1.0 / tt)
                        add(e0 + c + 1, inlet, -1.0 / tt)
                    Case Else
                        add(e0 + c + 1, b, 1.0 / tt)
                        add(e0 + c + 1, inlet, -(1.0 - CDbl(k) / n) / tt)
                        If kT >= 0 Then add(e0 + c + 1, kT, -(CDbl(k) / n) / tt)
                End Select
                prev = cur
                dprev = dcur
            Next
            Return True
        End Function

        Public Overrides Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String
            If Not EOHasEquations Then Return MyBase.EOEquationName(context, k)
            Dim c = context.CompoundCount
            Dim cell = k \ (c + 2) + 1, j = k Mod (c + 2)
            Dim what = If(j < c, "balance of compound " & j, If(j = c, "pressure", If(ReactorOperationMode = OperationMode.Adiabatic OrElse ReactorOperationMode = OperationMode.NonIsothermalNonAdiabatic, "energy balance from the inlet", "temperature")))
            Return $"cell {cell} of {EOCells}: {what}"
        End Function

    End Class

End Namespace
