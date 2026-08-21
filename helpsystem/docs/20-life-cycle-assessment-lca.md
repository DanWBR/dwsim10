# Life Cycle Assessment (LCA)

#### Overview

The **Life Cycle Assessment** (LCA) extension performs environmental impact assessment of chemical processes following the ISO 14040/14044 framework . It automates the construction of a Life Cycle Inventory (LCI) from the converged DWSIM flowsheet, applies the CML 2001 baseline midpoint characterisation method , and normalises results to a user-defined functional unit.

#### LCA Methodology {#sec:lca_method}

The four-phase ISO 14040 framework is implemented as follows:

##### Phase 1 — Goal and Scope Definition {#phase-1-goal-and-scope-definition}

Configured via the Study Setup tab:

- **Functional unit**: user selects a product stream and quantity (default: 1 kg of the largest product stream).

- **System boundary**: cradle-to-gate (includes upstream raw material production) or gate-to-gate (on-site process only).

- **Allocation**: mass-based, economic, or energy-based for multi-product systems.

- **Electricity grid mix**: selectable from 20 country/region profiles.

##### Phase 2 — Life Cycle Inventory (LCI) {#phase-2-life-cycle-inventory-lci}

The inventory is built automatically from the flowsheet graph. The following flow types are identified:









Unit operations with no energy duty (valves, mixers, splitters, solid/component separators, orifice plates, Product Blender, etc.) are automatically skipped.

###### Compound matching

Each feed-stream compound is matched to the embedded emission-factor database (approximately 50 common process chemicals) using CAS number as the primary key and compound name as fallback. Emission factors represent cradle-to-gate impacts per kilogram of substance produced and are sourced from ecoinvent 3.9  and GREET 2023 .

###### Utility emissions

Electricity emission factors are provided for 20 country/region grid mixes based on IEA data . Heating emissions assume a natural gas-fired industrial boiler at 85 % thermal efficiency ($0.217$ kg CO$_2$-eq/kWh$_{\mathrm{th}}$ including upstream impacts). Cooling emissions are derived from the electricity consumption of mechanical-draft cooling towers ($\sim$<!-- -->0.02 kWh$_e$/kWh$_{\mathrm{th}}$).

###### Refinery operations {#sec:lca_refinery}

The 11 shortcut refinery unit operations from the *Refining Unit Operations* package emit or consume utilities that are captured automatically during inventory construction:

- **Hydrogen consumers** — the HDS and Hydrocracker units report hydrogen consumption via `Results.H2Consumption­KgPerS`. This is added as a *Raw Material* inventory item with CAS 1333-74-0, enabling the cradle-to-gate environmental burden of industrial H$_2$ production to be attributed to these units.

- **Endothermic reactors with fired heaters** — the Catalytic Reformer, Isomerization, and Delayed Coker report a feed-heater duty via `Results.HeaterDuty`. This is added as a *Heating* inventory item and uses the natural-gas boiler emission factor.

- **FCC regenerator direct emissions** — the Fluid Catalytic Cracker burns coke on the regenerated catalyst. CO$_2$ emissions are computed from $m_{\mathrm{coke}} \times 0.85 \times (44/12)$ (assuming coke is $\sim$<!-- -->85 wt % carbon) and SO$_2$ from $m_{\mathrm{coke}} \times w_{\mathrm{S,coke}} \times (64/32)$. Both are flagged as *DirectEmission* items.

- **Claus SRU tail gas** — the un-recovered fraction of the fed H$_2$S is assumed to be flared, producing SO$_2$. Tail-gas SO$_2$ is computed as $m_{\mathrm{H_2S}}(1 - \eta_{\mathrm{rec}}) \times (64/34)$.

- **Alkylation and Hydrocracker cooling** — the exothermic reaction duty is treated as a cooling-utility demand (mechanical-draft cooling tower emission factor).

- **Amine circulation pump** — electricity demand is estimated at $\sim$<!-- -->2 kW/(L/s) of amine flow rate reported by `Results.AmineCirculationLPerS`.

- **Shortcut CDU crude furnace** — the fired heater duty is approximated as $\sim$<!-- -->300 kJ/kg crude feed (a typical atmospheric distillation heat demand) and treated as a heating-utility item.

- **Passive units** — the Product Blender has no energy or emission contribution and is skipped.

