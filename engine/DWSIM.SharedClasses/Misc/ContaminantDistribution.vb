Imports System.Collections.Generic
Imports System.Dynamic
Imports DWSIM.Interfaces

'    Petroleum Contaminant Distribution
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.

Namespace Utilities.PetroleumCharacterization.Contaminants

    ''' <summary>
    ''' Per-compound contaminant metadata stored in <c>ICompound.ExtraProperties</c>.
    ''' ConstantProperties is reserved for immutable pure-component data, so
    ''' stream-level / UO-mutated contaminants live on ExtraProperties instead.
    ''' </summary>
    Public Class CompoundContaminants

        Public Const K_WtPctSulfur As String = "WtPctSulfur"
        Public Const K_WtPctNitrogen As String = "WtPctNitrogen"
        Public Const K_MercaptanSulfurWtPct As String = "MercaptanSulfurWtPct"
        Public Const K_Ni_ppm_wt As String = "Ni_ppm_wt"
        Public Const K_V_ppm_wt As String = "V_ppm_wt"
        Public Const K_Fe_ppm_wt As String = "Fe_ppm_wt"
        Public Const K_Na_ppm_wt As String = "Na_ppm_wt"
        Public Const K_CCR_wt_pct As String = "CCR_wt_pct"
        Public Const K_AsphaltenesWtPct As String = "AsphaltenesWtPct"
        Public Const K_TAN_mgKOH_per_g As String = "TAN_mgKOH_per_g"

        Public Shared ReadOnly AllKeys As String() = New String() {
            K_WtPctSulfur, K_WtPctNitrogen, K_MercaptanSulfurWtPct,
            K_Ni_ppm_wt, K_V_ppm_wt, K_Fe_ppm_wt, K_Na_ppm_wt,
            K_CCR_wt_pct, K_AsphaltenesWtPct, K_TAN_mgKOH_per_g}

        Public Shared Function [Get](comp As ICompound, key As String) As Double
            If comp Is Nothing OrElse comp.ExtraProperties Is Nothing Then Return 0.0
            Dim d = DirectCast(comp.ExtraProperties, IDictionary(Of String, Object))
            Dim v As Object = Nothing
            If d.TryGetValue(key, v) AndAlso v IsNot Nothing Then
                Return Convert.ToDouble(v)
            End If
            Return 0.0
        End Function

        Public Shared Sub [Set](comp As ICompound, key As String, value As Double)
            If comp Is Nothing Then Return
            If comp.ExtraProperties Is Nothing Then comp.ExtraProperties = New ExpandoObject()
            Dim d = DirectCast(comp.ExtraProperties, IDictionary(Of String, Object))
            d(key) = value
        End Sub

        ''' <summary>
        ''' Copies all contaminant ExtraProperty values from source compound to target.
        ''' Missing keys on source default to zero; only writes keys that exist on source.
        ''' </summary>
        Public Shared Sub Propagate(source As ICompound, target As ICompound)
            If source Is Nothing OrElse target Is Nothing OrElse source.ExtraProperties Is Nothing Then Return
            Dim sd = DirectCast(source.ExtraProperties, IDictionary(Of String, Object))
            For Each k In AllKeys
                Dim v As Object = Nothing
                If sd.TryGetValue(k, v) AndAlso v IsNot Nothing Then
                    [Set](target, k, Convert.ToDouble(v))
                End If
            Next
        End Sub

    End Class

    ''' <summary>
    ''' Distributes bulk petroleum-assay contaminant totals (S, N, metals, CCR,
    ''' asphaltenes, TAN) across a set of pseudocomponents using NBP-based shape
    ''' functions, optionally overridden by per-NBP curves carried on the assay.
    ''' Results are written to <c>ICompound.ExtraProperties</c>.
    ''' </summary>
    Public Class ContaminantDistributor

        ''' <summary>
        ''' Applies contaminant attributes to each pseudocomponent's ExtraProperties in-place.
        ''' </summary>
        ''' <param name="pseudos">Pseudocomponent ICompound instances to decorate.</param>
        ''' <param name="massFractions">Mass fraction of each pseudo in the bulk crude (sums to 1). If null or mismatched, equal weighting is assumed.</param>
        ''' <param name="assay">Source assay with bulk totals and (optional) curves.</param>
        Public Shared Sub ApplyContaminants(
                pseudos As IList(Of ICompound),
                massFractions As IList(Of Double),
                assay As PetroleumCharacterization.Assay.Assay)

            If pseudos Is Nothing OrElse pseudos.Count = 0 OrElse assay Is Nothing Then Return

            Dim n As Integer = pseudos.Count
            Dim w(n - 1) As Double
            If massFractions IsNot Nothing AndAlso massFractions.Count = n Then
                For i = 0 To n - 1
                    w(i) = Math.Max(0.0, massFractions(i))
                Next
            Else
                For i = 0 To n - 1
                    w(i) = 1.0 / n
                Next
            End If
            Dim wsum As Double = 0.0
            For i = 0 To n - 1 : wsum += w(i) : Next
            If wsum <= 0.0 Then
                For i = 0 To n - 1 : w(i) = 1.0 / n : Next
            Else
                For i = 0 To n - 1 : w(i) /= wsum : Next
            End If

            ' Extract NBPs
            Dim nbp(n - 1) As Double
            For i = 0 To n - 1
                Dim cp = pseudos(i).ConstantProperties
                nbp(i) = cp.Normal_Boiling_Point
                If nbp(i) <= 0.0 Then nbp(i) = cp.NBP.GetValueOrDefault(0.0)
            Next

            ' ── Sulfur ────────────────────────────────────────────────────
            If assay.HasSulfurCurve AndAlso assay.PX IsNot Nothing AndAlso assay.PY_Sulfur IsNot Nothing AndAlso assay.PY_Sulfur.Count > 0 Then
                AssignFromCurve(pseudos, nbp, assay.PX, assay.PY_Sulfur, CompoundContaminants.K_WtPctSulfur)
            Else
                AssignByShape(pseudos, nbp, w, assay.BulkSulfurWtPct,
                              Function(t) SulfurShape(t),
                              CompoundContaminants.K_WtPctSulfur)
            End If

            ' ── Nitrogen ──────────────────────────────────────────────────
            If assay.HasNitrogenCurve AndAlso assay.PX IsNot Nothing AndAlso assay.PY_Nitrogen IsNot Nothing AndAlso assay.PY_Nitrogen.Count > 0 Then
                AssignFromCurve(pseudos, nbp, assay.PX, assay.PY_Nitrogen, CompoundContaminants.K_WtPctNitrogen)
            Else
                AssignByShape(pseudos, nbp, w, assay.BulkNitrogenWtPct,
                              Function(t) NitrogenShape(t),
                              CompoundContaminants.K_WtPctNitrogen)
            End If

            ' ── Mercaptan sulfur (light end) ──────────────────────────────
            AssignByShape(pseudos, nbp, w, assay.BulkMercaptanSulfurWtPct,
                          Function(t) MercaptanShape(t),
                          CompoundContaminants.K_MercaptanSulfurWtPct)

            ' ── Metals + CCR + asphaltenes (residue-dominated sigmoid) ────
            If assay.HasMetalsCurve AndAlso assay.PX IsNot Nothing Then
                If assay.PY_Ni IsNot Nothing AndAlso assay.PY_Ni.Count > 0 Then
                    AssignFromCurve(pseudos, nbp, assay.PX, assay.PY_Ni, CompoundContaminants.K_Ni_ppm_wt)
                End If
                If assay.PY_V IsNot Nothing AndAlso assay.PY_V.Count > 0 Then
                    AssignFromCurve(pseudos, nbp, assay.PX, assay.PY_V, CompoundContaminants.K_V_ppm_wt)
                End If
            Else
                AssignByShape(pseudos, nbp, w, assay.BulkNiPpm,
                              Function(t) ResidueShape(t),
                              CompoundContaminants.K_Ni_ppm_wt)
                AssignByShape(pseudos, nbp, w, assay.BulkVPpm,
                              Function(t) ResidueShape(t),
                              CompoundContaminants.K_V_ppm_wt)
            End If

            AssignByShape(pseudos, nbp, w, assay.BulkFePpm,
                          Function(t) ResidueShape(t),
                          CompoundContaminants.K_Fe_ppm_wt)
            AssignByShape(pseudos, nbp, w, assay.BulkNaPpm,
                          Function(t) ResidueShape(t),
                          CompoundContaminants.K_Na_ppm_wt)

            If assay.HasCCRCurve AndAlso assay.PX IsNot Nothing AndAlso assay.PY_CCR IsNot Nothing AndAlso assay.PY_CCR.Count > 0 Then
                AssignFromCurve(pseudos, nbp, assay.PX, assay.PY_CCR, CompoundContaminants.K_CCR_wt_pct)
            Else
                AssignByShape(pseudos, nbp, w, assay.BulkCCRWtPct,
                              Function(t) ResidueShape(t),
                              CompoundContaminants.K_CCR_wt_pct)
            End If

            AssignByShape(pseudos, nbp, w, assay.BulkAsphaltenesWtPct,
                          Function(t) ResidueShape(t),
                          CompoundContaminants.K_AsphaltenesWtPct)

            ' ── TAN: flat assignment (bulk value broadcast to all pseudos) ─
            If assay.BulkTAN > 0.0 Then
                For i = 0 To n - 1
                    CompoundContaminants.Set(pseudos(i), CompoundContaminants.K_TAN_mgKOH_per_g, assay.BulkTAN)
                Next
            End If

        End Sub

        ' Shape: sulfur - monotonic increasing from NBP ≈ 350 K
        Private Shared Function SulfurShape(t As Double) As Double
            If t <= 350.0 Then Return 0.0
            Dim x = (t - 350.0) / 500.0
            Return Math.Pow(x, 1.5)
        End Function

        ' Shape: nitrogen - monotonic, steeper than sulfur, onset ~400 K
        Private Shared Function NitrogenShape(t As Double) As Double
            If t <= 400.0 Then Return 0.0
            Dim x = (t - 400.0) / 500.0
            Return Math.Pow(x, 2.0)
        End Function

        ' Shape: metals / CCR / asphaltenes - sigmoid centred at ~540 °C (813 K)
        Private Shared Function ResidueShape(t As Double) As Double
            Dim z = (t - 813.15) / 30.0
            Return 1.0 / (1.0 + Math.Exp(-z))
        End Function

        ' Shape: mercaptan sulfur - reverse sigmoid around 200 °C (473 K)
        Private Shared Function MercaptanShape(t As Double) As Double
            Dim z = (t - 473.15) / 20.0
            Return 1.0 / (1.0 + Math.Exp(z))
        End Function

        Private Shared Sub AssignByShape(
                pseudos As IList(Of ICompound),
                nbp As Double(),
                w As Double(),
                bulk As Double,
                shape As Func(Of Double, Double),
                key As String)

            If bulk <= 0.0 Then Return
            Dim n = pseudos.Count
            Dim s(n - 1) As Double
            Dim denom As Double = 0.0
            For i = 0 To n - 1
                s(i) = shape(nbp(i))
                denom += w(i) * s(i)
            Next
            If denom <= 0.0 Then Return
            Dim k = bulk / denom
            For i = 0 To n - 1
                CompoundContaminants.Set(pseudos(i), key, k * s(i))
            Next
        End Sub

        Private Shared Sub AssignFromCurve(
                pseudos As IList(Of ICompound),
                nbp As Double(),
                px As ArrayList,
                py As ArrayList,
                key As String)

            If px Is Nothing OrElse py Is Nothing OrElse px.Count = 0 OrElse py.Count = 0 Then Return
            Dim m = Math.Min(px.Count, py.Count)
            Dim xs(m - 1) As Double
            Dim ys(m - 1) As Double
            For j = 0 To m - 1
                xs(j) = Convert.ToDouble(px(j))
                ys(j) = Convert.ToDouble(py(j))
            Next

            For i = 0 To pseudos.Count - 1
                CompoundContaminants.Set(pseudos(i), key, Interp(xs, ys, nbp(i)))
            Next
        End Sub

        Private Shared Function Interp(xs As Double(), ys As Double(), x As Double) As Double
            Dim m = xs.Length
            If m = 0 Then Return 0.0
            If x <= xs(0) Then Return ys(0)
            If x >= xs(m - 1) Then Return ys(m - 1)
            For j = 1 To m - 1
                If x <= xs(j) Then
                    Dim f = (x - xs(j - 1)) / (xs(j) - xs(j - 1))
                    Return ys(j - 1) + f * (ys(j) - ys(j - 1))
                End If
            Next
            Return ys(m - 1)
        End Function

    End Class

End Namespace
