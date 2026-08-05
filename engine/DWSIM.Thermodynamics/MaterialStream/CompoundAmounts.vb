Imports System.Linq
Imports DWSIM.Interfaces
Imports DWSIM.SharedClasses.SystemsOfUnits

Namespace Streams


''' <summary>
''' Reading and writing the compound amounts of a material stream on any of the bases the
''' editors offer. The editors only show what these methods return and hand back what the user
''' typed, so the WinForms and the cross-platform editors treat a basis change identically.
''' </summary>
Public Class CompoundAmounts

    ''' <summary>The bases, in the order the editors list them.</summary>
    Public Enum Basis
        MoleFractions = 0
        MassFractions = 1
        MoleFlows = 2
        MassFlows = 3
        StandardLiquidVolumeFractions = 4
        Molarities = 5
        Molalities = 6
    End Enum

    Public Shared ReadOnly BasisNames As String() = {
        "Mole Fractions",
        "Mass Fractions",
        "Mole Flows",
        "Mass Flows",
        "Standard Liquid Volume Flows",
        "Molarities",
        "Molalities"
    }

    ''' <summary>The unit of a basis, empty for the dimensionless ones.</summary>
    Public Shared Function Units(basis As Basis, su As IUnitsOfMeasure) As String

        Select Case basis
            Case Basis.MoleFlows
                Return su.molarflow
            Case Basis.MassFlows
                Return su.massflow
            Case Basis.Molarities
                Return "mol/L"
            Case Basis.Molalities
                Return "mol/kg solv."
            Case Else
                Return ""
        End Select

    End Function

    ''' <summary>
    ''' The amount of every compound of a phase on the given basis, in display units.
    ''' </summary>
    ''' <param name="asPercentage">Shows the two fraction bases as percentages.</param>
    Public Shared Function Read(stream As MaterialStream, phase As IPhase, basis As Basis,
                                su As IUnitsOfMeasure, Optional asPercentage As Boolean = False) As Dictionary(Of String, Double)

        Dim amounts As New Dictionary(Of String, Double)

        If phase Is Nothing OrElse phase.Compounds Is Nothing Then Return amounts

        Dim scale As Double = If(asPercentage, 100.0, 1.0)
        Dim W As Double = phase.Properties.massflow.GetValueOrDefault
        Dim Q As Double = phase.Properties.molarflow.GetValueOrDefault

        Select Case basis

            Case Basis.MoleFractions

                For Each c In phase.Compounds.Values
                    amounts.Add(c.Name, c.MoleFraction.GetValueOrDefault * scale)
                Next

            Case Basis.MassFractions

                For Each c In phase.Compounds.Values
                    amounts.Add(c.Name, c.MassFraction.GetValueOrDefault * scale)
                Next

            Case Basis.MoleFlows

                For Each c In phase.Compounds.Values
                    amounts.Add(c.Name, Converter.ConvertFromSI(su.molarflow, c.MoleFraction.GetValueOrDefault * Q))
                Next

            Case Basis.MassFlows

                For Each c In phase.Compounds.Values
                    amounts.Add(c.Name, Converter.ConvertFromSI(su.massflow, c.MassFraction.GetValueOrDefault * W))
                Next

            Case Basis.Molarities

                For Each c In phase.Compounds.Values
                    amounts.Add(c.Name, c.Molarity.GetValueOrDefault / 1000)
                Next

            Case Basis.Molalities

                For Each c In phase.Compounds.Values
                    amounts.Add(c.Name, c.Molality.GetValueOrDefault)
                Next

            Case Basis.StandardLiquidVolumeFractions

                Dim densities = LiquidDensities(stream, phase, 298.15)

                Dim totalvol As Double = 0.0
                Dim i As Integer = 0
                For Each c In phase.Compounds.Values
                    totalvol += c.MoleFraction.GetValueOrDefault * c.ConstantProperties.Molar_Weight / densities(i)
                    i += 1
                Next

                i = 0
                For Each c In phase.Compounds.Values
                    If totalvol > 0.0 Then
                        amounts.Add(c.Name, c.MoleFraction.GetValueOrDefault * c.ConstantProperties.Molar_Weight / densities(i) / totalvol)
                    Else
                        amounts.Add(c.Name, 0.0)
                    End If
                    i += 1
                Next

        End Select

        Return amounts

    End Function

    ''' <summary>
    ''' The amount of the phase itself on the given basis: what the editors show under the
    ''' compound grid as the phase total.
    ''' </summary>
    Public Shared Function PhaseTotal(stream As MaterialStream, phase As IPhase, basis As Basis,
                                      su As IUnitsOfMeasure, Optional asPercentage As Boolean = False) As Double

        If phase Is Nothing Then Return 0.0

        Dim scale As Double = If(asPercentage, 100.0, 1.0)

        Select Case basis

            Case Basis.MoleFractions

                If phase.Name = "Mixture" Then Return scale
                Return phase.Properties.molarfraction.GetValueOrDefault * scale

            Case Basis.MassFractions

                If phase.Name = "Mixture" Then Return scale
                Return phase.Properties.massfraction.GetValueOrDefault * scale

            Case Basis.MoleFlows

                Return Converter.ConvertFromSI(su.molarflow, phase.Properties.molarflow.GetValueOrDefault)

            Case Basis.MassFlows

                Return Converter.ConvertFromSI(su.massflow, phase.Properties.massflow.GetValueOrDefault)

            Case Basis.Molarities, Basis.Molalities

                Dim total As Double = 0.0
                For Each amount In Read(stream, phase, basis, su).Values
                    total += amount
                Next
                Return total

            Case Else

                Return Double.NaN

        End Select

    End Function

    ''' <summary>
    ''' Writes the amounts the user typed back into the overall phase of the stream, converting
    ''' them to the mole and mass fractions the engine works with and, for the flow bases, the
    ''' stream flow rates that go with them.
    ''' </summary>
    ''' <param name="referenceSolvent">Solvent of the molarity and molality bases.</param>
    Public Shared Sub Apply(stream As MaterialStream, basis As Basis,
                            amounts As Dictionary(Of String, Double),
                            su As IUnitsOfMeasure,
                            Optional referenceSolvent As String = "")

        Dim compounds = stream.Phases(0).Compounds
        Dim W, Q, mtotal, mmtotal As Double

        Select Case basis

            Case Basis.MoleFractions

                Normalize(amounts)
                For Each item In amounts
                    compounds(item.Key).MoleFraction = item.Value
                Next
                MassFractionsFromMoleFractions(compounds)

            Case Basis.MassFractions

                Normalize(amounts)
                For Each item In amounts
                    compounds(item.Key).MassFraction = item.Value
                Next
                MoleFractionsFromMassFractions(compounds)

            Case Basis.MoleFlows

                Dim total As Double = Sum(amounts)
                If total = 0.0 Then Exit Sub

                Q = Converter.ConvertToSI(su.molarflow, total)

                For Each item In amounts
                    compounds(item.Key).MoleFraction = item.Value / total
                Next

                mtotal = 0.0
                For Each c In compounds.Values
                    mtotal += c.MoleFraction.GetValueOrDefault * c.ConstantProperties.Molar_Weight
                Next

                W = 0.0
                For Each c In compounds.Values
                    c.MassFraction = c.MoleFraction.GetValueOrDefault * c.ConstantProperties.Molar_Weight / mtotal
                    W += c.MoleFraction.GetValueOrDefault * c.ConstantProperties.Molar_Weight / 1000 * Q
                Next

                stream.Phases(0).Properties.molarflow = Q
                stream.Phases(0).Properties.massflow = W

            Case Basis.MassFlows

                Dim total As Double = Sum(amounts)
                If total = 0.0 Then Exit Sub

                W = Converter.ConvertToSI(su.massflow, total)

                For Each item In amounts
                    compounds(item.Key).MassFraction = item.Value / total
                Next

                mmtotal = 0.0
                For Each c In compounds.Values
                    mmtotal += c.MassFraction.GetValueOrDefault / c.ConstantProperties.Molar_Weight
                Next

                Q = 0.0
                For Each c In compounds.Values
                    c.MoleFraction = c.MassFraction.GetValueOrDefault / c.ConstantProperties.Molar_Weight / mmtotal
                    Q += c.MassFraction.GetValueOrDefault * W / c.ConstantProperties.Molar_Weight * 1000
                Next

                stream.Phases(0).Properties.molarflow = Q
                stream.Phases(0).Properties.massflow = W

            Case Basis.StandardLiquidVolumeFractions

                ' densities at the standard temperature, or at the normal boiling point for
                ' whatever boils below it
                Dim densities = LiquidDensities(stream, stream.Phases(0), 273.15 + 15.56)

                mtotal = 0.0
                Dim i As Integer = 0
                For Each item In amounts
                    mtotal += item.Value * densities(i)
                    i += 1
                Next
                If mtotal = 0.0 Then Exit Sub

                i = 0
                For Each item In amounts
                    compounds(item.Key).MassFraction = item.Value * densities(i) / mtotal
                    i += 1
                Next

                MoleFractionsFromMassFractions(compounds)

            Case Basis.Molarities

                If referenceSolvent = "" Then Exit Sub

                Dim T As Double = stream.Phases(0).Properties.temperature.GetValueOrDefault
                Dim V As Double = stream.Phases(0).Properties.volumetric_flow.GetValueOrDefault * 1000 ' L

                Dim total As Double = 0.0
                Dim vs As Double = 0.0

                For Each item In amounts
                    If item.Key.Contains(referenceSolvent) Then Continue For
                    Dim c = compounds(item.Key)
                    total += item.Value * V ' mol
                    vs += item.Value * V * c.ConstantProperties.Molar_Weight / 1000 /
                          stream.PropertyPackage.AUX_LIQDENSi(c, T) * 1000
                Next

                Dim solvent = compounds(referenceSolvent)
                Dim solventamount As Double = (V - vs) / 1000 *
                    stream.PropertyPackage.AUX_LIQDENSi(solvent, T) / solvent.ConstantProperties.Molar_Weight * 1000 / V

                For Each key In amounts.Keys.ToList()
                    If key.Contains(referenceSolvent) Then amounts(key) = solventamount
                Next

                total += solventamount * V
                If total = 0.0 Then Exit Sub
                Q = total

                For Each item In amounts
                    compounds(item.Key).MoleFraction = item.Value * V / total
                Next

                MassFractionsFromMoleFractions(compounds)

                stream.Phases(0).Properties.molarflow = Q
                stream.Phases(0).Properties.massflow =
                    Q / 1000 * stream.PropertyPackage.AUX_MMM(PropertyPackages.Phase.Mixture)

                stream.ReferenceSolvent = referenceSolvent

            Case Basis.Molalities

                If referenceSolvent = "" Then Exit Sub

                W = stream.Phases(0).Properties.massflow.GetValueOrDefault

                Dim Ws As Double = 0.0
                For Each item In amounts
                    If item.Key.Contains(referenceSolvent) Then Continue For
                    ' total kg of solute per kg of solvent
                    Ws += item.Value * compounds(item.Key).ConstantProperties.Molar_Weight / 1000
                Next

                Dim solventamount As Double = W / (Ws + 1)
                Dim solvent = compounds(referenceSolvent)

                For Each key In amounts.Keys.ToList()
                    If key.Contains(referenceSolvent) Then
                        amounts(key) = 1000 / solvent.ConstantProperties.Molar_Weight
                    End If
                Next

                Q = 0.0
                For Each item In amounts
                    Q += item.Value * solventamount
                Next
                If Q = 0.0 Then Exit Sub

                For Each item In amounts
                    compounds(item.Key).MoleFraction = item.Value * solventamount / Q
                Next

                MassFractionsFromMoleFractions(compounds)

                stream.Phases(0).Properties.molarflow = Q

                stream.ReferenceSolvent = referenceSolvent

        End Select

    End Sub

    ''' <summary>True for the bases that need a reference solvent.</summary>
    Public Shared Function NeedsSolvent(basis As Basis) As Boolean
        Return basis = Basis.Molarities OrElse basis = Basis.Molalities
    End Function

    ' -------------------------------------------------------------------------

    ''' <summary>
    ''' Liquid density of every compound of the phase at the given temperature, falling back to
    ''' the normal boiling point for whatever boils below it.
    ''' </summary>
    Private Shared Function LiquidDensities(stream As MaterialStream, phase As IPhase, T As Double) As Double()

        Dim densities(phase.Compounds.Count - 1) As Double

        Dim pp As New PropertyPackages.RaoultPropertyPackage()
        pp.CurrentMaterialStream = stream

        Dim i As Integer = 0
        For Each c In phase.Compounds.Values
            Dim nbp As Double = c.ConstantProperties.Normal_Boiling_Point
            densities(i) = pp.AUX_LIQDENSi(c, If(T > nbp, nbp, T))
            i += 1
        Next

        Return densities

    End Function

    Private Shared Function Sum(amounts As Dictionary(Of String, Double)) As Double
        Dim total As Double = 0.0
        For Each amount In amounts.Values
            total += amount
        Next
        Return total
    End Function

    Private Shared Sub Normalize(amounts As Dictionary(Of String, Double))
        Dim total As Double = Sum(amounts)
        If total = 0.0 Then Exit Sub
        For Each key In amounts.Keys.ToList()
            amounts(key) /= total
        Next
    End Sub

    Private Shared Sub MassFractionsFromMoleFractions(compounds As Dictionary(Of String, ICompound))

        Dim mtotal As Double = 0.0
        For Each c In compounds.Values
            mtotal += c.MoleFraction.GetValueOrDefault * c.ConstantProperties.Molar_Weight
        Next
        If mtotal = 0.0 Then Exit Sub

        For Each c In compounds.Values
            c.MassFraction = c.MoleFraction.GetValueOrDefault * c.ConstantProperties.Molar_Weight / mtotal
        Next

    End Sub

    Private Shared Sub MoleFractionsFromMassFractions(compounds As Dictionary(Of String, ICompound))

        Dim mmtotal As Double = 0.0
        For Each c In compounds.Values
            mmtotal += c.MassFraction.GetValueOrDefault / c.ConstantProperties.Molar_Weight
        Next
        If mmtotal = 0.0 Then Exit Sub

        For Each c In compounds.Values
            c.MoleFraction = c.MassFraction.GetValueOrDefault / c.ConstantProperties.Molar_Weight / mmtotal
        Next

    End Sub

End Class

End Namespace