###### PNA-aware inventory sensitivity

The hydrogen, coke, and fired-heater inventory items above are computed from the refining block results and therefore inherit any PNA-driven modulation of the underlying yields (see the *PNA-Aware Yield Modulation* subsection of the Refining Unit Operations chapter). In particular, an aromatic-rich feed to the Hydrocracker or HDS increases `H2ConsumptionKgPerS` — and hence the raw-material H$_2$ burden — relative to a paraffinic feed at the same throughput; an aromatic-rich Coker or FCC feed increases coke yield and the associated regenerator or coke-handling emissions. No additional configuration is required: the LCA inventory is rebuilt from the current `Results` of each block on every run.

###### Bio operations {#sec:lca_bio}

The six bio/biotech unit operations are detected by class name (independent of `ObjectType` because they inherit generic reactor / unit-op base classes) and dispatched to a dedicated bio handler. Their emissions and utility demands are captured as follows:

- **BioReactor** — the net heat duty `Result_Q_duty_kW` (computed from the metabolic heat balance and thermal mode) is added as either a *Heating* or *Cooling* item depending on sign. For aerobic fermentation this is typically cooling because the metabolic oxidation is exothermic ($\sim$<!-- -->450 kJ/mol O$_2$). Agitation/aeration electricity is estimated at $\sim$<!-- -->1 kW/m$^3$ working volume (standard stirred-tank bioreactor design basis).

- **Anaerobic Digester** — heating duty for maintaining mesophilic ($\sim$<!-- -->35 °C) or thermophilic ($\sim$<!-- -->55 °C) operating temperature is taken from `Result_Q_duty_kW`. The biogenic CO$_2$ output (`Result_CO2_kgs`) is flagged as a *DirectEmission* with CAS 124-38-9; the biogenic characterisation factor is conventionally zero under ISO 14067 but the flow is listed for transparency. Mixing electricity is estimated at $\sim$<!-- -->0.2 kW/m$^3$.

- **Biomass Pretreatment** — heating duty scales with feed mass flow via a technology-specific specific energy (DiluteAcid 500 kJ/kg, SteamExplosion 1500 kJ/kg, Alkaline 300 kJ/kg, Organosolv 800 kJ/kg). Chemical consumption is added as a *Raw Material* input with its upstream cradle-to-gate emission factor: 1 wt % H$_2$SO$_4$ for DiluteAcid (CAS 7664-93-9), 2 wt % NaOH for Alkaline (CAS 1310-73-2), or 5 % of 0.5 kg/kg Ethanol make-up for Organosolv (CAS 64-17-5).

- **Cell Lysis** — electricity depends on technology: high-pressure homogeniser uses $0.0005 \times\mathrm{passes}\times
      p_{\mathrm{MPa}}$ kWh/kg (which gives $\sim$<!-- -->0.08 kWh/kg at 2 passes and 80 MPa); bead-mill $\sim$<!-- -->0.1 kWh/kg; ultrasound uses `Ultrasound_PowerDensity_WmL` directly scaled by flow; chemical / enzymatic / osmotic modes have negligible direct energy.

- **Biogas Upgrader** — technology-dependent electricity and heat (per kg raw biogas):

  - *WaterScrubbing*: 0.25 kWh$_e$/kg, no heat.

  - *Amine*: 0.15 kWh$_e$/kg + 0.6 kWh$_{\mathrm{th}}$/kg (regenerator).

  - *PSA*: 0.30 kWh$_e$/kg.

  - *MembraneSeparation*: 0.20 kWh$_e$/kg.

  CH$_4$ slip (from `CH4LossFraction`) is added as a *DirectEmission* with CAS 74-82-8 to capture its GWP$_{100}$ = 28 impact — a critical hotspot for biogas-upgrading LCA.

