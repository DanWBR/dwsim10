"""Generate MkDocs Markdown pages from the assembly's XML doc comments.

Reads `bin/Debug/DWSIM.Automation.FluentAPI.xml`, emits one .md per public type
under `docs/api-reference/`, plus an index page organized by namespace.

Usage (from DWSIM.FluentAPI/):
    python scripts/generate_api_reference.py
"""
from __future__ import annotations

import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from dataclasses import dataclass, field

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "docs", "api-reference")
ASSEMBLY_NS = "DWSIM.Automation.FluentAPI"
XML_NAME = "DWSIM.Automation.FluentAPI.xml"


def find_xml() -> str:
    """The newest doc file under bin/Debug.

    An SDK-style build puts it in a target-framework folder; the old project put it
    directly under bin/Debug. Take whichever exists, newest first, so the path does not
    have to be updated every time the framework moves.
    """
    root = os.path.join(ROOT, "bin", "Debug")
    found = []
    for base, _, files in os.walk(root):
        if XML_NAME in files:
            path = os.path.join(base, XML_NAME)
            found.append((os.path.getmtime(path), path))
    if not found:
        return os.path.join(root, XML_NAME)
    return max(found)[1]


XML_PATH = find_xml()


# ---------------------------------------------------------------- name parsing

def split_id(member_id: str):
    """Return (kind, full_name, args) for a member id like 'M:Ns.T.M(...)'."""
    kind, rest = member_id.split(":", 1)
    args = ""
    if "(" in rest:
        rest, args = rest.split("(", 1)
        args = "(" + args
    return kind, rest, args


def short_type_arg(t: str) -> str:
    """Trim BCL noise from a parameter type for display."""
    t = t.replace("System.", "")
    t = t.replace("DWSIM.Automation.FluentAPI.", "")
    t = t.replace("DWSIM.Automation.FluentAPI.Builders.", "")
    t = t.replace("Builders.", "")
    return t


def parse_args(args: str) -> str:
    """'(System.String,System.Double)' -> '(string, double)'."""
    if not args:
        return ""
    inner = args.strip("()")
    if not inner:
        return "()"
    parts = []
    depth = 0
    cur = ""
    for ch in inner:
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append(cur)
            cur = ""
        else:
            cur += ch
    if cur:
        parts.append(cur)
    pretty = []
    for p in parts:
        p = p.strip()
        # crude C#-ish names
        p = p.replace("System.String", "string")
        p = p.replace("System.Double", "double")
        p = p.replace("System.Int32", "int")
        p = p.replace("System.Boolean", "bool")
        p = p.replace("System.Object", "object")
        p = p.replace("System.Action", "Action")
        p = p.replace("System.Func", "Func")
        p = short_type_arg(p)
        pretty.append(p)
    return "(" + ", ".join(pretty) + ")"


def short_member(full_name: str) -> str:
    return full_name.rsplit(".", 1)[-1]


def short_type_name(full_name: str) -> str:
    name = full_name
    if name.startswith(ASSEMBLY_NS + "."):
        name = name[len(ASSEMBLY_NS) + 1 :]
    return name


def slug(name: str) -> str:
    return re.sub(r"[^a-zA-Z0-9_-]+", "-", name).strip("-").lower()


# ---------------------------------------------------------------- doc rendering

CREF_TEXT = re.compile(r'cref="([A-Z]):([^"]+)"')


def render_xml(elem: ET.Element, types_in_assembly: set[str]) -> str:
    """Recursively render an XML doc element to Markdown."""
    parts: list[str] = []
    if elem.text:
        parts.append(escape_text(elem.text))
    for child in elem:
        parts.append(render_child(child, types_in_assembly))
        if child.tail:
            parts.append(escape_text(child.tail))
    return "".join(parts)


def escape_text(t: str) -> str:
    # XML doc comments come pre-indented by VS. Treat any run of whitespace
    # (including newlines) as a single space so words flow naturally and
    # spaces around inline <see>/<c> tags are preserved.
    return re.sub(r"\s+", " ", t)


def render_child(child: ET.Element, types: set[str]) -> str:
    tag = child.tag
    if tag == "see" or tag == "seealso":
        cref = child.get("cref", "")
        return render_cref(cref, types)
    if tag == "paramref":
        return f"`{child.get('name', '')}`"
    if tag == "typeparamref":
        return f"`{child.get('name', '')}`"
    if tag == "c":
        return f"`{(child.text or '').strip()}`"
    if tag == "code":
        body = (child.text or "").rstrip()
        # strip leading common whitespace
        lines = body.splitlines()
        # remove the first blank line if any
        while lines and not lines[0].strip():
            lines.pop(0)
        if lines:
            indent = min((len(l) - len(l.lstrip()) for l in lines if l.strip()), default=0)
            lines = [l[indent:] for l in lines]
        return "\n\n```csharp\n" + "\n".join(lines) + "\n```\n\n"
    if tag == "para":
        return "\n\n" + render_xml(child, types) + "\n\n"
    if tag == "list":
        return render_list(child, types)
    if tag in ("b", "strong"):
        return f"**{render_xml(child, types)}**"
    if tag in ("i", "em"):
        return f"*{render_xml(child, types)}*"
    # passthrough for unknown tags
    return render_xml(child, types)


