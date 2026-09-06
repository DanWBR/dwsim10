'    Free-Radical Copolymerization CSTR - standalone steady-state solver (terminal model + moments)
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

Namespace Polymers

    ''' <summary>
    ''' Terminal-model kinetics for a binary free-radical copolymerization (monomers A and B). Propagation is
    ''' described by the two homo-propagation constants and the two reactivity ratios rA = kpAA/kpAB and
    ''' rB = kpBB/kpBA (so the cross constants are kpAB = kpAA/rA and kpBA = kpBB/rB). Each rate constant is
    ''' Arrhenius, k = A*exp(-E/(R*T)); the reactivity ratios are taken temperature-independent. Termination is
    ''' an average over the radical types.
    ''' </summary>
    Public Class CopolymerKinetics

        Public Ad As Double, Ed As Double            ' initiator decomposition (1/s)
        Public Efficiency As Double = 0.6
        Public ApAA As Double, EpAA As Double        ' homo-propagation of A (L/mol/s)
        Public ApBB As Double, EpBB As Double        ' homo-propagation of B (L/mol/s)
        Public ReactivityA As Double = 1.0           ' rA = kpAA / kpAB
        Public ReactivityB As Double = 1.0           ' rB = kpBB / kpBA
        Public Atc As Double, Etc As Double          ' termination by combination (average, L/mol/s)
        Public Atd As Double, Etd As Double          ' termination by disproportionation (average, L/mol/s)
        Public MonomerAMW As Double                  ' molar mass of monomer A (g/mol)
        Public MonomerBMW As Double                  ' molar mass of monomer B (g/mol)

        Public Shared Function Arrhenius(A As Double, E As Double, T As Double) As Double
            If A <= 0.0 Then Return 0.0
            Return A * Math.Exp(-E / (8.314 * T))
        End Function

        ''' <summary>
        ''' AIBN-initiated styrene (A) / methyl methacrylate (B) near 60 C, a classic copolymer pair:
        ''' reactivity ratios rA = 0.52, rB = 0.46; the IUPAC styrene and MMA propagation constants;
        ''' termination essentially by combination. Monomer molar masses 104.15 and 100.12 g/mol.
        ''' </summary>
        Public Shared Function StyreneMMA() As CopolymerKinetics
            Return New CopolymerKinetics With {
                .Ad = 1.58E+15, .Ed = 128000.0, .Efficiency = 0.6,
                .ApAA = 4.266E+7, .EpAA = 32510.0,
                .ApBB = 2.673E+6, .EpBB = 22360.0,
                .ReactivityA = 0.52, .ReactivityB = 0.46,
                .Atc = 1.255E+9, .Etc = 8000.0, .Atd = 0.0, .Etd = 0.0,
                .MonomerAMW = 104.15, .MonomerBMW = 100.12}
        End Function

    End Class

    ''' <summary>Steady-state result of a copolymer CSTR solve. Molar masses g/mol, concentrations mol/L.</summary>
    Public Class CopolymerCSTRResult
        Public Converged As Boolean
        Public ConversionA As Double            ' 1 - [A]/[A]in
        Public ConversionB As Double
        Public OverallConversion As Double      ' total monomer converted / total fed
        Public CopolymerCompositionA As Double  ' instantaneous mole fraction of A in the copolymer (Mayo-Lewis)
        Public Mn As Double
        Public Mw As Double
        Public PDI As Double
        Public Rp As Double                     ' total rate of polymerization (mol/L/s)
        Public MonomerAConc As Double           ' outlet [A]
        Public MonomerBConc As Double           ' outlet [B]
        Public InitiatorConc As Double          ' outlet [I]
        Public RadicalConc As Double            ' mu0
        Public Iterations As Integer
    End Class

    ''' <summary>
    ''' Standalone steady-state solver for a homogeneous, isothermal, binary free-radical copolymerization in a
    ''' perfectly mixed reactor. The two monomer balances are coupled through the radical composition and are
    ''' solved by successive substitution; the instantaneous copolymer composition follows the Mayo-Lewis
    ''' equation (recovered here from the monomer consumption rates), and the molar-mass averages come from the
    ''' method of moments with pseudo-kinetic propagation and termination constants and the average repeat-unit
    ''' mass. Transfer is neglected in this version. Concentrations mol/L, residence time seconds, temperature K.
    ''' </summary>
    Public Class CopolymerCSTR

        Public Shared Function Solve(kin As CopolymerKinetics, T As Double, ResidenceTime As Double,
                                     MonomerAFeed As Double, MonomerBFeed As Double, InitiatorFeed As Double,
                                     Optional gel As GelEffect = Nothing) As CopolymerCSTRResult

            Dim res As New CopolymerCSTRResult()
            Dim theta = ResidenceTime

            Dim kd = CopolymerKinetics.Arrhenius(kin.Ad, kin.Ed, T)
            Dim kpAA0 = CopolymerKinetics.Arrhenius(kin.ApAA, kin.EpAA, T)
            Dim kpBB0 = CopolymerKinetics.Arrhenius(kin.ApBB, kin.EpBB, T)
            Dim ktc0 = CopolymerKinetics.Arrhenius(kin.Atc, kin.Etc, T)
            Dim ktd0 = CopolymerKinetics.Arrhenius(kin.Atd, kin.Etd, T)
            Dim kt0 = ktc0 + ktd0
            Dim f = kin.Efficiency
            Dim rA = kin.ReactivityA, rB = kin.ReactivityB

            ' Initiator (first-order); the total radical concentration follows once the effective kt is known.
            Dim I = InitiatorFeed / (1.0 + kd * theta)
            res.InitiatorConc = I
            res.MonomerAConc = MonomerAFeed
            res.MonomerBConc = MonomerBFeed

            If kt0 <= 0.0 OrElse I <= 0.0 OrElse kd <= 0.0 Then
                res.Converged = True
                res.PDI = 1.0
                Return res
            End If

            ' Gel (Trommsdorff) and glass factors scale termination and propagation as conversion builds; the
            ' reactivity ratios are preserved (the glass factor scales every propagation equally). They depend on
            ' the conversion, which depends on them, so an outer fixed point resolves them around the coupled
            ' monomer balances; with no gel model both factors are 1 and the outer loop runs exactly once.
            Dim gelActive = (gel IsNot Nothing AndAlso gel.IsActive)
            Dim gt As Double = 1.0, gp As Double = 1.0
            Dim kpAA = kpAA0, kpBB = kpBB0, kpAB As Double = 0.0, kpBA As Double = 0.0
            Dim ktc = ktc0, ktd = ktd0, kt = kt0
            Dim mu0 As Double = 0.0
            Dim A = MonomerAFeed, B = MonomerBFeed
            Dim phiA As Double = 0.5, cA As Double = 0.0, cB As Double = 0.0
            Dim it As Integer = 0

            For outer As Integer = 1 To If(gelActive, 100, 1)
                kpAA = kpAA0 * gp : kpBB = kpBB0 * gp
                kpAB = If(rA > 0.0, kpAA / rA, kpAA * 1.0E+6)           ' cross constants from the reactivity ratios
                kpBA = If(rB > 0.0, kpBB / rB, kpBB * 1.0E+6)
                ktc = ktc0 * gt : ktd = ktd0 * gt : kt = ktc + ktd
                mu0 = Math.Sqrt(f * kd * I / kt)

                ' Couple the two monomer balances through the radical composition (successive substitution).
                A = MonomerAFeed : B = MonomerBFeed
                For it = 1 To 200
                    Dim denom = kpBA * A + kpAB * B
                    phiA = If(denom > 0.0, kpBA * A / denom, 0.5)       ' fraction of A-ended radicals
                    Dim phiB = 1.0 - phiA
                    cA = mu0 * (kpAA * phiA + kpBA * phiB)              ' pseudo first-order consumption of A
                    cB = mu0 * (kpBB * phiB + kpAB * phiA)
                    Dim Anew = MonomerAFeed / (1.0 + theta * cA)
                    Dim Bnew = MonomerBFeed / (1.0 + theta * cB)
                    Dim change = Math.Abs(Anew - A) + Math.Abs(Bnew - B)
                    A = 0.5 * Anew + 0.5 * A
                    B = 0.5 * Bnew + 0.5 * B
                    If change < 1.0E-12 * (MonomerAFeed + MonomerBFeed + 1.0E-30) Then Exit For
                Next

                If Not gelActive Then Exit For
                Dim Xo = ((MonomerAFeed - A) + (MonomerBFeed - B)) / (MonomerAFeed + MonomerBFeed)
                Dim gtn = gel.TerminationFactor(Xo), gpn = gel.PropagationFactor(Xo)
                Dim conv = (Math.Abs(gtn - gt) + Math.Abs(gpn - gp)) < 1.0E-10
                gt = gtn : gp = gpn
                If conv Then Exit For
            Next
            res.RadicalConc = mu0
            res.Iterations = it

            ' Recompute the rates at the converged composition.
            Dim d2 = kpBA * A + kpAB * B
            phiA = If(d2 > 0.0, kpBA * A / d2, 0.5)
            cA = mu0 * (kpAA * phiA + kpBA * (1.0 - phiA))
            cB = mu0 * (kpBB * (1.0 - phiA) + kpAB * phiA)

            Dim consumeA = A * cA   ' mol/L/s of A incorporated
            Dim consumeB = B * cB
            Dim Rp = consumeA + consumeB
            res.Rp = Rp

            res.MonomerAConc = A
            res.MonomerBConc = B
            res.ConversionA = If(MonomerAFeed > 0.0, 1.0 - A / MonomerAFeed, 0.0)
            res.ConversionB = If(MonomerBFeed > 0.0, 1.0 - B / MonomerBFeed, 0.0)
            Dim fedTotal = MonomerAFeed + MonomerBFeed
            res.OverallConversion = If(fedTotal > 0.0, ((MonomerAFeed - A) + (MonomerBFeed - B)) / fedTotal, 0.0)

            ' Instantaneous copolymer composition (equals the Mayo-Lewis equation).
            res.CopolymerCompositionA = If(Rp > 0.0, consumeA / Rp, 0.0)

            ' Molar-mass averages by the method of moments with pseudo-kinetic constants and the average
            ' repeat-unit mass. Transfer is neglected, so the propagation probability uses termination only.
            Dim M = A + B
            Dim kpBar = If(M > 0.0 AndAlso mu0 > 0.0, Rp / (mu0 * M), 0.0)
            Dim Mbar = res.CopolymerCompositionA * kin.MonomerAMW + (1.0 - res.CopolymerCompositionA) * kin.MonomerBMW

            Dim stopRate = kt * mu0
            If M <= 0.0 OrElse stopRate <= 0.0 OrElse kpBar <= 0.0 Then
                res.Converged = False
                Return res
            End If
            Dim alpha = kpBar * M / (kpBar * M + stopRate)
            Dim oneMinusAlpha = 1.0 - alpha
            If oneMinusAlpha <= 0.0 Then
                res.Converged = False
                Return res
            End If

            Dim mu1 = mu0 / oneMinusAlpha
            Dim mu2 = mu0 * (1.0 + alpha) / (oneMinusAlpha * oneMinusAlpha)
            Dim G0 = (ktd + 0.5 * ktc) * mu0 * mu0
            Dim G1 = kt * mu0 * mu1
            Dim G2 = kt * mu0 * mu2 + ktc * mu1 * mu1
            Dim L0 = theta * G0, L1 = theta * G1, L2 = theta * G2

            If L0 > 0.0 AndAlso L1 > 0.0 Then
                res.Mn = Mbar * L1 / L0
                res.Mw = Mbar * L2 / L1
                res.PDI = L0 * L2 / (L1 * L1)
                res.Converged = True
            Else
                res.Converged = False
            End If

            Return res

        End Function

        ''' <summary>
        ''' The instantaneous copolymer composition (mole fraction of A) from the Mayo-Lewis equation, for a
        ''' given monomer mole fraction of A and the two reactivity ratios. Exposed for validation and plots.
        ''' </summary>
        Public Shared Function MayoLewis(fA As Double, rA As Double, rB As Double) As Double
            Dim fB = 1.0 - fA
            Dim num = rA * fA * fA + fA * fB
            Dim den = rA * fA * fA + 2.0 * fA * fB + rB * fB * fB
            Return If(den > 0.0, num / den, 0.0)
        End Function

    End Class

End Namespace
