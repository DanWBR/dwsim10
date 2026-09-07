'    Free-Radical Copolymerization - dynamic (transient) reaction step for a well-mixed holdup
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
    ''' Transient state of a well-mixed copolymerization holdup, on total amounts: monomer, initiator, volume,
    ''' the dead-chain moments and the incorporated-monomer amounts. It is what a dynamic simulation carries from
    ''' one integration step to the next; feeding is the caller's job (the reactor adds inlet and removes any
    ''' draw through its accumulation), while <see cref="CopolymerDynamics.Advance"/> reacts the holdup over a
    ''' step. Amounts mol, volume L.
    ''' </summary>
    <Serializable()> Public Class CopolymerDynState
        Public MonomerA As Double       ' mol in the holdup
        Public MonomerB As Double
        Public Initiator As Double
        Public Volume As Double = 1.0   ' L
        Public Lambda0 As Double        ' dead-chain moments (extensive, mol and mol-monomer)
        Public Lambda1 As Double
        Public Lambda2 As Double
        Public IncorporatedA As Double  ' mol of A propagated into chains
        Public IncorporatedB As Double

        Public Function CumulativeCompositionA() As Double
            Dim tot = IncorporatedA + IncorporatedB
            Return If(tot > 0.0, IncorporatedA / tot, 0.0)
        End Function

        Public Function NumberAverageMW(kin As CopolymerKinetics) As Double
            Dim tot = IncorporatedA + IncorporatedB
            If Lambda0 <= 0.0 OrElse tot <= 0.0 Then Return 0.0
            Dim Mbar = (IncorporatedA * kin.MonomerAMW + IncorporatedB * kin.MonomerBMW) / tot
            Return Mbar * Lambda1 / Lambda0
        End Function

        Public Function WeightAverageMW(kin As CopolymerKinetics) As Double
            Dim tot = IncorporatedA + IncorporatedB
            If Lambda1 <= 0.0 OrElse tot <= 0.0 Then Return 0.0
            Dim Mbar = (IncorporatedA * kin.MonomerAMW + IncorporatedB * kin.MonomerBMW) / tot
            Return Mbar * Lambda2 / Lambda1
        End Function

        Public Function PolydispersityIndex() As Double
            If Lambda0 <= 0.0 OrElse Lambda1 <= 0.0 Then Return 1.0
            Return Lambda0 * Lambda2 / (Lambda1 * Lambda1)
        End Function

        Public Function Clone() As CopolymerDynState
            Return DirectCast(Me.MemberwiseClone(), CopolymerDynState)
        End Function
    End Class

    ''' <summary>
    ''' Advances a well-mixed copolymerization holdup over one integration step by reacting it at constant
    ''' volume (feed and draw are applied by the caller before and after this call). The instantaneous kinetics
    ''' are the same terminal-model, pseudo-kinetic treatment as the steady CSTR and the PFR - composition from
    ''' the propagation rates, transfer and the gel effect included - integrated across the step by Runge-Kutta.
    ''' Stepping this from an initial charge with no feed reproduces the batch (PFR) trajectory; letting the
    ''' caller add feed each step gives the semibatch trajectory.
    ''' </summary>
    Public Class CopolymerDynamics

        Public Shared Sub Advance(kin As CopolymerKinetics, T As Double, gel As GelEffect,
                                  st As CopolymerDynState, dt As Double, Optional Substeps As Integer = 20)

            If dt <= 0.0 OrElse st.Volume <= 0.0 Then Return

            Dim kd = CopolymerKinetics.Arrhenius(kin.Ad, kin.Ed, T)
            Dim kpAA0 = CopolymerKinetics.Arrhenius(kin.ApAA, kin.EpAA, T)
            Dim kpBB0 = CopolymerKinetics.Arrhenius(kin.ApBB, kin.EpBB, T)
            Dim ktc0 = CopolymerKinetics.Arrhenius(kin.Atc, kin.Etc, T)
            Dim ktd0 = CopolymerKinetics.Arrhenius(kin.Atd, kin.Etd, T)
            Dim kt0 = ktc0 + ktd0
            Dim ktrMA = CopolymerKinetics.Arrhenius(kin.AtrMA, kin.EtrMA, T)
            Dim ktrMB = CopolymerKinetics.Arrhenius(kin.AtrMB, kin.EtrMB, T)
            Dim ff = kin.Efficiency
            Dim rA = kin.ReactivityA, rB = kin.ReactivityB
            Dim gelActive = (gel IsNot Nothing AndAlso gel.IsActive)
            Dim V = st.Volume

            If kt0 <= 0.0 OrElse kd <= 0.0 Then Return

            ' State vector (extensive): 0=N_A 1=N_B 2=N_I 3=L0 4=L1 5=L2 6=PsiA 7=PsiB. Volume is constant here.
            Dim y(7) As Double
            y(0) = st.MonomerA : y(1) = st.MonomerB : y(2) = st.Initiator
            y(3) = st.Lambda0 : y(4) = st.Lambda1 : y(5) = st.Lambda2 : y(6) = st.IncorporatedA : y(7) = st.IncorporatedB

            Dim deriv = Sub(sv As Double(), dydt As Double())
                            For k = 0 To 7 : dydt(k) = 0.0 : Next
                            Dim A = Math.Max(sv(0), 0.0) / V, B = Math.Max(sv(1), 0.0) / V, Ii = Math.Max(sv(2), 0.0) / V
                            dydt(2) = -kd * Ii * V
                            Dim M = A + B
                            If M <= 0.0 OrElse Ii <= 0.0 Then Return

                            Dim reacted = sv(6) + sv(7)
                            Dim X = If(reacted + sv(0) + sv(1) > 0.0, reacted / (reacted + sv(0) + sv(1)), 0.0)
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

                            Dim RAprop = A * cA, RBprop = B * cB
                            Dim RpProp = RAprop + RBprop
                            Dim rateA = RAprop + A * mu0 * ktrMavg
                            Dim rateB = RBprop + B * mu0 * ktrMavg
                            Dim kpBar = If(mu0 > 0.0, RpProp / (mu0 * M), 0.0)
                            Dim transferRate = ktrMavg * M
                            Dim stopRate = kt * mu0 + transferRate
                            If stopRate <= 0.0 Then Return
                            Dim alpha = kpBar * M / (kpBar * M + stopRate)
                            Dim oneMinusAlpha = 1.0 - alpha
                            If oneMinusAlpha <= 0.0 Then Return
                            Dim mu1 = mu0 / oneMinusAlpha
                            Dim mu2 = mu0 * (1.0 + alpha) / (oneMinusAlpha * oneMinusAlpha)

                            dydt(0) = -rateA * V
                            dydt(1) = -rateB * V
                            dydt(3) = (transferRate * mu0 + (ktd + 0.5 * ktc) * mu0 * mu0) * V
                            dydt(4) = (transferRate * mu1 + kt * mu0 * mu1) * V
                            dydt(5) = (transferRate * mu2 + kt * mu0 * mu2 + ktc * mu1 * mu1) * V
                            dydt(6) = RAprop * V
                            dydt(7) = RBprop * V
                        End Sub

            Dim n = Math.Max(1, Substeps)
            Dim h = dt / n
            Dim k1(7) As Double, k2(7) As Double, k3(7) As Double, k4(7) As Double, tmp(7) As Double
            For s = 1 To n
                deriv(y, k1)
                For k = 0 To 7 : tmp(k) = y(k) + 0.5 * h * k1(k) : Next
                deriv(tmp, k2)
                For k = 0 To 7 : tmp(k) = y(k) + 0.5 * h * k2(k) : Next
                deriv(tmp, k3)
                For k = 0 To 7 : tmp(k) = y(k) + h * k3(k) : Next
                deriv(tmp, k4)
                For k = 0 To 7 : y(k) += h / 6.0 * (k1(k) + 2.0 * k2(k) + 2.0 * k3(k) + k4(k)) : Next
            Next

            st.MonomerA = Math.Max(y(0), 0.0)
            st.MonomerB = Math.Max(y(1), 0.0)
            st.Initiator = Math.Max(y(2), 0.0)
            st.Lambda0 = y(3) : st.Lambda1 = y(4) : st.Lambda2 = y(5)
            st.IncorporatedA = y(6) : st.IncorporatedB = y(7)

        End Sub

    End Class

End Namespace
