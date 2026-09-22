'    Petroleum Assay Light Ends
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

Namespace Utilities.PetroleumCharacterization.Assay

    ''' <summary>
    ''' Puts the light ends of a crude assay together with the pseudocomponents cut from its
    ''' distillation curve.
    ''' </summary>
    ''' <remarks>
    ''' A crude assay reports the light ends apart from the curve: the curve is run on what is left
    ''' after they are stripped off, and the light ends come as a short list of real compounds,
    ''' methane through the pentanes, with a fraction of the whole crude beside each. They are a few
    ''' per cent, and they set the front end of the flash: characterizing the curve alone gives a
    ''' crude that will not make the gas it makes in the plant.
    '''
    ''' The arithmetic here is the part that is easy to get wrong by hand, because the two halves are
    ''' rarely reported in the same basis: the light ends usually in moles, the curve in liquid
    ''' volume. Everything below converts to mole fractions of the whole crude, which is what a
    ''' material stream wants.
    ''' </remarks>
    Public Class LightEnds

        Public Const MoleBasis As String = "Mole"
        Public Const MassBasis As String = "Mass"
        Public Const VolumeBasis As String = "Volume"

        ''' <summary>The basis names, in the order the editors list them.</summary>
        Public Shared ReadOnly Property Bases As String()
            Get
                Return New String() {MoleBasis, MassBasis, VolumeBasis}
            End Get
        End Property

        ''' <summary>
        ''' Combines the declared light ends with the pseudocomponents and returns the mole fractions
        ''' of the whole crude: <paramref name="lightMoleFractions"/> for the light ends and
        ''' <paramref name="pseudoMoleFractionsOut"/> for the cuts, together summing to one.
        ''' </summary>
        ''' <param name="lightFractions">Fraction of the whole crude of each light end, in <paramref name="basis"/>.</param>
        ''' <param name="lightMW">Molar weight of each light end, kg/kmol. Needed on the mass and volume bases.</param>
        ''' <param name="lightSG">Liquid specific gravity of each light end. Needed on the volume basis.</param>
        ''' <param name="basis">"Mole", "Mass" or "Volume".</param>
        ''' <param name="pseudoMoleFractions">Mole fractions WITHIN the pseudocomponent mixture, summing to one.</param>
        ''' <param name="pseudoMW">Molar weight of each cut, kg/kmol.</param>
        ''' <param name="pseudoSG">Specific gravity of each cut.</param>
        Public Shared Sub Combine(lightFractions As Double(), lightMW As Double(), lightSG As Double(),
                                  basis As String,
                                  pseudoMoleFractions As Double(), pseudoMW As Double(), pseudoSG As Double(),
                                  ByRef lightMoleFractions As Double(), ByRef pseudoMoleFractionsOut As Double())

            If lightFractions Is Nothing Then lightFractions = New Double() {}
            If pseudoMoleFractions Is Nothing OrElse pseudoMoleFractions.Length = 0 Then
                Throw New ArgumentException("There are no pseudocomponents to combine the light ends with.")
            End If

            Dim m = lightFractions.Length
            Dim n = pseudoMoleFractions.Length

            Dim total = TotalFraction(lightFractions)

            lightMoleFractions = New Double(Math.Max(m - 1, 0)) {}
            pseudoMoleFractionsOut = New Double(n - 1) {}

            If m = 0 Then
                Array.Copy(pseudoMoleFractions, pseudoMoleFractionsOut, n)
                Return
            End If

            ' Mole basis: the fractions already are what the stream wants, and the cuts share what is
            ' left of the crude in the proportions the curve gave them.
            If String.Equals(basis, MoleBasis, StringComparison.OrdinalIgnoreCase) Then
                For i = 0 To m - 1
                    lightMoleFractions(i) = lightFractions(i)
                Next
                For p = 0 To n - 1
                    pseudoMoleFractionsOut(p) = pseudoMoleFractions(p) * (1.0 - total)
                Next
                Return
            End If

            RequirePositive(lightMW, m, "molar weight", "light end")
            RequirePositive(pseudoMW, n, "molar weight", "pseudocomponent")

            ' Moles per unit of crude, on whichever basis that unit is: one kilogram on the mass
            ' basis, one cubic metre on the volume basis. Only ratios matter, so the density of water
            ' cancels out of the volume basis and the specific gravities can be used as they are.
            Dim moles(m - 1) As Double
            Dim pseudoMoles As Double

            If String.Equals(basis, MassBasis, StringComparison.OrdinalIgnoreCase) Then

                For i = 0 To m - 1
                    moles(i) = lightFractions(i) / lightMW(i)
                Next

                Dim mwmix = 0.0
                For p = 0 To n - 1
                    mwmix += pseudoMoleFractions(p) * pseudoMW(p)
                Next
                pseudoMoles = (1.0 - total) / mwmix

            ElseIf String.Equals(basis, VolumeBasis, StringComparison.OrdinalIgnoreCase) Then

                RequirePositive(lightSG, m, "specific gravity", "light end")
                RequirePositive(pseudoSG, n, "specific gravity", "pseudocomponent")

                For i = 0 To m - 1
                    moles(i) = lightFractions(i) * lightSG(i) / lightMW(i)
                Next

                ' the molar volume of the cut mixture: its mass over its density collapses to the sum
                ' of the molar volumes of the cuts, so the mixture density never has to be formed
                Dim molarvolume = 0.0
                For p = 0 To n - 1
                    molarvolume += pseudoMoleFractions(p) * pseudoMW(p) / pseudoSG(p)
                Next
                pseudoMoles = (1.0 - total) / molarvolume

            Else

                Throw New ArgumentException("Unknown light ends basis '" & basis &
                                            "'. It has to be Mole, Mass or Volume.")

            End If

            Dim totalmoles = pseudoMoles
            For i = 0 To m - 1
                totalmoles += moles(i)
            Next

            For i = 0 To m - 1
                lightMoleFractions(i) = moles(i) / totalmoles
            Next
            For p = 0 To n - 1
                pseudoMoleFractionsOut(p) = pseudoMoleFractions(p) * pseudoMoles / totalmoles
            Next

        End Sub

        ''' <summary>
        ''' The share of the crude the light ends take, measured in <paramref name="basis"/>, once
        ''' everything is combined. This is what says where on the curve the cuts have to start when
        ''' the curve was run on the whole crude and already covers the light ends.
        ''' </summary>
        Public Shared Function ShareInBasis(lightMoleFractions As Double(), lightMW As Double(), lightSG As Double(),
                                            pseudoMoleFractions As Double(), pseudoMW As Double(), pseudoSG As Double(),
                                            basis As String) As Double

            If lightMoleFractions Is Nothing OrElse lightMoleFractions.Length = 0 Then Return 0.0

            If String.Equals(basis, MoleBasis, StringComparison.OrdinalIgnoreCase) Then
                Return TotalFraction(lightMoleFractions)
            End If

            Dim light = 0.0, pseudo = 0.0

            If String.Equals(basis, MassBasis, StringComparison.OrdinalIgnoreCase) Then
                For i = 0 To lightMoleFractions.Length - 1
                    light += lightMoleFractions(i) * lightMW(i)
                Next
                For p = 0 To pseudoMoleFractions.Length - 1
                    pseudo += pseudoMoleFractions(p) * pseudoMW(p)
                Next
            ElseIf String.Equals(basis, VolumeBasis, StringComparison.OrdinalIgnoreCase) Then
                For i = 0 To lightMoleFractions.Length - 1
                    light += lightMoleFractions(i) * lightMW(i) / lightSG(i)
                Next
                For p = 0 To pseudoMoleFractions.Length - 1
                    pseudo += pseudoMoleFractions(p) * pseudoMW(p) / pseudoSG(p)
                Next
            Else
                Throw New ArgumentException("Unknown basis '" & basis & "'. It has to be Mole, Mass or Volume.")
            End If

            If light + pseudo <= 0.0 Then Return 0.0

            Return light / (light + pseudo)

        End Function

        ''' <summary>Sums the declared fractions and refuses a set that leaves no crude behind.</summary>
        Public Shared Function TotalFraction(fractions As Double()) As Double

            If fractions Is Nothing Then Return 0.0

            Dim total = 0.0
            For Each f In fractions
                If f < 0.0 Then Throw New ArgumentException("A light end cannot have a negative fraction.")
                total += f
            Next

            If total >= 1.0 Then
                Throw New ArgumentException(
                    "The light ends add up to " & (total * 100).ToString("N2") &
                    " % of the crude, which leaves nothing for the distillation curve. They are " &
                    "fractions of the whole crude, not of the light ends themselves.")
            End If

            Return total

        End Function

        ''' <summary>The basis name the curve of an assay is on, for the NBP type the assay carries.</summary>
        Public Shared Function CurveBasisName(curvebasis As Integer) As String
            Select Case curvebasis
                Case 1 : Return MoleBasis
                Case 2 : Return MassBasis
                Case Else : Return VolumeBasis
            End Select
        End Function

        Private Shared Sub RequirePositive(values As Double(), count As Integer, what As String, whose As String)
            If values Is Nothing OrElse values.Length < count Then
                Throw New ArgumentException("The " & what & " of every " & whose &
                                            " is needed to convert the light ends to mole fractions.")
            End If
            For i = 0 To count - 1
                If values(i) <= 0.0 Then
                    Throw New ArgumentException("The " & what & " of " & whose & " " & (i + 1).ToString() &
                                                " is missing, and it is needed to convert the light ends to mole fractions.")
                End If
            Next
        End Sub

    End Class

End Namespace
