# Corrosion & Scaling Monitor

The *Corrosion & Scaling Monitor* (CSM) unit operation evaluates corrosion and scaling risks in pipes, heat exchangers, and metallic equipment from an aqueous DWSIM material stream. The operation is a *passthrough*: the outlet stream is identical to the inlet, and results are exposed as extra properties of the unit operation, visible in the object editor and the HTML report.

The calculation is organised in four sequential modules: (1) ionic speciation; (2) corrosion rates; (3) scaling indices; (4) remaining useful life and inhibitor dosing.

#### Ionic Speciation {#sec:csm:speciation}

Ionic speciation determines the concentrations and activities of ions in solution from the analytical (total) composition of the stream (total carbon, sulfide, sulfate and metal cation concentrations) and the operating conditions $T$ and $P$.

##### Ionic Strength {#ionic-strength-2 .unnumbered}

The ionic strength $I$ (mol kg$^{-1}$) is calculated as:


<a id="eq:ionic_strength"></a>

\[
I = \frac{1}{2}\sum_{i} m_i z_i^2
\]


where $m_i$ is the molality of ion $i$ (mol kg$^{-1}$) and $z_i$ is its charge number.

##### Activity Coefficients {#activity-coefficients-1 .unnumbered}

The Extended Debye–Hückel model (EDHE) is used with individual ionic size parameters $a_i$ :


<a id="eq:edhe"></a>

\[
\log_{10}\gamma_i
    = -\frac{A\,z_i^2\,\sqrt{I}}{1 + B\,a_i\,\sqrt{I}}
\]


The Debye–Hückel parameters $A$ and $B$ vary with temperature according to the correlations of :


<a id="eq:A_DH"></a><a id="eq:B_DH"></a>

\[
\begin{align}
  A(T) &= 1.131 + 1.335\times10^{-3}(T-298.15)
            + 1.164\times10^{-5}(T-298.15)^2
   \\
  B(T) &= 3.281 + 5.793\times10^{-3}(T-298.15)
\end{align}
\]


where $T$ is temperature in kelvin. For ionic strengths above 0.5 mol kg$^{-1}$, the Davies model is used as an alternative:


<a id="eq:davies"></a>

\[
\log_{10}\gamma_i
    = -A\,z_i^2
      \left(\frac{\sqrt{I}}{1+\sqrt{I}} - 0.3\,I\right)
\]


##### Equilibrium Constants {#equilibrium-constants-2 .unnumbered}

All equilibrium constants are corrected for temperature. The water ionization constant follows :


<a id="eq:Kw"></a>

\[
\ln K_w(T)
    = -\frac{4808.1}{T} - 7.077\ln T + 26.88
\]


For the carbonate system, the first and second dissociation constants of carbonic acid are:


<a id="eq:Ka1"></a><a id="eq:Ka2"></a>

\[
\begin{align}
  \log_{10}K_{a1,\mathrm{CO_{2}}}(T)
    &\approx -14.84 + 3.3\times10^{-3}(T-298.15)
   \\
  \log_{10}K_{a2,\mathrm{CO_{2}}}(T)
    &\approx -10.33 - 1.4\times10^{-2}(T-298.15)
\end{align}
\]


and for the sulfide system :


<a id="eq:Ka1H2S"></a>

\[
\log_{10}K_{a1,\mathrm{H_{2}S}}(T)
    \approx -6.99 - 6.0\times10^{-3}(T-298.15)
\]


##### Solution Procedure {#solution-procedure .unnumbered}

The electrical charge balance of the solution is:


<a id="eq:charge_balance"></a>

\[
\sum_{\mathrm{cations}} z_i m_i
    = \sum_{\mathrm{anions}} |z_j| m_j
\]


The pH is determined iteratively by Newton–Raphson until $|\Delta\mathrm{pH}| < 10^{-8}$ and $|\Delta I| < 10^{-6}$, recomputing activity coefficients at each iteration. The activity of each species is then:


<a id="eq:activity"></a>

\[
a_i = \gamma_i \, m_i
\]