- **CFB Fast Pyrolysis** — the LCI is split by sand-supply mode (`SandMode`):

  - *External* mode: the net pyrolysis duty `Result_PyrolysisDuty_kW` is supplied by an external heat source (natural-gas boiler or high-temperature utility sand) and is added as a *Heating* item.

  - *InternalCharCombustor* mode: the reactor is autothermal — char is burned in-situ to regenerate hot sand. No external heat is consumed, but the combustion generates a biogenic CO$_2$ direct emission estimated at $m_{\mathrm{CO_2}} = 3.12 \, m_{\mathrm{char}}$ (from $m_{\mathrm{char}} = m_{\mathrm{biomass}} \times
            w_{\mathrm{char,yield}}$, assuming $\sim$<!-- -->85 % carbon in char and full conversion to CO$_2$). Biogenic CO$_2$ has a characterisation factor of 0 under ISO 14067 but is listed for transparency.

  Both modes share: carrier-gas blower electricity for the fluidisation N$_2$ stream ($\sim$<!-- -->0.05 kWh per kg biomass feed) and pneumatic sand-lift electricity ($\sim$<!-- -->0.02 kWh per kg sand circulated, using `Result_SandCirculation_kgps`). The reactor is also covered by the double-count guard.

To prevent double-counting, the reactor-outlet emission scanner in the inventory builder skips any object whose class name matches a bio unit operation; their emissions are already captured explicitly above.

##### Phase 3 — Life Cycle Impact Assessment (LCIA) {#phase-3-life-cycle-impact-assessment-lcia}

The LCIA applies the CML 2001 baseline midpoint method . For each impact category $j$, the total impact is computed as:


<a id="eq:lcia"></a>

\[
I_j = \sum_{i} m_i \times \mathrm{CF}_{i,j}
\]


where $m_i$ is the annual mass of substance $i$ (kg/year) and $\mathrm{CF}_{i,j}$ is the characterisation factor for substance $i$ in impact category $j$.

The seven impact categories are:







| **Category** | **Abbreviation** | **Unit** | **Reference Substance** |
|:---|:---|:---|:---|
| Global Warming Potential | GWP$_{100}$ | kg CO$_2$-eq | CO$_2$ ($\mathrm{CF}=1$) |
| Acidification Potential | AP | kg SO$_2$-eq | SO$_2$ ($\mathrm{CF}=1$) |
| Eutrophication Potential | EP | kg PO$_4$-eq | PO$_4^{3-}$ |
| Ozone Depletion Potential | ODP | kg CFC-11-eq | CFC-11 ($\mathrm{CF}=1$) |
| Photochemical Ozone Creation | POCP | kg C$_2$H$_4$-eq | Ethylene ($\mathrm{CF}=1$) |
| Human Toxicity Potential | HTP | kg DCB-eq | 1,4-Dichlorobenzene |
| Abiotic Depletion (fossil fuels) | ADP$_{\mathrm{fossil}}$ | MJ | Fossil fuel energy |



###### Global Warming Potential

GWP$_{100}$ values follow IPCC AR5 : CO$_2 = 1$, CH$_4 = 28$, N$_2$O $= 265$, CFC-11 $= 4660$, CFC-12 $= 10200$, HCFC-22 $= 1760$, CO $= 1.57$.

###### Normalisation to functional unit

Total annual impacts are normalised to the functional unit:


<a id="eq:norm"></a>

\[
I_j^{\mathrm{FU}} = I_j \times \frac{m_{\mathrm{FU}}}{m_{\mathrm{product,annual}}}
\]


where $m_{\mathrm{FU}}$ is the functional unit mass (e.g., 1 kg) and $m_{\mathrm{product,annual}}$ is the total annual production.

##### Phase 4 — Interpretation {#phase-4-interpretation}

The plant-wide form provides:

- **Contribution analysis** — per-unit-operation breakdown of all impact categories (GWP, AP, EP, ADP), with percentage contributions and dominant category identification.

- **Hotspot identification** — automatic highlighting of the top 3 contributors for *each* impact category.

- **Sensitivity to grid mix** — users can change the electricity grid region and re-run to assess the effect on results.

- **AI-assisted interpretation** — LLM-generated expert analysis of results with improvement recommendations.

#### Normalization and Weighting {#sec:lca_normweight}

Characterised impacts can be normalised and aggregated into a single score.

###### Normalisation

Normalised impacts are computed as:


<a id="eq:lca_norm"></a>

\[
I_j^{\mathrm{norm}} = \frac{I_j^{\mathrm{FU}}}{R_j}
\]


where $R_j$ is the CML 2001 world reference value for impact category $j$ (world total, year 2000):







