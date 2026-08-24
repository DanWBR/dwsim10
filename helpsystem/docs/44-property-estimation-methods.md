# Property Estimation Methods

#### Petroleum Fractions

##### Molecular weight

###### Riazi and Al Sahhaf method  {#riazi-and-al-sahhaf-method .unnumbered}



\[
MM=\left[\frac{1}{0.01964}(6.97996-\ln(1080-T_{b})\right]^{3/2},
\]








where

$MM$ Molecular weight (kg/kmol)

$T_{b}$ Boiling point at 1 atm (K)



If the specific gravity ( $SG$ ) is available, the molecular weight is calculated by



\[
\begin{eqnarray}
MM & = & 42.965[\exp(2.097\times10^{-4}T_{b}-7.78712SG+ \\
 &  & +2.08476\times10^{-3}T_{b}SG)]T_{b}^{1.26007}SG^{4.98308}
\end{eqnarray}
\]


###### Winn  {#winn .unnumbered}



\[
MM=0.00005805PEMe^{2.3776}/d15^{0.9371},
\]


\
where







$PEMe$ Mean Boiling Point (K)

$d15$ Specific Gravity @ 60 °F



###### Riazi {#riazi .unnumbered}



\[
\begin{eqnarray}
MM & = & 42.965\exp(0.0002097PEMe-7.78d15+0.00208476\times PEMe\times d15)\times \\
 &  & \times PEMe^{1.26007}d15^{4.98308}
\end{eqnarray}
\]


###### Lee-Kesler {#lee-kesler-1 .unnumbered}



\[
\begin{eqnarray}
t_{1} & = & -12272.6+9486.4d15+(8.3741-5.9917d15)PEMe\\
t_{2} & = & (1-0.77084d15-0.02058d15^{2})\times \\
 &  & \times(0.7465-222.466/PEMe)\times10^{7}/PEMe\\
t_{3} & = & (1-0.80882d15-0.02226d15^{2})\times \\
 &  & \times(0.3228-17.335/PEMe)\times10^{12}/PEMe^{3}\\
MM & = & t_{1}+t_{2}+t_{3}
\end{eqnarray}
\]


###### Farah {#farah .unnumbered}



\[
\begin{eqnarray}
MM & = & \exp(6.8117+1.3372A-3.6283B)\\
MM & = & \exp(4.0397+0.1362A-0.3406B-0.9988d15+0.0039PEMe),
\end{eqnarray}
\]


\
where







$A,B$ *Walther-ASTM* equation parameters for viscosity calculation



##### Specific Gravity

###### Riazi e Al Sahhaf  {#riazi-e-al-sahhaf .unnumbered}



\[
SG=1.07-\exp(3.56073-2.93886MM^{0.1}),
\]








where

$SG$ Specific Gravity

$MM$ Molecular weight (kg/kmol)



##### Critical Properties

###### Lee-Kesler  {#lee-kesler-2 .unnumbered}



\[
T_{c}=189.8+450.6SG+(0.4244+0.1174SG)T_{b}+(0.1441-1.0069SG)10^{5}/T_{b}
\]




\[
\begin{eqnarray}
\ln P_{c} & = & 5.689-0.0566/SG-(0.43639+4.1216/SG+0.21343/SG^{2})\times \\
 &  & \times10^{-3}T_{b}+(0.47579+1.182/SG+0.15302/SG^{2})\times10^{-6}\times T_{b}^{2}- \\
 &  & -(2.4505+9.9099/SG^{2})\times10^{-10}\times T_{b}^{3},
\end{eqnarray}
\]








where

$T_{b}$ NBP (K)

$T_{c}$ Critical temperature (K)

$P_{c}$ Critical pressure (bar)



###### Farah {#farah-1 .unnumbered}



\[
\begin{eqnarray}
T_{c} & = & 731.968+291.952A-704.998B\\
T_{c} & = & 104.0061+38.75A-41.6097B+0.7831PEMe\\
T_{c} & = & 196.793+90.205A-221.051B+309.534d15+0.524PEMe
\end{eqnarray}
\]




\[
\begin{eqnarray}
P_{c} & = & \exp(20.0056-9.8758\ln(A)+12.2326\ln(B))\\
P_{c} & = & \exp(11.2037-0.5484A+1.9242B+510.1272/PEMe)\\
P_{c} & = & \exp(28.7605+0.7158\ln(A)-0.2796\ln(B)+2.3129\ln(d15)-2.4027\ln(PEMe))
\end{eqnarray}
\]


###### Riazi-Daubert {#riazi-daubert .unnumbered}



\[
\begin{eqnarray}
T_{c} & = & 9.5233\exp(-0.0009314PEMe-0.544442d15+0.00064791\times PEMe\times d15)\times \\
 &  & \times PEMe^{0.81067}d15^{0.53691}
\end{eqnarray}
\]




\[
\begin{eqnarray}
P_{c} & = & 31958000000\exp(-0.008505PEMe-4.8014d15+0.005749\times PEMe\times d15)\times \\
 &  & \times PEMe^{-0.4844}d15^{4.0846}
\end{eqnarray}
\]


###### Riazi {#riazi-1 .unnumbered}



\[
\begin{eqnarray}
T_{c} & = & 35.9413\exp(-0.00069PEMe-1.4442d15+0.000491\times PEMe\times d15)\times \\
 &  & \times PEMe^{0.7293}d15^{1.2771}
\end{eqnarray}
\]


##### Acentric Factor

###### Lee-Kesler method  {#lee-kesler-method .unnumbered}



<a id="eq:W LK"></a>

\[
\omega=\frac{-\ln\frac{P_{c}}{1.10325}-5.92714+6.09648/T_{br}+1.28862\ln T_{br}-0.169347T_{br}^{6}}{15.2518-15.6875/T_{br}-13.472\ln T_{br}+0.43577T_{br}^{6}}
\]


###### Korsten {#korsten .unnumbered}



\[
\omega=0.5899\times((PEMV/T_{c})^{1.3})/(1-(PEMV/T_{c})^{1.3})\times\log(P_{c}/101325)-1
\]


##### Vapor Pressure

###### Lee-Kesler method {#lee-kesler-method-1 .unnumbered}



<a id="eq:PVAP LK"></a>

\[
\begin{eqnarray}
\ln P_{r}^{pv} & = & 5.92714-6.09648/T_{br}-1.28862\ln T_{br}+0.169347T_{br}^{6}+\\
 &  & +\omega(15.2518-15.6875/T_{br}-13.4721\ln T_{br}+0.43577T_{br}^{6}),
\end{eqnarray}
\]








where

$P_{r}^{pv}$ Reduced vapor pressure, $P^{pv}/P_{c}$

$T_{br}$ Reduced NBP, $T_{b}/T_{c}$

$\omega$ Acentric factor



##### Viscosity

###### Letsou-Stiel  {#letsou-stiel .unnumbered}



\[
\begin{eqnarray}
\eta & = & \frac{\xi_{0}+\xi_{1}}{\xi}\\
\xi_{0} & = & 2.648-3.725T_{r}+1.309T_{r}^{2}\\
\xi_{1} & = & 7.425-13.39T_{r}+5.933T_{r}^{2}\\
\xi & = & 176\left(\frac{T_{c}}{MM^{3}{P}_{c}^{4}}\right)^{1/6}
\end{eqnarray}
\]








where

$\eta$ Viscosity (Pa.s)

$P_{c}$ Critical pressure (bar)

$T_{r}$ Reduced temperature, $T/T_{c}$

$MM$ Molecular weight (kg/kmol)



###### Abbott {#abbott .unnumbered}



\[
\begin{eqnarray}
t_{1} & = & 4.39371-1.94733Kw+0.12769Kw^{2}+0.00032629API^{2}-0.0118246KwAPI+ \\
 &  & +(0.171617Kw^{2}+10.9943API+0.0950663API^{2}-0.869218KwAPI\\
\log v_{100} & = & \frac{t_{1}}{API+50.3642-4.78231Kw},
\end{eqnarray}
\]




\[
\begin{eqnarray}
t_{2} & = & -0.463634-0.166532API+0.000513447API^{2}-0.00848995APIKw+ \\
 &  & +(0.080325Kw+1.24899API+0.19768API^{2}\\
\log v_{210} & = & \frac{t_{2}}{API+26.786-2.6296Kw},
\end{eqnarray}
\]


\
where







$v_{100}$ Viscosity at 100 °F (cSt)

$v_{210}$ Viscosity at 210 °F (cSt)

$K_{w}$ Watson characterization factor

$API$ Oil API degree



#### Hypothetical Components

The majority of properties of the hypothetical components is calculated, when necessary, using the group contribution methods, with the UNIFAC structure of the hypo as the basis of calculation. The table [31](#tab:Métodos-de-cálculo) lists the properties and their calculation methods.



<a id="tab:Métodos-de-cálculo"></a>



|  |  |  |
|:--:|:--:|:--:|
| Property | Symbol | Method |
| Critical temperature | $T_{c}$ | Joback |
| Critical pressure | $P_{c}$ | Joback |
| Critical volume | $V_{c}$ | Joback |
| Normal boiling point | $T_{b}$ | Joback |
| Vapor pressure | $P^{pv}$ | Lee-Kesler (Eq. [\[eq:PVAP LK\]](#eq:PVAP LK)) |
| Acentric factor | $\omega$ | Lee-Kesler (Eq. [\[eq:W LK\]](#eq:W LK)) |
| Vaporization enthalpy | $\Delta H_{vap}$ | Vetere |
| Ideal gas heat capacity | $C_{p}^{gi}$ | Harrison-Seaton |
| Ideal gas enthalpy of formation | $\Delta H_{f}^{298}$ | Marrero-Gani |

Hypo calculation methods.



