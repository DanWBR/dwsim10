"""
Runtime IntelliSense bridge for DWSIM.Automation.FluentAPI from Python (pythonnet).

Loads the XML doc emitted by the C# build and:

* Exposes ``doc(obj_or_member)`` — prints the C# <summary> for a .NET object,
  type, method, property or field.
* Exposes ``signature(member)`` — returns the qualified name + parameter list.
* Exposes ``patch_docstrings(*assemblies)`` — best-effort attempt to attach
  ``__doc__`` to every class / property / method that pythonnet exposes, so
  ``help(MyClass)`` and Jupyter shift+tab show the C# summary inline.

Usage::

    import sys, clr
    sys.path.append(r"C:\\path\\to\\DWSIM\\bin\\x64\\Debug")
    clr.AddReference("DWSIM.Automation.FluentAPI")
    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages

    from dwsim_fluent_help import patch_docstrings, doc
    patch_docstrings("DWSIM.Automation.FluentAPI")

    help(Flowsheet)            # → C# class summary
    help(Flowsheet.AddHeater)  # → "Adds a Heater unit operation tagged tag and ..."
    doc(PropertyPackages.NRTL) # → "Non-Random Two-Liquid activity-coefficient model ..."
"""

from __future__ import annotations

import os
import sys
import xml.etree.ElementTree as ET
from typing import Any, Dict, Iterable, Optional

_XML_CACHE: Dict[str, Dict[str, str]] = {}


# ----------------------------------------------------------------------------- loading


def _xml_path_for_assembly(asm_name: str) -> Optional[str]:
    """Locate ``<asm_name>.xml`` next to the loaded assembly DLL (or in sys.path)."""
    candidates: list[str] = []
    try:
        import clr  # noqa: F401  (only available under pythonnet)
        from System import AppDomain  # type: ignore
        for asm in AppDomain.CurrentDomain.GetAssemblies():
            if asm.GetName().Name == asm_name:
                loc = asm.Location
                if loc:
                    candidates.append(os.path.splitext(loc)[0] + ".xml")
                break
    except Exception:
        pass
    for p in sys.path:
        candidates.append(os.path.join(p, asm_name + ".xml"))
    for c in candidates:
        if c and os.path.isfile(c):
            return c
    return None


def load_xml_doc(asm_name: str = "DWSIM.Automation.FluentAPI") -> Dict[str, str]:
    """Parse the XML doc file once and return ``{member-name: summary}``."""
    if asm_name in _XML_CACHE:
        return _XML_CACHE[asm_name]
    path = _xml_path_for_assembly(asm_name)
    out: Dict[str, str] = {}
    if path is None:
        _XML_CACHE[asm_name] = out
        return out
    tree = ET.parse(path)
    for m in tree.findall(".//member"):
        name = m.get("name") or ""
        summary = m.find("summary")
        if summary is None:
            continue
        # Flatten <see cref="..."/> + nested text into one line.
        text = "".join(summary.itertext()).strip()
        text = " ".join(text.split())
        out[name] = text
    _XML_CACHE[asm_name] = out
    return out


# --------------------------------------------------------------------- name lookup


def _docid_for_member(member: Any) -> Optional[str]:
    """Build the C# XML doc-id for a pythonnet-exposed member.

    pythonnet types expose the underlying ``System.Reflection`` data via several
    paths (``__clr_type__``, ``__implementation__``, ``__class__``); the helper
    tries them all.
    """
    # Type / class
    try:
        t = member if str(type(member)).endswith("CLR Metatype'>") else None
        if t is None and hasattr(member, "__namespace__") and hasattr(member, "__name__"):
            t = member
        if t is not None:
            ns = getattr(t, "__namespace__", "")
            return f"T:{ns}.{t.__name__}" if ns else f"T:{t.__name__}"
    except Exception:
        pass

    # System.Reflection.MemberInfo
    try:
        from System.Reflection import MemberInfo, MethodInfo, PropertyInfo, FieldInfo  # type: ignore
        mi = member
        if isinstance(mi, MethodInfo):
            sig = ",".join(p.ParameterType.FullName for p in mi.GetParameters())
            qn = f"{mi.DeclaringType.FullName}.{mi.Name}"
            return f"M:{qn}({sig})" if sig else f"M:{qn}"
        if isinstance(mi, PropertyInfo):
            return f"P:{mi.DeclaringType.FullName}.{mi.Name}"
        if isinstance(mi, FieldInfo):
            return f"F:{mi.DeclaringType.FullName}.{mi.Name}"
        if isinstance(mi, MemberInfo):
            return f"M:{mi.DeclaringType.FullName}.{mi.Name}"
    except Exception:
        pass

    return None