| **Category** | **Reference Value**     | **Unit**               |
|:-------------|:------------------------|:-----------------------|
| GWP          | $4.22 \times 10^{13}$ | kgCO$_2$-eq/yr       |
| AP           | $2.39 \times 10^{11}$ | kgSO$_2$-eq/yr       |
| EP           | $1.58 \times 10^{11}$ | kgPO$_4$-eq/yr       |
| ODP          | $2.27 \times 10^{8}$  | kgCFC-11-eq/yr         |
| POCP         | $3.68 \times 10^{10}$ | kgC$_2$H$_4$-eq/yr |
| HTP          | $2.58 \times 10^{12}$ | kgDCB-eq/yr            |
| ADP          | $3.80 \times 10^{14}$ | MJ/yr                  |



###### Weighting

The weighted single score is:


<a id="eq:lca_single"></a>

\[
S = \sum_j I_j^{\mathrm{norm}} \times \frac{w_j}{100}
\]


where $w_j$ is the user-assigned weight (%) for category $j$. Default weights are: GWP = 30, HTP = 20, AP/EP/ODP/POCP/ADP = 10 each. The weights are editable in the Normalization & Weighting tab grid.

#### System Boundary {#sec:lca_boundary}

Two boundary options are available :

###### Cradle-to-gate

Includes the environmental burden of producing all raw materials (upstream impacts from the emission-factor database) plus on-site process impacts (utilities, direct emissions).

###### Gate-to-gate

Includes only on-site process impacts. Upstream raw material impacts are excluded; raw material flows are still listed in the inventory for mass-balance purposes but carry zero environmental burden.

#### Characterisation Factor Database

The embedded characterisation factor database contains CML 2001 baseline midpoint factors for 15 common direct-emission substances (to air compartment). Key entries include:







| **Substance** | **GWP** | **AP** | **EP** | **ODP** | **POCP** |
|:--------------|:-------:|:------:|:------:|:-------:|:--------:|
| CO$_2$      |   1.0   |   —    |   —    |    —    |  0.006   |
| CH$_4$      |   28    |   —    |   —    |    —    |  0.006   |
| N$_2$O      |   265   |   —    |   —    |  0.017  |    —     |
| SO$_2$      |    —    |  1.0   |   —    |    —    |  0.048   |
| NO$_x$      |    —    |  0.7   |  0.13  |    —    |  0.028   |
| NH$_3$      |    —    |  1.88  |  0.35  |    —    |    —     |



#### Electricity Grid Mix Database

Carbon intensity values (kg CO$_2$-eq/kWh) are provided for 20 regions based on IEA and national grid data :







| **Region**     | **kg CO$_2$/kWh** | **Region**     | **kg CO$_2$/kWh** |
|:---------------|:-------------------:|:---------------|:-------------------:|
| World Average  |        0.475        | France         |        0.052        |
| United States  |        0.386        | United Kingdom |        0.193        |
| European Union |        0.231        | Norway         |        0.017        |
| China          |        0.555        | Sweden         |        0.013        |
| India          |        0.708        | Brazil         |        0.074        |



#### User Interface {#sec:lca_ui}

The plant-wide LCA is launched from the **Tools** menu under **Life Cycle Assessment $\rightarrow$ Plant-Wide LCA**. The main form contains:

- **Status bar** — “Run LCA” button, GWP/FU/Method summary labels, “Export CSV” button, and “Analyze with AI” button.

- **Tab: Study Setup** — Study name, description, product stream selection, functional unit quantity, system boundary (cradle-to-gate or gate-to-gate), allocation method, electricity grid mix region, operating hours per year, carbon tax rate, and inclusion toggles (upstream, direct, utility emissions).

- **Tab: Life Cycle Inventory** — Grid of all material and energy flows with flow rates, annual amounts, source unit operations, and matched emission factor indicators.

- **Tab: Impact Assessment** — Side-by-side layout with colour-coded impact category cards (showing per-FU and annual values) and a detailed results table.

- **Tab: Contribution Analysis** — Per-unit-operation breakdown across GWP, AP, EP, and ADP categories with GWP percentage and dominant category identification.

- **Tab: Hotspot Analysis** — Top 3 contributors per impact category.

