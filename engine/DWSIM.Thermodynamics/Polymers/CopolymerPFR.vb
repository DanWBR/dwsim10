'    Free-Radical Copolymerization PFR / batch - standalone solver with composition drift (moment integration)
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

    ''' <summary>Outlet state of a copolymer plug-flow / batch solve. Molar masses g/mol, concentrations mol/L.</summary>
    Public Class CopolymerPFRResult
        Public Converged As Boolean
        Public ConversionA As Double
        Public ConversionB As Double
        Public OverallConversion As Double
        Public InstantaneousCompositionA As Double  ' F_A of the polymer formed at the outlet (Mayo-Lewis, drifted)
        Public CumulativeCompositionA As Double      ' F_A averaged over the whole product leaving the reactor
        Public Mn As Double
        Public Mw As Double
        Public PDI As Double
        Public MonomerAConc As Double
        Public MonomerBConc As Double
        Public InitiatorConc As Double
    End Class

    ''' <summary>
    ''' Standalone solver for a homogeneous, isothermal, binary free-radical copolymerization in a plug-flow
    ''' reactor - equivalently a batch reactor, with the residence time read as the reaction time. Unlike the
    ''' perfectly mixed CSTR, whose composition is pinned at its single outlet state, a plug-flow / batch reactor
    ''' lets the monomer composition drift as conversion builds: the more reactive monomer depletes first, so the
    ''' instantaneous copolymer composition changes along the reactor and the product carries a spread of
    ''' compositions. The monomer, initiator, dead-chain moment and incorporated-monomer balances are integrated
    ''' along the residence time by classical Runge-Kutta; the instantaneous kinetics at each point are the same
    ''' terminal-model, pseudo-kinetic treatment used by <see cref="CopolymerCSTR"/> (composition from the
    ''' propagation rates, transfer and the gel effect included). Concentrations mol/L, time seconds, temperature K.
    ''' </summary>
    Public Class CopolymerPFR

        Public Shared Function Solve(kin As CopolymerKinetics, T As Double, ResidenceTime As Double,
                                     MonomerAFeed As Double, MonomerBFeed As Double, InitiatorFeed As Double,
                                     Optional SolventConc As Double = 0.0,
                                     Optional gel As GelEffect = Nothing,
                                     Optional Steps As Integer = 1000) As CopolymerPFRResult

            Dim res As New CopolymerPFRResult()

            Dim kd = CopolymerKinetics.Arrhenius(kin.Ad, kin.Ed, T)
            Dim kpAA0 = CopolymerKinetics.Arrhenius(kin.ApAA, kin.EpAA, T)
            Dim kpBB0 = CopolymerKinetics.Arrhenius(kin.ApBB, kin.EpBB, T)
            Dim ktc0 = CopolymerKinetics.Arrhenius(kin.Atc, kin.Etc, T)
            Dim ktd0 = CopolymerKinetics.Arrhenius(kin.Atd, kin.Etd, T)
            Dim kt0 = ktc0 + ktd0
            Dim ktrMA = CopolymerKinetics.Arrhenius(kin.AtrMA, kin.EtrMA, T)
            Dim ktrMB = CopolymerKinetics.Arrhenius(kin.AtrMB, kin.EtrMB, T)
            Dim ktrSA = CopolymerKinetics.Arrhenius(kin.AtrSA, kin.EtrSA, T)
            Dim ktrSB = CopolymerKinetics.Arrhenius(kin.AtrSB, kin.EtrSB, T)
            Dim S = SolventConc
            Dim f = kin.Efficiency
            Dim rA = kin.ReactivityA, rB = kin.ReactivityB
            Dim fedTotal = MonomerAFeed + MonomerBFeed
            Dim gelActive = (gel IsNot Nothing AndAlso gel.IsActive)

            res.MonomerAConc = MonomerAFeed
            res.MonomerBConc = MonomerBFeed
            res.InitiatorConc = InitiatorFeed

            If kt0 <= 0.0 OrElse InitiatorFeed <= 0.0 OrElse kd <= 0.0 OrElse fedTotal <= 0.0 Then
                res.Converged = True
                res.PDI = 1.0
                Return res
            End If

            ' State: 0=[A] 1=[B] 2=[I] 3=lambda0 4=lambda1 5=lambda2 6=PsiA 7=PsiB (Psi = monomer moles propagated).
            Dim y(7) As Double
            y(0) = MonomerAFeed : y(1) = MonomerBFeed : y(2) = InitiatorFeed

            Dim n = Math.Max(10, Steps)
            Dim h = ResidenceTime / n

            Dim fInstA As Double = 0.0     ' instantaneous composition at the last evaluated state (closure-captured)
            Dim deriv = Sub(st As Double(), dydt As Double())
                            Dim A = Math.Max(st(0), 0.0), B = Math.Max(st(1), 0.0), Ii = Math.Max(st(2), 0.0)
                            For k = 0 To 7 : dydt(k) = 0.0 : Next
                            Dim M = A + B
                            If M <= 0.0 OrElse Ii <= 0.0 Then Return

                            Dim X = ((MonomerAFeed - A) + (MonomerBFeed - B)) / fedTotal
                            Dim gt As Double = 1.0, gp As Double = 1.0
                            If gelActive Then gt = gel.TerminationFactor(X) : gp = gel.PropagationFactor(X)
                            Dim kpAA = kpAA0 * gp, kpBB = kpBB0 * gp
                            Dim kpAB = If(rA > 0.0, kpAA / rA, kpAA * 1.0E+6)
                            Dim kpBA = If(rB > 0.0, kpBB / rB, kpBB * 1.0E+6)
                            Dim ktc = ktc0 * gt, ktd = ktd0 * gt, kt = ktc + ktd
                            Dim mu0 = Math.Sqrt(f * kd * Ii / kt)
                            If mu0 <= 0.0 Then Return

                            Dim denom = kpBA * A + kpAB * B
                            Dim phiA = If(denom > 0.0, kpBA * A / denom, 0.5)
                            Dim phiB = 1.0 - phiA
                            Dim cA = mu0 * (kpAA * phiA + kpBA * phiB)   ' propagation
                            Dim cB = mu0 * (kpBB * phiB + kpAB * phiA)
                            Dim ktrMavg = phiA * ktrMA + phiB * ktrMB
                            Dim ktrSavg = phiA * ktrSA + phiB * ktrSB

                            Dim RAprop = A * cA, RBprop = B * cB
                            Dim RpProp = RAprop + RBprop
                            fInstA = If(RpProp > 0.0, RAprop / RpProp, 0.0)  ' Mayo-Lewis at this local composition
                            Dim rateA = RAprop + A * mu0 * ktrMavg           ' total draw-down of each monomer
                            Dim rateB = RBprop + B * mu0 * ktrMavg

                            Dim kpBar = If(mu0 > 0.0, RpProp / (mu0 * M), 0.0)
                            Dim transferRate = ktrMavg * M + ktrSavg * S
                            Dim stopRate = kt * mu0 + transferRate
                            If stopRate <= 0.0 Then Return
                            Dim alpha = kpBar * M / (kpBar * M + stopRate)
                            Dim oneMinusAlpha = 1.0 - alpha
                            If oneMinusAlpha <= 0.0 Then Return
                            Dim mu1 = mu0 / oneMinusAlpha
                            Dim mu2 = mu0 * (1.0 + alpha) / (oneMinusAlpha * oneMinusAlpha)

                            dydt(0) = -rateA
                            dydt(1) = -rateB
                            dydt(2) = -kd * Ii
                            dydt(3) = transferRate * mu0 + (ktd + 0.5 * ktc) * mu0 * mu0
                            dydt(4) = transferRate * mu1 + kt * mu0 * mu1
                            dydt(5) = transferRate * mu2 + kt * mu0 * mu2 + ktc * mu1 * mu1
                            dydt(6) = RAprop
                            dydt(7) = RBprop
                        End Sub

            ' Classical fourth-order Runge-Kutta along the residence time.
            Dim k1(7) As Double, k2(7) As Double, k3(7) As Double, k4(7) As Double, tmp(7) As Double
            For stp = 1 To n
                deriv(y, k1)
                For k = 0 To 7 : tmp(k) = y(k) + 0.5 * h * k1(k) : Next
                deriv(tmp, k2)
                For k = 0 To 7 : tmp(k) = y(k) + 0.5 * h * k2(k) : Next
                deriv(tmp, k3)
                For k = 0 To 7 : tmp(k) = y(k) + h * k3(k) : Next
                deriv(tmp, k4)
                For k = 0 To 7 : y(k) += h / 6.0 * (k1(k) + 2.0 * k2(k) + 2.0 * k3(k) + k4(k)) : Next
            Next
            deriv(y, k1)   ' refresh the instantaneous composition at the outlet state

            Dim Aout = Math.Max(y(0), 0.0), Bout = Math.Max(y(1), 0.0)
            res.MonomerAConc = Aout
            res.MonomerBConc = Bout
            res.InitiatorConc = Math.Max(y(2), 0.0)
            res.ConversionA = If(MonomerAFeed > 0.0, 1.0 - Aout / MonomerAFeed, 0.0)
            res.ConversionB = If(MonomerBFeed > 0.0, 1.0 - Bout / MonomerBFeed, 0.0)
            res.OverallConversion = ((MonomerAFeed - Aout) + (MonomerBFeed - Bout)) / fedTotal
            res.InstantaneousCompositionA = fInstA

            Dim L0 = y(3), L1 = y(4), L2 = y(5), PsiA = y(6), PsiB = y(7)
            Dim PsiTot = PsiA + PsiB
            res.CumulativeCompositionA = If(PsiTot > 0.0, PsiA / PsiTot, 0.0)

            If L0 > 0.0 AndAlso L1 > 0.0 AndAlso PsiTot > 0.0 Then
                Dim Mbar = (PsiA * kin.MonomerAMW + PsiB * kin.MonomerBMW) / PsiTot
                res.Mn = Mbar * L1 / L0
                res.Mw = Mbar * L2 / L1
                res.PDI = L0 * L2 / (L1 * L1)
                res.Converged = True
            Else
                res.Converged = False
            End If

            Return res

        End Function

    End Class

End Namespace