def doc(member: Any, asm_name: str = "DWSIM.Automation.FluentAPI") -> str:
    """Return the C# <summary> for any pythonnet-exposed member, or ''."""
    table = load_xml_doc(asm_name)
    docid = _docid_for_member(member)
    if docid and docid in table:
        return table[docid]
    if docid:
        # MethodOverloads: try by-name fallback when overload signatures don't line up.
        prefix = docid.split("(", 1)[0]
        for k, v in table.items():
            if k.startswith(prefix):
                return v
    return ""


def signature(member: Any) -> str:
    """Return ``Class.Member(arg-types)`` — useful when shadowing pythonnet's
    own ``__signature__`` is impractical."""
    docid = _docid_for_member(member) or ""
    return docid.split(":", 1)[-1]


# -------------------------------------------------- runtime docstring monkey-patch


def patch_docstrings(*asm_names: str) -> int:
    """Walk every public type in the given assemblies and set ``__doc__`` on
    each class / property / method using the parsed XML. Returns the count
    of attached docs.

    pythonnet's method bindings don't always accept ``__doc__`` on individual
    overloads, so this best-effort patch focuses on classes + properties (which
    Jupyter/VS Code introspection picks up reliably).
    """
    if not asm_names:
        asm_names = ("DWSIM.Automation.FluentAPI",)

    try:
        from System import AppDomain  # type: ignore
    except Exception:
        return 0

    attached = 0
    for asm_name in asm_names:
        table = load_xml_doc(asm_name)
        if not table:
            continue
        target_asm = None
        for asm in AppDomain.CurrentDomain.GetAssemblies():
            if asm.GetName().Name == asm_name:
                target_asm = asm
                break
        if target_asm is None:
            continue
        try:
            types = list(target_asm.GetTypes())
        except Exception:
            try:
                types = [t for t in target_asm.GetTypes() if t is not None]  # type: ignore
            except Exception:
                continue

        for t in types:
            if t is None or not t.IsPublic:
                continue
            tid = f"T:{t.FullName}"
            if tid in table:
                try:
                    setattr(t, "__doc__", table[tid])
                    attached += 1
                except Exception:
                    pass
            for p in t.GetProperties():
                pid = f"P:{t.FullName}.{p.Name}"
                if pid in table:
                    try:
                        setattr(p, "__doc__", table[pid])
                        attached += 1
                    except Exception:
                        pass
    return attached


# ------------------------------------------------------------------------ CLI

def _cli() -> int:
    import argparse
    ap = argparse.ArgumentParser(description="Inspect DWSIM C# XML docs from Python")
    ap.add_argument("query", nargs="?", help="Substring to grep across member names")
    ap.add_argument("--asm", default="DWSIM.Automation.FluentAPI")
    args = ap.parse_args()
    table = load_xml_doc(args.asm)
    if not table:
        print(f"No XML doc found for assembly {args.asm}.")
        return 1
    if args.query is None:
        print(f"{len(table)} documented members in {args.asm}.")
        return 0
    q = args.query.lower()
    hits = sorted(k for k in table if q in k.lower())
    for k in hits[:50]:
        print(f"{k}\n    {table[k]}\n")
    if len(hits) > 50:
        print(f"... and {len(hits) - 50} more")
    return 0


if __name__ == "__main__":
    raise SystemExit(_cli())
