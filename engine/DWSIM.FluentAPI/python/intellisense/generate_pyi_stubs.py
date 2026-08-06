r"""
Generates Python stub files (``.pyi``) from a .NET assembly + its XML doc, so
VS Code (Pylance) and PyCharm display IntelliSense + hover docstrings when
users import DWSIM.Automation.FluentAPI through pythonnet.

Run::

    python generate_pyi_stubs.py \
        --asm DWSIM.Automation.FluentAPI \
        --bin "C:\\path\\to\\DWSIM\\bin\\x64\\Debug" \
        --out "./stubs"

This produces a ``stubs/`` package laid out the way IDE stub-resolvers expect:

    stubs/DWSIM-stubs/Automation/FluentAPI/__init__.pyi
    stubs/DWSIM-stubs/Automation/FluentAPI/Builders/__init__.pyi
    ...

Tell the IDE to look there (``python.analysis.stubPath`` in VS Code, or
"Mark as Sources Root" in PyCharm) and IntelliSense lights up — even though
the actual classes only exist after ``clr.AddReference`` at runtime.

Notes
-----
* Stubs use Python type ``Any`` for most params; that's plenty for
  documentation-driven IntelliSense.  Tighter typing would require a full
  CLR-type → Python-type mapping (System.Double → float, System.Boolean → bool,
  etc.) — feel free to extend the ``_py_type`` map below.
* Properties become ``property`` declarations.
* Static methods get the ``@staticmethod`` decorator; constructors merge into
  ``__init__``.
* Generic methods come through with their CLR-style ``\`1`` markers stripped.
"""

from __future__ import annotations

import argparse
import io
import os
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from typing import Dict, List, Optional, Tuple

# MSBuild captures our stdout in the system code page (cp1252 on most Windows
# installs). Force UTF-8 so the few non-ASCII chars in our progress messages
# don't blow up the build.
try:
    sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]
    sys.stderr.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]
except Exception:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")


# --------------------------------------------------------------------- type map

_PY_TYPE = {
    "System.Void": "None",
    "System.Boolean": "bool",
    "System.Byte": "int",
    "System.SByte": "int",
    "System.Int16": "int",
    "System.UInt16": "int",
    "System.Int32": "int",
    "System.UInt32": "int",
    "System.Int64": "int",
    "System.UInt64": "int",
    "System.Single": "float",
    "System.Double": "float",
    "System.Decimal": "float",
    "System.String": "str",
    "System.Char": "str",
    "System.Object": "Any",
    "System.DateTime": "Any",
}


def _py_type(clr_full_name: str) -> str:
    if clr_full_name is None:
        return "Any"
    name = clr_full_name
    # Nullable<T> → Optional[T]
    if name.startswith("System.Nullable`1[["):
        inner = name[len("System.Nullable`1[["):].split(",", 1)[0]
        return f"Optional[{_py_type(inner)}]"
    # Arrays → List[T]
    if name.endswith("[]"):
        return f"List[{_py_type(name[:-2])}]"
    # Generic List<T> / IEnumerable<T> / IList<T>
    for prefix in (
        "System.Collections.Generic.List`1[[",
        "System.Collections.Generic.IList`1[[",
        "System.Collections.Generic.IEnumerable`1[[",
        "System.Collections.Generic.IReadOnlyList`1[[",
        "System.Collections.Generic.ICollection`1[[",
    ):
        if name.startswith(prefix):
            inner = name[len(prefix):].split(",", 1)[0]
            return f"List[{_py_type(inner)}]"
    # Dictionary<K,V>
    if name.startswith("System.Collections.Generic.Dictionary`2[["):
        rest = name[len("System.Collections.Generic.Dictionary`2[["):]
        # crude split — works for the simple key/value combos we expect
        try:
            k_full, _, v_full = rest.partition("],[")
            return f"Dict[{_py_type(k_full.split(',')[0])}, {_py_type(v_full.split(',')[0])}]"
        except Exception:
            return "Dict[Any, Any]"
    return _PY_TYPE.get(name, name.split(".")[-1])


# ------------------------------------------------------------ XML doc lookup


def _load_xml(xml_path: str) -> Dict[str, str]:
    if not os.path.isfile(xml_path):
        return {}
    out: Dict[str, str] = {}
    tree = ET.parse(xml_path)
    for m in tree.findall(".//member"):
        name = m.get("name") or ""
        summary = m.find("summary")
        if summary is None:
            continue
        # Inline <see cref="..."/> as `LastSegment` so doc text reads naturally
        # instead of dropping the reference entirely (default itertext()).
        for see in summary.findall(".//see"):
            cref = see.get("cref") or see.get("href") or ""
            # cref looks like "M:DWSIM.X.Y.Method(System.String)" or "T:Foo.Bar".
            # Strip prefix kind, then drop arg list, then take the trailing segment.
            if cref.startswith(("T:", "M:", "P:", "F:", "E:")):
                cref = cref[2:]
            cref = cref.split("(", 1)[0]
            label = cref.split(".")[-1] if cref else ""
            see.text = (see.text or "") + label
        for pref in summary.findall(".//paramref"):
            pref.text = (pref.text or "") + (pref.get("name") or "")
        text = "".join(summary.itertext()).strip()
        text = " ".join(text.split())
        out[name] = text
    return out


