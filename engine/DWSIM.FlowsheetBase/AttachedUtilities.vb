'    Attached utilities without a user interface
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
Imports DWSIM.SharedClasses
Imports DWSIM.Thermodynamics.Streams

Namespace Utilities

    ''' <summary>
    ''' A utility attached to a flowsheet object, with no user interface of its own.
    ''' </summary>
    ''' <remarks>
    ''' In the Windows interface each of these is a window that also happens to implement
    ''' <see cref="IAttachedUtility"/>, so a host without those windows had no way to restore one
    ''' from a saved simulation: the utility, and the properties it contributes to its object,
    ''' silently disappeared. These carry the same names and the same stored data, so a simulation
    ''' moves between the two interfaces intact.
    ''' </remarks>
    Public MustInherit Class AttachedUtility

        Implements IAttachedUtility

        Public Property ID As Integer Implements IAttachedUtility.ID

        Public Property Name As String = "" Implements IAttachedUtility.Name

        Public Property AutoUpdate As Boolean = True Implements IAttachedUtility.AutoUpdate

        <Xml.Serialization.XmlIgnore>
        Public Property AttachedTo As ISimulationObject Implements IAttachedUtility.AttachedTo

        ''' <summary>The values the user sets, which are what a saved simulation carries.</summary>
        Protected ReadOnly Settings As New Dictionary(Of String, Object)

        ''' <summary>The values <see cref="Update"/> computes.</summary>
        Protected ReadOnly Results As New Dictionary(Of String, Object)

        Public MustOverride Function GetUtilityType() As FlowsheetUtility Implements IAttachedUtility.GetUtilityType

        Public Overridable Sub Initialize() Implements IAttachedUtility.Initialize
        End Sub

        ''' <summary>Refreshes the display. There is nothing to refresh without one.</summary>
        Public Overridable Sub Populate() Implements IAttachedUtility.Populate
        End Sub

        Public Overridable Sub Update() Implements IAttachedUtility.Update
        End Sub

        Public Overridable Function GetPropertyList() As List(Of String) Implements IAttachedUtility.GetPropertyList

            Dim list As New List(Of String) From {"Name", "AutoUpdate"}
            list.AddRange(Settings.Keys)
            list.AddRange(Results.Keys)
            Return list

        End Function

        Public Overridable Function GetPropertyValue(pname As String) As Object Implements IAttachedUtility.GetPropertyValue

            Select Case pname
                Case "Name" : Return Name
                Case "AutoUpdate" : Return AutoUpdate
            End Select

            If Settings.ContainsKey(pname) Then Return Settings(pname)
            If Results.ContainsKey(pname) Then Return Results(pname)

            Return ""

        End Function

        Public Overridable Sub SetPropertyValue(pname As String, pvalue As Object) Implements IAttachedUtility.SetPropertyValue

            Select Case pname
                Case "Name"
                    Name = pvalue.ToString()
                    Exit Sub
                Case "AutoUpdate"
                    AutoUpdate = Convert.ToBoolean(pvalue)
                    Exit Sub
            End Select

            ' the stored value keeps the type the utility declared, so a number written by the
            ' serializer as a string, or as a long, still lands as a number
            If Settings.ContainsKey(pname) Then
                Try
                    Settings(pname) = Convert.ChangeType(pvalue, Settings(pname).GetType())
                Catch
                    Settings(pname) = pvalue
                End Try
            ElseIf Results.ContainsKey(pname) Then
                ' a computed value is not restored: it is recalculated
            End If

        End Sub

        Public Overridable Function GetPropertyUnits(pname As String) As String Implements IAttachedUtility.GetPropertyUnits
            Return ""
        End Function

        Public Overridable Function SaveData() As Dictionary(Of String, Object) Implements IAttachedUtility.SaveData

            Dim data As New Dictionary(Of String, Object)
            For Each p In GetPropertyList()
                data(p) = GetPropertyValue(p)
            Next
            Return data

        End Function

        Public Overridable Sub LoadData(data As Dictionary(Of String, Object)) Implements IAttachedUtility.LoadData

            For Each item In data
                Try
                    SetPropertyValue(item.Key, item.Value)
                Catch
                End Try
            Next

        End Sub

        ''' <summary>The stream this utility works on, or Nothing if it is attached to something else.</summary>
        Protected Function GetStream() As MaterialStream
            Return TryCast(AttachedTo, MaterialStream)
        End Function

        Protected Function Units() As IUnitsOfMeasure
            Return AttachedTo?.GetFlowsheet()?.FlowsheetOptions?.SelectedUnitSystem
        End Function

    End Class

    ''' <summary>Phase envelope of the attached stream.</summary>
    Public Class PhaseEnvelopeUtility

        Inherits AttachedUtility

        Public Sub New()
            Settings("EnvelopeType") = 0
            Settings("EnvelopeSettings") = ""
            Results("Cricondentherm") = 0.0
            Results("Cricondenbar") = 0.0
            Results("Critical Pressure") = 0.0
            Results("Critical Temperature") = 0.0
            Results("Critical Volume") = 0.0
        End Sub

        Public Overrides Function GetUtilityType() As FlowsheetUtility
            Return FlowsheetUtility.PhaseEnvelope
        End Function

        Public Overrides Function GetPropertyUnits(pname As String) As String

            Dim su = Units()
            If su Is Nothing Then Return ""

            Select Case pname
                Case "Cricondentherm", "Critical Temperature" : Return su.temperature
                Case "Cricondenbar", "Critical Pressure" : Return su.pressure
                Case "Critical Volume" : Return su.molar_volume
                Case Else : Return ""
            End Select

        End Function

        Public Overrides Sub Update()

            Dim stream = GetStream()
            If stream Is Nothing Then Exit Sub

            Dim calc As New DWSIM.Thermodynamics.ShortcutUtilities.Calculation(stream) With {
                .CalcType = DWSIM.Thermodynamics.ShortcutUtilities.CalculationType.PhaseEnvelopePT
            }

            Dim res = calc.Calculate()
            If res Is Nothing OrElse res.ExceptionResult IsNot Nothing Then Exit Sub

            ' the cricondentherm is the warmest point of the dew curve and the cricondenbar the
            ' highest point of the bubble curve, as the Windows utility reads them
            If res.Data.ContainsKey("TD") AndAlso res.Data("TD").Count > 0 Then
                Results("Cricondentherm") = res.Data("TD").Max()
            End If

            Dim cricondenbar As Double = 0.0
            If res.Data.ContainsKey("PB") AndAlso res.Data("PB").Count > 0 Then cricondenbar = res.Data("PB").Max()
            If res.Data.ContainsKey("PD") AndAlso res.Data("PD").Count > 0 Then
                cricondenbar = Math.Max(cricondenbar, res.Data("PD").Max())
            End If
            Results("Cricondenbar") = cricondenbar

            If res.Data.ContainsKey("CP") AndAlso res.Data("CP").Count >= 3 Then
                Results("Critical Temperature") = res.Data("CP")(0)
                Results("Critical Pressure") = res.Data("CP")(1)
                Results("Critical Volume") = res.Data("CP")(2) * 1000.0
            End If

        End Sub

    End Class

    ''' <summary>Binary phase envelope of two of the compounds in the attached stream.</summary>
    Public Class BinaryEnvelopeUtility

        Inherits AttachedUtility

        Public Sub New()
            Settings("Comp1") = ""
            Settings("Comp2") = ""
            Settings("Type") = 0
            Settings("VLE") = True
            Settings("LLE") = False
            Settings("SLE") = False
            Settings("SLE_SS") = False
            Settings("CRIT") = False
            Settings("XAxisBase") = 0
            Settings("P") = 101325.0
            Settings("T") = 298.15
            Settings("dx") = 0.02
            Settings("PP") = ""
            Settings("CompareModels") = False
            Settings("ExpX") = ""
            Settings("ExpY") = ""
            Settings("ExpT") = ""
            Settings("ExpP") = ""
        End Sub

        Public Overrides Function GetUtilityType() As FlowsheetUtility
            Return FlowsheetUtility.PhaseEnvelopeBinary
        End Function

    End Class

    ''' <summary>Ternary liquid-liquid envelope of three of the compounds in the attached stream.</summary>
    Public Class TernaryEnvelopeUtility

        Inherits AttachedUtility

        Public Sub New()
            Settings("P") = 101325.0
            Settings("T") = 298.15
            Settings("Comp1") = ""
            Settings("Comp2") = ""
            Settings("Comp3") = ""
            Settings("PP") = ""
        End Sub

        Public Overrides Function GetUtilityType() As FlowsheetUtility
            Return FlowsheetUtility.PhaseEnvelopeTernary
        End Function

    End Class

    ''' <summary>Hydrate formation conditions for the attached stream.</summary>
    Public Class HydratesUtility

        Inherits AttachedUtility

        Public Overrides Function GetUtilityType() As FlowsheetUtility
            Return FlowsheetUtility.NaturalGasHydrates
        End Function

    End Class

    ''' <summary>True critical point of the attached stream.</summary>
    Public Class TrueCriticalPointUtility

        Inherits AttachedUtility

        Public Sub New()
            Results("Critical Pressure") = 0.0
            Results("Critical Temperature") = 0.0
            Results("Critical Volume") = 0.0
            Results("Critical Compressibility") = 0.0
        End Sub

        Public Overrides Function GetUtilityType() As FlowsheetUtility
            Return FlowsheetUtility.TrueCriticalPoint
        End Function

        Public Overrides Function GetPropertyUnits(pname As String) As String

            Dim su = Units()
            If su Is Nothing Then Return ""

            Select Case pname
                Case "Critical Temperature" : Return su.temperature
                Case "Critical Pressure" : Return su.pressure
                Case "Critical Volume" : Return su.molar_volume
                Case Else : Return ""
            End Select

        End Function

        Public Overrides Sub Update()

            Dim stream = GetStream()
            If stream Is Nothing Then Exit Sub

            Dim calc As New DWSIM.Thermodynamics.ShortcutUtilities.Calculation(stream) With {
                .CalcType = DWSIM.Thermodynamics.ShortcutUtilities.CalculationType.CriticalPoint
            }

            Dim res = calc.Calculate()
            If res Is Nothing OrElse res.ExceptionResult IsNot Nothing Then Exit Sub

            If res.Data.ContainsKey("CriticalPoint") AndAlso res.Data("CriticalPoint").Count >= 3 Then
                Dim cp = res.Data("CriticalPoint")
                Results("Critical Temperature") = cp(0)
                Results("Critical Pressure") = cp(1)
                Results("Critical Volume") = cp(2)

                Dim su = Units()
                If su IsNot Nothing Then
                    Dim tsi = SystemsOfUnits.Converter.ConvertToSI(su.temperature, cp(0))
                    Dim psi = SystemsOfUnits.Converter.ConvertToSI(su.pressure, cp(1))
                    Dim vsi = SystemsOfUnits.Converter.ConvertToSI(su.molar_volume, cp(2))
                    If tsi > 0.0 Then Results("Critical Compressibility") = psi * vsi / (8.314 * tsi)
                End If
            End If

        End Sub

    End Class

    ''' <summary>Pressure safety valve sizing for the attached stream.</summary>
    Public Class PSVSizingUtility

        Inherits AttachedUtility

        Public Sub New()
            Settings("OverPressure") = 10.0
            Settings("Kd") = 0.975
            Settings("Kb") = 1.0
            Settings("Kc") = 1.0
            Settings("Method") = 0
            Settings("RelievedFluid") = 0
        End Sub

        Public Overrides Function GetUtilityType() As FlowsheetUtility
            Return FlowsheetUtility.PSVSizing
        End Function

    End Class

    ''' <summary>Gas-liquid separator sizing for the attached stream.</summary>
    Public Class SeparatorSizingUtility

        Inherits AttachedUtility

        Public Sub New()
            Settings("L_D") = 3.0
            Settings("C") = 100.0
            Settings("Tres") = 5.0
            Settings("Fsurge") = 1.0
            Settings("Vmaxliq") = 1.0
            Settings("Vgi") = 75.0
            Settings("K") = 0.1
            Settings("Type") = 0
        End Sub

        Public Overrides Function GetUtilityType() As FlowsheetUtility
            Return FlowsheetUtility.SeparatorSizing
        End Function

    End Class

    ''' <summary>Cold flow properties of the attached petroleum stream.</summary>
    Public Class PetroleumColdFlowUtility

        Inherits AttachedUtility

        Public Sub New()
            For Each p In PropertyNames
                Results(p) = 0.0
            Next
        End Sub

        Private Shared ReadOnly PropertyNames As String() = {
            "Flash Point", "Pour Point", "Cloud Point", "Freezing Point",
            "Reid Vapor Pressure @ 100 F", "True Vapor Pressure @ 100 F",
            "Refraction Index @ 20 C", "Cetane Index",
            "Viscosity @ 100 F", "Viscosity @ 210 F"}

        Public Overrides Function GetUtilityType() As FlowsheetUtility
            Return FlowsheetUtility.PetroleumProperties
        End Function

        Public Overrides Function GetPropertyUnits(pname As String) As String

            Dim su = Units()
            If su Is Nothing Then Return ""

            Select Case pname
                Case "Flash Point", "Pour Point", "Cloud Point", "Freezing Point"
                    Return su.temperature
                Case "Reid Vapor Pressure @ 100 F", "True Vapor Pressure @ 100 F"
                    Return su.pressure
                Case "Viscosity @ 100 F", "Viscosity @ 210 F"
                    Return su.cinematic_viscosity
                Case Else
                    Return ""
            End Select

        End Function

        Public Overrides Sub Update()

            Dim stream = GetStream()
            If stream Is Nothing Then Exit Sub

            Dim res = DWSIM.Thermodynamics.Utilities.PetroleumProperties.ColdFlowProperties.Calculate(stream)
            If res Is Nothing Then Exit Sub

            Dim su = Units()

            Results("Flash Point") = Convert(su?.temperature, res.FlashPoint)
            Results("Pour Point") = Convert(su?.temperature, res.PourPoint)
            Results("Cloud Point") = Convert(su?.temperature, res.CloudPoint)
            Results("Freezing Point") = Convert(su?.temperature, res.FreezingPoint)
            Results("Reid Vapor Pressure @ 100 F") = Convert(su?.pressure, res.ReidVaporPressure)
            Results("True Vapor Pressure @ 100 F") = Convert(su?.pressure, res.TrueVaporPressure)
            Results("Refraction Index @ 20 C") = res.RefractionIndex
            Results("Cetane Index") = res.CetaneIndex
            ' the engine reports these at 37.8 and 98.9 C, which is 100 and 210 F
            Results("Viscosity @ 100 F") = Convert(su?.cinematic_viscosity, res.Viscosity37C)
            Results("Viscosity @ 210 F") = Convert(su?.cinematic_viscosity, res.Viscosity98C)

        End Sub

        Private Shared Function Convert(unit As String, value As Double) As Double
            If unit Is Nothing Then Return value
            Return SystemsOfUnits.Converter.ConvertFromSI(unit, value)
        End Function

    End Class

    ''' <summary>Pure compound properties of the compounds in the attached stream.</summary>
    Public Class PureCompoundPropertiesUtility

        Inherits AttachedUtility

        Public Overrides Function GetUtilityType() As FlowsheetUtility
            Return FlowsheetUtility.PureCompoundProperties
        End Function

    End Class

End Namespace
