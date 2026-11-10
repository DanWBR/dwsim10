# Copyright 2026 Daniel Wagner O. de Medeiros
#
# This file is part of DWSIM.
#
# DWSIM is free software: you can redistribute it and/or modify it under the terms of the GNU
# General Public License as published by the Free Software Foundation, either version 3 of the
# License, or (at your option) any later version.
#
# DWSIM is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even
# the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General
# Public License for more details.
#
# You should have received a copy of the GNU General Public License along with DWSIM. If not, see
# <http://www.gnu.org/licenses/>.
#
# This program imports pyDEXPI (GNU Affero General Public License v3). A copy distributed together
# with pyDEXPI, as source or as a frozen executable, is conveyed under the AGPL v3 as a whole.

"""DEXPI -> DWSIM drawing reading.

Reads a DEXPI file (2.0 / 2.0.1 XML or JSON, or Proteus XML) with pyDEXPI and writes the JSON
"reading" that DWSIM's drawing import opens (DrawingReading.FromJson in
DWSIM.Extensions.SolidCraft). A process model becomes a "pfd" reading, a plant model (P&ID) a
"pid" reading.

    python dwsim_dexpi.py model.xml -o model.reading.json

Values are written in C, bar (absolute) and kg/h.
"""

from __future__ import annotations

import argparse
import json
import logging
import sys
from collections import Counter
from pathlib import Path

from pydexpi import get_graph, load_json_file, load_xml_file
from pydexpi.toolkits import process_toolkit as pt
from pydexpi.toolkits.diagram_toolkit import object_extents
from pydexpi.toolkits.display_name import get_tag

ITEM, PASS, END = "item", "pass", "end"

# DEXPI process step class -> the reading's category, the first class of the MRO that is listed
# wins. None: a step that carries the flow through (contracted when it has one line in and one
# out, a junction otherwise). END: where a line starts or ends.
STEP_CATEGORIES: dict[str, str | None] = {
    "Source": END, "Sink": END, "Emitting": END, "Flaring": END,
    "Pumping": "Pump",
    "Compressing": "Compressor",
    "DrivingByTurbine": "Expander",
    "ExchangingThermalEnergy": "Exchanger",
    "RemovingThermalEnergy": "Cooler", "Cooling": "Cooler",
    "HeatingInFurnace": "Heater", "HeatingElectrical": "Heater", "SupplyingThermalEnergy": "Heater",
    "Boiling": "Heater", "Evaporating": "Heater", "GeneratingSteam": "Heater",
    "Distilling": "Column", "Absorbing": "Column", "Adsorbing": "Separator",
    "ReactingChemicals": "Reactor",
    "Filtering": "Filter", "Sieving": "Filter",
    "SeparatingByFlash": "Vessel", "SeparatingByGravity": "Vessel", "SeparatingByPhaseSeparation": "Vessel",
    "Separating": "Separator",
    "RegulatingFlow": "Valve", "LimitingFlow": "Orifice",
    "RelievingOverpressure": "Relief", "RelievingVacuum": "Relief", "RelievingVacuumAndOverpressure": "Relief",
    "BlowingDown": "Relief",
    "StoringInTank": "Tank", "StoringInPressureVessel": "Vessel", "StoringFluids": "Tank",
    "Mixing": None, "SplittingMaterial": None,
    "ShuttingOffFlow": None, "PreventingBackflow": None, "SteeringFlow": None,
    "TransportingFluids": None, "TransportingMaterial": None, "Draining": None,
}

# DEXPI plant classes (any class of the hierarchy, in this order) -> category.
EQUIPMENT_CATEGORIES = [
    ("Pump", "Pump"), ("Compressor", "Compressor"), ("Blower", "Compressor"), ("Fan", "Compressor"),
    ("Turbine", "Expander"),
    ("AirCoolingSystem", "AirCooler"), ("HeatExchanger", "Exchanger"),
    ("Furnace", "Heater"), ("Boiler", "Heater"), ("Heater", "Heater"), ("Burner", "Heater"),
    ("Column", "Column"), ("Tank", "Tank"), ("Vessel", "Vessel"),
    ("Filter", "Filter"), ("Sieve", "Filter"), ("Separator", "Separator"), ("Centrifuge", "Separator"),
]
EQUIPMENT_BASES = {"Equipment", "ProcessEquipment", "TaggedPlantItem"}
FLOW_EDGES = {"Pipe", "DirectPipingConnection"}

