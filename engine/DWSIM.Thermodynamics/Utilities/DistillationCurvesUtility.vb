'    Distillation Curves Attached Utility
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
Imports DWSIM.Thermodynamics.Streams

Namespace Utilities.PetroleumProperties

    ''' <summary>
    ''' Distillation curves (TBP, ASTM D86, D1160, D2887 and EFV) and petroleum product properties of
    ''' the attached stream, with no user interface of its own. Both interfaces show it, and both save
    ''' the same data with the stream.
    ''' </summary>
    ''' <remarks>
    ''' Each curve point is a result named after the curve and the percent distilled ("D86 95%"), so
    ''' an Adjust, a specification, the spreadsheet or a column secondary specification can read it
    ''' from the stream as "&lt;utility name&gt;: D86 95%".
    ''' </remarks>
    Public Class DistillationCurvesUtility

        Implements IAttachedUtility

        Public Const KeyLiquidOnly As String = "Liquid Phase Only"
        Public Const KeyTBPBasis As String = "TBP Basis"
        Public Const KeyPercents As String = "Percents Distilled"
        Public Const KeyD1160Pressure As String = "D1160 Pressure"
        Public Const KeyEFVPressure As String = "EFV Pressure"
        Public Const KeyEFVMethod As String = "EFV Method"

        ''' <summary>The curve prefixes used in the result names, in curve type order.</summary>
        Public Shared ReadOnly CurvePrefixes As String() = {"TBP", "D86", "D1160", "D2887", "EFV"}

        ''' <summary>The product properties reported as temperatures.</summary>
        Public Shared ReadOnly TemperatureProperties As String() = {
            "Mean Average Boiling Point", "Flash Point (Closed Cup)", "Flash Point (Open Cup)",
            "Pour Point", "Cloud Point", "Freezing Point", "Aniline Point"}

        ''' <summary>The other product properties.</summary>
        Public Shared ReadOnly OtherProperties As String() = {
            "Molecular Weight", "Specific Gravity", "API Gravity", "Watson K",
            "Density @ 15 C", "Density @ 20 C", "Kinematic Viscosity @ 40 C", "Kinematic Viscosity @ 100 C",
            "Smoke Point", "Refraction Index", "Cetane Index (API)", "Cetane Index (D4737)",
            "True Vapor Pressure @ 100 F", "Reid Vapor Pressure", "Sulfur Content"}

        Public Property ID As Integer Implements IAttachedUtility.ID

        Public Property Name As String = "" Implements IAttachedUtility.Name

        Public Property AutoUpdate As Boolean = True Implements IAttachedUtility.AutoUpdate

        <Xml.Serialization.XmlIgnore>
        Public Property AttachedTo As ISimulationObject Implements IAttachedUtility.AttachedTo

        ''' <summary>The full result of the last update, for a window that draws the curves.</summary>
        <Xml.Serialization.XmlIgnore>
        Public Property LastResults As DistillationCurveResults

        Private ReadOnly Settings As New Dictionary(Of String, Object)
        Private ReadOnly Results As New Dictionary(Of String, Object)

        Public Sub New()
            Settings(KeyLiquidOnly) = False
            Settings(KeyTBPBasis) = 0
            Settings(KeyPercents) = "0, 5, 10, 30, 50, 70, 90, 95, 100"
            Settings(KeyD1160Pressure) = 1333.224
            Settings(KeyEFVPressure) = 101325.0
            Settings(KeyEFVMethod) = 0
            ResetResults()
        End Sub

        Public Function GetUtilityType() As FlowsheetUtility Implements IAttachedUtility.GetUtilityType
            Return FlowsheetUtility.DistillationCurves
        End Function

        Public Sub Initialize() Implements IAttachedUtility.Initialize
        End Sub

        Public Sub Populate() Implements IAttachedUtility.Populate
        End Sub

        ''' <summary>The percents distilled in the settings, sorted and limited to 0-100.</summary>
        Public Function GetPercents() As Double()
            Return ParsePercents(Settings(KeyPercents).ToString())
        End Function

        Public Shared Function ParsePercents(text As String) As Double()
            Dim list As New List(Of Double)
            For Each item In text.Split({","c, ";"c, " "c}, StringSplitOptions.RemoveEmptyEntries)
                Dim v As Double
                If Double.TryParse(item, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, v) AndAlso v >= 0 AndAlso v <= 100 Then
                    If Not list.Contains(v) Then list.Add(v)
                End If
            Next
            If list.Count = 0 Then list.AddRange({0, 5, 10, 30, 50, 70, 90, 95, 100})
            list.Sort()
            Return list.ToArray()
        End Function

        ''' <summary>The result name of a curve point, e.g. "D86 95%".</summary>
        Public Shared Function PointName(curveType As Integer, percent As Double) As String
            Return CurvePrefixes(curveType) & " " & percent.ToString("0.##", Globalization.CultureInfo.InvariantCulture) & "%"
        End Function

        ''' <summary>Reads a curve point name ("D86 95%") back into its curve and percent.</summary>
        Public Shared Function TryParsePointName(name As String, ByRef curveType As DistillationCurveType, ByRef percent As Double) As Boolean
            If String.IsNullOrWhiteSpace(name) OrElse Not name.EndsWith("%") Then Return False
            Dim parts = name.Substring(0, name.Length - 1).Split(" "c)
            If parts.Length <> 2 Then Return False
            Dim idx = Array.IndexOf(CurvePrefixes, parts(0))
            If idx < 0 Then Return False
            If Not Double.TryParse(parts(1), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, percent) Then Return False
            If percent < 0 OrElse percent > 100 Then Return False
            curveType = CType(idx, DistillationCurveType)
            Return True
        End Function

        ''' <summary>
        ''' The kind of unit a result takes: "temperature", "density", "cinematic_viscosity", "pressure",
        ''' "molecularWeight", or "" for a number without units in the unit systems.
        ''' </summary>
        Public Shared Function UnitKind(name As String) As String
            Dim ct As DistillationCurveType, pct As Double
            If TryParsePointName(name, ct, pct) Then Return "temperature"
            If TemperatureProperties.Contains(name) Then Return "temperature"
            Select Case name
                Case "Molecular Weight" : Return "molecularWeight"
                Case "Density @ 15 C", "Density @ 20 C" : Return "density"
                Case "Kinematic Viscosity @ 40 C", "Kinematic Viscosity @ 100 C" : Return "cinematic_viscosity"
                Case "True Vapor Pressure @ 100 F", "Reid Vapor Pressure" : Return "pressure"
                Case Else : Return ""
            End Select
        End Function

        ''' <summary>
        ''' A result in SI units (smoke point in mm, sulfur in wt %), from a calculation; NaN when the name is unknown.
        ''' </summary>
        Public Shared Function GetValueSI(res As DistillationCurveResults, name As String) As Double

            Dim ct As DistillationCurveType, pct As Double
            If TryParsePointName(name, ct, pct) Then
                Dim c As DistillationCurve = Nothing
                If res.Curves.TryGetValue(ct, c) Then Return c.TemperatureAt(pct)
                Return Double.NaN
            End If

            Dim pr = res.Properties
            Select Case name
                Case "Mean Average Boiling Point" : Return pr.MeanAverageBoilingPoint
                Case "Flash Point (Closed Cup)" : Return pr.FlashPointClosedCup
                Case "Flash Point (Open Cup)" : Return pr.FlashPointOpenCup
                Case "Pour Point" : Return pr.PourPoint
                Case "Cloud Point" : Return pr.CloudPoint
                Case "Freezing Point" : Return pr.FreezingPoint
                Case "Aniline Point" : Return pr.AnilinePoint
                Case "Molecular Weight" : Return pr.MolecularWeight
                Case "Specific Gravity" : Return pr.SpecificGravity
                Case "API Gravity" : Return pr.API
                Case "Watson K" : Return pr.WatsonK
                Case "Density @ 15 C" : Return pr.Density15C
                Case "Density @ 20 C" : Return pr.Density20C
                Case "Kinematic Viscosity @ 40 C" : Return pr.KinematicViscosity40C
                Case "Kinematic Viscosity @ 100 C" : Return pr.KinematicViscosity100C
                Case "Smoke Point" : Return pr.SmokePoint * 1000
                Case "Refraction Index" : Return pr.RefractionIndex
                Case "Cetane Index (API)" : Return pr.CetaneIndex
                Case "Cetane Index (D4737)" : Return pr.CetaneIndexD4737
                Case "True Vapor Pressure @ 100 F" : Return pr.TrueVaporPressure
                Case "Reid Vapor Pressure" : Return pr.ReidVaporPressure
                Case "Sulfur Content" : Return pr.SulfurWtPct
                Case Else : Return Double.NaN
            End Select

        End Function

        Private Sub ResetResults()
            Results.Clear()
            For c = 0 To CurvePrefixes.Length - 1
                For Each p In GetPercents()
                    Results(PointName(c, p)) = 0.0
                Next
            Next
            For Each p In TemperatureProperties : Results(p) = 0.0 : Next
            For Each p In OtherProperties : Results(p) = 0.0 : Next
        End Sub

        Private Function Units() As IUnitsOfMeasure
            Return AttachedTo?.GetFlowsheet()?.FlowsheetOptions?.SelectedUnitSystem
        End Function

        Private Shared Function UnitOf(su As IUnitsOfMeasure, kind As String) As String
            If su Is Nothing Then Return Nothing
            Select Case kind
                Case "temperature" : Return su.temperature
                Case "density" : Return su.density
                Case "cinematic_viscosity" : Return su.cinematic_viscosity
                Case "pressure" : Return su.pressure
                Case "molecularWeight" : Return su.molecularWeight
                Case Else : Return Nothing
            End Select
        End Function

        Public Function GetPropertyList() As List(Of String) Implements IAttachedUtility.GetPropertyList
            Dim list As New List(Of String) From {"Name", "AutoUpdate"}
            list.AddRange(Settings.Keys)
            list.AddRange(Results.Keys)
            Return list
        End Function

        Public Function GetPropertyValue(pname As String) As Object Implements IAttachedUtility.GetPropertyValue
            Select Case pname
                Case "Name" : Return Name
                Case "AutoUpdate" : Return AutoUpdate
            End Select
            If Settings.ContainsKey(pname) Then Return Settings(pname)
            If Results.ContainsKey(pname) Then Return Results(pname)
            Return ""
        End Function

        Public Sub SetPropertyValue(pname As String, pvalue As Object) Implements IAttachedUtility.SetPropertyValue
            Select Case pname
                Case "Name"
                    Name = pvalue.ToString()
                    Exit Sub
                Case "AutoUpdate"
                    AutoUpdate = Convert.ToBoolean(pvalue)
                    Exit Sub
            End Select
            ' the stored value keeps its type, so a number written by the serializer as a string still lands as a number
            If Settings.ContainsKey(pname) Then
                Try
                    Settings(pname) = Convert.ChangeType(pvalue, Settings(pname).GetType(), Globalization.CultureInfo.InvariantCulture)
                Catch
                    Settings(pname) = pvalue
                End Try
                If pname = KeyPercents Then ResetResults()
            End If
        End Sub

        Public Function GetPropertyUnits(pname As String) As String Implements IAttachedUtility.GetPropertyUnits
            Select Case pname
                Case KeyD1160Pressure, KeyEFVPressure : Return "Pa"
                Case "Smoke Point" : Return "mm"
                Case "Sulfur Content" : Return "%"
            End Select
            Return If(UnitOf(Units(), UnitKind(pname)), "")
        End Function

        Public Function SaveData() As Dictionary(Of String, Object) Implements IAttachedUtility.SaveData
            Dim data As New Dictionary(Of String, Object)
            For Each p In GetPropertyList()
                data(p) = GetPropertyValue(p)
            Next
            Return data
        End Function

        Public Sub LoadData(data As Dictionary(Of String, Object)) Implements IAttachedUtility.LoadData
            ' the percents first, so the results they name exist before anything else is read
            If data.ContainsKey(KeyPercents) Then SetPropertyValue(KeyPercents, data(KeyPercents))
            For Each item In data
                Try
                    SetPropertyValue(item.Key, item.Value)
                Catch
                End Try
            Next
        End Sub

        ''' <summary>The calculation settings from the stored values.</summary>
        Public Function GetSettings() As DistillationCurveSettings
            Return New DistillationCurveSettings With {
                .LiquidPhaseOnly = Convert.ToBoolean(Settings(KeyLiquidOnly)),
                .TBPBasis = CType(Convert.ToInt32(Settings(KeyTBPBasis)), DistillationCurveBasis),
                .Percents = GetPercents(),
                .D1160Pressure = Convert.ToDouble(Settings(KeyD1160Pressure), Globalization.CultureInfo.InvariantCulture),
                .EFVPressure = Convert.ToDouble(Settings(KeyEFVPressure), Globalization.CultureInfo.InvariantCulture),
                .EFVMethod = CType(Convert.ToInt32(Settings(KeyEFVMethod)), EFVCalculationMethod)}
        End Function

        Public Sub Update() Implements IAttachedUtility.Update

            Dim stream = TryCast(AttachedTo, MaterialStream)
            If stream Is Nothing Then Exit Sub

            Dim res = DistillationCurves.Calculate(stream, GetSettings())
            LastResults = res

            Dim su = Units()

            ResetResults()
            For Each key In Results.Keys.ToList()
                Dim v = GetValueSI(res, key)
                Dim unit = UnitOf(su, UnitKind(key))
                If unit IsNot Nothing AndAlso Not Double.IsNaN(v) Then v = SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(unit, v)
                Results(key) = v
            Next

        End Sub

    End Class

End Namespace
