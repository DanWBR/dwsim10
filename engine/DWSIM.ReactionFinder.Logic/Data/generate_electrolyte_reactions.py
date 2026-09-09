"""
Regenerator for electrolyte_reactions.json.

Run this when electrolyte.xml has been extended with new salts. The script
walks every <Salt>True</Salt> entry and emits one Equilibrium dissociation
reaction:

    salt(aq) <-> nu+ cation + nu- anion (+ n H2O for hydrates)

Reactions are written to ReactionFinder/Data/electrolyte_reactions.json,
which is loaded as an embedded resource by ElectrolyteDissociationSuggester.

The first 20 entries (curated below) cover common aqueous-process equilibria
not derivable from electrolyte.xml alone (water autoionization, CO2/HCO3,
H2S/HS, NH3, sulfite/bisulfite, sulfate/bisulfate, phosphate stepwise, HCl,
HF, HNO3, H2SO4, MEA carbamate, MEAH+/MDEAH+ protonation).

Reaction K is computed by DWSIM at runtime from compound Gibbs energies of
formation (KExprType = KOpt.Gibbs); the optional delta_G_rxn_298_kJ_mol
field stored alongside each entry is informational only.

Usage:
    cd DWSIM_Private/DWSIM.Extensions.ReactionFinder/Data
    python generate_electrolyte_reactions.py
"""

import json
import xml.etree.ElementTree as ET
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
XML  = os.path.normpath(os.path.join(
    HERE, "..", "..", "DWSIM.Thermodynamics", "Assets", "Databases", "electrolyte.xml"))
OUT  = os.path.join(HERE, "electrolyte_reactions.json")

# ─── Standard formation Gibbs energies for common ionic species ──────────────
# Values in kJ/mol, NBS (Wagman 1982) tables. Used to compute ΔG_rxn for the
# auto-generated dissociations; must match the values used inside electrolyte.xml.
DELGF_ION = {
    # cations
    "H+": 0.0, "Li+": -293.31, "Na+": -261.91, "K+": -283.27, "Rb+": -283.98,
    "Cs+": -291.46, "NH4+": -79.31, "Ag+": 77.11, "Cu+": 49.98, "Cu2+": 65.49,
    "Mg2+": -454.80, "Ca2+": -553.58, "Sr2+": -559.48, "Ba2+": -560.77,
    "Mn2+": -228.03, "Fe2+": -78.87, "Fe3+": -4.60, "Co2+": -54.39,
    "Ni2+": -45.60, "Zn2+": -147.06, "Cd2+": -77.61, "Hg2+": 164.40,
    "Pb2+": -24.43, "Al3+": -485.34, "Cr3+": -215.50, "UO2+2": -952.70,
    # anions
    "OH-": -157.24, "F-": -278.79, "Cl-": -131.23, "Br-": -103.96, "I-": -51.57,
    "S-2": 85.80, "HS-": 12.05, "CN-": 172.40, "SCN-": 92.71,
    "NO3-": -111.25, "NO2-": -32.20,
    "ClO-": -36.80, "ClO3-": -7.95, "ClO4-": -8.49, "BrO3-": 19.79, "IO3-": -128.00,
    "SO4-2": -744.53, "SO42-": -744.53, "HSO4-": -755.91,
    "SO3-2": -486.50, "SO32-": -486.50, "HSO3-": -527.73,
    "S2O3-2": -522.50, "S2O32-": -522.50, "S2O8-2": -1114.90,
    "CO3-2": -527.81, "CO32-": -527.81, "HCO3-": -586.77,
    "PO4-3": -1018.70, "PO43-": -1018.70, "HPO4-2": -1089.15, "HPO42-": -1089.15,
    "H2PO4-": -1130.28,
    "MnO4-": -447.20, "CrO4-2": -727.75, "CrO42-": -727.75, "Cr2O7-2": -1301.10,
    "CH3COO-": -369.31, "HCOO-": -351.04, "C2O4-2": -676.63, "C2O42-": -676.63,
    "H2NCOO-": -448.20,
}

def fmt_int(v):
    iv = int(round(v))
    return iv if abs(v - iv) < 1e-9 else round(v, 4)