# ------------------------------------------------------------------- emission


def _docstring(text: Optional[str], indent: str) -> str:
    if not text:
        return ""
    safe = text.replace('"""', '\\"\\"\\"')
    return f'{indent}"""{safe}"""\n'


def _emit_class(t, xml: Dict[str, str], buf: List[str]) -> None:
    bases: list[str] = []
    if t.BaseType is not None and t.BaseType.FullName not in (None, "System.Object"):
        b = _py_type(t.BaseType.FullName)
        # Skip generic / cross-namespace bases that would produce invalid Python:
        # CLR generic encoding (`SomeType`1[[Other, Assembly, Version=...]]`) does
        # not survive the simple .Name fallback. Stubs only need readable hints,
        # so drop the base in those cases — Pylance still picks up class members.
        if b and "[" not in b and "," not in b and " " not in b:
            bases.append(b)
    interfaces = [i.Name for i in t.GetInterfaces() if i is not None]
    base_str = f"({', '.join(bases)})" if bases else ""
    buf.append(f"class {t.Name}{base_str}:")
    cls_doc = xml.get(f"T:{t.FullName}", "")
    if cls_doc:
        buf.append(_docstring(cls_doc, "    "))
    if interfaces:
        buf.append(f"    # implements: {', '.join(interfaces)}")
        buf.append("")

    body_lines = 0

    # Constructors
    from System.Reflection import BindingFlags  # type: ignore
    flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
    try:
        ctors = list(t.GetConstructors())
    except Exception:
        ctors = []
    for c in ctors:
        params = ", ".join(
            f"{_safe_name(p.Name)}: {_py_type(p.ParameterType.FullName)}" for p in c.GetParameters()
        )
        buf.append(f"    def __init__(self{', ' + params if params else ''}) -> None: ...")
        body_lines += 1

    # Public properties
    try:
        props = [p for p in t.GetProperties(flags) if p is not None]
    except Exception:
        props = []
    for p in props:
        pty = _py_type(p.PropertyType.FullName)
        pdoc = xml.get(f"P:{t.FullName}.{p.Name}", "")
        buf.append("    @property")
        buf.append(f"    def {p.Name}(self) -> {pty}:")
        if pdoc:
            buf.append(_docstring(pdoc, "        ").rstrip("\n"))
            buf.append("        ...")
        else:
            buf.append("        ...")
        if p.CanWrite:
            buf.append(f"    @{p.Name}.setter")
            buf.append(f"    def {p.Name}(self, value: {pty}) -> None: ...")
        body_lines += 1

    # Public methods (skip property accessors and operators)
    try:
        methods = [m for m in t.GetMethods(flags) if m is not None and not m.IsSpecialName]
    except Exception:
        methods = []
    seen_names: dict[str, int] = defaultdict(int)
    for m in methods:
        if m.DeclaringType is None or m.DeclaringType.FullName != t.FullName:
            continue  # skip inherited
        seen_names[m.Name] += 1
    for m in methods:
        if m.DeclaringType is None or m.DeclaringType.FullName != t.FullName:
            continue
        params = list(m.GetParameters())
        param_sig = ", ".join(
            f"{_safe_name(p.Name)}: {_py_type(p.ParameterType.FullName)}" for p in params
        )
        ret = _py_type(m.ReturnType.FullName)
        # Static or instance?
        if m.IsStatic:
            buf.append("    @staticmethod")
            head = f"    def {m.Name}({param_sig}) -> {ret}:"
        else:
            head = f"    def {m.Name}(self{', ' + param_sig if param_sig else ''}) -> {ret}:"
        # Match XML doc — try several signatures
        sig_csv = ",".join(p.ParameterType.FullName for p in params)
        docid_full = f"M:{t.FullName}.{m.Name}({sig_csv})" if sig_csv else f"M:{t.FullName}.{m.Name}"
        mdoc = xml.get(docid_full, "")
        if not mdoc:
            # fallback: find any overload
            prefix = f"M:{t.FullName}.{m.Name}"
            for k, v in xml.items():
                if k == prefix or k.startswith(prefix + "("):
                    mdoc = v
                    break
        buf.append(head)
        if mdoc:
            buf.append(_docstring(mdoc, "        ").rstrip("\n"))
            buf.append("        ...")
        else:
            buf.append("        ...")
        body_lines += 1

    if body_lines == 0:
        buf.append("    ...")
    buf.append("")