ATM_BAR = 1.01325
TO_BAR = {"Bar": 1.0, "Kilopascal": 0.01, "Megapascal": 10.0, "Millibar": 0.001, "Pascal": 1e-5,
          "PoundForcePerInchSquared": 0.0689475729}
TO_KGH = {"KilogramPerHour": 1.0, "KilogramPerMinute": 60.0, "KilogramPerSecond": 3600.0,
          "PoundMassPerHour": 0.45359237, "PoundMassPerMinute": 27.2155422, "PoundMassPerSecond": 1632.932532}
TO_FRACTION = {"Fraction": 1.0, "Percent": 1e-2, "PartsPerThousand": 1e-3, "PartsPerMillion": 1e-6,
               "PartsPerBillion": 1e-9}


# -- values ---------------------------------------------------------------------------------

def _quantity(values):
    """The first value of a list of QualifiedValue that holds a number (or a vector) with a unit."""
    for qualified in values or []:
        value = getattr(qualified, "value", None)
        if value is None or getattr(value, "unit", None) is None:
            continue
        if getattr(value, "value", None) is not None or getattr(value, "values", None):
            return value
    return None


def _unit(quantity) -> tuple[str, str]:
    return type(quantity.unit).__name__, quantity.unit.value


def temperature_c(values) -> float | None:
    q = _quantity(values)
    if q is None or getattr(q, "value", None) is None:
        return None
    unit = _unit(q)[1]
    v = float(q.value)
    return {"DegreeCelsius": v, "Kelvin": v - 273.15, "DegreeFahrenheit": (v - 32.0) / 1.8}.get(unit)


def pressure_bar(values) -> float | None:
    q = _quantity(values)
    if q is None or getattr(q, "value", None) is None:
        return None
    enum, unit = _unit(q)
    if unit not in TO_BAR:
        return None
    return float(q.value) * TO_BAR[unit] + (ATM_BAR if "Gauge" in enum else 0.0)


def massflow_kgh(values) -> float | None:
    q = _quantity(values)
    if q is None or getattr(q, "value", None) is None:
        return None
    unit = _unit(q)[1]
    return float(q.value) * TO_KGH[unit] if unit in TO_KGH else None


# -- the network both models are reduced to -------------------------------------------------

class Network:
    """Nodes (items, ends, pass-through pieces) and directed lines between them."""

    def __init__(self):
        self.nodes: dict[str, dict] = {}
        self.edges: list[dict] = []
        self.notes: list[str] = []

    def node(self, key, kind, tag="", category="", cls=""):
        self.nodes.setdefault(key, {"kind": kind, "tag": tag, "category": category, "cls": cls})

    def edge(self, u, v, **data):
        self.edges.append({"u": u, "v": v, **data})

    def _ins(self, n):
        return [e for e in self.edges if e["v"] == n]

    def _outs(self, n):
        return [e for e in self.edges if e["u"] == n]

    def reduce(self):
        """Leaves items, ends and junctions: drops dead-end pieces, joins the lines through the rest."""
        pruned = Counter()
        joined = Counter()
        changed = True
        while changed:
            changed = False
            for n, d in list(self.nodes.items()):
                if d["kind"] != PASS:
                    continue
                ins, outs = self._ins(n), self._outs(n)
                if not ins or not outs:
                    self.edges = [e for e in self.edges if e["u"] != n and e["v"] != n]
                    del self.nodes[n]
                    pruned[d["cls"]] += 1
                    changed = True
                elif len(ins) == 1 and len(outs) == 1:
                    a, b = ins[0], outs[0]
                    merged = dict(b)
                    merged.update({k: v for k, v in a.items() if v not in (None, "", {})})
                    merged["u"], merged["v"] = a["u"], b["v"]
                    merged["through"] = a.get("through", []) + [d["tag"] or d["cls"]] + b.get("through", [])
                    self.edges = [e for e in self.edges if e is not a and e is not b]
                    if merged["u"] != merged["v"]:
                        self.edges.append(merged)
                    del self.nodes[n]
                    joined[d["cls"]] += 1
                    changed = True
        count = 0
        for n, d in self.nodes.items():
            if d["kind"] != PASS:
                continue
            count += 1
            d["kind"] = ITEM
            d["category"] = "Mixer" if len(self._ins(n)) > 1 and len(self._outs(n)) == 1 else "Splitter"
            d["tag"] = d["tag"] or f"J-{count}"
        if joined:
            self.notes.append("lines joined through: " + ", ".join(f"{k} x{v}" for k, v in sorted(joined.items())))
        if pruned:
            self.notes.append("dead ends left out: " + ", ".join(f"{k} x{v}" for k, v in sorted(pruned.items())))

    def unique_tags(self):
        seen: Counter = Counter()
        by_class: Counter = Counter()
        for d in self.nodes.values():
            if d["kind"] != ITEM:
                continue
            if not d["tag"]:
                by_class[d["cls"]] += 1
                d["tag"] = f"{d['cls']}-{by_class[d['cls']]}"
            seen[d["tag"].lower()] += 1
            if seen[d["tag"].lower()] > 1:
                d["tag"] = f"{d['tag']}_{seen[d['tag'].lower()]}"


