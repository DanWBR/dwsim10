'    Free-Radical Copolymerization semibatch reactor - composition control by monomer feed policy
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
    ''' Semibatch feed policy: an initial reactor charge plus constant molar and volumetric feed rates applied
    ''' over a feed window, then held (batch finish) to the total reaction time. Feeding the more reactive
    ''' monomer during the run is how a semibatch reactor cancels the composition drift a batch shows; in the
    ''' monomer-starved limit the monomers react as fast as they are fed, so the copolymer composition equals
    ''' the feed composition. Amounts mol, volume L, rates per second.
    ''' </summary>
    Public Class SemibatchFeed
        Public ChargeA As Double, ChargeB As Double, ChargeI As Double   ' initial reactor charge (mol)
        Public ChargeVolume As Double = 1.0                              ' initial reactor volume (L)
        Public FeedA As Double, FeedB As Double, FeedI As Double         ' molar feed rates during the feed window (mol/s)
        Public FeedVolumetric As Double                                  ' volumetric feed rate (L/s)
        Public FeedDuration As Double                                    ' feed stops after this time (s)
        Public TotalTime As Double                                       ' total reaction time (s)
    End Class

    ''' <summary>Outlet state of a semibatch copolymer solve. Molar masses g/mol, concentrations mol/L, volume L.</summary>
    Public Class SemibatchResult
        Public Converged As Boolean
        Public OverallConversion As Double           ' monomer reacted / monomer charged + fed
        Public InstantaneousCompositionA As Double   ' F_A of the polymer forming at the final time
        Public CumulativeCompositionA As Double      ' F_A averaged over the whole product
        Public Mn As Double
        Public Mw As Double
        Public PDI As Double
        Public MonomerAConc As Double
        Public MonomerBConc As Double
        Public FinalVolume As Double
        Public MonomerAIncorporated As Double        ' mol of A propagated into chains
        Public MonomerBIncorporated As Double
    End Class

    ''' <summary>
    ''' Standalone solver for a homogeneous, isothermal, well-mixed semibatch binary free-radical
    ''' copolymerization. The reactor holdup grows as feed is added, so the balances are written on total amounts
    ''' (moles, volume) rather than concentrations and integrated in time by classical Runge-Kutta. The
    ''' instantaneous kinetics at each instant are the same terminal-model, pseudo-kinetic treatment as the CSTR
    ''' and PFR (composition from the propagation rates, transfer and the gel effect included). The point of the
    ''' semibatch is the feed policy: metering the more reactive monomer in during the run holds the reactor
    ''' monomer ratio, and hence the instantaneous copolymer composition, roughly constant - suppressing the
    ''' drift a batch reactor of the same charge would show. Amounts mol, volume L, time seconds, temperature K.
    ''' </summary>
    Public Class SemibatchCopolymer

        Public Shared Function Solve(kin As CopolymerKinetics, T As Double, feed As SemibatchFeed,
                                     Optional gel As GelEffect = Nothing,
                                     Optional Steps As Integer = 2000) As SemibatchResult

            Dim res As New SemibatchResult()

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
            Dim ff = kin.Efficiency
            Dim rA = kin.ReactivityA, rB = kin.ReactivityB
            Dim gelActive = (gel IsNot Nothing AndAlso gel.IsActive)

            If kt0 <= 0.0 OrElse kd <= 0.0 OrElse feed.ChargeVolume <= 0.0 OrElse feed.TotalTime <= 0.0 Then
                res.Converged = False
                Return res
            End If

            Dim monomerCharged = feed.ChargeA + feed.ChargeB

            ' State: 0=N_A 1=N_B 2=N_I 3=V 4=N_l0 5=N_l1 6=N_l2 7=N_PsiA 8=N_PsiB (all extensive).
            Dim y(8) As Double
            y(0) = feed.ChargeA : y(1) = feed.ChargeB : y(2) = feed.ChargeI : y(3) = feed.ChargeVolume

            Dim n = Math.Max(20, Steps)
            Dim h = feed.TotalTime / n
            Dim fInstA As Double = 0.0

            Dim deriv = Sub(tm As Double, st As Double(), dydt As Double())
                            For k = 0 To 8 : dydt(k) = 0.0 : Next
                            Dim feedOn = (tm < feed.FeedDuration)
                            Dim FA = If(feedOn, feed.FeedA, 0.0)
                            Dim FB = If(feedOn, feed.FeedB, 0.0)
                            Dim FI = If(feedOn, feed.FeedI, 0.0)
                            Dim QF = If(feedOn, feed.FeedVolumetric, 0.0)
                            dydt(3) = QF

                            Dim V = Math.Max(st(3), 1.0E-30)
                            Dim A = Math.Max(st(0), 0.0) / V, B = Math.Max(st(1), 0.0) / V, Ii = Math.Max(st(2), 0.0) / V
                            ' Feed source terms apply regardless of whether the reaction is running yet.
                            dydt(0) = FA : dydt(1) = FB : dydt(2) = FI - kd * Ii * V

                            Dim M = A + B
                            If M <= 0.0 OrElse Ii <= 0.0 Then Return

                            Dim monomerInSoFar = monomerCharged + (feed.FeedA + feed.FeedB) * Math.Min(tm, feed.FeedDuration)
                            Dim X = If(monomerInSoFar > 0.0, (monomerInSoFar - (st(0) + st(1))) / monomerInSoFar, 0.0)
                            Dim gt As Double = 1.0, gp As Double = 1.0
                            If gelActive Then gt = gel.TerminationFactor(X) : gp = gel.PropagationFactor(X)
                            Dim kpAA = kpAA0 * gp, kpBB = kpBB0 * gp
                            Dim kpAB = If(rA > 0.0, kpAA / rA, kpAA * 1.0E+6)
                            Dim kpBA = If(rB > 0.0, kpBB / rB, kpBB * 1.0E+6)
                            Dim ktc = ktc0 * gt, ktd = ktd0 * gt, kt = ktc + ktd
                            Dim mu0 = Math.Sqrt(ff * kd * Ii / kt)
                            If mu0 <= 0.0 Then Return

                            Dim denom = kpBA * A + kpAB * B
                            Dim phiA = If(denom > 0.0, kpBA * A / denom, 0.5)
                            Dim phiB = 1.0 - phiA
                            Dim cA = mu0 * (kpAA * phiA + kpBA * phiB)
                            Dim cB = mu0 * (kpBB * phiB + kpAB * phiA)
                            Dim ktrMavg = phiA * ktrMA + phiB * ktrMB
                            Dim ktrSavg = phiA * ktrSA + phiB * ktrSB

                            Dim RAprop = A * cA, RBprop = B * cB               ' per volume
                            Dim RpProp = RAprop + RBprop
                            fInstA = If(RpProp > 0.0, RAprop / RpProp, 0.0)
                            Dim rateA = RAprop + A * mu0 * ktrMavg
                            Dim rateB = RBprop + B * mu0 * ktrMavg

                            Dim kpBar = If(mu0 > 0.0, RpProp / (mu0 * M), 0.0)
                            Dim transferRate = ktrMavg * M + ktrSavg * 0.0    ' solvent / CTA slot unused in the semibatch policy
                            Dim stopRate = kt * mu0 + transferRate
                            If stopRate <= 0.0 Then Return
                            Dim alpha = kpBar * M / (kpBar * M + stopRate)
                            Dim oneMinusAlpha = 1.0 - alpha
                            If oneMinusAlpha <= 0.0 Then Return
                            Dim mu1 = mu0 / oneMinusAlpha
                            Dim mu2 = mu0 * (1.0 + alpha) / (oneMinusAlpha * oneMinusAlpha)

                            ' Extensive balances: consumption per volume times the current holdup.
                            dydt(0) = FA - rateA * V
                            dydt(1) = FB - rateB * V
                            dydt(4) = (transferRate * mu0 + (ktd + 0.5 * ktc) * mu0 * mu0) * V
                            dydt(5) = (transferRate * mu1 + kt * mu0 * mu1) * V
                            dydt(6) = (transferRate * mu2 + kt * mu0 * mu2 + ktc * mu1 * mu1) * V
                            dydt(7) = RAprop * V
                            dydt(8) = RBprop * V
                        End Sub

            Dim k1(8) As Double, k2(8) As Double, k3(8) As Double, k4(8) As Double, tmp(8) As Double
            Dim t0 As Double = 0.0
            For stp = 1 To n
                deriv(t0, y, k1)
                For k = 0 To 8 : tmp(k) = y(k) + 0.5 * h * k1(k) : Next
                deriv(t0 + 0.5 * h, tmp, k2)
                For k = 0 To 8 : tmp(k) = y(k) + 0.5 * h * k2(k) : Next
                deriv(t0 + 0.5 * h, tmp, k3)
                For k = 0 To 8 : tmp(k) = y(k) + h * k3(k) : Next
                deriv(t0 + h, tmp, k4)
                For k = 0 To 8 : y(k) += h / 6.0 * (k1(k) + 2.0 * k2(k) + 2.0 * k3(k) + k4(k)) : Next
                t0 += h
            Next
            deriv(feed.TotalTime, y, k1)   ' refresh the instantaneous composition at the final state

            Dim NAout = Math.Max(y(0), 0.0), NBout = Math.Max(y(1), 0.0), Vf = Math.Max(y(3), 1.0E-30)
            res.FinalVolume = Vf
            res.MonomerAConc = NAout / Vf
            res.MonomerBConc = NBout / Vf
            Dim monomerFed = (feed.FeedA + feed.FeedB) * feed.FeedDuration
            Dim monomerTotal = monomerCharged + monomerFed
            res.OverallConversion = If(monomerTotal > 0.0, (monomerTotal - (NAout + NBout)) / monomerTotal, 0.0)
            res.InstantaneousCompositionA = fInstA

            Dim L0 = y(4), L1 = y(5), L2 = y(6), PsiA = y(7), PsiB = y(8)
            Dim PsiTot = PsiA + PsiB
            res.MonomerAIncorporated = PsiA
            res.MonomerBIncorporated = PsiB
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