def _safe_name(name: Optional[str]) -> str:
    if not name:
        return "_"
    if name in ("from", "import", "in", "is", "as", "with", "return", "lambda", "global", "yield", "class", "def", "if", "else", "for", "while", "True", "False", "None", "and", "or", "not", "pass", "raise", "try", "except", "finally", "del"):
        return name + "_"
    return name


# ---------------------------------------------------------- assembly walking


def _emit_namespace_file(
    types: list, xml: Dict[str, str], out_path: str
) -> None:
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    buf: List[str] = [
        "# Auto-generated stub for DWSIM.Automation.FluentAPI — DO NOT EDIT.",
        "from __future__ import annotations",
        "from typing import Any, Dict, List, Optional",
        "",
    ]
    for t in sorted(types, key=lambda x: x.Name):
        if t.IsNested:
            continue
        try:
            _emit_class(t, xml, buf)
        except Exception as exc:  # don't kill the whole emit on one bad type
            buf.append(f"# (skipped {t.FullName}: {exc})")
            buf.append("")
    with open(out_path, "w", encoding="utf-8") as fh:
        fh.write("\n".join(buf))


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--asm", default="DWSIM.Automation.FluentAPI",
                    help="Assembly name (no extension).")
    ap.add_argument("--bin", required=True,
                    help="Folder containing the assembly DLL + its .xml.")
    ap.add_argument("--out", default="./stubs",
                    help="Output root folder (a '<rootns>-stubs' tree is created inside).")
    args = ap.parse_args()

    sys.path.append(args.bin)
    try:
        import clr  # type: ignore
    except ImportError:
        print("pythonnet (`pip install pythonnet`) is required.", file=sys.stderr)
        return 2

    # Register the resolver from the FluentAPI helper so transitively-loaded
    # Plus DLLs (refining, electrolyte, advanced HX, ExtensionPack, ...) are
    # findable when reflection walks them.
    try:
        clr.AddReference(args.asm)
        from DWSIM.Automation.FluentAPI import Flowsheet  # type: ignore
        Flowsheet.RegisterAssemblyResolver()
    except Exception:
        clr.AddReference(args.asm)

    # Force-load every Plus / extension assembly that lives next to the FluentAPI
    # DLL. Without this, asm.GetTypes() raises ReflectionTypeLoadException for
    # types whose dependencies aren't yet resolved and they're silently dropped.
    from System.Reflection import Assembly  # type: ignore
    for sub in ("unitops", "unitops2", "extenders", "extenders2", "ppacks", "ppacks2"):
        d = os.path.join(args.bin, sub)
        if not os.path.isdir(d):
            continue
        for fn in os.listdir(d):
            if not fn.lower().endswith(".dll"):
                continue
            try:
                Assembly.LoadFrom(os.path.join(d, fn))
            except Exception:
                pass

    from System import AppDomain  # type: ignore

    asm = next((a for a in AppDomain.CurrentDomain.GetAssemblies()
                if a.GetName().Name == args.asm), None)
    if asm is None:
        print(f"Assembly {args.asm} not loaded.", file=sys.stderr)
        return 1
    xml_path = os.path.join(args.bin, args.asm + ".xml")
    xml = _load_xml(xml_path)
    print(f"Loaded {len(xml)} XML doc entries from {xml_path}")

    try:
        all_types = list(asm.GetTypes())
    except Exception as exc:
        # ReflectionTypeLoadException — recover what we can
        all_types = list(getattr(exc, "Types", []) or [])
        all_types = [t for t in all_types if t is not None]

    by_ns: Dict[str, list] = defaultdict(list)
    for t in all_types:
        if t is None or not t.IsPublic:
            continue
        ns = t.Namespace or ""
        by_ns[ns].append(t)

    root_ns = args.asm.split(".")[0]  # "DWSIM"
    pkg_root = os.path.join(args.out, f"{root_ns}-stubs")

    n_files = 0
    for ns, types in by_ns.items():
        rel = ns.replace(root_ns + ".", "", 1).replace(".", os.sep) if ns.startswith(root_ns + ".") else ns.replace(".", os.sep)
        out_path = os.path.join(pkg_root, rel, "__init__.pyi")
        _emit_namespace_file(types, xml, out_path)
        n_files += 1
        print(f"  → {out_path}  ({len(types)} types)")

    # Marker file so Pylance treats the tree as a stub package.
    with open(os.path.join(pkg_root, "py.typed"), "w") as fh:
        fh.write("partial\n")

    print(f"\nGenerated {n_files} .pyi files in {pkg_root}")
    print("Add this to VS Code settings.json:")
    print(f'  "python.analysis.stubPath": "{os.path.abspath(args.out)}"')
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
