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



#### Distillation Curves and Petroleum Product Properties {#sec:distillation-curves}

The Distillation Curves utility reports the distillation curves of any material stream (real compounds, pseudocomponents or both) together with the product properties a pool check needs: flash point, densities, viscosities, cold flow points and cetane index. It is meant for the products of crude and vacuum towers, where each cut has to be checked against the pool it goes to (naphtha, jet fuel, diesel, gas oil).

###### Where to find it

In the Windows interface, open it from **Utilities \> Distillation Curves** or attach it to a stream through **Utilities \> Add Utility** (Material Stream, Distillation Curves). In the cross-platform interface, use **Utilities \> Distillation Curves...** or the Utilities tab of the stream editor. The window has the inputs on the left and the curves (plot and table) and product properties on the right; **Copy data** places the tables on the clipboard.

###### Settings

- **Liquid phase only:** calculates the curves of the stream’s liquid phase instead of the whole stream.

- **TBP basis:** liquid volume (default), mass or mole percent distilled for the TBP curve. The D86, D1160 and EFV curves are always on liquid volume and the D2887 curve on mass, as their test methods define them.

- **Percents distilled:** the points to report, separated by commas, from 0 (initial boiling point) to 100 (final boiling point). The default is 0, 5, 10, 30, 50, 70, 90, 95 and 100.

- **D1160 pressure:** the pressure of the D1160 curve (10 mmHg by default).

- **EFV pressure and method:** the pressure of the equilibrium flash vaporization curve, and whether it comes from flash calculations or from the Riazi-Daubert correlation.

##### TBP Curve from the Stream Composition

The true boiling point curve is built from the compounds themselves. The compounds are sorted by normal boiling point $T_{b,i}$; each one occupies its share of the stream on the chosen basis, and boils at its normal boiling point at the middle of that share. On a liquid volume basis the share of compound $i$ is


\[
v_{i}=\frac{x_{i}M_{i}/\rho_{i}^{60}}{\sum_{k}x_{k}M_{k}/\rho_{k}^{60}}
\]


where $\rho_{i}^{60}$ is the standard liquid density of the compound at 60°F (from its specific gravity for a pseudocomponent). With the cumulative fraction $V_{i}=\sum_{k\le i}v_{k}$, compound $i$ gives the point $\left(100\,(V_{i}-v_{i}/2),\;T_{b,i}\right)$, and the curve passes through these points by monotone cubic (Fritsch-Carlson) interpolation, extrapolated linearly to 0 and 100 %. Traces of compounds that fall on the same percent are merged into one point. A stream cut from an assay with several pseudocomponents gives back the assay’s TBP curve; a stream with few compounds gives a coarse curve, and the traces of light or heavy cuts in a side draw show up at the ends of its TBP curve.

##### ASTM D86

The D86 curve comes from the TBP curve by API procedure 3A1.1 in reverse. The 50 % points are related by


\[
T_{50}^{TBP}=0.87180\,\left(T_{50}^{D86}\right)^{1.0258}
\]


