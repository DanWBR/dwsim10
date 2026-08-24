# CompoundExtensions

`DWSIM.Automation.FluentAPI.CompoundExtensions`

Fluent endpoints for adding non-database compounds to a flowsheet.

## Methods

### `WithCompound(Flowsheet, DWSIM.Interfaces.ICompoundConstantProperties)`

Adds an externally-built compound. Use this when you have a `DWSIM.Interfaces.ICompoundConstantProperties` produced from any source (ThermoML import, DWSIM.PureCompoundData index, custom estimator pipeline). The compound is registered both with the global compound catalog and with the flowsheet's selected-components collection.

### `WithCompoundFromJson(Flowsheet, string)`

Adds a compound by deserialising a UserDB-style JSON file (same schema as the files under `addcomps/`) and registering it with the flowsheet. Useful for compounds the user maintains outside the standard databases - for example, values produced by an external ThermoML or PubChem pipeline serialised to disk.

### `WithPseudoComponent(Flowsheet, string, Quantity, double, double, string, string, string, Nullable{double}, Nullable{double}, Nullable{double}, Nullable{double})`

Adds a petroleum pseudo-component to the flowsheet, computing its critical properties, acentric factor, formation enthalpy/entropy, vaporisation enthalpy and Chao-Seader parameters from `normalBoilingPoint`, `specificGravity` and `molarWeight` via `FinalizeCompoundProperties`.

**Parameters**

- `fs` — Flowsheet to add the component to.
- `name` — Display name (must be unique within the flowsheet).
- `normalBoilingPoint` — Mean atmospheric NBP (e.g. `650.0.Kelvin()`).
- `specificGravity` — SG at 60/60 °F (water = 1.0). Typical petroleum cuts: 0.65–0.95.
- `molarWeight` — Molar weight in g/mol.
- `tcMethod` — Tc correlation: "Riazi-Daubert (1985)" (default), "Riazi (2005)", "Lee-Kesler (1976)", "Twu (1984)", "Farah (2006)" or "PNA-Weighted (Riazi)".
- `pcMethod` — Pc correlation (same options).
- `acentricMethod` — ω correlation: "Lee-Kesler (1976)" (default) or "Korsten (2000)".
- `paraffinFrac` — Optional measured paraffin mass fraction (0–1).
- `naphtenicFrac` — Optional measured naphtenic mass fraction.
- `aromaticFrac` — Optional measured aromatic mass fraction.
- `refractiveIndexN20` — Optional measured n_D at 20 °C.