def main():
    if not os.path.exists(XML):
        sys.exit(f"electrolyte.xml not found at {XML}")

    tree = ET.parse(XML)
    root = tree.getroot()

    reactions = []
    skipped = []

    for c in root.findall("compound"):
        is_salt = (c.findtext("Salt", "False").strip().lower() == "true")
        if not is_salt:
            continue

        name = c.findtext("Name", "").strip()
        formula = c.findtext("Formula", "").strip()
        cation = c.findtext("PositiveIon", "").strip()
        anion  = c.findtext("NegativeIon", "").strip()

        try:
            nu_pos = int(c.findtext("PositiveIonStoichCoeff", "1"))
            nu_neg = int(c.findtext("NegativeIonStoichCoeff", "1"))
            n_h2o  = int(c.findtext("HydrationNumber", "0"))
        except ValueError:
            skipped.append((name, "non-integer stoich"))
            continue

        if not cation or not anion:
            skipped.append((name, "missing ion field"))
            continue

        stoich = {name: -1, cation: nu_pos, anion: nu_neg}
        if n_h2o > 0:
            stoich["Water"] = n_h2o

        try:
            dGf_salt = float(c.findtext("DelGF_kJ_mol", "0"))
            dGf_cation = DELGF_ION.get(cation)
            dGf_anion  = DELGF_ION.get(anion)
            if dGf_cation is not None and dGf_anion is not None:
                dGf_h2o = -237.13 if n_h2o > 0 else 0
                dG_rxn = (nu_pos * dGf_cation + nu_neg * dGf_anion
                          + n_h2o * dGf_h2o - dGf_salt)
            else:
                dG_rxn = None
        except (TypeError, ValueError):
            dG_rxn = None

        equation = f"{formula or name} <-> {nu_pos if nu_pos>1 else ''}{cation} + {nu_neg if nu_neg>1 else ''}{anion}"
        if n_h2o > 0:
            equation += f" + {n_h2o} H2O"

        reactions.append({
            "id": "DISS_" + name.replace(" ", "_").replace("(", "").replace(")", "").replace(".", "_"),
            "name": f"{name} dissociation",
            "description": equation,
            "category": "SaltDissociation",
            "phase": "Liquid",
            "stoich": {k: fmt_int(v) for k, v in stoich.items()},
            "delta_G_rxn_298_kJ_mol": round(dG_rxn, 3) if dG_rxn is not None else None,
            "source": "auto-generated from electrolyte.xml",
        })

    # ─── Curated common process equilibria ──────────────────────────────────
    common = [
        {"id":"AB_H2O_autoionization","name":"Water autoionization",
         "description":"H2O <-> H+ + OH-","category":"WaterChemistry","phase":"Liquid",
         "stoich":{"Water":-1,"H+":1,"OH-":1},
         "delta_G_rxn_298_kJ_mol":79.89,"pKa_or_pKw_298":14.00,
         "source":"Marshall & Franck 1981"},
        {"id":"AB_CO2_first_ionization","name":"CO2 ionization (1st)",
         "description":"CO2(aq) + H2O <-> H+ + HCO3-","category":"AcidBase","phase":"Liquid",
         "stoich":{"Carbon dioxide":-1,"Water":-1,"H+":1,"HCO3-":1},
         "pKa_or_pKw_298":6.35,"source":"Plummer & Busenberg 1982"},
        {"id":"AB_HCO3_dissociation","name":"Bicarbonate dissociation (2nd)",
         "description":"HCO3- <-> H+ + CO3-2","category":"AcidBase","phase":"Liquid",
         "stoich":{"HCO3-":-1,"H+":1,"CO3-2":1},
         "pKa_or_pKw_298":10.33,"source":"Plummer & Busenberg 1982"},
        {"id":"AB_H2S_first_ionization","name":"H2S ionization (1st)",
         "description":"H2S <-> H+ + HS-","category":"AcidBase","phase":"Liquid",
         "stoich":{"Hydrogen sulfide":-1,"H+":1,"HS-":1},
         "pKa_or_pKw_298":7.05,"source":"Millero 1986"},
        {"id":"AB_HS_dissociation","name":"Bisulfide dissociation (2nd)",
         "description":"HS- <-> H+ + S-2","category":"AcidBase","phase":"Liquid",
         "stoich":{"HS-":-1,"H+":1,"S-2":1},
         "pKa_or_pKw_298":19.0,"source":"Millero 1986"},
        {"id":"AB_NH3_protonation","name":"Ammonia protonation",
         "description":"NH3 + H2O <-> NH4+ + OH-","category":"AcidBase","phase":"Liquid",
         "stoich":{"Ammonia":-1,"Water":-1,"NH4+":1,"OH-":1},
         "pKa_or_pKw_298":9.25,"source":"Edwards et al. 1978"},
        {"id":"AB_SO2_first_ionization","name":"SO2 ionization (1st)",
         "description":"SO2(aq) + H2O <-> H+ + HSO3-","category":"AcidBase","phase":"Liquid",
         "stoich":{"Sulfur dioxide":-1,"Water":-1,"H+":1,"HSO3-":1},
         "pKa_or_pKw_298":1.85,"source":"Edwards et al. 1978"},
        {"id":"AB_HSO3_dissociation","name":"Bisulfite dissociation (2nd)",
         "description":"HSO3- <-> H+ + SO3-2","category":"AcidBase","phase":"Liquid",
         "stoich":{"HSO3-":-1,"H+":1,"SO3-2":1},
         "pKa_or_pKw_298":7.20,"source":"Edwards et al. 1978"},
        {"id":"AB_HSO4_dissociation","name":"Bisulfate dissociation",
         "description":"HSO4- <-> H+ + SO4-2","category":"AcidBase","phase":"Liquid",
         "stoich":{"HSO4-":-1,"H+":1,"SO4-2":1},
         "pKa_or_pKw_298":1.99,"source":"Pitzer 1991"},
        {"id":"AB_H3PO4_first_ionization","name":"H3PO4 ionization (1st)",
         "description":"H3PO4 <-> H+ + H2PO4-","category":"AcidBase","phase":"Liquid",
         "stoich":{"Phosphoric acid":-1,"H+":1,"H2PO4-":1},
         "pKa_or_pKw_298":2.15,"source":"CRC Handbook 96th ed"},
        {"id":"AB_H2PO4_dissociation","name":"Dihydrogen phosphate dissociation (2nd)",
         "description":"H2PO4- <-> H+ + HPO4-2","category":"AcidBase","phase":"Liquid",
         "stoich":{"H2PO4-":-1,"H+":1,"HPO4-2":1},
         "pKa_or_pKw_298":7.20,"source":"CRC Handbook 96th ed"},
        {"id":"AB_HPO4_dissociation","name":"Hydrogen phosphate dissociation (3rd)",
         "description":"HPO4-2 <-> H+ + PO4-3","category":"AcidBase","phase":"Liquid",
         "stoich":{"HPO4-2":-1,"H+":1,"PO4-3":1},
         "pKa_or_pKw_298":12.35,"source":"CRC Handbook 96th ed"},
        {"id":"AB_HCl_dissociation","name":"HCl dissociation",
         "description":"HCl(aq) <-> H+ + Cl-","category":"AcidBase","phase":"Liquid",
         "stoich":{"Hydrogen chloride":-1,"H+":1,"Cl-":1},
         "pKa_or_pKw_298":-7.0,"source":"Robinson & Stokes 1959"},
        {"id":"AB_HF_dissociation","name":"HF dissociation",
         "description":"HF <-> H+ + F-","category":"AcidBase","phase":"Liquid",
         "stoich":{"Hydrogen fluoride":-1,"H+":1,"F-":1},
         "pKa_or_pKw_298":3.17,"source":"CRC Handbook 96th ed"},
        {"id":"AB_HNO3_dissociation","name":"HNO3 dissociation",
         "description":"HNO3 <-> H+ + NO3-","category":"AcidBase","phase":"Liquid",
         "stoich":{"Nitric acid":-1,"H+":1,"NO3-":1},
         "pKa_or_pKw_298":-1.4,"source":"Pitzer 1991"},
        {"id":"AB_H2SO4_first_ionization","name":"H2SO4 ionization (1st)",
         "description":"H2SO4 <-> H+ + HSO4-","category":"AcidBase","phase":"Liquid",
         "stoich":{"Sulfuric acid":-1,"H+":1,"HSO4-":1},
         "pKa_or_pKw_298":-3.0,"source":"Pitzer 1991"},
        {"id":"AB_CO2_dissolution","name":"CO2 gas dissolution",
         "description":"CO2(g) <-> CO2(aq)","category":"AcidBase","phase":"Liquid",
         "stoich":{"Carbon dioxide":1,"Carbon dioxide(g)":-1},
         "source":"Henry's law (informational)"},
        {"id":"AB_MEA_carbamate","name":"MEA carbamate formation",
         "description":"MEA + CO2 + H2O <-> MEA-carbamate + H+","category":"AcidBase","phase":"Liquid",
         "stoich":{"Monoethanolamine":-1,"Carbon dioxide":-1,"Water":-1,"MEA-carbamate":1,"H+":1},
         "source":"Faramarzi et al. 2009"},
        {"id":"AB_MEA_protonation","name":"MEA protonation",
         "description":"MEA + H+ <-> MEAH+","category":"AcidBase","phase":"Liquid",
         "stoich":{"Monoethanolamine":-1,"H+":-1,"MEAH+":1},
         "pKa_or_pKw_298":9.50,"source":"Faramarzi et al. 2009"},
        {"id":"AB_MDEA_protonation","name":"MDEA protonation",
         "description":"MDEA + H+ <-> MDEAH+","category":"AcidBase","phase":"Liquid",
         "stoich":{"Methyldiethanolamine":-1,"H+":-1,"MDEAH+":1},
         "pKa_or_pKw_298":8.52,"source":"Faramarzi et al. 2009"},
    ]

    payload = {
        "_metadata": {
            "version": "1.0.0",
            "description": (
                "Electrolyte equilibrium reactions auto-generated from "
                "electrolyte.xml plus a curated list of common aqueous-process "
                "equilibria. Reaction type is Equilibrium, ReactionBasis is "
                "Activity, and K is computed by DWSIM at runtime from compound "
                "Gibbs energies of formation (KExprType = Gibbs)."
            ),
            "ion_charge_convention": (
                "Ion formula matches what the host electrolyte.xml database uses; "
                "the loader normalizes between SO4-2 / SO42- variants."
            ),
            "phase_convention": (
                "Liquid = aqueous solution. Solid-phase salt-precipitation "
                "reactions can be added later by extending this file."
            ),
        },
        "reactions": common + reactions,
    }

    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(payload, f, indent=2, ensure_ascii=False)

    print(f"wrote {OUT}")
    print(f"  total reactions: {len(payload['reactions'])}")
    print(f"  curated:         {len(common)}")
    print(f"  auto-generated:  {len(reactions)}")
    if skipped:
        print(f"  skipped salts:   {len(skipped)}")
        for s in skipped[:5]:
            print(f"    - {s}")

if __name__ == "__main__":
    main()
