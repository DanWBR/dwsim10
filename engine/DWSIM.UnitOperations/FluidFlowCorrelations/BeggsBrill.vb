'    Beggs and Brill Pressure Drop Calculation Routine
'    Copyright 2008 Daniel Wagner O. de Medeiros
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

Imports System.Numerics

Namespace FlowPackages

    <Serializable()> Public Class BeggsBrill

        Inherits FPBaseClass

        Public Overrides Function CalculateDeltaP(ByVal D As Double, ByVal L As Double, ByVal deltaz As Double, ByVal k As Double, ByVal qv As Double, ByVal ql As Double, ByVal muv As Double, ByVal mul As Double, ByVal rhov As Double, ByVal rhol As Double, ByVal surft As Double) As Object()
            'Function PA_DP(ByVal D, ByVal L, ByVal Z, ByVal epsilon, ByVal QG, ByVal QL, ByVal mu_g, ByVal mu_l, ByVal rho_g, ByVal rho_l, ByVal sigma)

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "CalculateDeltaP", "Beggs and Brill Pressure Drop", "Beggs and Brill Multiphase Pressure Drop Calculation Routine", True)

            IObj?.SetCurrent()

            IObj?.Paragraphs.Add("Beggs and Brill (1973) correlation, is one of the few correlations capable of handling all flow directions encountered in oil and gas operations, namely uphill, downhill, horizontal, inclined and vertical flow for two phase fluid.")
            IObj?.Paragraphs.Add("Total pressure gradient Is described by following relation.")
            IObj?.Paragraphs.Add("<m>\frac{dP}{dZ} = [\frac{dP}{dZ}_{Fric.} + \frac{dP}{dZ}_{Ele.}]/(1-E_k)</m>")

            IObj?.Paragraphs.Add("<p>where, (dP / dZ)<Sub>Fric.</Sub> Is pressure gradient due To friction, (dP / dZ)<Sub>Ele.</Sub> Is hydrostatic pressure difference And E<Sub>k</Sub> estimates pressure loss due To acceleration.</p>
                                <h3>Flow Pattern Map</h3>
                                <p>A flow regime Is identified based On the Froude number Of the mixture (Fr<Sub>m</Sub>) And input liquid content (no slip liquid holdup C<Sub>L</Sub>).</p>
                                <m>Fr_m = \frac{v_m^2}{g.D}</m>
                                <p>where, v<Sub>m</Sub> Is mixture velocity, D Is pipe inside diameter And g Is gravitational constant.</p>
                                <pre>C<Sub>L</Sub>= Q<Sub>L</Sub>/ (Q<Sub>L</Sub> + Q<Sub>G</Sub>)</pre>
                                <p>where, Q<Sub>L</Sub> Is liquid volumetric flow And Q<Sub>G</Sub> Is gas volumetric flow.</p>
                                <p>The transition lines For correlation are defined As follows:</p>
                                <pre>L<Sub>1</Sub>= 316 C<Sub>L</Sub><sup>0.302</sup>
                                L<Sub>2</Sub>= 0.0009252 C<Sub>L</Sub><sup>-2.4684</sup>
                                L<Sub>3</Sub>= 0.1 C<Sub>L</Sub><sup>-1.4516</sup>
                                L<Sub>4</Sub>= 0.5 C<Sub>L</Sub><sup>-6.738</sup></pre>
                                <h4>Segregated Flow</h4>
                                <pre>C<Sub>L</Sub> &lt; 0.01 And Fr<Sub>m</Sub> &lt; L<Sub>1</Sub>
                                OR C<Sub>L</Sub> &gt;= 0.01 And Fr < Sub() > m</Sub> &lt; L<Sub>2</Sub></pre>
                                <h4>Intermittent Flow</h4>
                                <pre>0.01 &lt;= C < Sub() > L</Sub> &lt; 0.4 And L<Sub>3</Sub> &lt; Fr<Sub>m</Sub> &lt;= L<Sub>1</Sub>
                                OR C<Sub>L</Sub> &gt;= 0.4 And L < Sub() > 3</Sub> &lt; Fr<Sub>m</Sub> &lt;= L<Sub>4</Sub></pre>
                                <h4>Distributed Flow</h4>
                                <pre><code>C<Sub>L</Sub> &lt; 0.4 And Fr<Sub>m</Sub> &gt;= L<Sub>4</Sub></code>
                                <code>OR C<Sub>L</Sub> &gt;= 0.4 And Fr < Sub() > m</Sub> &gt; L<Sub>4</Sub></code></pre>
                                <h4>Transition Flow</h4>
                                <pre><code>L<Sub>2</Sub> &lt; Fr<Sub>m</Sub> &lt; L<Sub>3</Sub></code></pre>
                                <h3>Liquid Holdup, E<Sub>L</Sub>(?)</h3>
                                <p>Once flow type has been determined, liquid holdup For horizontal flow E<Sub>L</Sub>(0) Is calculated.</p>
                                <pre><code>E<Sub>L</Sub>(0) = a C<Sub>L</Sub><sup>b</sup> / Fr<Sub>m</Sub><sup>c</sup></code></pre>
                                <table Class='table table-bordered'>
                                <thead>
                                            <tr>
                                            <th Class='heading' align='center'>Flow Regime</th>
                                <th> a</th>
                                <th> b</th>
                                <th> c</th>
                                </tr>
                                </thead>
                                <tbody>
                                                <tr>
                                                <td> Segregated</td>
                                <td>0.98</td>
                                <td>0.4846</td>
                                <td>0.0868</td>
                                </tr>
                                <tr>
                                                <td> Intermittent</td>
                                <td>0.845</td>
                                <td>0.5351</td>
                                <td>0.0173</td>
                                </tr>
                                <tr>
                                                <td> Distributed</td>
                                <td>1.065</td>
                                <td>0.5824</td>
                                <td>0.0609</td>
                                </tr>
                                </tbody>
                                </table>
                                <p> E<Sub>L</Sub>(0) must be greater than C<Sub>L</Sub>, If E<Sub>L</Sub>(0) Is smaller than C<Sub>L</Sub>, Then E<Sub>L</Sub>(0) Is assigned a value Of C<Sub>L</Sub>. Actual liquid volume fraction Is obtained by multiplying E<Sub>L</Sub>(0) by a correction factor, B(?).</p>
                                <pre> <code> E<Sub>L</Sub>(?) = B(?) x E<Sub>L</Sub>(0)</code></pre>
                                <p> B(?) Is obtained As &#8211;</p>
                                <pre> <code> B(?) = 1 + ß(sin(1.8?) - (1 / 3)sin³(1.8?))</code></pre>
                                <p> where? Is the angle Of inclination Of pipe With horizontal.<br />
                                Correction factor ß Is calculated As following &#8211;</p>
                                <pre> <code> ß = (1 - c < Sub() > L</sub>)ln( d.C<sub>L</sub><sup>e</sup>.N<sub>LV</sub><sup>f</sup>.Fr<sub>m</sub><sup>g</sup> )</code></pre>
                                <table Class='table table-bordered'>
                                <thead>
                                                    <tr>
                                                    <th Class='heading' align='center'>Uphill</th>
                                <th> d</th>
                                <th> e</th>
                                <th> f</th>
                                <th> g</th>
                                </tr>
                                </thead>
                                <tbody>
                                                        <tr>
                                                        <td> Segregated</td>
                                <td>0.011</td>
                                <td>-3.768</td>
                                <td>3.539</td>
                                <td>-1.614</td>
                                </tr>
                                <tr>
                                                        <td> Intermittent</td>
                                <td>2.96</td>
                                <td>0.305</td>
                                <td>-0.4473</td>
                                <td>0.0978</td>
                                </tr>
                                <tr>
                                                        <td> Distributed</td>
                                <td colspan ='4' align='center'>ß = 0</td>
                                </tr>
                                                        </tbody>
                                <thead>
                                                        <tr>
                                                        <th Class='heading' align='center'>Downhill</th>
                                <th> d</th>
                                <th> e</th>
                                <th> f</th>
                                <th> g</th>
                                </tr>
                                <tr>
                                                            <td> All</td>
                                <td>4.7</td>
                                <td>-0.3692</td>
                                <td>0.1244</td>
                                <td>-0.5056</td>
                                </tr>
                                </thead>
                                </table>
                                <p> Liquid velocity number, N<Sub>LV</Sub> Is given by:</p>
                                <pre> <code> N<Sub>LV</Sub>= 1.938 V<Sub>sl</Sub>(?<Sub>L</Sub>/ (g.s))<sup>1/4</sup></code></pre>
                                <p> V<Sub>sl</Sub> Is no slip liquid velocity, ?<Sub>L</Sub> Is liquid density, g Is gravitational constant And s Is surface tension.</p>
                                <p>For transition flow,</p>
                                <pre> <code> E<Sub>L</Sub>(?)<Sub>transition</Sub>= AE<Sub>L</Sub>(?)<Sub>segregated</Sub> + BE<Sub>L</Sub>(?)<Sub>intermittent</Sub></code></pre>
                                <p> where A And B are As following &#8211;</p>
                                <pre> <code> A = (L < Sub() > 3</sub> - Fr<sub>m</sub>)/(L<sub>3</sub> - L<sub>2</sub>)</code>
                                <code> B = 1 - A</code></pre>
                                <p> Liquid holdup, E<Sub>L</Sub>(?) Is used To calculate mixture density ?<Sub>m</Sub>.</p>
                                <pre> <code>?<Sub>m</Sub>= ?<Sub>L</Sub>.E<Sub>L</Sub>(?) + ?<Sub>G</Sub>.(1-E<Sub>L</Sub>(?))</code></pre>
                                <h3>(dP/dZ)<Sub>Elevation</Sub></h3>
                                <p> Pressure change due To the hydrostatic head Of the vertical component Of the pipe Is given by:</p>
                                <pre> <code>(dP/dZ)<Sub>Ele.</Sub>= ?<Sub>m</Sub>.g.sin(?)/(144.g<Sub>c</Sub>)</code></pre>
                                <h3>(dP/dZ)<Sub>Friction</Sub></h3>
                                <p> Calculate no slip Reynold&#8217;s number Using no slip mixture density And viscosity.</p>
                                <pre> <code> Re<Sub>NS</Sub>= ?<Sub>NS</Sub>.V<Sub>m</Sub>.D/µ<Sub>NS</Sub></code></pre>
                                <p> No slip friction factor, f<Sub>NS</Sub> Is Then calculated Using Colebrook-White equation.</p>
                                <p> Ratio Of friction factor Is defined As</p>
                                <pre> <code> f<Sub>TP</Sub>/ f<Sub>NS</Sub>= e<sup>S</sup></code></pre>
                                <p> Value Of S Is governed by following conditions &#8211;</p>
                                <pre> <code> S = ln(2.2y - 1.2)</code></pre>
                                <p>If 1 &lt; y &lt; 1.2, otherwise &#8211;</p>
                                <pre> <code> S = ln(y) / (-0.0523 + 3.182.ln(y) - 0.8725.(ln(y)) < sup > 2</sup> + 0.01853.(ln(y))<sup>4</sup> )</code></pre>
                                <p> where y Is defined As</p>
                                <pre> <code> y = c < Sub() > L</Sub> / E<Sub>L</Sub>(?)²</code></pre>
                                <p> Pressure loss due To friction Is:</p>
                                <pre> <code>(dP/dZ)<Sub>Fric.</Sub>= 2.f<Sub>TP</Sub>.V<Sub>m</Sub>².?<Sub>NS</Sub> /(144.g<Sub>c</Sub>.D)</code></pre>
                                <p> Pressure loss due To acceleration, factor E<Sub>k</Sub> Is given by:</p>
                                <pre> <code> E<Sub>k</Sub>= ?<Sub>m</Sub>.V<Sub>m</Sub>.V<Sub>sg</Sub>/(g<Sub>c</Sub>.P)</code></pre>
                                <p> where, V<Sub>sg</Sub> Is no slip gas velocity And P Is gas pressure.")

            IObj?.Paragraphs.Add("Reference: <a href='https://cheguide.com/2015/08/beggs-brill-method/'>https://cheguide.com/2015/08/beggs-brill-method/</a>")

            IObj?.Paragraphs.Add("<h2>Input Parameters</h2>")

            IObj?.Paragraphs.Add("<mi>D</mi> = " & D & " m")
            IObj?.Paragraphs.Add("<mi>L</mi> = " & L & " m")
            IObj?.Paragraphs.Add("<mi>H</mi> = " & deltaz & " m")
            IObj?.Paragraphs.Add("<mi>k</mi> = " & k & " m")
            IObj?.Paragraphs.Add("<mi>Q_V</mi> = " & qv & " m3/d actual")
            IObj?.Paragraphs.Add("<mi>Q_L</mi> = " & ql & " m3/d actual")
            IObj?.Paragraphs.Add("<mi>\mu _V</mi> = " & muv & " cP")
            IObj?.Paragraphs.Add("<mi>\mu _L</mi> = " & mul & " cP")
            IObj?.Paragraphs.Add("<mi>\rho _V</mi> = " & rhov & " kg/m3")
            IObj?.Paragraphs.Add("<mi>\rho _L</mi> = " & rhol & " kg/m3")
            IObj?.Paragraphs.Add("<mi>\sigma</mi> = " & surft & " N/m")

            IObj?.Paragraphs.Add("<h2>Calculated Parameters</h2>")

            CalculateDeltaP = Nothing

            Dim ResVector(4) As Object

            If qv = 0.0# Then

                ResVector = Me.CalculateDeltaPLiquid(D, L, deltaz, k, ql, mul, rhol)
                CalculateDeltaP = ResVector

            ElseIf ql = 0.0# Then

                ResVector = Me.CalculateDeltaPGas(D, L, deltaz, k, qv, muv, rhov)
                CalculateDeltaP = ResVector

            Else


                'qv =  vazao de gas em m3/d reais
                'ql = vazao de liquido em m3/d reais
                'muv = viscosidade do gas em cP
                'mul = viscosidade do liquido em cP
                'rhov = densidade do gas em kg/m3
                'rhol = densidade do liquido em kg/m3
                'd = diametro da tubulacao em metros
                'l = comprimento da tubulacao em metros
                'deltaz = elevacao da tubulacao em metros
                'surft = tensao superficial em N/m
                'k = rugosidade do duto

                'diametro de metros para ft
                D = D * 3.28084

                'vazoes de m3/d para ft3/s
                qv = qv / 24 * 0.00980963
                ql = ql / 24 * 0.00980963

                'massas especificas de kg/m3 para lb/ft3
                rhov = rhov * 0.062428
                rhol = rhol * 0.062428

                'tensao superficial de N/m para dyn/cm
                surft = surft * 1000

                'distancias de metros para ft
                L = L * 3.28084
                deltaz = deltaz * 3.28084

                'inclinacao da tubulacao (graus)
                Dim teta = Math.Atan(deltaz / (L ^ 2 - deltaz ^ 2) ^ 0.5) * 180 / Math.PI

                'area da secao transversal (ft2)
                Dim ap = Math.PI * D ^ 2 / 4

                'velocidade da mistura (ft/s)
                Dim vm = (qv + ql) / ap

                'fracao volumetrica de liquido
                Dim Cl = ql / (qv + ql)

                IObj?.Paragraphs.Add("<mi>v_m</mi> = " & vm & " m/s")

                'calculo do regime de fluxo

                Dim L1 = 316 * Cl ^ 0.302
                Dim L2 = 0.0009252 * Cl ^ -2.4684
                Dim L3 = 0.1 * Cl ^ -1.4516
                Dim L4 = 0.5 * Cl ^ -6.738

                'Numero de Froude p/ a mistura
                Dim Frm = vm ^ 2 / (32.2 * D)

                IObj?.Paragraphs.Add("<mi>Fr_m</mi> = " & Frm)

                Dim fluxo As String
                fluxo = ("NA")
                If Cl < 0.01 And Frm < L1 Then fluxo = "Segregated"
                If Cl >= 0.01 And Frm < L2 Then fluxo = "Segregated"
                If Cl >= 0.01 And Cl < 0.4 And Frm > L3 And Frm <= L1 Then fluxo = "Intermittent"
                If Cl >= 0.4 And Frm > L3 And Frm <= L4 Then fluxo = "Intermittent"
                If Cl < 0.4 And Frm >= L1 Then fluxo = "Distributed"
                If Cl >= 0.4 And Frm > L4 Then fluxo = "Distributed"
                If Cl >= 0.01 And Frm > L2 And Frm < L3 Then fluxo = "Transition"

                'calculo do ("liquidholdup") horizontal
                Dim El_0, El_teta As Double
                If fluxo = "Segregated" Then
                    El_0 = 0.98 * Cl ^ 0.4846 / (Frm ^ 0.0868)
                ElseIf fluxo = "Intermittent" Then
                    El_0 = 0.845 * Cl ^ 0.5351 / (Frm ^ 0.0173)
                ElseIf fluxo = "Distributed" Then
                    El_0 = 1.065 * Cl ^ 0.5824 / (Frm ^ 0.0609)
                ElseIf fluxo = "Transition" Then
                    Dim AI = (L3 - Frm) / (L3 - L2)
                    Dim BI = 1 - AI
                    El_0 = AI * 0.98 * Cl ^ 0.4846 / (Frm ^ 0.0868) + BI * 0.845 * Cl ^ 0.5351 / (Frm ^ 0.0173)
                Else
                    ' the regime map covers the plane, but a NaN Froude number or holdup would fall through
                    ' to here and leave El_0 at zero, which reads as a pipe full of gas. No-slip is the
                    ' honest answer when the map cannot place the point.
                    El_0 = Cl
                End If

                ' "E_L(0) must be greater than C_L; if E_L(0) is smaller than C_L, then E_L(0) is assigned a
                ' value of C_L" - Beggs-Brill, and the help text at the top of this file says so too. It was
                ' never applied. Besides being wrong on its own (a slip holdup below the no-slip one means
                ' the liquid travels faster than the mixture), leaving it out is what made the holdup STEP
                ' at a regime boundary: with the clamp active on both sides the value is Cl either way and
                ' the crossing is invisible, without it the two regime formulas disagree. Measured on a
                ' vertical well tubing, the steps in dP(W) were 338 kPa at 10.79 kg/s and 45 kPa at 11.70.
                If El_0 < Cl Then El_0 = Cl

                'calculo do fator de inclinacao
                Dim beta As Double
                Dim vsl = ql / ap
                Dim Nvl = 1.938 * vsl * (rhol / (32.2 * surft)) ^ 0.25

                If deltaz > 0 Then
                    If fluxo = "Segregated" Then
                        beta = BetaSegregated(Cl, Frm, Nvl)
                    ElseIf fluxo = "Intermittent" Then
                        beta = BetaIntermittent(Cl, Frm, Nvl)
                    ElseIf fluxo = "Distributed" Then
                        beta = 0
                    ElseIf fluxo = "Transition" Then
                        ' Beggs-Brill interpolates the INCLINED holdup across the transition band, so the
                        ' inclination factor has to be interpolated with the same A/B weights the holdup
                        ' above uses. There was no branch here, so inside the band beta kept its zero
                        ' default and the inclination correction vanished: B_teta collapsed to 1 and the
                        ' mixture density stepped, which on an uphill pipe is a step in static head. A
                        ' vertical well tubing crossing the band showed jumps of 45 to 340 kPa in dP(W),
                        ' enough to stall the pipe network's outer loop in a limit cycle.
                        Dim wSeg = (L3 - Frm) / (L3 - L2)
                        Dim wInt = 1 - wSeg
                        beta = wSeg * BetaSegregated(Cl, Frm, Nvl) + wInt * BetaIntermittent(Cl, Frm, Nvl)
                    End If
                Else
                    beta = (1 - Cl) * Math.Log(4.7 * Nvl ^ 0.1244 / (Cl ^ 0.3692 * Frm ^ 0.5056))
                End If
                If beta < 0 Then beta = 0

                Dim B_teta = 1 + beta * (Math.Sin(1.8 * teta * Math.PI / 180) - 0.3333 * (Math.Sin(1.8 * teta * Math.PI / 180)) ^ 3)

                El_teta = El_0 * B_teta

                'calculo da densidade da mistura
                Dim rhom = rhol * El_teta + rhov * (1 - El_teta)

                IObj?.Paragraphs.Add("<mi>\rho_M</mi> = " & rhom & " kg/m3")

                'calculo do delta P em funcao da carga hidrostatica em lbf/ft2
                Dim dP_hh = rhom * deltaz

                'calculo do delta P em funcao da friccao
                Dim y = Math.Log(Cl / El_teta ^ 2)

                Dim S = 0.0#
                If y > 1 And y < 1.2 Then
                    S = Math.Log(2.2 * Math.Exp(y) - 1.2)
                Else
                    S = y / (-0.0523 + 3.182 * y - 0.8725 * y ^ 2 + 0.01853 * y ^ 4)
                End If

                IObj?.Paragraphs.Add("<mi>S</mi> = " & S)

                'calculo do numero de Reynolds
                Dim rho_ns = Cl * rhol + (1 - Cl) * rhov
                Dim mu_ns = Cl * mul + (1 - Cl) * muv
                Dim NRe_ns = rho_ns * vm * D / (mu_ns * 0.00067197)

                'Dim k = 0.0018 ' Rugosidade do duto - aco carbono

                'calculo do fator de friccao
                Dim f_ns = 0.0#

                f_ns = FrictionFactor(NRe_ns, D / 3.28084, k)

                IObj?.Paragraphs.Add("<mi>Re_{ns}</mi> = " & NRe_ns)
                IObj?.Paragraphs.Add("<mi>f_{ns}</mi> = " & f_ns)

                'calculo do fator de friccao bifasico
                Dim f_tp = f_ns * Math.Exp(S)

                IObj?.Paragraphs.Add("<mi>f_{tp}</mi> = " & f_tp)

                ' delta P devido a friccao em lbf/ft2
                'Dim dP_fr = 2 * f_tp * vm ^ 2 * rho_ns * L / (32.2 * D)
                Dim dP_fr = f_tp * vm ^ 2 / 2 * rho_ns * L / (32.2 * D)

                ResVector(0) = fluxo
                ResVector(1) = El_teta
                ResVector(2) = ((dP_fr) * 47.88)
                ResVector(3) = ((dP_hh) * 47.88)
                ResVector(4) = ((dP_hh + dP_fr) * 47.88)

                CalculateDeltaP = ResVector

            End If

            IObj?.Paragraphs.Add("<h2>Results</h2>")

            IObj?.Paragraphs.Add("Flow Regime: " & ResVector(0))
            IObj?.Paragraphs.Add("<mi>e_L</mi> = " & ResVector(1))
            IObj?.Paragraphs.Add("<mi>\Delta P_{friction}</mi> = " & ResVector(2) & " Pa")
            IObj?.Paragraphs.Add("<mi>\Delta P_{elevation}</mi> = " & ResVector(3) & " Pa")
            IObj?.Paragraphs.Add("<mi>\Delta P_{total}</mi> = " & ResVector(4) & " Pa")

            IObj?.Close()

        End Function

        ''' <summary>
        ''' Beggs-Brill inclination factor C for uphill SEGREGATED flow, clamped at zero as the correlation
        ''' requires. Shared with the transition blend so the two cannot drift apart, which is exactly what
        ''' would leave a step at the edge of the band.
        ''' </summary>
        Private Shared Function BetaSegregated(Cl As Double, Frm As Double, Nvl As Double) As Double
            Dim b = (1 - Cl) * Math.Log(0.011 * Nvl ^ 3.539 / (Cl ^ 3.768 * Frm ^ 1.614))
            If Double.IsNaN(b) OrElse b < 0 Then Return 0
            Return b
        End Function

        ''' <summary>The same for uphill INTERMITTENT flow.</summary>
        Private Shared Function BetaIntermittent(Cl As Double, Frm As Double, Nvl As Double) As Double
            Dim b = (1 - Cl) * Math.Log(2.96 * Cl ^ 0.305 * Frm ^ 0.0978 / Nvl ^ 0.4473)
            If Double.IsNaN(b) OrElse b < 0 Then Return 0
            Return b
        End Function


        ''' <summary>
        ''' Derivative of the total pressure drop with respect to the gas and liquid volumetric rates, as
        ''' {d(dP)/dqv, d(dP)/dql} in Pa per (m3/day). Returns Nothing for single-phase input, where the
        ''' caller should difference the single-phase routines instead.
        '''
        ''' Obtained by the COMPLEX-STEP approximation: evaluate the correlation at qv + ih and read
        ''' Im(f)/h. Because no subtraction of nearby values takes place, there is no cancellation, so h
        ''' can be taken far below what a finite difference tolerates and the result is accurate to
        ''' machine precision with no step size to choose. That matters here: differencing this
        ''' correlation by hand gave slopes that moved with the step, because the flow-regime map and the
        ''' holdup clamps make it only piecewise smooth. Within one regime the complex step is exact.
        '''
        ''' The regime and the clamps are decided on the REAL part, so the derivative returned is the one
        ''' of the branch the operating point sits on. At a regime boundary the true derivative jumps, and
        ''' no method can report a single value for both sides.
        ''' </summary>
        Public Function CalculateDeltaPGradient(ByVal D As Double, ByVal L As Double, ByVal deltaz As Double,
                                                ByVal k As Double, ByVal qv As Double, ByVal ql As Double,
                                                ByVal muv As Double, ByVal mul As Double, ByVal rhov As Double,
                                                ByVal rhol As Double, ByVal surft As Double) As Double()

            If qv = 0.0# OrElse ql = 0.0# Then Return Nothing

            Const h As Double = 1.0E-30

            Dim dqv = DeltaPComplex(D, L, deltaz, k, New Complex(qv, h), New Complex(ql, 0.0),
                                    muv, mul, rhov, rhol, surft).Imaginary / h
            Dim dql = DeltaPComplex(D, L, deltaz, k, New Complex(qv, 0.0), New Complex(ql, h),
                                    muv, mul, rhov, rhol, surft).Imaginary / h

            If Double.IsNaN(dqv) OrElse Double.IsNaN(dql) Then Return Nothing
            If Double.IsInfinity(dqv) OrElse Double.IsInfinity(dql) Then Return Nothing

            Return New Double() {dqv, dql}

        End Function

        ''' <summary>
        ''' Complex twin of the two-phase branch of <see cref="CalculateDeltaP"/>, returning the total drop
        ''' in Pa. It exists only to carry the complex step; every line mirrors the real routine, and
        ''' BeggsBrillGradientTests asserts that the two agree on the real part, so the pair cannot drift
        ''' apart unnoticed.
        ''' </summary>
        Private Shared Function DeltaPComplex(ByVal D As Double, ByVal L As Double, ByVal deltaz As Double,
                                              ByVal k As Double, ByVal qvc As Complex, ByVal qlc As Complex,
                                              ByVal muv As Double, ByVal mul As Double, ByVal rhov As Double,
                                              ByVal rhol As Double, ByVal surft As Double) As Complex

            D = D * 3.28084
            Dim qvz = qvc / 24 * 0.00980963
            Dim qlz = qlc / 24 * 0.00980963
            rhov = rhov * 0.062428
            rhol = rhol * 0.062428
            surft = surft * 1000
            L = L * 3.28084
            deltaz = deltaz * 3.28084

            Dim teta = Math.Atan(deltaz / (L ^ 2 - deltaz ^ 2) ^ 0.5) * 180 / Math.PI
            Dim ap = Math.PI * D ^ 2 / 4
            Dim vm = (qvz + qlz) / ap
            Dim Cl = qlz / (qvz + qlz)
            Dim L1 = 316 * Complex.Pow(Cl, 0.302)
            Dim L2 = 0.0009252 * Complex.Pow(Cl, -2.4684)
            Dim L3 = 0.1 * Complex.Pow(Cl, -1.4516)
            Dim L4 = 0.5 * Complex.Pow(Cl, -6.738)
            Dim Frm = vm * vm / (32.2 * D)

            Dim clr = Cl.Real, frr = Frm.Real
            Dim l1r = L1.Real, l2r = L2.Real, l3r = L3.Real, l4r = L4.Real

            Dim fluxo As String = "NA"
            If clr < 0.01 And frr < l1r Then fluxo = "Segregated"
            If clr >= 0.01 And frr < l2r Then fluxo = "Segregated"
            If clr >= 0.01 And clr < 0.4 And frr > l3r And frr <= l1r Then fluxo = "Intermittent"
            If clr >= 0.4 And frr > l3r And frr <= l4r Then fluxo = "Intermittent"
            If clr < 0.4 And frr >= l1r Then fluxo = "Distributed"
            If clr >= 0.4 And frr > l4r Then fluxo = "Distributed"
            If clr >= 0.01 And frr > l2r And frr < l3r Then fluxo = "Transition"

            Dim El_0 As Complex
            If fluxo = "Segregated" Then
                El_0 = 0.98 * Complex.Pow(Cl, 0.4846) / Complex.Pow(Frm, 0.0868)
            ElseIf fluxo = "Intermittent" Then
                El_0 = 0.845 * Complex.Pow(Cl, 0.5351) / Complex.Pow(Frm, 0.0173)
            ElseIf fluxo = "Distributed" Then
                El_0 = 1.065 * Complex.Pow(Cl, 0.5824) / Complex.Pow(Frm, 0.0609)
            ElseIf fluxo = "Transition" Then
                Dim AI = (L3 - Frm) / (L3 - L2)
                Dim BI = 1 - AI
                El_0 = AI * 0.98 * Complex.Pow(Cl, 0.4846) / Complex.Pow(Frm, 0.0868) +
                       BI * 0.845 * Complex.Pow(Cl, 0.5351) / Complex.Pow(Frm, 0.0173)
            Else
                El_0 = Cl
            End If
            If El_0.Real < clr Then El_0 = Cl

            Dim beta As Complex = New Complex(0.0, 0.0)
            Dim vsl = qlz / ap
            Dim Nvl = 1.938 * vsl * (rhol / (32.2 * surft)) ^ 0.25
            If deltaz > 0 Then
                If fluxo = "Segregated" Then
                    beta = BetaSegregatedComplex(Cl, Frm, Nvl)
                ElseIf fluxo = "Intermittent" Then
                    beta = BetaIntermittentComplex(Cl, Frm, Nvl)
                ElseIf fluxo = "Distributed" Then
                    beta = New Complex(0.0, 0.0)
                ElseIf fluxo = "Transition" Then
                    Dim wSeg = (L3 - Frm) / (L3 - L2)
                    Dim wInt = 1 - wSeg
                    beta = wSeg * BetaSegregatedComplex(Cl, Frm, Nvl) + wInt * BetaIntermittentComplex(Cl, Frm, Nvl)
                End If
            Else
                beta = (1 - Cl) * Complex.Log(4.7 * Complex.Pow(Nvl, 0.1244) /
                                              (Complex.Pow(Cl, 0.3692) * Complex.Pow(Frm, 0.5056)))
            End If
            If beta.Real < 0 Then beta = New Complex(0.0, 0.0)

            Dim sinT = Math.Sin(1.8 * teta * Math.PI / 180)
            Dim B_teta = 1 + beta * (sinT - 0.3333 * sinT ^ 3)
            Dim El_teta = El_0 * B_teta
            Dim rhom = rhol * El_teta + rhov * (1 - El_teta)
            Dim dP_hh = rhom * deltaz

            Dim y = Complex.Log(Cl / (El_teta * El_teta))
            Dim S As Complex
            If y.Real > 1 And y.Real < 1.2 Then
                S = Complex.Log(2.2 * Complex.Exp(y) - 1.2)
            Else
                S = y / (-0.0523 + 3.182 * y - 0.8725 * y * y + 0.01853 * y * y * y * y)
            End If

            Dim rho_ns = Cl * rhol + (1 - Cl) * rhov
            Dim mu_ns = Cl * mul + (1 - Cl) * muv
            Dim NRe_ns = rho_ns * vm * D / (mu_ns * 0.00067197)
            Dim f_ns = FrictionFactorComplex(NRe_ns, D / 3.28084, k)
            Dim f_tp = f_ns * Complex.Exp(S)
            Dim dP_fr = f_tp * vm * vm / 2 * rho_ns * L / (32.2 * D)

            Return (dP_hh + dP_fr) * 47.88

        End Function

        ''' <summary>Complex twin of <see cref="FPBaseClass.FrictionFactor"/>.</summary>
        Private Shared Function FrictionFactorComplex(ByVal Re As Complex, ByVal D As Double, ByVal k As Double) As Complex
            Dim ln10 = Math.Log(10.0)
            If Re.Real > 4000 Then
                Dim a1 = Complex.Log((k / D) ^ 1.1096 / 2.8257 + Complex.Pow(5.8506 / Re, 0.8961)) / ln10
                Dim b1 = -2 * Complex.Log((k / D) / 3.7065 - 5.0452 * a1 / Re) / ln10
                Return 1 / (b1 * b1)
            ElseIf Re.Real < 2100 Then
                Return 64 / Re
            Else
                Dim a = Complex.Pow(8 / Re, 12)
                Dim b = Complex.Pow(2.457 * Complex.Log(1.0 / (Complex.Pow(7.0 / Re, 0.9) + 0.27 * k / D)), 16)
                Dim c = Complex.Pow(37530 / Re, 16)
                Return 8 * Complex.Pow(a + 1 / Complex.Pow(b + c, 1.5), 1.0 / 12.0)
            End If
        End Function

        ''' <summary>Complex twin of <see cref="BetaSegregated"/>.</summary>
        Private Shared Function BetaSegregatedComplex(Cl As Complex, Frm As Complex, Nvl As Complex) As Complex
            Dim b = (1 - Cl) * Complex.Log(0.011 * Complex.Pow(Nvl, 3.539) /
                                           (Complex.Pow(Cl, 3.768) * Complex.Pow(Frm, 1.614)))
            If Double.IsNaN(b.Real) OrElse b.Real < 0 Then Return New Complex(0.0, 0.0)
            Return b
        End Function

        ''' <summary>Complex twin of <see cref="BetaIntermittent"/>.</summary>
        Private Shared Function BetaIntermittentComplex(Cl As Complex, Frm As Complex, Nvl As Complex) As Complex
            Dim b = (1 - Cl) * Complex.Log(2.96 * Complex.Pow(Cl, 0.305) * Complex.Pow(Frm, 0.0978) /
                                           Complex.Pow(Nvl, 0.4473))
            If Double.IsNaN(b.Real) OrElse b.Real < 0 Then Return New Complex(0.0, 0.0)
            Return b
        End Function

    End Class

End Namespace