Table [32](#tab:ion_params) lists the ionic size parameters $a_i$ and charges $z_i$ used in the EDHE model.



<a id="tab:ion_params"></a>



| Ion         | Formula         | $z_i$ | $a_i$ (Å) |
|:------------|:----------------|:-------:|:-----------:|
| Hydrogen    | H$^{+}$       |    1    |     9.0     |
| Hydroxide   | OH$^{-}$      |   -1    |     3.5     |
| Calcium     | Ca$^{2+}$     |    2    |     6.0     |
| Magnesium   | Mg$^{2+}$     |    2    |     8.0     |
| Barium      | Ba$^{2+}$     |    2    |     5.0     |
| Strontium   | Sr$^{2+}$     |    2    |     5.0     |
| Iron(II)    | Fe$^{2+}$     |    2    |     6.0     |
| Iron(III)   | Fe$^{3+}$     |    3    |     9.0     |
| Sodium      | Na$^{+}$      |    1    |     4.0     |
| Potassium   | K$^{+}$       |    1    |     3.0     |
| Chloride    | Cl$^{-}$      |   -1    |     3.0     |
| Sulfate     | SO$_{4}^{2-}$ |   -2    |     4.0     |
| Bicarbonate | HCO$_{3}^{-}$ |   -1    |     4.0     |
| Carbonate   | CO$_{3}^{2-}$ |   -2    |     4.5     |
| Bisulfide   | HS$^{-}$      |   -1    |     3.5     |
| Sulfide     | S$^{2-}$      |   -2    |     5.0     |

Ionic parameters for the Extended Debye–Hückel model .



#### Corrosion Rate Models {#sec:csm:corrosion}

Three corrosion mechanisms are evaluated independently. The total corrosion rate is conservatively estimated as the sum of individual contributions:


<a id="eq:CR_total"></a>

\[
\mathrm{CR}_{\mathrm{total}} = \mathrm{CR}_{\mathrm{CO_{2}}}
                               + \mathrm{CR}_{\mathrm{O_{2}}}
                               + \mathrm{CR}_{\mathrm{H_{2}S}}
\]


Risk is classified according to the thresholds of NACE SP0775 (Table [33](#tab:corr_risk)).



<a id="tab:corr_risk"></a>



| Classification | $\mathrm{CR}_{\mathrm{total}}$ (mm/yr) |
|:---------------|:----------------------------------------:|
| Negligible     |                $<0.025$                |
| Low            |            $0.025$–$0.1$             |
| Moderate       |             $0.1$–$0.25$             |
| High           |             $0.25$–$1.0$             |
| Severe         |                 $>1.0$                 |

Corrosion risk classification .



##### CO$_{2}$ Corrosion — de Waard & Milliams Model {#sec:csm:co2}

The CO$_{2}$ corrosion rate is calculated using the de Waard & Milliams model , extensively validated for oil and gas systems:


<a id="eq:CR_CO2_base"></a>

\[
\log_{10}\mathrm{CR}_{\mathrm{base}}
    = 4.762 - \frac{1710}{T} + 0.67\,\log_{10}p_{\mathrm{CO_{2}}}
\]


where $T$ is temperature in kelvin and $p_{\mathrm{CO_{2}}}$ is the CO$_{2}$ partial pressure in bar. The result is in mm/yr for bare carbon steel at the reference condition ($\mathrm{pH} \approx 3.8$, no protective film).

The corrected rate is:


<a id="eq:CR_CO2"></a>

\[
\mathrm{CR}_{\mathrm{CO_{2}}}
    = \mathrm{CR}_{\mathrm{base}}\;
      f_{T}\; f_{\mathrm{pH}}\; f_{m}\; f_{\mathrm{mat}}
\]


**Temperature factor** $f_{T}$: above approximately 60 °C a precipitated FeCO$_{3}$ layer becomes protective, reducing the corrosion rate. Values are based on NORSOK M-506 (Table [34](#tab:fT)).



<a id="tab:fT"></a>



| Temperature (°C) | $f_{T}$ |
|:----------------:|:---------:|
|     $<60$      |   1.00    |
|      60–80       |   0.80    |
|      80–100      |   0.60    |
|     100–120      |   0.30    |
|     $>120$     |   0.15    |

Temperature factor $f_{T}$ for CO$_{2}$ corrosion .



**pH factor** $f_{\mathrm{pH}}$: correction relative to the reference pH of a CO$_{2}$-only solution (no added alkalinity):


<a id="eq:f_pH"></a>

\[
f_{\mathrm{pH}}
    = \min\!\left(10,\;
        10^{0.317\,(\mathrm{pH}_{\mathrm{ref}} - \mathrm{pH})}\right)
\]


The upper limit of 10 prevents pH from reducing the rate by more than one order of magnitude below the reference.

**Mass-transfer factor** $f_{m}$: under turbulent flow the resistance to H$^{+}$ diffusion towards the metal surface may limit the rate. Using the Chilton–Colburn analogy:


<a id="eq:Sherwood"></a><a id="eq:km"></a><a id="eq:fm"></a>

\[
\begin{align}
  Sh &= 0.023\,Re^{0.8}\,Sc^{1/3}  \\
  k_{m} &= \frac{Sh\,D_{\mathrm{CO_{2}}}}{d}  \\
  f_{m} &= \frac{k_{m}\,k_{r}}{(k_{m}+k_{r})\,k_{r}}
\end{align}
\]


where $Sc = \nu/D_{\mathrm{CO_{2}}}$, $k_{r}$ is the surface reaction rate constant , and $d$ is the internal diameter.

**Material factor** $f_{\mathrm{mat}}$: corrosion-resistant alloys exhibit substantially lower rates than carbon steel (Table [35](#tab:fmat)).



<a id="tab:fmat"></a>



| Material                       | $f_{\mathrm{mat}}$ |
|:-------------------------------|:--------------------:|
| Carbon steel (API 5L X52/X65)  |        1.000         |
| 13% Cr martensitic alloy steel |        0.050         |
| Duplex stainless steel 2205    |        0.010         |
| Inconel 625                    |        0.001         |
| Titanium Gr. 2                 |        0.000         |

Material factor $f_{\mathrm{mat}}$ for CO$_{2}$ corrosion.



##### Dissolved O$_{2}$ Corrosion {#sec:csm:o2}

Oxygen corrosion is controlled by the cathodic limiting current for O$_{2}$ reduction:


<a id="eq:iL_O2"></a>

\[
i_{L} = 4F\,k_{m,\mathrm{O_{2}}}\,[\mathrm{O_{2}}]
\]


where $F = 96485$ C mol$^{-1}$, $k_{m,\mathrm{O_{2}}}$ is the O$_{2}$ mass-transfer coefficient (m s$^{-1}$), and $[\mathrm{O_{2}}]$ is the molar concentration (mol L$^{-1}$). Conversion to corrosion rate (NACE RP0176 factor for iron, $n=2$):


<a id="eq:CR_O2"></a>

\[
\mathrm{CR}_{\mathrm{O_{2}}}
    = i_{L}\,f_{T}\,f_{\mathrm{Cl}}\,f_{\mathrm{mat}}
      \times 1.16\times10^{-3}
\]


The chloride factor accounts for passive film breakdown:


<a id="eq:f_Cl"></a>

\[
f_{\mathrm{Cl}}
    = 1 + 0.5\,\log_{10}\!\left(1 + \frac{m_{\mathrm{Cl^{-}}}}{0.1}\right)
\]


##### H$_{2}$S Corrosion and SSC Assessment {#sec:csm:h2s}

The uniform H$_{2}$S corrosion rate follows :


<a id="eq:CR_H2S"></a>

\[
\mathrm{CR}_{\mathrm{H_{2}S}}
    = 0.1\,p_{\mathrm{H_{2}S}}^{0.36}\,
      \exp\!\left[-3200\!\left(\frac{1}{T}-\frac{1}{298.15}\right)\right]
      f_{v}\,f_{\mathrm{pH}}\,f_{\mathrm{mat}}
\]


where $p_{\mathrm{H_{2}S}}$ is in kPa and:


<a id="eq:fv_H2S"></a>

\[
f_{v} = 1 + 0.15\,v^{1.2}
\]


with $v$ the fluid velocity in m s$^{-1}$.

**Sulfide Stress Cracking (SSC) assessment** follows NACE MR0175 / ISO 15156 . The severity index is:


<a id="eq:SSC_IS"></a><a id="eq:pH_lim"></a>

\[
\begin{align}
  \mathrm{IS} &= \mathrm{pH}_{\mathrm{lim}} - \mathrm{pH}
   \\
  \mathrm{pH}_{\mathrm{lim}}
    &= 3.5 + 0.5\,\log_{10}\!\left(\frac{p_{\mathrm{H_{2}S}}}{100}\right)
\end{align}
\]


SSC risk is flagged when IS $>0$, $p_{\mathrm{H_{2}S}} \geq 0.3$ kPa, hardness exceeds 250 HB (22 HRC) or working stress exceeds 450 MPa, and the material is susceptible (carbon steel or 13% Cr).

#### Scaling Indices {#sec:csm:scaling}

The general saturation index $\mathrm{SI}_{i}$ for mineral species $i$ is:


<a id="eq:SI"></a>

\[
\mathrm{SI}_{i} = \log_{10}\!\frac{Q_{i}}{K_{\mathrm{sp},i}(T)}
\]


where $Q_{i}$ is the ionic product computed from ionic activities and $K_{\mathrm{sp},i}(T)$ is the solubility product. $\mathrm{SI} > 0$ indicates supersaturation (precipitation risk); $\mathrm{SI} < 0$ indicates undersaturation.

##### Solubility Products {#solubility-products .unnumbered}

Temperature-dependent $K_{\mathrm{sp}}$ values are given in Table [36](#tab:Ksp).



<a id="tab:Ksp"></a>



| Mineral | Formula | $\log_{10}K_{\mathrm{sp}}(T)$ |
|:---|:---|:---|
| Calcite | CaCO$_{3}$ | $-171.91 - 0.0780\,T + 2839.3/T + 71.60\log_{10}T$ |
| Gypsum | CaSO$_{4}{\cdot}2$H$_{2}$O | $-4.481 + 9.516\times10^{-3}T_{C} - 1.077\times10^{-4}T_{C}^{2}$ |
| Anhydrite | CaSO$_{4}$ | $-4.268 - 1.869\times10^{-3}T_{C} + 2.577\times10^{-7}T_{C}^{2}$ |
| Barite | BaSO$_{4}$ | $-9.90 - 1.24\times10^{-2}T_{C} + 5.9\times10^{-5}T_{C}^{2}$ |
| Celestite | SrSO$_{4}$ | $-6.63 - 6.7\times10^{-3}T_{C}$ |
| Siderite | FeCO$_{3}$ | $-10.89 + 3.0\times10^{-3}(T-298.15)$ |
| Mackinawite | FeS | $-3.60 - 8.0\times10^{-3}(T-298.15)$ |

Solubility product correlations for the main mineral phases.



$T$ in kelvin; $T_{C} = T - 273.15$ in °C.

##### Langelier Saturation Index (LSI) {#langelier-saturation-index-lsi .unnumbered}

The LSI quantifies the CaCO$_{3}$ precipitation tendency:


<a id="eq:LSI"></a>

\[
\mathrm{LSI} = \mathrm{pH} - \mathrm{pH}_{s}
\]




<a id="eq:pHs"></a>

\[
\mathrm{pH}_{s}
    = \log_{10}\!\frac{K_{a2}}{K_{\mathrm{sp,calcite}}}
      - \log_{10}(a_{\mathrm{Ca^{2+}}})
      - \log_{10}(a_{\mathrm{HCO_{3}^{-}}})
\]


LSI $> 0$: scale-forming; LSI $< 0$: corrosive (undersaturated).

##### Ryznar Stability Index (RSI) {#ryznar-stability-index-rsi .unnumbered}

The RSI provides better field correlation than LSI:


<a id="eq:RSI"></a>

\[
\mathrm{RSI} = 2\,\mathrm{pH}_{s} - \mathrm{pH}
\]


Interpretation is given in Table [37](#tab:RSI).



<a id="tab:RSI"></a>



|   RSI    | Tendency                         |
|:--------:|:---------------------------------|
| $<4.5$ | Severe scaling                   |
| 4.5–5.5  | Heavy scaling                    |
| 5.5–6.5  | Some scaling                     |
| 6.5–7.0  | Stable / slight scaling tendency |
| 7.0–8.0  | Stable / slightly corrosive      |
| 8.0–9.0  | Corrosive                        |
| $>9.0$ | Highly corrosive                 |

Interpretation of the Ryznar Stability Index .



##### Stiff–Davis Index (SDI) {#stiffdavis-index-sdi .unnumbered}

For high-ionic-strength solutions ($I > 0.5$ mol kg$^{-1}$, e.g. seawater and produced brines), the SDI corrects for the salinity effect on calcite solubility:


<a id="eq:SDI"></a>

\[
\mathrm{SDI} = \mathrm{pH} - (p\mathrm{Ca} + p\mathrm{Alk} + K')
\]




<a id="eq:Kprime_SD"></a>

\[
K'(T,I)
    = \bigl[1.845 + 8.0\times10^{-3}\,T_{C}
            - 1.0\times10^{-4}\,T_{C}^{2}\bigr]
      - 0.45\,\sqrt{I} - 0.06\,I
\]


#### Remaining Useful Life {#sec:csm:rul}

The Remaining Useful Life (RUL) analysis follows API570 and API 579-1 / ASME FFS-1 .

##### Retirement Thickness {#retirement-thickness .unnumbered}

The minimum required thickness per ASME B31.3 §304.1.2 is:


<a id="eq:t_req"></a>

\[
t_{\mathrm{req}}
    = \frac{P\,D}{2(SE + PY)}
\]


where $P$ is design pressure (MPa), $D$ outside diameter (mm), $S$ allowable stress (MPa), $E$ weld joint efficiency, and $Y = 0.4$ for carbon/alloy steel below 482 °C. The retirement thickness is $t_{\mathrm{ret}} = \max(t_{\min}, t_{\mathrm{req}})$.

##### Design Corrosion Rate {#design-corrosion-rate .unnumbered}

The design rate is the more conservative of the mechanistic model and the inspection-derived rate:


<a id="eq:CR_design"></a>

\[
\mathrm{CR}_{\mathrm{design}}
    = \max(\mathrm{CR}_{\mathrm{model}},\;\mathrm{CR}_{\mathrm{measured}})
\]




<a id="eq:CR_measured"></a>

\[
\mathrm{CR}_{\mathrm{measured}}
    = \frac{t_{\mathrm{prev}} - t_{\mathrm{last}}}
           {\Delta t_{\mathrm{insp}}}
\]


##### RUL Calculation (API 570 Eq. 6.1) {#rul-calculation-api-570-eq.-6.1 .unnumbered}

The estimated current thickness is:


<a id="eq:t_current"></a>

\[
t_{\mathrm{current}}
    = t_{\mathrm{last}} - \mathrm{CR}_{\mathrm{design}}\,\Delta t
\]


and the remaining useful life:


<a id="eq:RUL"></a>

\[
\mathrm{RUL}
    = \frac{t_{\mathrm{current}} - t_{\mathrm{ret}}}
           {\mathrm{CR}_{\mathrm{design}}}
\]


##### MAWP and Inspection Interval {#mawp-and-inspection-interval .unnumbered}

The Maximum Allowable Working Pressure at the current thickness (ASME B31.3) is:


<a id="eq:MAWP"></a>

\[
\mathrm{MAWP}
    = \frac{2\,S\,E\,t_{\mathrm{current}}}{D - 2\,Y\,t_{\mathrm{current}}}
\]


If $\mathrm{MAWP} < P_{\mathrm{design}}$, a de-rating alert is generated.

The inspection interval is set to the lesser of the risk-category maximum and half the RUL :


<a id="eq:t_insp"></a>

\[
t_{\mathrm{insp}}
    = \min\!\bigl(t_{\mathrm{max,cat}},\;\tfrac{1}{2}\,\mathrm{RUL}\bigr)
\]




<a id="tab:insp_intervals"></a>



| Corrosion risk | $t_{\mathrm{max,cat}}$ (years) |
|:---------------|:--------------------------------:|
| Negligible     |                15                |
| Low            |                10                |
| Moderate       |                5                 |
| High           |                2                 |
| Severe         |                1                 |

Maximum inspection intervals by risk category .



#### Chemical Inhibitor Dosing {#sec:csm:inhibitors}

##### Corrosion Inhibitor {#corrosion-inhibitor .unnumbered}

The inhibition efficiency of film-forming amines and phosphates follows the Langmuir adsorption model :


<a id="eq:IE"></a>

\[
\eta = 100\,\bigl[1 - e^{-k_{\mathrm{ads}}\,C_{\mathrm{inh}}}\bigr]
\]




<a id="eq:kads"></a>

\[
k_{\mathrm{ads}}(T)
    = k_{\mathrm{ads}}^{0}\,
      \exp\!\left[-E_{a}\!\left(\frac{1}{T}-\frac{1}{298.15}\right)\right]
\]


Parameters by inhibitor family are listed in Table [39](#tab:inh_params).



<a id="tab:inh_params"></a>



| Type | Application | $k_{\mathrm{ads}}^{0}$ (L mg$^{-1}$) | $E_{a}$ (K) |
|:---|:--:|:--:|:--:|
| Imidazoline + amide | CO$_{2}$ | 0.060 | 1200 |
| Quaternary imidazoline | CO$_{2}$/H$_{2}$S | 0.055 | 1500 |
| Zinc salt + phosphate | O$_{2}$ | 0.040 | — |
| Zinc phosphate film-former | General | 0.050 | — |

Adsorption parameters for the main corrosion inhibitor families .



The required dose and daily product volume are:


<a id="eq:C_inh"></a>

\[
C_{\mathrm{inh}}
    = -\frac{1}{k_{\mathrm{ads}}}\,
       \ln(1 - \eta^{*}), \qquad
  \eta^{*} = 1 - \frac{\mathrm{CR}^{*}}{\mathrm{CR}_{\mathrm{total}}}
\]




<a id="eq:V_prod"></a>

\[
V_{\mathrm{prod}}
    = \frac{C_{\mathrm{inh}}\,Q}{1000\,\chi_{\mathrm{active}}}
\]


where $Q$ is the fluid flow rate (m$^{3}$ day$^{-1}$) and $\chi_{\mathrm{active}}$ is the mass fraction of active ingredient in the commercial product.

##### Scale Inhibitors — Threshold Model {#scale-inhibitors-threshold-model .unnumbered}

Threshold inhibition relies on sub-stoichiometric phosphonate or polymer concentrations to block crystal growth . The threshold dose for CaCO$_{3}$ is:


<a id="eq:Cth_CaCO3"></a>

\[
C_{\mathrm{th,CaCO_{3}}}
    = A\,
      \sqrt{\frac{c_{\mathrm{Ca^{2+}}}}{1000}
            \cdot
            \frac{c_{\mathrm{HCO_{3}^{-}}}}{1000}}\;
      (\mathrm{LSI})^{0.6}\;
      e^{0.02(T_{C} - 25)}\;
      (1 + 0.3\sqrt{I})
\]


where $c$ is in mg L$^{-1}$ and $A = 0.8$ for HEDP (LSI $< 1.5$) or $A = 1.8$ for DTPMP (LSI $\geq 1.5$).

For BaSO$_{4}$ :


<a id="eq:Cth_BaSO4"></a>

\[
C_{\mathrm{th,BaSO_{4}}}
    = 0.45\,
      \sqrt{c_{\mathrm{Ba^{2+}}}}\;
      \mathrm{SI}_{\mathrm{Barite}}^{0.7}\;
      e^{0.025(T_{C} - 25)}\;
      (1 + 0.5\sqrt{I})
\]


When $\mathrm{SI}_{\mathrm{Barite}} > 2.0$, threshold inhibition alone may be insufficient; sulfate removal by nanofiltration or produced-water dilution should be evaluated.

For CaSO$_{4}$ (gypsum / anhydrite), ATMP and polyacrylate blends are preferred:


<a id="eq:Cth_CaSO4"></a>

\[
C_{\mathrm{th,CaSO_{4}}}
    = 1.5\,
      \sqrt{\frac{c_{\mathrm{Ca^{2+}}}}{1000}
            \cdot
            \frac{c_{\mathrm{SO_{4}^{2-}}}}{1000}}\;
      (\mathrm{SI}_{\mathrm{max}} + 1)^{0.7}\;
      e^{0.015(T_{C} - 25)}
\]


The estimated daily chemical cost is:


<a id="eq:cost"></a>

\[
C_{\mathrm{chem}}
    = c_{\mathrm{unit}}\,
      \sum_{j} V_{\mathrm{prod},j}
\]


where $c_{\mathrm{unit}}$ is the unit product cost (USD L$^{-1}$; default: 3.50 USD L$^{-1}$).

#### Incremental Analysis in Heat Exchangers {#sec:csm:hx}

The temperature gradient along a heat exchanger alters local equilibrium constants, saturation indices, and corrosion rates. The CSM divides the exchanger into $N$ segments ($N = 20$ by default) with linear interpolation:


<a id="eq:T_segment"></a><a id="eq:Tw_segment"></a>

\[
\begin{align}
  T_{\mathrm{fluid},i}
    &= T_{\mathrm{in}} + \frac{i}{N-1}\,(T_{\mathrm{out}} - T_{\mathrm{in}})
   \\
  T_{\mathrm{wall},i}
    &= T_{w,\mathrm{in}} + \frac{i}{N-1}\,(T_{w,\mathrm{out}} - T_{w,\mathrm{in}})
\end{align}
\]


In each segment $i$, speciation is recomputed at the local temperature while keeping the inlet analytical concentrations (no accumulated precipitation), and all corrosion and scaling modules are evaluated at the local wall temperature $T_{\mathrm{wall},i}$.

The precipitation front is defined as the relative axial position $x_{f}$ where the LSI crosses zero, indicating the onset of CaCO$_{3}$ deposition along the tube bundle.

#### Configuration Parameters {#sec:csm:params}

All configurable parameters and their defaults are listed in Table [40](#tab:params).



<a id="tab:params"></a>



<table>
<caption>Configuration parameters of the Corrosion &amp; Scaling Monitor.</caption>
<thead>
<tr>
<th style="text-align: left;">Parameter</th>
<th style="text-align: center;">Unit</th>
<th style="text-align: center;">Default</th>
<th style="text-align: left;">Description</th>
</tr>
</thead>
<tbody>
<tr>
<td colspan="4" style="text-align: center;">Table <a href="#tab:params" data-reference-type="ref" data-reference="tab:params">40</a> (continued)</td>
</tr>
<tr>
<td style="text-align: left;">Parameter</td>
<td style="text-align: center;">Unit</td>
<td style="text-align: center;">Default</td>
<td style="text-align: left;">Description</td>
</tr>
<tr>
<td colspan="4" style="text-align: right;">Continued on next page…</td>
</tr>
<tr>
<td style="text-align: left;">Internal diameter</td>
<td style="text-align: center;">m</td>
<td style="text-align: center;">0.1016</td>
<td style="text-align: left;">Internal pipe diameter</td>
</tr>
<tr>
<td style="text-align: left;">Fluid velocity</td>
<td style="text-align: center;">m s<span class="math inline">\(^{-1}\)</span></td>
<td style="text-align: center;">1.0</td>
<td style="text-align: left;">Mean flow velocity</td>
</tr>
<tr>
<td style="text-align: left;"><span class="math inline">\(p_{\mathrm{CO_{2}}}\)</span></td>
<td style="text-align: center;">bar</td>
<td style="text-align: center;">0</td>
<td style="text-align: left;">CO<span class="math inline">\(_{2}\)</span> partial pressure (0 = compute)</td>
</tr>
<tr>
<td style="text-align: left;"><span class="math inline">\(p_{\mathrm{H_{2}S}}\)</span></td>
<td style="text-align: center;">kPa</td>
<td style="text-align: center;">0</td>
<td style="text-align: left;">H<span class="math inline">\(_{2}\)</span>S partial pressure (0 = compute)</td>
</tr>
<tr>
<td style="text-align: left;">Dissolved O<span class="math inline">\(_{2}\)</span></td>
<td style="text-align: center;">ppb</td>
<td style="text-align: center;">0</td>
<td style="text-align: left;">Dissolved oxygen</td>
</tr>
<tr>
<td style="text-align: left;">Material</td>
<td style="text-align: center;">—</td>
<td style="text-align: center;">C. steel</td>
<td style="text-align: left;">Material grade</td>
</tr>
<tr>
<td style="text-align: left;">Working stress</td>
<td style="text-align: center;">MPa</td>
<td style="text-align: center;">200</td>
<td style="text-align: left;">Operating stress</td>
</tr>
<tr>
<td style="text-align: left;">Hardness</td>
<td style="text-align: center;">HB</td>
<td style="text-align: center;">200</td>
<td style="text-align: left;">Brinell hardness</td>
</tr>
<tr>
<td style="text-align: left;">Nominal thickness</td>
<td style="text-align: center;">mm</td>
<td style="text-align: center;">9.53</td>
<td style="text-align: left;">Nominal wall thickness</td>
</tr>
<tr>
<td style="text-align: left;">Minimum thickness</td>
<td style="text-align: center;">mm</td>
<td style="text-align: center;">3.0</td>
<td style="text-align: left;">Minimum allowable thickness</td>
</tr>
<tr>
<td style="text-align: left;">Outside diameter</td>
<td style="text-align: center;">mm</td>
<td style="text-align: center;">114.3</td>
<td style="text-align: left;">Outside diameter</td>
</tr>
<tr>
<td style="text-align: left;">Design pressure</td>
<td style="text-align: center;">MPa</td>
<td style="text-align: center;">5.0</td>
<td style="text-align: left;">Design pressure</td>
</tr>
<tr>
<td style="text-align: left;">Allowable stress</td>
<td style="text-align: center;">MPa</td>
<td style="text-align: center;">138.0</td>
<td style="text-align: left;">Material allowable stress</td>
</tr>
<tr>
<td style="text-align: left;">Last meas. thickness</td>
<td style="text-align: center;">mm</td>
<td style="text-align: center;">0</td>
<td style="text-align: left;">Last UT inspection thickness</td>
</tr>
<tr>
<td style="text-align: left;">Last measurement date</td>
<td style="text-align: center;">—</td>
<td style="text-align: center;">—</td>
<td style="text-align: left;">Date of last UT inspection</td>
</tr>
<tr>
<td style="text-align: left;">Previous thickness</td>
<td style="text-align: center;">mm</td>
<td style="text-align: center;">0</td>
<td style="text-align: left;">Previous inspection thickness</td>
</tr>
<tr>
<td style="text-align: left;">Previous meas. date</td>
<td style="text-align: center;">—</td>
<td style="text-align: center;">—</td>
<td style="text-align: left;">Date of previous inspection</td>
</tr>
<tr>
<td style="text-align: left;">Installation date</td>
<td style="text-align: center;">—</td>
<td style="text-align: center;">—</td>
<td style="text-align: left;">Component installation date</td>
</tr>
<tr>
<td style="text-align: left;">Fluid flow rate</td>
<td style="text-align: center;">m<span class="math inline">\(^{3}\)</span> day<span class="math inline">\(^{-1}\)</span></td>
<td style="text-align: center;">100</td>
<td style="text-align: left;">Total volumetric flow rate</td>
</tr>
<tr>
<td style="text-align: left;">Target corr. rate</td>
<td style="text-align: center;">mm/yr</td>
<td style="text-align: center;">0.10</td>
<td style="text-align: left;">Corrosion rate target after inhibition</td>
</tr>
<tr>
<td style="text-align: left;">Target LSI</td>
<td style="text-align: center;">—</td>
<td style="text-align: center;">0.0</td>
<td style="text-align: left;">Langelier index target</td>
</tr>
<tr>
<td style="text-align: left;">Chemical cost</td>
<td style="text-align: center;">USD L<span class="math inline">\(^{-1}\)</span></td>
<td style="text-align: center;">3.50</td>
<td style="text-align: left;">Unit cost of chemical product</td>
</tr>
</tbody>
</table>



#### Output Properties {#sec:csm:outputs}

After calculation, results are available as `ExtraProperties` of the unit operation, accessible through the DWSIM object editor, the IronPython console, and the CAPE-OPEN API (Table [41](#tab:outputs)).



<a id="tab:outputs"></a>



| Property                | Unit    | Description                 |
|:------------------------|:--------|:----------------------------|
| `CR_Total_mmyr`         | mm/yr   | Total corrosion rate        |
| `CR_CO2_mmyr`           | mm/yr   | CO$_{2}$ contribution     |
| `CR_H2S_mmyr`           | mm/yr   | H$_{2}$S contribution     |
| `CR_O2_mmyr`            | mm/yr   | O$_{2}$ contribution      |
| `pH`                    | —       | Computed pH                 |
| `SSC_Risk`              | —       | 1 = SSC risk; 0 = no risk   |
| `Risk_Level`            | —       | NACE classification         |
| `LSI`                   | —       | Langelier Saturation Index  |
| `RSI`                   | —       | Ryznar Stability Index      |
| `SDI`                   | —       | Stiff–Davis Index           |
| `SI_Barite`             | —       | Barite saturation index     |
| `SI_Gypsum`             | —       | Gypsum saturation index     |
| `SI_Siderite`           | —       | Siderite saturation index   |
| `RUL_yr`                | yr      | Remaining useful life       |
| `t_current_mm`          | mm      | Estimated current thickness |
| `MAWP_MPa`              | MPa     | MAWP at current thickness   |
| `Status`                | —       | Integrity status (API 570)  |
| `Next_Inspection`       | —       | Next inspection date        |
| `Inhibitor_Dose_ppm`    | ppm     | Corrosion inhibitor dose    |
| `Chemical_Cost_USD_day` | USD/day | Daily chemical cost         |

Main output properties of the Corrosion & Scaling Monitor.



