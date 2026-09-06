'    Free-Radical Polymerization CSTR - standalone steady-state solver (method of moments)
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
    ''' Arrhenius kinetic parameters for a single-monomer free-radical polymerization. Every rate constant is
    ''' k = A*exp(-E/(R*T)); the initiator-decomposition pre-exponential A is in 1/s, every bimolecular A in
    ''' L/mol/s, and every activation energy E in J/mol. Termination is split into combination and
    ''' disproportionation, and chain transfer into transfer-to-monomer and transfer-to-solvent (a chain
    ''' transfer agent uses the solvent slot). Concentrations elsewhere are mol/L.
    ''' </summary>
    Public Class FreeRadicalKinetics

        Public Ad As Double, Ed As Double                 ' initiator decomposition (1/s)
        Public Efficiency As Double = 0.6                 ' initiator efficiency f
        Public Ap As Double, Ep As Double                 ' propagation (L/mol/s)
        Public Atc As Double, Etc As Double               ' termination by combination (L/mol/s)
        Public Atd As Double, Etd As Double               ' termination by disproportionation (L/mol/s)
        Public AtrM As Double, EtrM As Double             ' transfer to monomer (L/mol/s)
        Public AtrS As Double, EtrS As Double             ' transfer to solvent / chain-transfer agent (L/mol/s)
        Public MonomerMW As Double                        ' monomer molar mass M0 (g/mol)

        ''' <summary>k = A*exp(-E/(R*T)); returns 0 when A is 0 so an unused pathway drops out cleanly.</summary>
        Public Shared Function Arrhenius(A As Double, E As Double, T As Double) As Double
            If A <= 0.0 Then Return 0.0
            Return A * Math.Exp(-E / (8.314 * T))
        End Function

        ''' <summary>
        ''' AIBN-initiated bulk styrene, a standard benchmark set near 60 C: AIBN decomposition (Ad 1.58e15/s,
        ''' Ed 128 kJ/mol, f 0.6); the IUPAC styrene propagation (Ap 4.266e7, Ep 32.5 kJ/mol); termination
        ''' essentially all by combination (kt ~ 7e7 L/mol/s at 60 C); and transfer to monomer at the styrene
        ''' constant Cm ~ 6e-5 (so AtrM tracks Ap). Monomer molar mass 104.15 g/mol.
        ''' </summary>
        Public Shared Function StyreneAIBN() As FreeRadicalKinetics
            Return New FreeRadicalKinetics With {
                .Ad = 1.58E+15, .Ed = 128000.0, .Efficiency = 0.6,
                .Ap = 4.266E+7, .Ep = 32510.0,
                .Atc = 1.255E+9, .Etc = 8000.0, .Atd = 0.0, .Etd = 0.0,
                .AtrM = 4.266E+7 * 6.0E-5, .EtrM = 32510.0,
                .AtrS = 0.0, .EtrS = 0.0,
                .MonomerMW = 104.15}
        End Function

    End Class

    ''' <summary>Steady-state result of a free-radical CSTR solve. Molar masses are g/mol, concentrations mol/L.</summary>
    Public Class FreeRadicalCSTRResult
        Public Converged As Boolean
        Public Conversion As Double         ' X = 1 - [M]/[M]in
        Public Mn As Double                 ' number-average molar mass
        Public Mw As Double                 ' weight-average molar mass
        Public PDI As Double                ' polydispersity Mw/Mn
        Public Rp As Double                 ' rate of propagation (mol/L/s)
        Public MonomerConc As Double        ' outlet [M]
        Public InitiatorConc As Double      ' outlet [I]
        Public RadicalConc As Double        ' total live-radical concentration mu0
        Public Lambda0 As Double            ' dead-chain moments
        Public Lambda1 As Double
        Public Lambda2 As Double
        Public KineticChainLength As Double ' propagation rate per chain-stopping rate
    End Class

    ''' <summary>
    ''' Standalone steady-state solver for a homogeneous, isothermal, single-monomer free-radical
    ''' polymerization in a perfectly mixed reactor, by the method of moments (Phase 1 of the polymerization
    ''' reactor: kinetics and moments only, no unit-operation wiring). With a constant termination rate the
    ''' balances are closed form: the initiator and monomer CSTR balances are each first order in their own
    ''' concentration, and the total radical concentration follows from the quasi-steady-state assumption
    ''' (mu0 = sqrt(f*kd*[I]/kt), which reproduces the textbook rate of polymerization). The live-radical
    ''' moments close under the most-probable distribution (exact in the long-chain limit); the dead-chain
    ''' moments are then explicit as lambda_k = theta * G_k, and Mn/Mw/PDI fall straight out. Reference for
    ''' the moment source terms: Dotson, Galvan, Laurence and Tirrell, "Polymerization Process Modeling".
    ''' </summary>
    Public Class FreeRadicalCSTR

        ''' <summary>
        ''' Solves the reactor at steady state. Concentrations are mol/L, residence time seconds, temperature
        ''' kelvin. <paramref name="SolventConc"/> is the solvent or chain-transfer-agent concentration (0 for
        ''' bulk polymerization).
        ''' </summary>
        Public Shared Function Solve(kin As FreeRadicalKinetics, T As Double, ResidenceTime As Double,
                                     MonomerFeed As Double, InitiatorFeed As Double,
                                     Optional SolventConc As Double = 0.0) As FreeRadicalCSTRResult

            Dim res As New FreeRadicalCSTRResult()

            Dim theta = ResidenceTime
            Dim kd = FreeRadicalKinetics.Arrhenius(kin.Ad, kin.Ed, T)
            Dim kp = FreeRadicalKinetics.Arrhenius(kin.Ap, kin.Ep, T)
            Dim ktc = FreeRadicalKinetics.Arrhenius(kin.Atc, kin.Etc, T)
            Dim ktd = FreeRadicalKinetics.Arrhenius(kin.Atd, kin.Etd, T)
            Dim ktrM = FreeRadicalKinetics.Arrhenius(kin.AtrM, kin.EtrM, T)
            Dim ktrS = FreeRadicalKinetics.Arrhenius(kin.AtrS, kin.EtrS, T)
            Dim kt = ktc + ktd
            Dim M0 = kin.MonomerMW
            Dim f = kin.Efficiency
            Dim S = SolventConc

            ' Initiator: first-order decomposition, so the CSTR outlet is closed form.
            Dim I = InitiatorFeed / (1.0 + kd * theta)
            res.InitiatorConc = I

            ' Quasi-steady state on the total radical concentration.
            Dim mu0 As Double = 0.0
            If kt > 0.0 AndAlso I > 0.0 AndAlso kd > 0.0 Then mu0 = Math.Sqrt(f * kd * I / kt)
            res.RadicalConc = mu0

            If mu0 <= 0.0 Then
                ' No radicals means no reaction: pass the monomer through.
                res.MonomerConc = MonomerFeed
                res.Conversion = 0.0
                res.Mn = 0.0 : res.Mw = 0.0 : res.PDI = 1.0
                res.Converged = True
                Return res
            End If

            ' Monomer is consumed by propagation and transfer to monomer, both first order in [M].
            Dim M = MonomerFeed / (1.0 + theta * (kp + ktrM) * mu0)
            res.MonomerConc = M
            res.Conversion = 1.0 - M / MonomerFeed
            res.Rp = kp * mu0 * M

            ' Propagation probability and the live-radical moments (most-probable closure).
            Dim stopRate = kt * mu0 + ktrM * M + ktrS * S
            If stopRate <= 0.0 Then
                res.Converged = False
                Return res
            End If
            Dim alpha = kp * M / (kp * M + stopRate)
            Dim oneMinusAlpha = 1.0 - alpha
            res.KineticChainLength = kp * M / stopRate

            Dim mu1 = mu0 / oneMinusAlpha
            Dim mu2 = mu0 * (1.0 + alpha) / (oneMinusAlpha * oneMinusAlpha)

            ' Dead-chain moment generation; combination convolves two live chains (the mu1^2 term).
            Dim transfer = ktrM * M + ktrS * S
            Dim G0 = transfer * mu0 + (ktd + 0.5 * ktc) * mu0 * mu0
            Dim G1 = transfer * mu1 + kt * mu0 * mu1
            Dim G2 = transfer * mu2 + kt * mu0 * mu2 + ktc * mu1 * mu1

            ' In a CSTR the dead chains are only generated and swept out, so each moment is explicit.
            res.Lambda0 = theta * G0
            res.Lambda1 = theta * G1
            res.Lambda2 = theta * G2

            If res.Lambda0 > 0.0 AndAlso res.Lambda1 > 0.0 Then
                res.Mn = M0 * res.Lambda1 / res.Lambda0
                res.Mw = M0 * res.Lambda2 / res.Lambda1
                res.PDI = res.Lambda0 * res.Lambda2 / (res.Lambda1 * res.Lambda1)
                res.Converged = True
            Else
                res.Converged = False
            End If

            Return res

        End Function

        ''' <summary>
        ''' Couples a reactor result to the thermodynamics: expands the polymer into <paramref name="N"/>
        ''' pseudo-component cuts at the result's Mn and PDI (through PolymerCharacterization.BuildCuts) and
        ''' returns the outlet mole fractions in the order [monomer, cut_1 .. cut_N] - unreacted monomer plus
        ''' the polymer distribution. The polymer is a trace by mole (a few long chains) yet carries the bulk
        ''' of the reacted mass. Solvent and initiator, negligible in a bulk low-initiator outlet, are the
        ''' caller's to append. The cuts share the base polymer's CAS, so the equation of state reuses its
        ''' segment parameters at each cut's own molar mass; the base polymer's User database flags carry
        ''' through the clone. This is the hand-off the polymerization unit operation makes to a material stream.
        ''' </summary>
        Public Shared Function ExpandOutlet(result As FreeRadicalCSTRResult,
                                            basePolymer As BaseClasses.ConstantProperties,
                                            N As Integer, distribution As PolymerDistribution,
                                            ByRef cuts As List(Of BaseClasses.ConstantProperties)) As Double()

            Dim zrel As Double() = Nothing
            cuts = PolymerCharacterization.BuildCuts(basePolymer, result.Mn, result.PDI, N, distribution, zrel)

            Dim monomer = Math.Max(result.MonomerConc, 0.0)
            Dim chains = Math.Max(result.Lambda0, 0.0)
            Dim total = monomer + chains

            Dim x(cuts.Count) As Double
            If total <= 0.0 Then Return x
            x(0) = monomer / total
            For k As Integer = 0 To cuts.Count - 1
                x(k + 1) = chains * zrel(k) / total
            Next
            Return x

        End Function

    End Class

End Namespace
