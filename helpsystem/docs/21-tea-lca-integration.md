# TEA/LCA Integration

#### Overview

The **TEA/LCA Integration** extension provides a unified settings interface and automatic post-solve execution for both the TEA and LCA extensions. When enabled, the TEA and/or LCA analyses are automatically invoked after each flowsheet solve, keeping all results up to date without manual intervention.

The integration is accessible from the **Tools** menu under **TEA/LCA Integration $\rightarrow$ TEA/LCA Integration Settings**.

#### Settings

The settings dialog is divided into four sections:

- **Auto-Run After Flowsheet Solve** — individual checkboxes to enable or disable automatic TEA and LCA execution after each flowsheet calculation completes.

- **Common Parameters** — settings shared between TEA and LCA: product stream selection, operating hours per year, and carbon tax rate.

- **TEA Parameters** — discount rate, plant life, tax rate, construction period, working capital fraction, depreciation years, salvage value, product selling price, and annual production (kg/year).

- **LCA Parameters** — functional unit quantity, system boundary, allocation method, electricity grid mix region, and emission inclusion toggles (upstream, direct, utility).

All settings are saved with the flowsheet file and automatically restored when the flowsheet is reopened.

#### ExtraProperties for Optimization and Sensitivity {#sec:tealca_extraprops}

When auto-run is enabled, each flowsheet solve automatically updates all TEA and LCA key performance indicators (KPIs) in the flowsheet’s `ExtraProperties`. This makes them available as objective functions or constraints in DWSIM’s built-in optimization and sensitivity analysis tools.

###### TEA KPIs written to ExtraProperties

The following keys are updated after each solve when TEA auto-run is enabled (see §[1.15](#sec:tea_api) for the complete list):







| **Key**                          | **Typical Use**                    |
|:---------------------------------|:-----------------------------------|
| `TEA_NPV_USD`                    | Objective function (maximise)      |
| `TEA_IRR`                        | Objective function (maximise)      |
| `TEA_PaybackPeriod_Years`        | Constraint (minimise)              |
| `TEA_ROI`                        | Objective function (maximise)      |
| `TEA_ProductionCost_PerKg`       | Objective function (minimise)      |
| `TEA_TotalCapitalInvestment_USD` | Constraint (budget limit)          |
| `TEA_AnnualOperatingCosts_USD`   | Constraint or sensitivity variable |
| `TEA_AnnualRevenue_USD`          | Sensitivity variable               |



###### LCA KPIs written to ExtraProperties

The following keys are updated after each solve when LCA auto-run is enabled (see §[2.9](#sec:lca_api) for the complete list):







| **Key**          | **Typical Use**                             |
|:-----------------|:--------------------------------------------|
| `LCA_GWP_PerFU`  | Objective function (minimise) or constraint |
| `LCA_GWP_Annual` | Constraint (emission cap)                   |
| `LCA_AP_PerFU`   | Multi-objective optimisation                |
| `LCA_EP_PerFU`   | Multi-objective optimisation                |
| `LCA_ADP_PerFU`  | Energy efficiency metric                    |



###### Usage in DWSIM optimization

In the DWSIM Sensitivity Analysis or Optimization tools, these `ExtraProperties` keys can be referenced as objective variables. For example, to minimise production cost while varying a reactor temperature:

1.  Enable TEA auto-run in the Integration Settings.

2.  In the DWSIM Optimization tool, set the objective to minimise `TEA_ProductionCost_PerKg`.

3.  Add decision variables (e.g., reactor temperature, pressure).

4.  Each optimisation iteration solves the flowsheet, which triggers auto-run TEA, updating the KPIs before the optimiser reads them.

Multi-objective studies can combine TEA and LCA KPIs — for example, minimising both `TEA_ProductionCost_PerKg` and `LCA_GWP_PerFU` to find Pareto-optimal designs.

#### TEA–LCA Interoperation {#sec:tea_lca_interop}

When both extensions are active, they interoperate automatically:

- The TEA extension reads GWP values from the LCA results to compute a “Carbon Tax” line in the OPEX breakdown, using the user-specified carbon price (\$/tonne CO$_2$-eq).

- The eco-efficiency index in the TEA Breakeven tab combines TEA production cost with LCA Global Warming Potential (see §[1.12](#sec:tea_ecoeff)).

Both extensions work standalone; interoperation features are available only when both have been run.

