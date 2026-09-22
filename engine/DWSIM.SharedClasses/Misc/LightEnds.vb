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

        ''' <summary>The boiling point of n-pentane, where an assay usually cuts its light ends off.</summary>
        Public Const PentaneNBP As Double = 309.2

        ''' <summary>
        ''' The boiling point of n-heptane. Above it a compound is curve material, whatever the sheet
        ''' calls it, and declaring it as a light end counts the same oil twice.
        ''' </summary>
        Public Const HeptaneNBP As Double = 371.6

        ''' <summary>Something the matters about a compound declared as a light end.</summary>
        Public Class Issue

            Public Property Compound As String
            ''' <summary>True when the characterization cannot go on with it.</summary>
            Public Property Blocking As Boolean
            Public Property Message As String

            Public Sub New(compound As String, blocking As Boolean, message As String)
                Me.Compound = compound
                Me.Blocking = blocking
                Me.Message = message
            End Sub

        End Class

        ''' <summary>
        ''' Checks that what was declared as a light end can be one. The light ends of an assay are the
        ''' compounds that boil below the front of the curve, methane through the pentanes; a heavier
        ''' compound named here is material the curve already carries, and counting it on both sides
        ''' inflates the crude.
        ''' </summary>
        ''' <param name="names">The compound names, in the order they were declared.</param>
        ''' <param name="nbp">The normal boiling point of each, K. Zero where it is not known.</param>
        ''' <param name="isPetroleumFraction">True for a pseudocomponent, which is never a light end.</param>
        Public Shared Function Validate(names As IList(Of String), nbp As IList(Of Double),
                                        isPetroleumFraction As IList(Of Boolean)) As List(Of Issue)

            Dim issues As New List(Of Issue)

            If names Is Nothing Then Return issues

            Dim seen As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

            For i = 0 To names.Count - 1

                Dim name = names(i)

                If seen.ContainsKey(name) Then
                    issues.Add(New Issue(name, True,
                        name & " is declared twice in the light ends. Give it one line with its total fraction."))
                Else
                    seen.Add(name, i)
                End If

                If isPetroleumFraction IsNot Nothing AndAlso i < isPetroleumFraction.Count AndAlso isPetroleumFraction(i) Then
                    issues.Add(New Issue(name, True,
                        name & " is a petroleum fraction. The light ends are the real compounds the assay " &
                        "reports beside the curve, and a pseudocomponent is the curve itself."))
                    Continue For
                End If

                Dim t = 0.0
                If nbp IsNot Nothing AndAlso i < nbp.Count Then t = nbp(i)

                If t <= 0.0 Then
                    issues.Add(New Issue(name, False,
                        "The normal boiling point of " & name & " is not in the database, so there is no " &
                        "saying whether it belongs with the light ends."))
                ElseIf t > HeptaneNBP Then
                    issues.Add(New Issue(name, True,
                        name & " boils at " & (t - 273.15).ToString("N1") & " C, well inside the range the " &
                        "distillation curve covers. Declaring it as a light end counts that material twice, " &
                        "once here and once in the cuts."))
                ElseIf t > PentaneNBP Then
                    issues.Add(New Issue(name, False,
                        name & " boils at " & (t - 273.15).ToString("N1") & " C, above the pentanes. Some " &
                        "assays do report the hexanes with the light ends; check that the curve does not " &
                        "cover it as well."))
                End If

            Next

            Return issues

        End Function

        ''' <summary>
        ''' The same check for a plus fraction rather than a distillation curve: the defined
        ''' composition of a reservoir fluid, measured compound by compound up to the point where the
        ''' plus fraction takes over.
        ''' </summary>
        ''' <remarks>
        ''' The boiling point rule of <see cref="Validate"/> is wrong here. A condensate analysis
        ''' routinely lists the heptanes, octanes and nonanes one by one before a C10+ fraction, and
        ''' those are not light ends by any reading; what makes a compound belong to the defined
        ''' composition is being lighter than the plus fraction itself. That line is the molar weight
        ''' of the lightest member of the plus fraction, which the tool already asks for.
        ''' </remarks>
        ''' <param name="names">The compound names, in the order they were declared.</param>
        ''' <param name="mw">The molar weight of each, kg/kmol. Zero where it is not known.</param>
        ''' <param name="isPetroleumFraction">True for a pseudocomponent, which is never a defined compound.</param>
        ''' <param name="plusFractionMW">Molar weight of the lightest member of the plus fraction.</param>
        Public Shared Function ValidateAgainstPlusFraction(names As IList(Of String), mw As IList(Of Double),
                                                           isPetroleumFraction As IList(Of Boolean),
                                                           plusFractionMW As Double) As List(Of Issue)

            Dim issues As New List(Of Issue)

            If names Is Nothing Then Return issues

            Dim seen As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

            For i = 0 To names.Count - 1

                Dim name = names(i)

                If seen.ContainsKey(name) Then
                    issues.Add(New Issue(name, True,
                        name & " is declared twice. Give it one line with its total fraction."))
                Else
                    seen.Add(name, i)
                End If

                If isPetroleumFraction IsNot Nothing AndAlso i < isPetroleumFraction.Count AndAlso isPetroleumFraction(i) Then
                    issues.Add(New Issue(name, True,
                        name & " is a petroleum fraction. The defined composition is the real compounds the " &
                        "analysis reports one by one, and a pseudocomponent is what the plus fraction is split into."))
                    Continue For
                End If

                Dim m = 0.0
                If mw IsNot Nothing AndAlso i < mw.Count Then m = mw(i)

                If m <= 0.0 Then
                    issues.Add(New Issue(name, False,
                        "The molar weight of " & name & " is not in the database, so there is no saying " &
                        "whether it sits below the plus fraction."))
                ElseIf plusFractionMW > 0.0 AndAlso m >= plusFractionMW Then
                    issues.Add(New Issue(name, True,
                        name & " has a molar weight of " & m.ToString("N1") & " kg/kmol, at or above the " &
                        plusFractionMW.ToString("N1") & " kg/kmol the plus fraction starts at. That material is " &
                        "already in the pseudocomponents, and declaring it here counts it twice."))
                End If

            Next

            Return issues

        End Function

        ''' <summary>
        ''' The compounds of the database that can be declared as light ends, lightest first: the ones
        ''' that boil below the front of a distillation curve. This is the list the editors put in
        ''' front of the user, so that a light end is picked rather than typed.
        ''' </summary>
        Public Shared Function Candidates(compounds As IEnumerable(Of Interfaces.ICompoundConstantProperties)) As List(Of String)

            If compounds Is Nothing Then Return New List(Of String)

            Return compounds.
                Where(Function(c) c IsNot Nothing AndAlso c.IsPF <> 1 AndAlso
                                  c.Normal_Boiling_Point > 0.0 AndAlso c.Normal_Boiling_Point <= HeptaneNBP).
                OrderBy(Function(c) c.Normal_Boiling_Point).
                Select(Function(c) c.Name).
                Distinct().
                ToList()

        End Function

        ''' <summary>
        ''' The compounds of the database that can sit below a plus fraction, lightest first: the ones
        ''' whose molar weight is under the weight the plus fraction starts at.
        ''' </summary>
        Public Shared Function CandidatesBelowPlusFraction(compounds As IEnumerable(Of Interfaces.ICompoundConstantProperties),
                                                           plusFractionMW As Double) As List(Of String)

            If compounds Is Nothing Then Return New List(Of String)

            Return compounds.
                Where(Function(c) c IsNot Nothing AndAlso c.IsPF <> 1 AndAlso c.Molar_Weight > 0.0 AndAlso
                                  (plusFractionMW <= 0.0 OrElse c.Molar_Weight < plusFractionMW)).
                OrderBy(Function(c) c.Molar_Weight).
                Select(Function(c) c.Name).
                Distinct().
                ToList()

        End Function

        ''' <summary>The blocking issues in one message, or an empty string when there are none.</summary>
        Public Shared Function BlockingMessage(issues As IEnumerable(Of Issue)) As String

            If issues Is Nothing Then Return ""

            Dim blocking = issues.Where(Function(x) x.Blocking).Select(Function(x) x.Message).ToList()
            If blocking.Count = 0 Then Return ""

            Return String.Join(" ", blocking)

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