# -- process model (PFD) --------------------------------------------------------------------

def _step_category(step) -> tuple[str, str]:
    for cls in type(step).__mro__:
        if cls.__name__ in STEP_CATEGORIES:
            category = STEP_CATEGORIES[cls.__name__]
            if category is END:
                return END, ""
            return (PASS, "") if category is None else (ITEM, category)
    return ITEM, "Generic"


def _tag(obj) -> str:
    try:
        tag = get_tag(obj)
    except Exception:  # noqa: BLE001 - a tag is a convenience, the id is the fallback
        tag = None
    for candidate in (tag, getattr(obj, "identifier", None), getattr(obj, "label", None)):
        if isinstance(candidate, str) and candidate.strip() and candidate.strip() != "None":
            return candidate.strip()
    return ""


def _first(value):
    if isinstance(value, (list, tuple)):
        return value[0] if value else None
    return value


def _components(template) -> list[str]:
    listing = getattr(template, "listOfComponents", None)
    names = []
    for holder in vars(listing).values() if listing is not None else []:
        if isinstance(holder, list) and holder and hasattr(holder[0], "identifier"):
            for c in holder:
                label = getattr(c, "label", None)
                names.append(label if isinstance(label, str) and label else (c.identifier or c.id))
            break
    return names


def _composition(stream, w_kgh, notes) -> tuple[dict[str, float], float | None]:
    """Mass flow of each compound in kg/h, and the stream's mass flow when only the state gives it."""
    state = getattr(stream, "materialStateReference", None)
    overall = getattr(state, "state", None)
    template = getattr(stream, "materialTemplateReference", None)
    names = _components(template) if template is not None else []
    if overall is None:
        return {}, None
    total = massflow_kgh(overall.massFlow)
    comp = overall.composition
    if comp is None or not names:
        return {}, total
    flows = [massflow_kgh([q]) for q in comp.massFlow]
    if len(flows) == len(names) and all(f is not None for f in flows):
        return dict(zip(names, flows, strict=True)), total
    fractions = _quantity(comp.massFractions)
    basis = w_kgh if w_kgh is not None else total
    if fractions is not None and getattr(fractions, "values", None) and len(fractions.values) == len(names) and basis is not None:
        factor = TO_FRACTION.get(_unit(fractions)[1], 1.0)
        return {n: float(x) * factor * basis for n, x in zip(names, fractions.values, strict=True)}, total
    if comp.moleFractions or comp.moleFlow:
        notes.append(f"stream {_tag(stream) or stream.id}: composition given in moles only, not converted")
    return {}, total


