# Refining Unit Operations

This section describes a suite of shortcut unit operation models intended for conceptual design, mass/energy balance closure, and techno-economic screening of petroleum refineries. Each model is a steady-state, explicit calculation that captures the principal yield, conversion, and contaminant-partitioning behaviour of its reference process without requiring detailed kinetic parameter tuning. All models share a common contaminant vector (sulfur, nitrogen, mercaptan sulfur, Ni/V/Fe, CCR, asphaltenes, TAN) that is carried across the flowsheet through mass-weighted pseudocomponent partitioning.

#### Shortcut Crude Distillation Unit (CDU)

##### Overview {#overview-33}

The **Shortcut CDU** models an atmospheric (optionally coupled with vacuum) crude distillation tower as a set of TBP-cut-point separations. The feed crude assay is characterised by a set of pseudocomponents generated from the bulk TBP curve following the method of Riazi and Daubert . No tray-by-tray calculation is performed; the tower is treated as an idealised sequence of sharp cuts with user-specified overlap expressed through a cut-point uncertainty band.

##### Stream Topology {#stream-topology-5}







| **Port**     | **Direction**     | **Description**                      |
|:-------------|:------------------|:-------------------------------------|
| Crude In     | Inlet (material)  | Preheated crude (post-desalter)      |
| LPG / OffGas | Outlet (material) | Tower overhead light ends            |
| Naphtha      | Outlet (material) | Light + heavy naphtha combined cut   |
| Kerosene     | Outlet (material) | Kerosene / jet side-draw             |
| Diesel       | Outlet (material) | Light + heavy diesel (AGO) side-draw |
| AR / Bottoms | Outlet (material) | Atmospheric residue                  |



##### Cut-Point Distribution

Each pseudocomponent $c$ in the feed is assigned to the product cut whose TBP window contains its normal boiling point $T_{b,c}$:


<a id="eq:cdu_cutweight"></a>

\[
w_{c \rightarrow k}
      = \tfrac{1}{2}\!\left[
          \mathrm{erf}\!\left(\frac{T_{b,c} - T_{k-1}}{\sigma\sqrt{2}}\right)
        - \mathrm{erf}\!\left(\frac{T_{b,c} - T_{k}}{\sigma\sqrt{2}}\right)
        \right]
\]