(temperatures in °F), and each segment of the curve by $\Delta T^{TBP}=A\,\left(\Delta T^{D86}\right)^{B}$, with the coefficients of Table [58](#tab:d86-tbp). Inverting the power law gives the D86 segments. A D86 segment is never wider than its TBP segment: past that point the inverted power law runs away (a few light ends in a side draw place the TBP initial boiling point far below its 10 % point), so each D86 segment is limited to the TBP segment it comes from.



<a id="tab:d86-tbp"></a>



| Segment (vol %) |  $A$  |  $B$  |
|:----------------|:-------:|:-------:|
| 100–90          | 0.11798 | 1.6606  |
| 90–70           | 3.0419  | 0.75497 |
| 70–50           | 2.5282  | 0.82002 |
| 50–30           | 3.0305  | 0.80076 |
| 30–10           | 4.9004  | 0.71644 |
| 10–0            | 7.4012  | 0.60244 |

API procedure 3A1.1 coefficients



##### ASTM D2887 (Simulated Distillation)

The D2887 curve (mass percent) comes from the TBP curve by API procedure 3A3.1 in reverse: the 50 % points are equal, and each segment follows $\Delta T^{TBP}=C\,\left(\Delta T^{SD}\right)^{D}$ with the coefficients of Table [59](#tab:sd-tbp).



<a id="tab:sd-tbp"></a>



| Segment (%) |  $C$   | $D$  |
|:------------|:--------:|:------:|
| 100–95      | 0.02172  | 1.9733 |
| 95–90       | 0.97476  | 0.8723 |
| 90–70       | 0.31531  | 1.2938 |
| 70–50       | 0.19861  | 1.3975 |
| 50–30       | 0.05342  | 1.6988 |
| 30–10       | 0.011903 | 2.0253 |
| 10–5        | 0.15779  | 1.4296 |

API procedure 3A3.1 coefficients



##### ASTM D1160

The D1160 curve is a vacuum distillation. The TBP curve is first brought to 10 mmHg with the Maxwell-Bonnell vapor pressure relation (API procedure 5A1.19), and the D1160 curve at 10 mmHg follows from the Edmister-Okamoto relation of API figure 3A2.1: the 50 % points are equal, the segments above 50 % are equal, and the segments below 50 % follow the digitized curves of the figure. A pressure other than 10 mmHg is reached again through Maxwell-Bonnell.

##### Equilibrium Flash Vaporization (EFV)

Two methods are offered:

- **Flash:** each EFV point is the temperature at which the vapor holds the given share of the stream’s standard liquid volume, at the EFV pressure. The temperature is found between the bubble and dew points by the Illinois method, with a flash calculation of the property package at each step.

- **Riazi-Daubert correlation:** each EFV point comes from the D86 point at the same percent and the specific gravity ,


\[
T^{EFV}=a\,\left(T^{D86}\right)^{b}SG^{c}
\]


  with temperatures in K and the coefficients of Riazi’s equation 3.16 for each percent.

##### Product Properties

###### Specific gravity, API gravity and Watson K

The specific gravity is the mass of the stream over its standard liquid volume at 60°F, relative to water. The mean average boiling point (MeABP) is the mean of the molal and cubic average boiling points, and the Watson characterization factor is $K_{W}=(1.8\,\mathrm{MeABP})^{1/3}/SG$.

###### Densities and kinematic viscosities

The density at 15°C is anchored on the standard liquid density of the compounds (their specific gravity, which is what the assay measured); the property package gives only how the density changes with temperature. For any temperature $T$,


\[
\rho(T)=SG\,\rho_{w}^{60}\,\frac{\rho_{PP}(T)}{\rho_{PP}(60\,\text{\textdegree F})}
\]


This keeps the densities of heavy cuts right where the liquid density of a cubic equation of state is far off. The densities at 15 and 20°C and the kinematic viscosities at 40 and 100°C (the property package’s dynamic viscosity over this density) are reported.

###### Flash point

API procedure 2B7.1, from the D86 10 % point (°F): closed cup $FP=0.69\,T_{10}^{D86}-118.2$, open cup $FP=0.68\,T_{10}^{D86}-109.6$.

###### Cetane index

Two values are given: the API procedure 2B13.1 index from the API gravity and the MeABP, and the calculated cetane index of ASTM D4737 (procedure A) from the D86 10, 50 and 90 % points (°C) and the density at 15°C (g/mL):


\[
\begin{eqnarray}
CCI & = & 45.2+0.0892\,T_{10N}+(0.131+0.901B)\,T_{50N}+(0.0523-0.420B)\,T_{90N}\\
 &  & +0.00049\,(T_{10N}^{2}-T_{90N}^{2})+107B+60B^{2}
\end{eqnarray}
\]


with $T_{10N}=T_{10}-215$, $T_{50N}=T_{50}-260$, $T_{90N}=T_{90}-310$, $B=\exp(-3.5\,(\rho_{15}-0.85))-1$.

###### Other properties

The aniline point (API 2B9.1), smoke point (API 2B10.1), refraction index, pour, cloud and freezing points and Reid vapor pressure use the correlations of the Petroleum Cold Flow Properties utility, fed with the D86 curve and the specific gravity calculated here. The pour point is left empty when the kinematic viscosity at 100°F lies outside the range the correlation was fitted on (up to about 1000 cSt). The sulfur content is the mass-weighted sulfur of the compounds that carry it.

##### Using the Results

Every curve point and property of an attached utility becomes a property of its stream, named as in the utility’s table: for example `D86 95%`, `TBP 10%`, `EFV 50%` or `Flash Point (Closed Cup)`. They can be read from the spreadsheet, swept in a sensitivity study, used by an Adjust, and used as secondary specifications of the rigorous distillation column (Section ). The utility recalculates whenever its stream changes, and its settings are saved with the simulation.

##### Validation

The conversions reproduce the worked examples of the API Technical Data Book: procedure 3A1.1 (D86 10/30/50/70/90 % points of 350, 380, 404, 433 and 469°F give a TBP of 316.6, 372.6, 411.2, 451.2 and 496.7°F, and back), 3A2.1 (D1160 to TBP at 10 mmHg) and 3A3.1 (simulated distillation to TBP); the Riazi-Daubert EFV correlation reproduces Riazi’s example 3.2, and the product correlations the examples of procedures 2B7.1, 2B9.1, 2B10.1 and 2B13.1. A stream built from pseudocomponents cut from a known TBP curve returns that curve.

##### Limitations

- The curves come from the compounds and their normal boiling points: a stream with few compounds gives a coarse curve.

- The D86, D1160 and D2887 conversions are those of the API Technical Data Book, with its accuracy (a few °F on smooth curves). Points outside the range of a test method, such as the heavy D86 points of an atmospheric residue, are extrapolations.

- The product property correlations were fitted on petroleum fractions; for streams of real compounds far from that class they are indicative only.

#### Chao-Seader Parameters

The Chao-Seader parameters needed by the CS/GS models are the Modified Acentric Factor, Solubility Parameter and Liquid Molar Volume. When absent, the Modified Acentric Factor is taken as the normal acentric factor, either read from the databases or calculated by using the methods described before in this document. The Solubility Parameter is given by



\[
\delta=\left(\frac{\Delta H_{v}-RT}{V_{L}}\right)^{1/2}
\]


where







$\Delta H_{v}$ Molar Heat of Vaporization

$V_{L}$ Liquid Molar Volume at 20 °C