- **Tab: Normalization & Weighting** — Normalised scores using CML 2001 world references, with editable weights per category and an aggregate single score (see §[2.3](#sec:lca_normweight)).

- **Tab: Report** — Markdown report viewer. The report is auto-generated after each analysis run and includes impact results, contribution analysis, per-category hotspots, full life cycle inventory, and normalization/weighting results. The report can be enriched with AI-generated analysis.

##### AI-Assisted Analysis

The “Analyze with AI” button sends the full Markdown report to the DWSIM AI assistant, which provides expert insights on key environmental hotspots, improvement opportunities, benchmarking against typical values, data quality assessment, and prioritised recommendations. The AI response is streamed in real time and appended to the report.

##### CSV Export

The “Export CSV” button exports the complete analysis to a CSV file containing the life cycle inventory, total and normalised impact assessment results, and the unit operation breakdown.

#### ExtraProperties Keys

Per-equipment LCA results are stored in each unit operation’s `ExtraProperties` with the following keys:







| **Key** | **Description** |
|:---|:---|
| `LCA_{Tag}_GWP_kgCO2eq` | Global Warming Potential (kg CO$_2$-eq/yr) |
| `LCA_{Tag}_AP_kgSO2eq` | Acidification Potential (kg SO$_2$-eq/yr) |
| `LCA_{Tag}_EP_kgPO4eq` | Eutrophication Potential (kg PO$_4$-eq/yr) |
| `LCA_{Tag}_ODP_kgCFC11eq` | Ozone Depletion Potential (kg CFC-11-eq/yr) |
| `LCA_{Tag}_POCP_kgC2H4eq` | Photochemical Ozone Creation (kg C$_2$H$_4$-eq/yr) |
| `LCA_{Tag}_HTP_kgDCBeq` | Human Toxicity Potential (kg DCB-eq/yr) |
| `LCA_{Tag}_ADP_MJ` | Abiotic Depletion, fossil (MJ/yr) |
| `LCA_{Tag}_DominantCategory` | Name of the dominant impact category |
| `LCA_{Tag}_MarkdownReport` | Per-equipment impact report (Markdown) |



###### Plant-wide results

When the plant-wide analysis or API is run, the following keys are stored in the *flowsheet-level* `ExtraProperties`:









#### Programmatic API {#sec:lca_api}

The `LCAApi` static class provides a programmatic interface for running a complete life cycle assessment from code.

##### Basic Usage

    using DWSIM.Extensions.LifeCycleAssessment;

    // Run LCA with default settings
    LCAApiResult result = LCAApi.Analyze(flowsheet);

    // Access key impacts (per functional unit)
    double gwp = result.GWP;   // kg CO2-eq per FU
    double ap  = result.AP;    // kg SO2-eq per FU
    double ep  = result.EP;    // kg PO4-eq per FU

    // Access annual totals
    double gwpAnnual = result.GWP_Annual;

    // Identify hotspots
    foreach (string hotspot in result.Hotspots)
        Console.WriteLine($"Hotspot: {hotspot}");

    // Get the full Markdown report
    string report = result.MarkdownReport;

##### Custom Settings

    var settings = new LCASettings
    {
        SystemBoundary = SystemBoundaryType.CradleToGate,
        ElectricityGridRegion = "European Union",
        OperatingHoursPerYear = 8000,
        IncludeUpstreamEmissions = true,
        IncludeDirectEmissions = true,
        IncludeUtilityEmissions = true
    };
    LCAApiResult result = LCAApi.Analyze(flowsheet, settings);

##### Result Structure

The `LCAApiResult` class contains:

- **Scope**: `FunctionalUnit` (description string), `AnnualProductionFU` (annual production in functional units).

- **Per-FU impacts**: `GWP`, `AP`, `EP`, `ODP`, `POCP`, `HTP`, `ADP`.

- **Annual impacts**: `GWP_Annual`, `AP_Annual`, `EP_Annual`, `ODP_Annual`, `POCP_Annual`, `HTP_Annual`, `ADP_Annual`.

- **Detailed breakdown**: `Impacts`, `Contributions`, `Hotspots` (top 3 contributor names).

- **Inventory**: `InventorySummary` (counts by flow type) and `Inventory` (full list of inventory items).

- **Report**: `MarkdownReport` — complete analysis in Markdown format.

The API automatically stores all results in the flowsheet’s `ExtraProperties` (see table above), making them accessible to other extensions, scripts, and the DWSIM AI assistant.