where $T_{k-1}$ and $T_{k}$ are the lower and upper TBP cut points of product $k$ and $\sigma$ (K) is the cut-point uncertainty. When $\sigma \rightarrow 0$ [\[eq:cdu_cutweight\]](#eq:cdu_cutweight) reduces to a sharp-cut assignment.

The product mass flow is obtained by summing the cut fractions over all pseudocomponents:


<a id="eq:cdu_mprod"></a>

\[
\dot m_k = \sum_{c} w_{c \rightarrow k}\, \dot m_c
\]


##### Contaminant Partitioning

Contaminants (S, N, Ni, V, Fe, CCR, asphaltenes) are distributed according to user-supplied boiling-point-dependent concentration curves or, alternatively, using the default correlations of Gary, Handwerk & Kaiser . Metals and CCR are concentrated in the residue according to


<a id="eq:cdu_contam"></a>

\[
x_k^{\mathrm{cont}} = f_{\mathrm{cont}}(T_{b,k})
\]


with a mass-balance closure step that renormalises the distribution so that the total contaminant mass across all products matches the feed.

#### Hydrodesulphurisation (HDS) Reactor

##### Overview {#overview-34}

The **Shortcut HDS** model represents a fixed-bed hydrotreating reactor operating on middle-distillate feeds. Sulfur conversion follows an $n$-th order power-law kinetic expression with explicit hydrogen partial-pressure dependence , evaluated at liquid hourly space velocity (LHSV). Nitrogen removal is optionally computed using an independent Arrhenius law.

##### Stream Topology {#stream-topology-6}







| **Port**        | **Direction**     | **Description**                         |
|:----------------|:------------------|:----------------------------------------|
| Feed In         | Inlet (material)  | Hydrocarbon feed                        |
| H$_2$ Make-up | Inlet (material)  | Hydrogen feed                           |
| Product Out     | Outlet (material) | Desulfurised hydrocarbon                |
| Sour Gas Out    | Outlet (material) | H$_2$S + NH$_3$ + unreacted H$_2$ |



##### Kinetics

The pseudo-homogeneous sulfur conversion rate is


<a id="eq:hds_rate"></a>

\[
r_{\mathrm{S}} = k(T)\, C_{\mathrm{S}}^{\,n}\, P_{\mathrm{H_2}}^{\,m}
\]


with


<a id="eq:hds_arrhenius"></a>

\[
k(T) = k_0\, \exp\!\left(-\frac{E_a}{R T}\right)
\]


where $n$ is the reaction order in sulfur (typically 1.5–2), $m$ is the hydrogen partial-pressure order, $k_0$ is the pre-exponential factor, $E_a$ is the apparent activation energy, $T$ is the reactor temperature, and $C_{\mathrm{S}}$ is the sulfur mass concentration in the liquid phase.

Integrating [\[eq:hds_rate\]](#eq:hds_rate) over the reactor residence time $\tau = 1/\mathrm{LHSV}$ for $n \ne 1$ gives the sulfur conversion:


<a id="eq:hds_conversion"></a>

\[
X_{\mathrm{S}} = 1 - \left[\,1 + (n-1)\, k\, P_{\mathrm{H_2}}^m
          \, C_{\mathrm{S,0}}^{\,n-1}\, \tau\,\right]^{-1/(n-1)}
\]


Mercaptan sulfur is removed at a user-specified fraction $X_{\mathrm{RSH}}$ and the removed sulfur is added to the sour-gas stream as H$_2$S.

##### Hydrogen Balance

Chemical hydrogen consumption is proportional to the mass of sulfur and (optionally) nitrogen removed:


<a id="eq:hds_h2cons"></a>

\[
\dot n_{\mathrm{H_2}}^{\mathrm{chem}}
        = \alpha_{\mathrm{S}}\, \dot m_{\mathrm{S,rem}}
        + \alpha_{\mathrm{N}}\, \dot m_{\mathrm{N,rem}}
\]


with stoichiometric coefficients $\alpha_{\mathrm{S}}$ (mol H$_2$ / kg S) and $\alpha_{\mathrm{N}}$ (mol H$_2$ / kg N).

#### Fluid Catalytic Cracking (FCC) Unit

##### Overview {#overview-35}

The **Shortcut FCC** supports two yield methods: an empirical *yield-slate* adjusted by feed quality (CCR) and a simplified three-lump *Weekman kinetic* model . The unit models the riser/regenerator pair lumped as a single conversion volume operating at a specified temperature, pressure, and catalyst-to-oil ratio.

##### Stream Topology {#stream-topology-7}







| **Port** | **Direction** | **Description** |
|:---|:---|:---|
| Feed In | Inlet (material) | VGO / gas-oil feed |
| Dry Gas | Outlet (material) | C$_2^-$ cracked gas |
| LPG | Outlet (material) | C$_3$–C$_4$ olefin-rich cut |
| Gasoline | Outlet (material) | FCC gasoline (naphtha range) |
| LCO | Outlet (material) | Light cycle oil |
| Slurry | Outlet (material) | Heavy cycle oil / slurry |
| Flue Gas | Outlet (material) | Regenerator effluent (CO$_2$, O$_2$, N$_2$) |



##### Weekman Three-Lump Kinetics

The classical Weekman & Nace three-lump formulation tracks gas-oil ($A$), gasoline ($B$), and coke+gas ($C$):


<a id="eq:fcc_lumps"></a>

\[
\begin{align}
    A \xrightarrow{k_1} B, \qquad
    A \xrightarrow{k_3} C, \qquad
    B \xrightarrow{k_2} C
\end{align}
\]


Assuming second-order gas-oil cracking and first-order gasoline overcracking, the lump balances along the riser are


<a id="eq:fcc_odes"></a>

\[
\begin{align}
    \frac{\mathrm d A}{\mathrm d\tau} &= -(k_1 + k_3)\, A^2 \\
    \frac{\mathrm d B}{\mathrm d\tau} &= k_1\, A^2 - k_2\, B
\end{align}
\]


where $\tau$ is the riser residence time. All rate constants are modulated by a catalyst deactivation function $\phi(t_c)$ dependent on the catalyst-on-stream time $t_c$ . The conversion $X = 1 - A/A_0$ is obtained by explicit integration.

##### Yield-Slate Method

When the *Slate* method is selected, product yields $Y_i^0$ at a reference CCR are tabulated and corrected for feed CCR according to


<a id="eq:fcc_cokeccr"></a>

\[
Y_{\mathrm{coke}} = Y_{\mathrm{coke}}^0 + \beta_{\mathrm{CCR}}
                        \left(\mathrm{CCR}_{\mathrm{feed}}
                            - \mathrm{CCR}_{\mathrm{ref}}\right)
\]


with all other yields rebalanced to close the mass balance.

##### Regenerator Heat Release

The coke combustion duty in the regenerator is


<a id="eq:fcc_regenduty"></a>

\[
\dot Q_{\mathrm{regen}} = \dot m_{\mathrm{coke}}\,
                              \Delta H_{\mathrm{coke}}^{\mathrm{comb}}
\]


with $\Delta H_{\mathrm{coke}}^{\mathrm{comb}}$ a user-editable coke combustion heat (default 32 500 kJ kg$^{-1}$).

#### Hydrocracker (HCR)

##### Overview {#overview-36}

The **Shortcut HCR** converts VGO / atmospheric residue into light ends, naphtha, kerosene, diesel, and unconverted oil (UCO) according to a target conversion and a yield slate that is adjusted as conversion moves away from the reference point. Hydrogen consumption is derived from conversion and heat of reaction.

##### Stream Topology {#stream-topology-8}







| **Port**        | **Direction**     | **Description**                         |
|:----------------|:------------------|:----------------------------------------|
| Feed In         | Inlet (material)  | VGO / DAO feed                          |
| H$_2$ Make-up | Inlet (material)  | Hydrogen                                |
| Light Ends      | Outlet (material) | C$_1$–C$_4$                         |
| Naphtha         | Outlet (material) | C$_5$–C$_{11}$                      |
| Kerosene        | Outlet (material) | C$_{11}$–C$_{14}$                   |
| Diesel          | Outlet (material) | C$_{14}$–C$_{20}$                   |
| UCO             | Outlet (material) | Unconverted oil                         |
| Sour Gas        | Outlet (material) | H$_2$S + NH$_3$ + unreacted H$_2$ |



##### Yield Model

At the target conversion $X$, the yield of each light cut is


<a id="eq:hcr_yield"></a>

\[
Y_i = Y_i^{\mathrm{base}}
         \left( 1 + \gamma_i \left( X - X_{\mathrm{base}} \right) \right)
\]


where $Y_i^{\mathrm{base}}$ is the tabulated yield at the reference conversion $X_{\mathrm{base}}$ and $\gamma_i$ is the conversion sensitivity. The UCO yield is obtained by mass-balance closure. Sulfur and nitrogen are removed at user-specified fractions $X_{\mathrm{S}}$ and $X_{\mathrm{N}}$ with the removed mass reported as H$_2$S and NH$_3$.

##### Heat of Reaction

The net reactor exotherm is


<a id="eq:hcr_duty"></a>

\[
\dot Q_{\mathrm{rxn}} = - \dot m_{\mathrm{feed}}\, X\,
                              \Delta H_{\mathrm{conv}}
\]


with $\Delta H_{\mathrm{conv}}$ the enthalpy released per kg of gas-oil converted.

#### Delayed Coker

##### Overview {#overview-37}

The **Shortcut Coker** models a delayed-coking drum at a specified heater outlet temperature and drum pressure. Product yields are computed from empirical CCR-based correlations in the Gary, Handwerk & Kaiser style, with explicit partitioning of dry gas from total gas and LGO from total gas-oil.

##### Stream Topology {#stream-topology-9}







| **Port** | **Direction**     | **Description**        |
|:---------|:------------------|:-----------------------|
| Feed In  | Inlet (material)  | Vacuum residue         |
| Dry Gas  | Outlet (material) | C$_2^-$              |
| LPG      | Outlet (material) | C$_3$–C$_4$        |
| Naphtha  | Outlet (material) | Coker naphtha          |
| LGO      | Outlet (material) | Light gas-oil          |
| HGO      | Outlet (material) | Heavy gas-oil          |
| Coke Out | Outlet (material) | Petroleum coke (solid) |



##### Yield Correlations

The coke yield is a linear function of feed Conradson carbon residue (CCR):


<a id="eq:coker_cokeyield"></a>

\[
Y_{\mathrm{coke}} = f_{\mathrm{coke}}\,\mathrm{CCR}_{\mathrm{feed}}
\]


Total gas and total naphtha are expressed as


<a id="eq:coker_gasnph"></a>

\[
\begin{align}
    Y_{\mathrm{gas}} &= a_{\mathrm{gas}}
                     + b_{\mathrm{gas}}\,\mathrm{CCR}_{\mathrm{feed}} \\
    Y_{\mathrm{nph}} &= a_{\mathrm{nph}}
                     + b_{\mathrm{nph}}\,\mathrm{CCR}_{\mathrm{feed}}
\end{align}
\]


Total gas is split into dry gas and LPG through the fraction $\phi_{\mathrm{dg}}$; total gas-oil (obtained by mass-balance closure) is split into LGO and HGO through $\phi_{\mathrm{LGO}}$. Metals (Ni, V, Fe) are concentrated quantitatively in the coke. The fired heater duty is $\dot Q = h_{\mathrm{heater}}\, \dot m_{\mathrm{feed}}$.

#### Catalytic Reformer

##### Overview {#overview-38}

The **Shortcut Reformer** models a fixed-bed catalytic reformer operating on heavy naphtha feed. Product yields (H$_2$, light ends, reformate) depend on the target RON severity; the RON-sensitivity coefficients allow the user to calibrate the model to a specific catalyst generation (semi-regenerative, cyclic, or CCR).

##### Stream Topology {#stream-topology-10}







| **Port**     | **Direction**     | **Description**              |
|:-------------|:------------------|:-----------------------------|
| Feed In      | Inlet (material)  | Hydrotreated heavy naphtha   |
| H$_2$ Rich | Outlet (material) | Net hydrogen gas             |
| Light Ends   | Outlet (material) | C$_1$–C$_4$              |
| Reformate    | Outlet (material) | High-octane aromatic product |



##### Yield-Severity Correlation

Let $R$ be the target reformate RON and $R_{\mathrm{base}}$ the reference severity. Product yields are


<a id="eq:reformer_yield"></a>

\[
Y_i = Y_i^{\mathrm{base}}
        + s_i\,(R - R_{\mathrm{base}}),\qquad i \in \{\mathrm{H_2, LE, Reformate}\}
\]


where $s_i$ is the RON sensitivity (typically $s_{\mathrm{H_2}}>0$, $s_{\mathrm{LE}}>0$, $s_{\mathrm{Reformate}}<0$). The heater duty is $\dot Q = h_{\mathrm{heater}}\, \dot m_{\mathrm{feed}}$.

#### Amine Treater

##### Overview {#overview-39}

The **Shortcut Amine Treater** separates H$_2$S and CO$_2$ from a sour gas using an aqueous alkanolamine solvent. Rather than resolving the vapour–liquid equilibrium of the reactive H$_2$S/CO$_2$/amine system, the unit uses user-specified removal fractions calibrated against rigorous column simulations or operating data.

##### Stream Topology {#stream-topology-11}







| **Port**    | **Direction**     | **Description**                         |
|:------------|:------------------|:----------------------------------------|
| Sour Gas In | Inlet (material)  | H$_2$S/CO$_2$-containing gas        |
| Sweet Gas   | Outlet (material) | Treated gas                             |
| Acid Gas    | Outlet (material) | Stripper overhead (H$_2$S + CO$_2$) |



##### Component Removal

For each acid gas component $i \in \{\mathrm{H_2S, CO_2}\}$,


<a id="eq:amine_removal"></a>

\[
\begin{align}
    \dot n_{i}^{\mathrm{acid}} &= X_i^{\mathrm{rem}}\,
                                  \dot n_{i}^{\mathrm{feed}}\\
    \dot n_{i}^{\mathrm{sweet}} &= (1 - X_i^{\mathrm{rem}})\,
                                   \dot n_{i}^{\mathrm{feed}}
\end{align}
\]


A small hydrocarbon slip $\phi_{\mathrm{HC}}$ (mol fraction of sweet-gas hydrocarbons) is co-absorbed and reports to the acid-gas stream. The amine circulation rate is computed from


<a id="eq:amine_circ"></a>

\[
\dot V_{\mathrm{amine}} = \lambda\, \dot n_{\mathrm{H_2S}}^{\mathrm{acid}}
\]


where $\lambda$ is the user-specified amine circulation (L of amine solution per mol of H$_2$S absorbed, typical 25–50 for MDEA).

#### Claus Sulfur Recovery Unit

##### Overview {#overview-40}

The **Shortcut Claus** models a two- or three-stage modified Claus SRU as a lumped conversion reactor. The unit takes acid gas (H$_2$S + CO$_2$, typically from the Amine regenerator) and converts H$_2$S to elemental sulfur at a specified recovery fraction that reflects the global equilibrium + kinetic limitations of the Claus reactors and condensers.

##### Stream Topology {#stream-topology-12}







| **Port**     | **Direction**     | **Description**                          |
|:-------------|:------------------|:-----------------------------------------|
| Acid Gas In  | Inlet (material)  | H$_2$S-rich feed                       |
| Sulfur Out   | Outlet (material) | Elemental sulfur (S$_n$ surrogate)     |
| Tail Gas Out | Outlet (material) | Unconverted H$_2$S + SO$_2$ + inerts |
| Waste Heat   | Outlet (energy)   | Steam-generator duty                     |



##### Reaction Stoichiometry

The overall Claus reaction is


<a id="eq:claus_overall"></a>

\[
3\,\mathrm{H_2S} + \tfrac{3}{2}\,\mathrm{O_2}
        \rightarrow \tfrac{3}{n}\,\mathrm{S}_n + 3\,\mathrm{H_2O}
\]


The moles of H$_2$S reacted are


<a id="eq:claus_hsrxn"></a>

\[
\dot n_{\mathrm{H_2S}}^{\mathrm{rxn}} = X_{\mathrm{rec}}\,
                                            \dot n_{\mathrm{H_2S}}^{\mathrm{feed}}
\]


where $X_{\mathrm{rec}}$ is the sulfur-recovery fraction (typically 0.95–0.99). The sulfur produced is reported using a user-selected elemental sulfur surrogate (default S$_1$).

##### Waste Heat

The reaction releases a user-tuneable heat per mol of H$_2$S reacted $\Delta H_{\mathrm{rxn}}$ (default 220 kJ/mol):


<a id="eq:claus_whb"></a>

\[
\dot Q_{\mathrm{WHB}} = \dot n_{\mathrm{H_2S}}^{\mathrm{rxn}}\,
                             \Delta H_{\mathrm{rxn}}
\]


#### Product Blender

##### Overview {#overview-41}

The **Product Blender** pools multiple material-stream inlets into a single outlet, aggregating mass, energy, composition, and contaminant load. It is most commonly used upstream of a storage tank or to form a combined off-site feed in TEA/LCA flow-sheet closures.

##### Stream Topology {#stream-topology-13}

The blender has a variable number of inlet ports ($n_{\mathrm{inlets}} \ge 2$) and a single outlet.

##### Mixing Equations

The outlet mass and enthalpy follow simple conservation:


<a id="eq:blender_mass"></a>

\[
\begin{align}
    \dot m_{\mathrm{out}} &= \sum_{j=1}^{n_{\mathrm{inlets}}} \dot m_j \\
    \dot H_{\mathrm{out}} &= \sum_{j=1}^{n_{\mathrm{inlets}}} \dot H_j
\end{align}
\]


The outlet pressure is either


<a id="eq:blender_pressure"></a>

\[
\begin{align}
    P_{\mathrm{out}} &= \min_{j}\, P_j
        \qquad\text{(mode \textit{Min}, conservative)} \\
    P_{\mathrm{out}} &= \frac{\sum_j \dot m_j\, P_j}{\sum_j \dot m_j}
        \qquad\text{(mode \textit{Average}, mass-weighted)}
\end{align}
\]


The outlet temperature is obtained from a flash at the outlet $(P, H)$. Contaminant concentrations of the pool are mass-weighted:


<a id="eq:blender_contam"></a>

\[
x_{\mathrm{out}}^{\mathrm{cont}}
        = \frac{\displaystyle\sum_{j} \dot m_j\, x_j^{\mathrm{cont}}}
               {\displaystyle\sum_{j} \dot m_j}
\]


#### Isomerization Unit

##### Overview {#overview-42}

The **Shortcut Isomerization** unit converts light straight-run naphtha (C$_5$–C$_6$) to a high-octane isomerate through $n$-paraffin to iso-paraffin rearrangement over a Pt/Al$_2$O$_3$-Cl or zeolitic catalyst. Yields are parameterised as a function of the octane uplift from feed RON to isomerate RON.

##### Stream Topology {#stream-topology-14}







| **Port**   | **Direction**     | **Description**                       |
|:-----------|:------------------|:--------------------------------------|
| Feed In    | Inlet (material)  | Light straight-run naphtha            |
| Isomerate  | Outlet (material) | High-octane iC$_5$/iC$_6$ product |
| Light Ends | Outlet (material) | C$_1$–C$_4$ by-products           |



##### Yield Model

Let $R_{\mathrm{iso}}$ and $R_{\mathrm{feed}}$ be the isomerate and feed RON. Yields are


<a id="eq:isom_yield"></a>

\[
Y_i = Y_i^{\mathrm{base}}
        + s_i\,(R_{\mathrm{iso}} - R_{\mathrm{feed}}),\qquad i \in
        \{\mathrm{Isomerate, LightEnds}\}
\]


The heater duty is $\dot Q = h_{\mathrm{heater}}\, \dot m_{\mathrm{feed}}$. Sulfur and nitrogen in the feed partition to the products according to user-specified fractions ($\mathrm{SFrac}_{\mathrm{iso}}$, $\mathrm{SFrac}_{\mathrm{LE}}$, $\mathrm{NFrac}_{\mathrm{iso}}$, $\mathrm{NFrac}_{\mathrm{LE}}$).

#### Alkylation Unit

##### Overview {#overview-43}

The **Shortcut Alkylation** unit models the acid-catalysed alkylation of isobutane with light olefins (propylene and butenes) to produce a high-octane, low-sulfur alkylate. The model is a lumped olefin-conversion yield calculation at a specified reactor temperature, pressure, iC$_4$/olefin mol ratio, and olefin conversion.

##### Stream Topology {#stream-topology-15}







| **Port**      | **Direction**     | **Description**             |
|:--------------|:------------------|:----------------------------|
| Olefin Feed   | Inlet (material)  | Propylene / butylene cut    |
| iC$_4$ Feed | Inlet (material)  | Isobutane (fresh + recycle) |
| Alkylate      | Outlet (material) | High-octane alkylate        |
| Purge         | Outlet (material) | nC$_4$ + heavy ends purge |
| Cooler Duty   | Outlet (energy)   | Reactor heat-removal duty   |



##### Yield and Duty

Let $\dot m_{\mathrm{ol}}$ be the olefin mass flow and $X_{\mathrm{ol}}$ the olefin conversion. The alkylate mass is


<a id="eq:alk_mass"></a>

\[
\dot m_{\mathrm{alk}} = y_{\mathrm{alk}}\, X_{\mathrm{ol}}\,
                            \dot m_{\mathrm{ol}}
\]


where $y_{\mathrm{alk}}$ (typical 1.7–2.2) is the alkylate yield per kg of olefin reacted. Unreacted olefins plus any nC$_4$ reporting to the purge form the purge stream. The reactor exotherm is


<a id="eq:alk_duty"></a>

\[
\dot Q_{\mathrm{cool}} = -\, X_{\mathrm{ol}}\, \dot m_{\mathrm{ol}}\,
                              \Delta H_{\mathrm{alk}}
\]


with $\Delta H_{\mathrm{alk}}$ the heat release per kg of olefin reacted (typical 1350–1500 kJ kg$^{-1}$).

#### Shared Contaminant Vector

All refining blocks above carry and update a **common contaminant vector** that travels with every material stream:







| **Property**                 | **Symbol**            | **Unit** |
|:-----------------------------|:----------------------|:---------|
| Total sulfur                 | $x_{\mathrm{S}}$    | wt%      |
| Total nitrogen               | $x_{\mathrm{N}}$    | wt%      |
| Mercaptan sulfur             | $x_{\mathrm{RSH}}$  | wt%      |
| Nickel                       | $x_{\mathrm{Ni}}$   | mass ppm |
| Vanadium                     | $x_{\mathrm{V}}$    | mass ppm |
| Iron                         | $x_{\mathrm{Fe}}$   | mass ppm |
| Conradson carbon             | $x_{\mathrm{CCR}}$  | wt%      |
| Asphaltenes (C$_7$ insol.) | $x_{\mathrm{asph}}$ | wt%      |
| Total acid number            | $\mathrm{TAN}$      | mg KOH/g |



In any unit operation, the outlet-stream contaminant vector is computed by mass-weighted partitioning using user-editable or default split fractions; removed contaminants (e.g. sulfur reporting as H$_2$S) are converted to the corresponding molecular surrogate and reported in the by-product stream.

#### PNA-Aware Yield Modulation {#sec:refining_pna}

In addition to the contaminant vector, each pseudocomponent may carry a paraffin / naphthene / aromatic (PNA) composition triplet $(x_{\mathrm{P}}, x_{\mathrm{N}}, x_{\mathrm{A}})$ estimated at characterisation time from the TBP curve and bulk SG (see the Petroleum Characterization section) or overridden by the user. The feed-average PNA composition is aggregated as a mass-weighted mean over those feed pseudocomponents that have a PNA triplet set:


<a id="eq:pna_agg"></a>

\[
\bar{x}_{i} \;=\;
        \frac{\displaystyle\sum_{c \in \mathcal{C}_{\mathrm{PF}}}
              \dot m_{c}\, x_{i,c}}
             {\displaystyle\sum_{c \in \mathcal{C}_{\mathrm{PF}}} \dot m_{c}},
        \qquad i \in \{\mathrm{P}, \mathrm{N}, \mathrm{A}\}
\]


where $\mathcal{C}_{\mathrm{PF}}$ is the set of petroleum-fraction pseudocomponents for which at least one of $x_{\mathrm{P}},\,x_{\mathrm{N}},\,
x_{\mathrm{A}}$ is defined. When $\mathcal{C}_{\mathrm{PF}} = \varnothing$ the PNA machinery is inactive and default (PNA-independent) yields are used. Triplets are renormalised to sum to unity before use. The feed PNA triplet is reported by every PNA-aware unit operation (output properties `Feed xP`, `Feed xN`, `Feed xA`).

Each PNA-aware unit applies a multiplicative modulation of the form


<a id="eq:pna_mod"></a>

\[
y'_{j} \;=\; y_{j}\,\bigl(1 + \alpha_{j}\,(\bar{x}_{i} - \bar{x}^{\mathrm{ref}}_{i})\bigr),
    \qquad
    \bar{x}^{\mathrm{ref}}_{i} \;\text{user-configurable}
\]


to a base yield or consumption $y_{j}$ (with the modulated yield set renormalised when $j$ indexes a complete product slate). The sensitivity coefficient $\alpha_{j}$ and the reference PNA fraction $\bar{x}^{\mathrm{ref}}_{i}$ are unit-specific configuration parameters. The PNA-dependent behaviour of each block is summarised below:







| **Unit** | **Effect of feed PNA** |
|:---|:---|
| Reformer | H$_2$ and light-ends yields scale with $(\bar{x}_{\mathrm{P}} - \bar{x}^{\mathrm{ref}}_{\mathrm{P}})$ — paraffin-rich naphthas dehydrocyclise to aromatics, releasing H$_2$ and gas. |
| Isomerization | Aromatics are inert under C$_5$/C$_6$ isomerisation; an aromatic fraction above $\bar{x}^{\mathrm{ref}}_{\mathrm{A}}$ linearly penalises isomerate yield and increases the light-ends slip. |
| FCC | Target riser conversion is scaled by $(1 + \alpha_{X}(\bar{x}_{\mathrm{P}} - \bar{x}^{\mathrm{ref}}_{\mathrm{P}}))$ (paraffin-rich gas oils crack more readily); the coke yield is bumped by $(1 + \alpha_{\mathrm{coke}}(\bar{x}_{\mathrm{A}} - \bar{x}^{\mathrm{ref}}_{\mathrm{A}}))$ on top of the CCR-driven baseline. |
| HCR | Per-pass conversion is paraffin-accelerated through $(\bar{x}_{\mathrm{P}} - \bar{x}^{\mathrm{ref}}_{\mathrm{P}})$; chemical H$_2$ consumption per kg feed is aromatic-accelerated through $(\bar{x}_{\mathrm{A}} - \bar{x}^{\mathrm{ref}}_{\mathrm{A}})$ to account for ring saturation. |
| HDS | An aromatic-saturation term adds $\dot m_{\mathrm{feed}}\,\bar{x}_{\mathrm{A}}\,f_{\mathrm{sat}}\,(\mathrm{H}_2/\mathrm{kg~Ar})$ to the hydrogen demand computed from the sulfur/nitrogen HDS/HDN kinetics. |
| Coker | Coke yield is boosted by $(1 + \alpha_{\mathrm{coke}}(\bar{x}_{\mathrm{A}} - \bar{x}^{\mathrm{ref}}_{\mathrm{A}}))$ beyond the CCR anchor; gas and naphtha yields are boosted by $(1 + \alpha_{\mathrm{gas}}(\bar{x}_{\mathrm{P}} - \bar{x}^{\mathrm{ref}}_{\mathrm{P}}))$. Yields are renormalised so that $\sum_j y_j + y_{\mathrm{coke}} = 1$. |



PNA awareness can be disabled per unit through the `UseFeedPNA` flag; it is likewise inactive whenever the feed has no PNA data attached, making the correlation strictly additive with respect to the baseline yield slates.

#### Common Assumptions and Limitations

1.  **Steady state.** All blocks are solved as steady-state conservation models; no dynamic inventory or start-up effects are captured.

2.  **Lumped conversion volume.** Reactors are treated as a single lump; intra-reactor temperature, pressure, or composition profiles are not resolved. LHSV and residence time enter only through their integrated effect on the global conversion.

3.  **Pseudocomponent basis.** Feed streams are characterised through the standard DWSIM pseudocomponent machinery; model accuracy therefore depends on the quality of the feed assay (TBP curve, bulk sulfur, nitrogen, CCR, metals).

4.  **No detailed VLE for reactive systems.** Reactive systems (Amine, Claus) are modelled through user-calibrated removal / recovery fractions rather than rigorous reactive VLE. This is adequate for conceptual design but should be verified against rigorous column/reactor simulations before detailed engineering.

5.  **Contaminant split fractions are user data.** The default partitioning coefficients are indicative only and must be calibrated against operating data for any given refinery.

6.  **Energy balance closure.** Reactor exotherms / endotherms are returned as side-streams (energy ports or duty results). The user is responsible for placing the corresponding utility streams (fired heater, cooler, steam generator) on the flowsheet to close the overall heat balance.

#### Numerical Solution Procedure

All refining unit operations follow the same explicit calculation pattern:

1.  Resolve the connected inlet material and energy streams; read temperature, pressure, composition, and contaminant vector.

2.  Initialise the pseudocomponent list from the feed `SelectedCompounds` collection.

3.  Evaluate the conversion / yield equations of the unit as a function of the configured operating conditions ([\[eq:hds_conversion\]](#eq:hds_conversion), [\[eq:fcc_lumps\]](#eq:fcc_lumps), [\[eq:hcr_yield\]](#eq:hcr_yield), [\[eq:reformer_yield\]](#eq:reformer_yield), [\[eq:claus_hsrxn\]](#eq:claus_hsrxn), etc.).

4.  Partition the feed mass among the outlet streams; close the mass balance by adjusting the residue / UCO / purge stream.

5.  Update the contaminant vector of each outlet by applying the user-specified split fractions and accounting for contaminants converted in-reactor (S $\rightarrow$ H$_2$S, N $\rightarrow$ NH$_3$, S $\rightarrow$ S$_n$).

6.  Set outlet-stream temperature and pressure; when not independently specified, the outlet temperature is computed from a $(P, H)$ flash that reflects the reactor exotherm.

7.  Write the energy-balance duties to any connected energy-stream outlet (heater, cooler, waste-heat boiler).

8.  Collect per-unit key performance indicators (conversion, severity, recovery, CCR, RON) into the results object for display in the editor and for downstream TEA / LCA consumption.

No iteration is required for any of the refining shortcut models; all equations admit an explicit closed-form or one-pass evaluation given the inlet stream conditions and configuration parameters.

#### Typical Usage Workflow

1.  Build the crude assay by adding pseudocomponents from a TBP curve (see *Characterise Crude Assay* in the main Tools menu) and set the bulk contaminant properties of the feed material stream.

2.  Drop the *Shortcut CDU* on the flowsheet and connect the crude feed; verify that the product cuts close the feed mass balance and contaminant distribution.

3.  Build the downstream refinery by chaining the remaining refining unit operations (HDS, FCC, HCR, Coker, Reformer, Isomerization, Alkylation) to the CDU cuts as appropriate.

4.  Connect an *Amine Treater* to any sour-gas stream that requires H$_2$S removal, and route the acid gas to the *Claus SRU*.

5.  Use *Product Blenders* to pool streams into gasoline, diesel, and fuel-oil pools prior to storage.

6.  Review the **Results** tab of each unit for conversion, yield, severity, and contaminant-vector summaries, and feed the resulting pool qualities into the DWSIM TEA / LCA extensions for economic and environmental assessment.