def process_network(model) -> tuple[Network, list[str]]:
    pmod = model.conceptualModel
    net = Network()
    owners = pt.port_owners(pmod)
    steps = {id(step): (step, parent) for step, parent in pt.iter_process_steps(pmod)}

    def owner_step(port):
        hit = owners.get(id(port))
        if hit is None:
            return None
        owner = hit[0]
        if id(owner) not in steps:
            owner = pt.step_holding(pmod, owner)
        if owner is None or id(owner) not in steps:
            return None
        # a step that only groups others hands the line to the sub step its port delegates to
        while getattr(owner, "subProcessSteps", None) and _step_category(owner)[0] != ITEM:
            delegate = _first(getattr(port, "subReference", None))
            below = owners.get(id(delegate)) if delegate is not None else None
            if below is None or id(below[0]) not in steps:
                break
            port, owner = delegate, below[0]
        return owner

    def add(step):
        kind, category = _step_category(step)
        net.node(step.id, kind, _tag(step), category, type(step).__name__)
        return step.id

    compounds: list[str] = []
    skipped = Counter()
    for conn in pmod.processConnections:
        if type(conn).__name__ != "Stream":
            skipped[type(conn).__name__] += 1
            continue
        src, dst = owner_step(conn.source), owner_step(conn.target)
        if src is None or dst is None:
            net.notes.append(f"stream {_tag(conn) or conn.id}: an end names no process step, left out")
            continue
        w = massflow_kgh(conn.massFlow)
        comp, total = _composition(conn, w, net.notes)
        for name in comp:
            if name not in compounds:
                compounds.append(name)
        label = conn.label if isinstance(conn.label, str) else ""
        net.edge(add(src), add(dst), id=conn.identifier or "", name=label, line_number="",
                 T=temperature_c(conn.temperature), P=pressure_bar(conn.pressure),
                 W=w if w is not None else total, compounds=comp)
    def inside_item(step):
        parent = steps[id(step)][1]
        while parent is not None and id(parent) in steps:
            if _step_category(parent)[0] == ITEM:
                return True
            parent = steps[id(parent)][1]
        return False

    for step, _parent in steps.values():
        if not getattr(step, "subProcessSteps", None) and _step_category(step)[0] == ITEM and not inside_item(step):
            add(step)
    if skipped:
        net.notes.append("connections that are not material streams left out: " +
                         ", ".join(f"{k} x{v}" for k, v in sorted(skipped.items())))
    if not compounds:
        compounds = [c.label if isinstance(c.label, str) and c.label else c.identifier
                     for c in pmod.materialComponents if (c.label or c.identifier)]
    return net, compounds


# -- plant model (P&ID) ---------------------------------------------------------------------

def _equipment_category(classes: list[str]) -> str:
    for needle, category in EQUIPMENT_CATEGORIES:
        if any(needle in c for c in classes):
            return category
    return "Generic"


def plant_network(model) -> tuple[Network, list[str]]:
    net = Network()
    g = get_graph(model, graph_level="conceptual")
    controlled = {}
    for u, v, d in g.edges(data=True):
        if d.get("label") == "OperatedValveReference":
            name = g.nodes[u].get("displayName", "")
            controlled[v] = name[name.rfind("(") + 1:name.rfind(")")] if "(" in name else ""
    piped = set()
    for u, v, d in g.edges(data=True):
        if d.get("label") in FLOW_EDGES:
            piped.update((u, v))
    for n, d in g.nodes(data=True):
        classes = d.get("labels", "").split(":")
        cls = classes[0]
        if EQUIPMENT_BASES & set(classes):
            net.node(n, ITEM, d.get("tagName") or "", _equipment_category(classes), cls)
        elif n not in piped:
            continue
        elif "PipeOffPageConnector" in classes:
            net.node(n, END, cls=cls)
        elif n in controlled:
            net.node(n, ITEM, controlled[n], "Valve", cls)
        elif "SafetyValveOrFitting" in classes:
            net.node(n, ITEM, d.get("pipingComponentName") or "", "Relief", cls)
        else:
            net.node(n, PASS, cls=cls)
    for u, v, d in g.edges(data=True):
        if d.get("label") not in FLOW_EDGES or u not in net.nodes or v not in net.nodes:
            continue
        parts = [d.get("nominalDiameterRepresentation"), d.get("fluidCode"), d.get("lineNumber"), d.get("pipingClassCode")]
        net.edge(u, v, id=d.get("collapsed_node_id") or "", name="",
                 line_number="-".join(str(p).replace(" ", "") for p in parts if p) if d.get("lineNumber") else "",
                 T=None, P=None, W=None, compounds={})
    return net, []