def render_cref(cref: str, types: set[str]) -> str:
    if not cref or ":" not in cref:
        return f"`{cref}`"
    kind, rest = cref.split(":", 1)
    args = ""
    if "(" in rest:
        rest, args = rest.split("(", 1)
        args = "(" + args
    if kind == "T":
        if rest in types:
            return f"[`{short_type_name(rest)}`]({slug(rest)}.md)"
        return f"`{short_type_name(rest)}`"
    # member: link to its declaring type page (without anchor — Markdown
    # slugs can't disambiguate method overloads reliably).
    parent = rest.rsplit(".", 1)[0]
    member = short_member(rest)
    if parent in types:
        return f"[`{member}`]({slug(parent)}.md)"
    return f"`{member}`"


def render_list(elem: ET.Element, types: set[str]) -> str:
    out = ["\n"]
    for item in elem.findall("item"):
        desc = item.find("description")
        if desc is not None:
            out.append("- " + render_xml(desc, types).strip() + "\n")
        elif item.text:
            out.append("- " + escape_text(item.text).strip() + "\n")
    out.append("\n")
    return "".join(out)


# ---------------------------------------------------------------- model

@dataclass
class MemberDoc:
    kind: str           # M / P / F / E
    name: str           # e.g. WithOutletTemperature
    args_pretty: str    # e.g. (Quantity)
    summary: str = ""
    remarks: str = ""
    params: list[tuple[str, str]] = field(default_factory=list)
    returns: str = ""
    examples: list[str] = field(default_factory=list)
    is_ctor: bool = False


@dataclass
class TypeDoc:
    full_name: str       # DWSIM.Automation.FluentAPI.Flowsheet
    summary: str = ""
    remarks: str = ""
    examples: list[str] = field(default_factory=list)
    type_params: list[tuple[str, str]] = field(default_factory=list)
    members: list[MemberDoc] = field(default_factory=list)


# ---------------------------------------------------------------- main

def main():
    if not os.path.exists(XML_PATH):
        print(f"XML not found at {XML_PATH}", file=sys.stderr)
        sys.exit(1)

    tree = ET.parse(XML_PATH)
    members_xml = tree.findall("./members/member")

    # First pass: collect all type ids
    type_ids: set[str] = set()
    for m in members_xml:
        mid = m.get("name", "")
        kind, full, _ = split_id(mid)
        if kind == "T":
            type_ids.add(full)

    types: dict[str, TypeDoc] = {tid: TypeDoc(full_name=tid) for tid in type_ids}

    # Second pass: populate
    for m in members_xml:
        mid = m.get("name", "")
        kind, full, args = split_id(mid)
        if kind == "T":
            t = types[full]
            t.summary = render_section(m, "summary", type_ids)
            t.remarks = render_section(m, "remarks", type_ids)
            t.examples = render_examples(m, type_ids)
            for tp in m.findall("typeparam"):
                t.type_params.append((tp.get("name", ""), render_xml(tp, type_ids).strip()))
            continue

        # member — find owning type
        parent = full.rsplit(".", 1)[0]
        member_name = full.rsplit(".", 1)[-1]

        # Walk up — handles nested types (Flowsheet.NestedClass.Method)
        owning = None
        candidate = parent
        while candidate and "." in candidate:
            if candidate in type_ids:
                owning = candidate
                break
            # try assembling more of the suffix into the member name
            head, tail = candidate.rsplit(".", 1)
            member_name = tail + "." + member_name
            candidate = head
        if owning is None and parent in type_ids:
            owning = parent
        if owning is None:
            continue  # orphan

        is_ctor = member_name.startswith("#ctor")
        if is_ctor:
            display = "(ctor)"
        else:
            display = member_name

        md = MemberDoc(
            kind=kind,
            name=display,
            args_pretty=parse_args(args),
            summary=render_section(m, "summary", type_ids),
            remarks=render_section(m, "remarks", type_ids),
            returns=render_section(m, "returns", type_ids),
            examples=render_examples(m, type_ids),
            is_ctor=is_ctor,
        )
        for p in m.findall("param"):
            md.params.append((p.get("name", ""), render_xml(p, type_ids).strip()))
        types[owning].members.append(md)

    # Sort members within each type: ctors, then methods, props, fields, events
    kind_order = {"#ctor": 0, "M": 1, "P": 2, "F": 3, "E": 4}
    for t in types.values():
        t.members.sort(key=lambda x: (
            0 if x.is_ctor else kind_order.get(x.kind, 9),
            x.name.lower(),
        ))

    # Emit pages
    os.makedirs(OUT_DIR, exist_ok=True)
    # purge existing .md
    for f in os.listdir(OUT_DIR):
        if f.endswith(".md"):
            os.remove(os.path.join(OUT_DIR, f))

    by_namespace = defaultdict(list)
    for tid in sorted(type_ids):
        ns = tid.rsplit(".", 1)[0]
        by_namespace[ns].append(tid)
        write_type_page(types[tid], type_ids)

    write_index(by_namespace)
    print(f"Wrote {len(type_ids)} type pages to {OUT_DIR}")


