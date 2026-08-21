# Other Properties

#### True Critical Point

The Gibbs criteria for the true critical point of a mixture of $n$ components may be expressed of various forms, but the most convenient when using a pressure explicit cubic equation of state is



<a id="eq:PC1"></a>

\[
L=\left|\begin{array}{cccc}
A_{11} & A_{12} & \ldots & A_{1n}\\
A_{21} & A_{22}\\
\vdots\\
A_{n1} & \ldots & \ldots & A_{nn}
\end{array}\right|=0
\]




<a id="eq:PC2"></a>

\[
M=\left|\begin{array}{cccc}
A_{11} & A_{12} & \ldots & A_{1n}\\
A_{21} & A_{22}\\
\vdots\\
A_{n-1,1} & \ldots & \ldots & A_{n-1,n}\\
\frac{\partial L}{\partial n_{1}} & \ldots & \ldots & \frac{\partial L}{\partial n_{n}}
\end{array}\right|=0,
\]


where



\[
A_{12}=\left(\frac{\partial^{2}A}{\partial n_{1}\partial n_{2}}\right)_{T,V}
\]


All the $A$ terms in the equations [\[eq:PC1\]](#eq:PC1) and [\[eq:PC2\]](#eq:PC2) are the second derivatives of the total Helmholtz energy $\underline{A}$ with respect to mols and constant $T$ and $V$ . The determinants expressed by [\[eq:PC1\]](#eq:PC1) and [\[eq:PC2\]](#eq:PC2) are simultaneously solved for the critical volume and temperature. The critical pressure is then found by using the original EOS.

DWSIM utilizes the method described by Heidemann and Khalil for the true critical point calculation using the *Peng-Robinson* and *Soave-Redlich-Kwong* equations of state.

#### Phase Envelope Lookup Table

DWSIM can optionally pre-compute the phase envelope of a material stream and store it as a lookup table. This table is used for two purposes: fast phase region identification and initial temperature estimation for enthalpy- and entropy-based flash calculations.

###### Phase Region Identification

Given a query point $(T,P)$ , the lookup table determines the phase region by the following algorithm:

1.  If Solid-Liquid Equilibrium data is available, interpolate the solidus temperature $T_{\mathrm{sol}}(P)$ from the SLE curve. If $T<T_{\mathrm{sol}}$ , the point is in the Solid region. If a liquidus curve is also present and $T_{\mathrm{sol}}\leq T\leq T_{\mathrm{liq}}$ , it is Solid+Liquid.

2.  A closed polygon is formed by concatenating the bubble curve (sorted by pressure ascending) with the dew curve (reversed). A ray-casting point-in-polygon test determines whether $(T,P)$ lies inside the Vapor-Liquid Equilibrium region.

3.  For pressures above the critical pressure $P_{c}$ , the averaged Widom line is interpolated at the query pressure. If $T\leq T_{\mathrm{Widom}}(P)$ , the fluid is classified as liquid-like; otherwise as vapor-like.

4.  Outside the VLE envelope at subcritical pressures: if $T\leq T_{c}$ , the phase is Liquid; otherwise Vapor.

###### Flash Initial Estimates

The lookup table stores enthalpy $H$ and entropy $S$ values along the bubble and dew curves. For Pressure-Enthalpy (PH) and Pressure-Entropy (PS) flash calculations, DWSIM interpolates along these curves to provide an initial temperature estimate $T_{0}$ that replaces the default value passed to the iterative solver.

For a PH flash at specified $(P,H)$ :

- The bubble and dew curves are searched for segments where the specified enthalpy is bracketed at the given pressure.

- If the point falls between the bubble enthalpy $H_{\mathrm{bub}}(P)$ and the dew enthalpy $H_{\mathrm{dew}}(P)$ , the temperature is estimated by linear interpolation:



\[
T_{0}=T_{\mathrm{bub}}+\frac{H-H_{\mathrm{bub}}}{H_{\mathrm{dew}}-H_{\mathrm{bub}}}\left(T_{\mathrm{dew}}-T_{\mathrm{bub}}\right)
\]


- If the point is outside the two-phase region, the temperature of the nearest boundary (bubble or dew) is used as the initial estimate.

The same procedure applies to PS flashes using entropy values. These estimates significantly improve convergence in the supercritical and near-critical regions where default initial guesses are often far from the solution.

###### Configuration

The lookup table is controlled by two material stream properties:

- **GeneratePhaseEnvelopeLookup** (Boolean, default False) - enables or disables the lookup table generation. When enabled, the table is built synchronously before each equilibrium calculation.

- **PhaseEnvelopeLookupMode** - selects between*WidomOnly* (bubble, dew and Widom curves) and*FullEnvelope* (additionally includes Solid-Liquid Equilibrium curves).

The table is persisted with the material stream when the simulation is saved, avoiding recalculation on reload. A composition validation check ensures that a persisted table is only reused if the stream composition has not changed.

#### Petroleum Cold Flow Properties

##### Refraction Index

###### API Procedure 2B5.1 {#api-procedure-2b5.1 .unnumbered}



\[
\begin{eqnarray}
I & = & 0.02266\exp(0.0003905\times(1.8MeABP)+2.468SG-0.0005704(1.8MeABP)\times SG)\times \\
 &  & \times(1.8MeABP)^{0.0572}SG^{-0.72}
\end{eqnarray}
\]




\[
r=\left(\frac{1+2I}{1-I}\right)^{1/2}
\]


\
where







$r$ Refraction Index

$SG$ Specific Gravity

$MeABP$ Mean Averaged Boiling Point (K)



##### Flash Point

###### API Procedure 2B7.1 {#api-procedure-2b7.1 .unnumbered}



\[
PF=\left\{ \left[0.69\times((t_{10ASTM}-273.15)\times9/5+32)-118.2\right]-32\right\} \times5/9+273.15
\]


\
where







$PF$ Flash Point (K)

$t_{10ASTM}$ ASTM D93 10% vaporized temperature (K)



##### Pour Point

###### API Procedure 2B8.1 {#api-procedure-2b8.1 .unnumbered}



\[
PFL=\left[753+136(1-\exp(-0.15v_{100}))-572SG+0.0512v_{100}+0.139(1.8MeABP)\right]/1.8
\]


\
where







$PFL$ Pour Point (K)

$v_{100}$ Viscosity @ 100 °F (cSt)



##### Freezing Point

###### API Procedure 2B11.1 {#api-procedure-2b11.1 .unnumbered}



\[
PC=-2390.42+1826SG+122.49K-0.135\times1.8\times MeABP
\]


where







$PC$ Freezing Point (K)

$K$ API characterization factor (API)



##### Cloud Point

###### API Procedure 2B12.1 {#api-procedure-2b12.1 .unnumbered}



\[
PN=\left[10^{(-7.41+5.49\log(1.8MeABP)-0.712\times(1.8MeABP)^{0.315}-0.133SG)}\right]/1.8
\]


where







$PN$ Cloud Point (K)



##### Cetane Index

###### API Procedure 2B13.1 {#api-procedure-2b13.1 .unnumbered}



\[
\begin{eqnarray}
IC & = & 415.26-7.673API+0.186\times(1.8MeABP-458.67)+3.503API\times \\
 &  & \times\log(1.8MeABP-458.67)-193.816\times\log(1.8MeABP-458.67)
\end{eqnarray}
\]


where







$IC$ Cetane Index

$API$ API degree of the oil



#### Chao-Seader Parameters

The Chao-Seader parameters needed by the CS/GS models are the Modified Acentric Factor, Solubility Parameter and Liquid Molar Volume. When absent, the Modified Acentric Factor is taken as the normal acentric factor, either read from the databases or calculated by using the methods described before in this document. The Solubility Parameter is given by



\[
\delta=\left(\frac{\Delta H_{v}-RT}{V_{L}}\right)^{1/2}
\]


where







$\Delta H_{v}$ Molar Heat of Vaporization

$V_{L}$ Liquid Molar Volume at 20 °C



