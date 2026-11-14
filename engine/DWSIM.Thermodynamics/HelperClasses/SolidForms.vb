'    Solid forms (polymorphs) of a compound
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

Imports System.Math
Imports System.Linq
Imports DWSIM.Interfaces

Namespace PropertyPackages.Auxiliary

    ''' <summary>One crystalline form of a compound, with the fusion data that set its solubility.</summary>
    Public Class SolidForm

        ''' <summary>Name of the form, for instance "Form I".</summary>
        Public Property Name As String = ""

        ''' <summary>Melting point of the form, K.</summary>
        Public Property TemperatureOfFusion As Double

        ''' <summary>Enthalpy of fusion of the form at its melting point, kJ/mol.</summary>
        Public Property EnthalpyOfFusion As Double

        ''' <summary>Heat capacity of the liquid minus the one of this form at the melting point, J/[mol.K]. Zero when unknown.</summary>
        Public Property HeatCapacityDifference As Double

        ''' <summary>Density of the crystal, kg/m3. Zero when unknown.</summary>
        Public Property Density As Double

        ''' <summary>Where the data come from.</summary>
        Public Property Source As String = ""

        ''' <summary>
        ''' ln of the ratio between the fugacities of this form and of the pure subcooled liquid at T; zero at or above
        ''' the melting point of the form. The form with the lowest value is the stable one and the least soluble.
        ''' </summary>
        Public Function LnActivity(T As Double) As Double
            If TemperatureOfFusion <= 0.0 OrElse EnthalpyOfFusion <= 0.0 Then
                Throw New InvalidOperationException("The solid form '" & Name & "' does not have a melting point and an enthalpy of fusion.")
            End If
            If T >= TemperatureOfFusion Then Return 0.0
            Return SolidForms.LnActivityBelowMelting(Me, T)
        End Function

        ''' <summary>Enthalpy of fusion of the form at T, kJ/mol.</summary>
        Public Function EnthalpyOfFusionAt(T As Double) As Double
            Return EnthalpyOfFusion + HeatCapacityDifference / 1000.0 * (T - TemperatureOfFusion)
        End Function

        Public Function Clone() As SolidForm
            Return DirectCast(MemberwiseClone(), SolidForm)
        End Function

        Public Overrides Function ToString() As String
            Return Name
        End Function

    End Class

    ''' <summary>How two solid forms of a compound relate below their melting points.</summary>
    Public Class SolidFormPair

        Public FormA As SolidForm

        Public FormB As SolidForm

        ''' <summary>True when the stable form changes at a temperature below both melting points.</summary>
        Public Enantiotropic As Boolean

        ''' <summary>Temperature at which the two forms have the same solubility, K. NaN for a monotropic pair.</summary>
        Public TransitionTemperature As Double = Double.NaN

        ''' <summary>The form that is stable below the transition temperature, or over the whole range for a monotropic pair.</summary>
        Public StableAtLowTemperature As SolidForm

        ''' <summary>The form that is stable from the transition temperature to its melting point. The same as
        ''' <see cref="StableAtLowTemperature"/> for a monotropic pair.</summary>
        Public StableAtHighTemperature As SolidForm

    End Class

    ''' <summary>
    ''' The solid forms of a compound are kept as a list inside its ExtraProperties, which is saved with the flowsheet
    ''' and with the compound files. The fusion data of the compound itself (melting point, enthalpy of fusion and heat
    ''' capacity difference) are the ones every calculation reads; <see cref="Apply"/> copies a form into them.
    ''' </summary>
    Public Module SolidForms

        ''' <summary>ExtraProperties key with the list of forms, as JSON text.</summary>
        Public Const FormsKey As String = "SolidForms"

        ''' <summary>ExtraProperties key with the name of the form copied into the fusion data of the compound.</summary>
        Public Const ActiveFormKey As String = "ActiveSolidForm"

        Private Const R As Double = 8.31446

        Friend Function LnActivityBelowMelting(f As SolidForm, T As Double) As Double
            Dim Tf = f.TemperatureOfFusion
            Dim v = -f.EnthalpyOfFusion * 1000.0 / R / T * (1.0 - T / Tf)
            If f.HeatCapacityDifference <> 0.0 Then v -= f.HeatCapacityDifference / R * ((T - Tf) / T + Log(Tf / T))
            Return v
        End Function

        Private Function Extras(cp As ICompoundConstantProperties, create As Boolean) As IDictionary(Of String, Object)
            If cp.ExtraProperties Is Nothing Then
                If Not create Then Return Nothing
                cp.ExtraProperties = New Dynamic.ExpandoObject()
            End If
            Return DirectCast(cp.ExtraProperties, IDictionary(Of String, Object))
        End Function

        ''' <summary>The solid forms stored in the compound; an empty list when it does not have any.</summary>
        Public Function GetForms(cp As ICompoundConstantProperties) As List(Of SolidForm)
            Dim d = Extras(cp, False)
            Dim v As Object = Nothing
            If d Is Nothing OrElse Not d.TryGetValue(FormsKey, v) OrElse v Is Nothing Then Return New List(Of SolidForm)
            Dim text = TryCast(v, String)
            If text Is Nothing Then text = Newtonsoft.Json.JsonConvert.SerializeObject(v)
            If String.IsNullOrWhiteSpace(text) Then Return New List(Of SolidForm)
            Try
                Dim list = Newtonsoft.Json.JsonConvert.DeserializeObject(Of List(Of SolidForm))(text)
                Return If(list, New List(Of SolidForm))
            Catch ex As Exception
                Return New List(Of SolidForm)
            End Try
        End Function

        ''' <summary>Stores the solid forms in the compound. An empty list removes them.</summary>
        Public Sub SetForms(cp As ICompoundConstantProperties, forms As IEnumerable(Of SolidForm))
            Dim d = Extras(cp, True)
            Dim list = If(forms, Enumerable.Empty(Of SolidForm)()).ToList()
            Dim names As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each f In list
                If String.IsNullOrWhiteSpace(f.Name) Then Throw New ArgumentException("A solid form needs a name.")
                If Not names.Add(f.Name.Trim()) Then Throw New ArgumentException("There are two solid forms named '" & f.Name & "'.")
                If f.TemperatureOfFusion <= 0.0 OrElse f.EnthalpyOfFusion <= 0.0 Then
                    Throw New ArgumentException("The solid form '" & f.Name & "' needs a melting point and an enthalpy of fusion.")
                End If
            Next
            If list.Count = 0 Then
                d.Remove(FormsKey)
                d.Remove(ActiveFormKey)
                Return
            End If
            d(FormsKey) = Newtonsoft.Json.JsonConvert.SerializeObject(list)
            Dim active = GetActiveFormName(cp)
            If active <> "" AndAlso Not names.Contains(active) Then d.Remove(ActiveFormKey)
        End Sub

        ''' <summary>Name of the form whose data were copied into the compound; empty when none was.</summary>
        Public Function GetActiveFormName(cp As ICompoundConstantProperties) As String
            Dim d = Extras(cp, False)
            Dim v As Object = Nothing
            If d Is Nothing OrElse Not d.TryGetValue(ActiveFormKey, v) OrElse v Is Nothing Then Return ""
            Return Convert.ToString(v)
        End Function

        ''' <summary>The form with this name among the forms of the compound, or Nothing.</summary>
        Public Function FindForm(cp As ICompoundConstantProperties, name As String) As SolidForm
            If String.IsNullOrWhiteSpace(name) Then Return Nothing
            Return GetForms(cp).FirstOrDefault(Function(f) String.Equals(f.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))
        End Function

        ''' <summary>The fusion data of the compound itself, as a form.</summary>
        Public Function FromCompound(cp As ICompoundConstantProperties) As SolidForm
            Dim active = GetActiveFormName(cp)
            Return New SolidForm With {
                .Name = If(active <> "", active, cp.Name),
                .TemperatureOfFusion = cp.TemperatureOfFusion,
                .EnthalpyOfFusion = cp.EnthalpyOfFusionAtTf,
                .HeatCapacityDifference = cp.HeatCapacityDifferenceAtTf}
        End Function

        ''' <summary>
        ''' Copies the melting point, the enthalpy of fusion and the heat capacity difference of a form into the
        ''' compound, so that every solid-liquid calculation uses that form, and records its name.
        ''' </summary>
        Public Sub Apply(cp As ICompoundConstantProperties, form As SolidForm)
            If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))
            If form.TemperatureOfFusion <= 0.0 OrElse form.EnthalpyOfFusion <= 0.0 Then
                Throw New ArgumentException("The solid form '" & form.Name & "' needs a melting point and an enthalpy of fusion.")
            End If
            cp.TemperatureOfFusion = form.TemperatureOfFusion
            cp.EnthalpyOfFusionAtTf = form.EnthalpyOfFusion
            cp.HeatCapacityDifferenceAtTf = form.HeatCapacityDifference
            Extras(cp, True)(ActiveFormKey) = form.Name
        End Sub

        ''' <summary>
        ''' The stable form at T: the one with the lowest fugacity, which is also the least soluble. Forms at or above
        ''' their melting point do not count. Returns Nothing when every form has melted.
        ''' </summary>
        Public Function MostStable(forms As IEnumerable(Of SolidForm), T As Double) As SolidForm
            Dim best As SolidForm = Nothing
            Dim bestValue = 0.0
            For Each f In forms
                If T >= f.TemperatureOfFusion Then Continue For
                Dim v = LnActivityBelowMelting(f, T)
                If best Is Nothing OrElse v < bestValue Then
                    best = f
                    bestValue = v
                End If
            Next
            Return best
        End Function

        ''' <summary>
        ''' Ratio between the solubilities of two forms at T in the same solvent, taken with the same activity
        ''' coefficient for both, which holds while the solutions are dilute.
        ''' </summary>
        Public Function SolubilityRatio(a As SolidForm, b As SolidForm, T As Double) As Double
            Return Exp(a.LnActivity(T) - b.LnActivity(T))
        End Function

        ''' <summary>
        ''' Compares two forms between <paramref name="Tmin"/> and the lower of the two melting points. The pair is
        ''' enantiotropic when the difference between the fugacities changes sign in that range.
        ''' </summary>
        Public Function Compare(a As SolidForm, b As SolidForm, Optional Tmin As Double = 150.0) As SolidFormPair
            Dim res As New SolidFormPair With {.FormA = a, .FormB = b}
            Dim Thigh = Min(a.TemperatureOfFusion, b.TemperatureOfFusion)
            Dim d As Func(Of Double, Double) = Function(T) LnActivityBelowMelting(a, T) - LnActivityBelowMelting(b, T)
            'just below the lower melting point the form that melts higher is the stable one
            Dim upper = If(a.TemperatureOfFusion >= b.TemperatureOfFusion, a, b)
            Dim lower = If(upper Is a, b, a)
            res.StableAtHighTemperature = upper
            res.StableAtLowTemperature = upper
            If Tmin >= Thigh Then Return res
            Const steps As Integer = 400
            Dim t1 = Thigh * (1.0 - 0.000001), d1 = d(t1)
            If d1 = 0.0 Then Return res
            For k = 1 To steps
                Dim t0 = Thigh - (Thigh - Tmin) * k / steps
                Dim d0 = d(t0)
                If d0 = 0.0 OrElse (d0 > 0.0) <> (d1 > 0.0) Then
                    Dim lo = t0, hi = t1, dhi = d1
                    For it = 1 To 80
                        Dim m = 0.5 * (lo + hi), dm = d(m)
                        If (dm > 0.0) = (dhi > 0.0) Then
                            hi = m
                        Else
                            lo = m
                        End If
                        If hi - lo < 0.0001 Then Exit For
                    Next
                    res.Enantiotropic = True
                    res.TransitionTemperature = 0.5 * (lo + hi)
                    res.StableAtLowTemperature = lower
                    Return res
                End If
                t1 = t0
                d1 = d0
            Next
            Return res
        End Function

    End Module

End Namespace