def render_section(elem: ET.Element, tag: str, types: set[str]) -> str:
    node = elem.find(tag)
    if node is None:
        return ""
    return collapse(render_xml(node, types))


def render_examples(elem: ET.Element, types: set[str]) -> list[str]:
    return [collapse(render_xml(e, types)) for e in elem.findall("example")]


def collapse(s: str) -> str:
    # Collapse 3+ blank lines to 2, strip leading/trailing whitespace
    s = re.sub(r"\n{3,}", "\n\n", s)
    return s.strip()


def write_type_page(t: TypeDoc, types: set[str]):
    short = short_type_name(t.full_name)
    fname = slug(t.full_name) + ".md"
    path = os.path.join(OUT_DIR, fname)

    lines: list[str] = []
    lines.append(f"# {short}")
    lines.append("")
    lines.append(f"`{t.full_name}`")
    lines.append("")
    if t.summary:
        lines.append(t.summary)
        lines.append("")
    if t.type_params:
        lines.append("**Type parameters**")
        lines.append("")
        for name, desc in t.type_params:
            lines.append(f"- `{name}` — {desc}")
        lines.append("")
    if t.remarks:
        lines.append("## Remarks")
        lines.append("")
        lines.append(t.remarks)
        lines.append("")
    for ex in t.examples:
        lines.append("**Example**")
        lines.append("")
        lines.append(ex)
        lines.append("")

    # Members — group by kind
    ctors = [m for m in t.members if m.is_ctor]
    methods = [m for m in t.members if m.kind == "M" and not m.is_ctor]
    props = [m for m in t.members if m.kind == "P"]
    fields = [m for m in t.members if m.kind == "F"]
    events = [m for m in t.members if m.kind == "E"]

    if ctors:
        lines.append("## Constructors")
        lines.append("")
        for m in ctors:
            emit_member(lines, m)
    if methods:
        lines.append("## Methods")
        lines.append("")
        for m in methods:
            emit_member(lines, m)
    if props:
        lines.append("## Properties")
        lines.append("")
        for m in props:
            emit_member(lines, m)
    if fields:
        lines.append("## Fields")
        lines.append("")
        for m in fields:
            emit_member(lines, m)
    if events:
        lines.append("## Events")
        lines.append("")
        for m in events:
            emit_member(lines, m)

    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines).rstrip() + "\n")


def emit_member(lines: list[str], m: MemberDoc):
    sig = m.name + (m.args_pretty if m.kind == "M" or m.is_ctor else "")
    lines.append(f"### `{sig}`")
    lines.append("")
    if m.summary:
        lines.append(m.summary)
        lines.append("")
    if m.params:
        lines.append("**Parameters**")
        lines.append("")
        for name, desc in m.params:
            lines.append(f"- `{name}` — {desc}")
        lines.append("")
    if m.returns:
        lines.append(f"**Returns**: {m.returns}")
        lines.append("")
    if m.remarks:
        lines.append("**Remarks**")
        lines.append("")
        lines.append(m.remarks)
        lines.append("")
    for ex in m.examples:
        lines.append("**Example**")
        lines.append("")
        lines.append(ex)
        lines.append("")


def write_index(by_namespace: dict):
    path = os.path.join(OUT_DIR, "index.md")
    lines = [
        "# API Reference",
        "",
        "This section is **generated automatically** from the XML doc comments",
        "in `DWSIM.Automation.FluentAPI.dll`. Every public type, method,",
        "property and parameter shown here is sourced directly from the assembly,",
        "so it stays in sync with the build.",
        "",
        "For task-oriented walk-throughs and examples, see the hand-written",
        "[API Reference](../api/flowsheet.md) and [Examples](../examples/index.md)",
        "sections.",
        "",
    ]
    for ns in sorted(by_namespace.keys()):
        lines.append(f"## `{ns}`")
        lines.append("")
        for tid in sorted(by_namespace[ns]):
            short = short_type_name(tid)
            lines.append(f"- [`{short}`]({slug(tid)}.md)")
        lines.append("")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines).rstrip() + "\n")


if __name__ == "__main__":
    main()
