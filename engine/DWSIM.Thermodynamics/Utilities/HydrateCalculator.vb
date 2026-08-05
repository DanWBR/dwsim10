'    Natural Gas Hydrate Formation Conditions
'    Copyright 2009-2025 Daniel Wagner O. de Medeiros
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

Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Thermodynamics.Streams

Namespace Utilities.Hydrates

    Public Enum HydrateModel
        VanDerWaalsPlatteeuw = 0
        KlaudaSandler = 1
        ChenGuo = 2
        KlaudaSandlerModified = 3
    End Enum

    ''' <summary>Hydrate formation conditions for a stream at its current T and P.</summary>
    Public Class HydrateResults

        ''' <summary>Formation pressure at the stream temperature, Pa.</summary>
        Public Property FormationPressure As Double
        ''' <summary>Formation temperature at the stream pressure, K.</summary>
        Public Property FormationTemperature As Double

        ''' <summary>Structure that forms first at the stream temperature: sI or sII.</summary>
        Public Property StructureAtStreamTemperature As String = ""
        ''' <summary>Structure that forms first at the stream pressure: sI or sII.</summary>
        Public Property StructureAtStreamPressure As String = ""

        ''' <summary>Detailed results at the formation pressure, as returned by the model.</summary>
        Public Property DetailsAtStreamTemperature As Object()
        ''' <summary>Detailed results at the formation temperature, as returned by the model.</summary>
        Public Property DetailsAtStreamPressure As Object()

        Public Property StreamTemperature As Double
        Public Property StreamPressure As Double

        ''' <summary>True when the stream conditions are inside the hydrate formation region.</summary>
        Public ReadOnly Property FormsHydrate As Boolean
            Get
                Return StreamTemperature <= FormationTemperature OrElse StreamPressure >= FormationPressure
            End Get
        End Property

        ''' <summary>Structure that forms at the stream conditions, or an empty string.</summary>
        Public ReadOnly Property FormedStructure As String
            Get
                If StreamTemperature <= FormationTemperature Then Return StructureAtStreamTemperature
                If StreamPressure >= FormationPressure Then Return StructureAtStreamPressure
                Return ""
            End Get
        End Property

    End Class

    ''' <summary>
    ''' Drives the hydrate formation models and picks the structure that forms first. Shared by
    ''' the WinForms and the Avalonia hydrate utilities.
    ''' </summary>
    Public Class HydrateCalculator

        ''' <summary>The stream must contain water, otherwise there is no hydrate to form.</summary>
        Public Shared Function HasWater(mat As MaterialStream) As Boolean
            Return mat.PropertyPackage.RET_VCAS().Contains("7732-18-5")
        End Function

        ''' <summary>
        ''' Calculates the hydrate formation pressure and temperature of a stream at its current
        ''' conditions. <paramref name="vaporOnly"/> selects the vapor-hydrate equilibrium instead
        ''' of the full ice / liquid water / vapor / hydrate one.
        ''' </summary>
        Public Shared Function Calculate(mat As MaterialStream, model As HydrateModel,
                                         vaporOnly As Boolean) As HydrateResults

            mat.PropertyPackage.CurrentMaterialStream = mat

            If Not HasWater(mat) Then
                Throw New Exception("There is no water in the stream, so no hydrate can form.")
            End If

            Dim n As Integer = mat.Phases(0).Compounds.Count - 1
            Dim Vz(n) As Double
            Dim compoundNames(n) As String

            Dim i As Integer = 0
            For Each comp As Compound In mat.Phases(0).Compounds.Values
                Vz(i) = comp.MoleFraction.GetValueOrDefault
                compoundNames(i) = comp.Name
                i += 1
            Next

            Dim T = mat.Phases(0).Properties.temperature.GetValueOrDefault
            Dim P = mat.Phases(0).Properties.pressure.GetValueOrDefault

            Dim aux As New NaturalGasHydrates.AuxMethods
            Dim ids = aux.GetIDsForHydrateCalculation(compoundNames)

            Dim pform As Object() = Nothing, tform As Object() = Nothing
            Dim res As New HydrateResults With {.StreamTemperature = T, .StreamPressure = P}

            Select Case model

                Case HydrateModel.VanDerWaalsPlatteeuw

                    Dim hid As New NaturalGasHydrates.vdwP_PP(mat)
                    pform = hid.HYD_vdwP2(T, Vz, ids, vaporOnly)
                    tform = hid.HYD_vdwP2T(P, Vz, ids, vaporOnly)
                    PickStructures(res, pform, tform)
                    res.DetailsAtStreamPressure = hid.DET_HYD_vdwP(res.StructureAtStreamPressure, P, res.FormationTemperature, Vz, ids, vaporOnly)
                    res.DetailsAtStreamTemperature = hid.DET_HYD_vdwP(res.StructureAtStreamTemperature, res.FormationPressure, T, Vz, ids, vaporOnly)

                Case HydrateModel.KlaudaSandler

                    Dim hid As New NaturalGasHydrates.KlaudaSandler(mat)
                    pform = hid.HYD_KS2(T, Vz, ids, vaporOnly)
                    tform = hid.HYD_KS2T(P, Vz, ids, vaporOnly)
                    PickStructures(res, pform, tform)
                    res.DetailsAtStreamPressure = hid.DET_HYD_KS(res.StructureAtStreamPressure, P, res.FormationTemperature, Vz, ids, vaporOnly)
                    res.DetailsAtStreamTemperature = hid.DET_HYD_KS(res.StructureAtStreamTemperature, res.FormationPressure, T, Vz, ids, vaporOnly)

                Case HydrateModel.KlaudaSandlerModified

                    Dim hid As New NaturalGasHydrates.KlaudaSandlerMOD(mat)
                    pform = hid.HYD_KS2(T, Vz, ids, vaporOnly)
                    tform = hid.HYD_KS2T(P, Vz, ids, vaporOnly)
                    PickStructures(res, pform, tform)
                    If res.FormationTemperature > 0 Then
                        res.DetailsAtStreamPressure = hid.DET_HYD_KS(res.StructureAtStreamPressure, P, res.FormationTemperature, Vz, ids, vaporOnly)
                    End If
                    res.DetailsAtStreamTemperature = hid.DET_HYD_KS(res.StructureAtStreamTemperature, res.FormationPressure, T, Vz, ids, vaporOnly)

                Case HydrateModel.ChenGuo

                    Dim hid As New NaturalGasHydrates.ChenGuo(mat)
                    pform = hid.HYD_CG2(T, Vz, ids, vaporOnly)
                    tform = hid.HYD_CG2T(P, Vz, ids, vaporOnly)
                    PickStructures(res, pform, tform)
                    res.DetailsAtStreamPressure = hid.DET_HYD_CG(res.StructureAtStreamPressure, P, res.FormationTemperature, Vz, ids, vaporOnly)
                    res.DetailsAtStreamTemperature = hid.DET_HYD_CG(res.StructureAtStreamTemperature, res.FormationPressure, T, Vz, ids, vaporOnly)

            End Select

            Return res

        End Function

        ''' <summary>
        ''' Of the two structures, sI forms first at the lower pressure and at the higher
        ''' temperature. Index 0 is sI and index 1 is sII in both model outputs.
        ''' </summary>
        Private Shared Sub PickStructures(res As HydrateResults, pform As Object(), tform As Object())

            If pform(0) <= pform(1) Then
                res.StructureAtStreamTemperature = "sI"
                res.FormationPressure = pform(0)
            Else
                res.StructureAtStreamTemperature = "sII"
                res.FormationPressure = pform(1)
            End If

            If tform(0) >= tform(1) Then
                res.StructureAtStreamPressure = "sI"
                res.FormationTemperature = tform(0)
            Else
                res.StructureAtStreamPressure = "sII"
                res.FormationTemperature = tform(1)
            End If

        End Sub

    End Class

End Namespace