# -- the reading ----------------------------------------------------------------------------

def reading(path: Path, strict: bool) -> dict:
    load = load_json_file if path.suffix.lower() == ".json" else load_xml_file
    model = load(str(path), strict=strict)
    is_process = type(model.conceptualModel).__name__ == "ProcessModel"
    net, compounds = process_network(model) if is_process else plant_network(model)
    net.reduce()
    net.unique_tags()

    extents = object_extents(model)
    sheet = None
    for b in extents.values():
        sheet = b if sheet is None else sheet.union(b)

    def place(key):
        b = extents.get(key)
        if b is None or sheet is None or sheet.max_x == sheet.min_x or sheet.max_y == sheet.min_y:
            return None, None
        return (round(((b.min_x + b.max_x) / 2 - sheet.min_x) / (sheet.max_x - sheet.min_x), 4),
                round(((b.min_y + b.max_y) / 2 - sheet.min_y) / (sheet.max_y - sheet.min_y), 4))

    units = []
    for key, d in net.nodes.items():
        if d["kind"] != ITEM:
            continue
        x, y = place(key)
        units.append({"tag": d["tag"], "category": d["category"], "x": x, "y": y, "dexpi_class": d["cls"], "dexpi_id": key})

    def end(key):
        d = net.nodes[key]
        return d["tag"] if d["kind"] == ITEM else ""

    lines = []
    for e in net.edges:
        frm, to = end(e["u"]), end(e["v"])
        if not frm and not to:
            net.notes.append(f"line {e['id'] or e['line_number']}: joins two sheet ends only, left out")
            continue
        lines.append({"id": e["id"], "name": e["name"], "line_number": e["line_number"], "from": frm, "to": to,
                      "T": e["T"], "P": e["P"], "W": e["W"],
                      "compounds": {k: round(v, 6) for k, v in e["compounds"].items()},
                      "through": e.get("through", [])})

    origin = " ".join(str(v) for v in (model.originatingSystemName, model.originatingSystemVersion)
                      if isinstance(v, str) and v)
    return {
        "kind": "pfd" if is_process else "pid",
        "source": path.name,
        "units_system": {"temperature": "C", "pressure": "bar", "massflow": "kg/h", "energy": "kW"},
        "units": units,
        "streams" if is_process else "lines": lines,
        "process": {"description": "", "class": "", "compounds": compounds,
                    "evidence": "DEXPI " + ("process" if is_process else "plant") + " model" + (", from " + origin if origin else "")},
        "texts": [],
        "notes": net.notes,
        "title_block": {"title": path.stem},
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("model", type=Path)
    parser.add_argument("-o", "--output", type=Path)
    parser.add_argument("--strict", action="store_true", help="refuse a file that omits mandatory DEXPI attributes")
    args = parser.parse_args()
    logging.disable(logging.CRITICAL)
    try:
        r = reading(args.model, args.strict)
    except Exception as ex:  # noqa: BLE001 - whatever the loader raises is the message for the user
        print(f"{args.model.name} is not a DEXPI model pyDEXPI can read: {ex}", file=sys.stderr)
        return 1
    out = args.output or args.model.with_suffix(".reading.json")
    out.write_text(json.dumps(r, indent=2, ensure_ascii=False), encoding="utf-8")
    lines = r.get("streams") or r.get("lines") or []
    print(f"{args.model.name}: {r['kind']}, {len(r['units'])} items "
          f"({', '.join(f'{k} {v}' for k, v in Counter(u['category'] for u in r['units']).most_common())}), "
          f"{len(lines)} lines ({sum(1 for l in lines if l['T'] is not None or l['W'] is not None)} with values), "
          f"{len(r['process']['compounds'])} compounds", file=sys.stderr)
    for note in r["notes"]:
        print("  - " + note, file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
