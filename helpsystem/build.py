"""
DWSIM User Guide → MkDocs Material help system.

Pipeline:
    1. Parse user_guide_revised.lyx directly into LaTeX (no lyx CLI)
    2. Flatten \\include{...} for the 13 external .tex files
    3. Expand custom macros (\\CRm, \\SIidx, \\Ksp)
    4. Run pandoc: LaTeX -> Markdown
    5. Split Markdown into one file per top-level section
    6. Copy screens*/ image trees and rewrite image paths
    7. Rewrite \\ref{} cross-references to Markdown anchors
    8. Generate mkdocs.yml nav

Run:  python build.py
"""

from __future__ import annotations

import io
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

# OUTPUT_DIR is this script's own folder, so the build works wherever the repo is checked out.
# CI runs `--skip-convert --portable`, which only needs the committed docs/ and this tree - no LyX,
# no Pandoc. The LyX->Markdown conversion is author-only.
OUTPUT_DIR = Path(__file__).resolve().parent
# The LyX source (author-only, for regenerating docs/) lives outside the repo; override the location
# with DWSIM_GUIDE_SRC. The --skip-convert path never reads it.
SOURCE_DIR = Path(os.environ.get(
    "DWSIM_GUIDE_SRC",
    r"C:\Users\danie\OneDrive\Arquivos DWSIM\DWSIM 6.x+ Release\docs\user_guide"))
LYX_FILE = SOURCE_DIR / "User_Guide.lyx"

INCLUDED_TEX = [
    "convergence_enhancer",
    "tea_lca_extensions",
    "zeolite_adsorber_model",
    "copper_bed_mercury_adsorber_model",
    "pipe_network_model",
    "restriction_orifice_model",
    "advanced_heat_exchanger_model",
    "vapor_compression_chiller_model",
    "additional_unit_operations",
    "refining_unit_operations_model",
    "model_descriptions",
    "thermopack_model_descriptions",
    "corrosion_scaling_manual",
]

PANDOC = os.environ.get("PANDOC", r"C:\Program Files\Pandoc\pandoc.exe")
BUILD_DIR = OUTPUT_DIR / "build"
DOCS_DIR = OUTPUT_DIR / "docs"
DIST_DIR = OUTPUT_DIR / "dist" / "dwsim-help"  # offline-shippable output

# Where the dwsim-assistant Python project keeps its RAG corpus on the
# build machine. Used by --install-assistant-knowledge to push freshly
# converted user-guide markdown into the assistant's knowledge folder.
ASSISTANT_KNOWLEDGE_DIR = Path(os.environ.get(
    "DWSIM_ASSISTANT_KNOWLEDGE",
    r"C:\Users\danie\source\repos\DanWBR\dwsim-assistant\knowledge\user_guide"))


# ---------------------------------------------------------------------------
# LyX -> LaTeX converter
# ---------------------------------------------------------------------------

LAYOUT_MAP = {
    "Part": ("\\part{", "}"),
    "Section": ("\\section{", "}"),
    "Section*": ("\\section*{", "}"),
    "Addsec*": ("\\section*{", "}"),
    "Minisec": ("\\paragraph{", "}"),
    "Subsection": ("\\subsection{", "}"),
    "Subsection*": ("\\subsection*{", "}"),
    "Subsubsection": ("\\subsubsection{", "}"),
    "Subsubsection*": ("\\subsubsection*{", "}"),
    "Paragraph": ("\\paragraph{", "}"),
    "Paragraph*": ("\\paragraph*{", "}"),
    "Subparagraph": ("\\subparagraph{", "}"),
    "Subparagraph*": ("\\subparagraph*{", "}"),
    "Title": ("\\title{", "}"),
    "Author": ("\\author{", "}"),
    "Date": ("\\date{", "}"),
    "Abstract": ("\\begin{abstract}\n", "\n\\end{abstract}"),
    "Quote": ("\\begin{quote}\n", "\n\\end{quote}"),
    "Quotation": ("\\begin{quotation}\n", "\n\\end{quotation}"),
    "Verse": ("\\begin{verse}\n", "\n\\end{verse}"),
}


# Special character maps for LyX text content
SPECIAL_CHARS = {
    "\\SpecialChar nobreakdash": "-",
    "\\SpecialChar endofsentence": ".",
    "\\SpecialChar zerowidthnonjoiner": "",
    "\\SpecialChar ldots": r"\ldots{}",
    "\\SpecialChar menuseparator": r"\,$\triangleright$\,",
    "\\SpecialChar TeX": r"\TeX{}",
    "\\SpecialChar LaTeX": r"\LaTeX{}",
    "\\SpecialChar LaTeX2e": r"\LaTeXe{}",
    "\\SpecialChar dash": "--",
    "\\SpecialChar breakableslash": "/",
    "\\SpecialChar allowbreak": "",
    "\\SpecialChar softhyphen": r"\-",
}


class LyxParser:
    """Convert LyX 2.4 file format to LaTeX source.

    Handles every layout and inset type observed in user_guide_revised.lyx.
    Unknown insets and layouts are dropped with a warning rather than aborting.
    """

    def __init__(self, lines: list[str]):
        self.lines = lines
        self.i = 0
        self.warnings: list[str] = []

    # ---- entry point -----------------------------------------------------

    def parse(self) -> str:
        # Skip until \begin_body
        while self.i < len(self.lines) and not self.lines[self.i].startswith("\\begin_body"):
            self.i += 1
        self.i += 1
        return self._parse_layout_sequence(stop_tokens=("\\end_body", "\\end_document"))

    def _parse_layout_sequence(self, stop_tokens: tuple[str, ...] = ()) -> str:
        """Parse a sequence of `\\begin_layout` blocks at the current depth,
        grouping consecutive Itemize / Enumerate blocks into list environments.

        Stops when one of `stop_tokens` is encountered or when input ends.
        Handles `\\begin_deeper` ... `\\end_deeper` for nested lists.
        """
        out: list[str] = []
        list_kind: str | None = None
        list_items: list[str] = []

        def flush_list():
            nonlocal list_kind, list_items
            if list_kind:
                env = "itemize" if list_kind == "Itemize" else "enumerate" if list_kind == "Enumerate" else "description"
                inner = "\n".join(f"\\item {it}" for it in list_items)
                out.append(f"\\begin{{{env}}}\n{inner}\n\\end{{{env}}}")
                list_kind = None
                list_items = []

        while self.i < len(self.lines):
            line = self.lines[self.i]
            stripped = line.rstrip("\n")
            if any(stripped.startswith(t) for t in stop_tokens):
                break
            if stripped.startswith("\\begin_layout "):
                layout = stripped[len("\\begin_layout "):].strip()
                if layout in ("Itemize", "Enumerate", "Description", "Labeling"):
                    if list_kind and list_kind != layout:
                        flush_list()
                    list_kind = layout
                    self.i += 1
                    body = self._parse_inline_until("\\end_layout").strip()
                    self.i += 1
                    if layout in ("Description", "Labeling"):
                        # First word(s) become the label; rest is content
                        list_items.append(body)
                    else:
                        list_items.append(body)
                    continue
                else:
                    flush_list()
                    self.i += 1
                    body = self._parse_inline_until("\\end_layout").strip()
                    self.i += 1
                    out.append(self._wrap_layout(layout, body))
                    continue
            if stripped.startswith("\\begin_deeper"):
                self.i += 1
                nested = self._parse_layout_sequence(stop_tokens=("\\end_deeper",))
                # consume the \end_deeper
                if self.i < len(self.lines) and self.lines[self.i].rstrip("\n").startswith("\\end_deeper"):
                    self.i += 1
                # Attach to the last list item if currently building a list
                if list_kind and list_items:
                    list_items[-1] += "\n" + nested
                else:
                    flush_list()
                    out.append(nested)
                continue
            # Anything else (blank line, metadata) — skip
            self.i += 1

        flush_list()
        return "\n\n".join(p for p in out if p.strip())

    # ---- layout parsing --------------------------------------------------

    def _parse_layout(self) -> str:
        line = self.lines[self.i]
        layout = line[len("\\begin_layout "):].strip()
        self.i += 1
        body = self._parse_inline_until("\\end_layout")
        self.i += 1  # past \end_layout
        return self._wrap_layout(layout, body)

    def _wrap_layout(self, layout: str, body: str) -> str:
        body = body.strip()
        # Heading-style layouts can't contain block-level content like tabular.
        # If they do (the LyX file does abuse Minisec for callouts), strip the
        # heading wrapper and emit the body as a plain block.
        if layout in LAYOUT_MAP:
            pre, post = LAYOUT_MAP[layout]
            if pre.startswith("\\") and pre.endswith("{") and re.search(
                r"\\begin\{(tabular|longtable|figure|table|itemize|enumerate|verbatim|quote|equation|align)\b",
                body,
            ):
                return body
            return pre + body + post
        if layout == "Standard":
            return body
        if layout in ("Plain", "Plain Layout"):
            # "Plain Layout" — used inside insets/cells; no wrapping
            return body
        if layout == "Itemize":
            return f"\\item {body}"
        if layout == "Enumerate":
            return f"\\item {body}"
        if layout == "Description":
            # body starts with the term, then content
            return f"\\item[] {body}"
        if layout == "Labeling":
            return f"\\item[] {body}"
        if layout == "LyX-Code":
            return f"\\begin{{verbatim}}\n{body}\n\\end{{verbatim}}"
        if layout == "Bibliography":
            return body
        # Unknown: pass through as plain paragraph
        if layout not in self._unknown_layouts:
            self._unknown_layouts.add(layout)
            self.warnings.append(f"Unknown layout: {layout}")
        return body

    _unknown_layouts: set[str] = set()

    # ---- nested block parsing -------------------------------------------

    def _parse_block_until(self, end_token: str, in_list: bool = False) -> str:
        """Parse a sequence of \\begin_layout blocks until end_token. Used for \\begin_deeper."""
        out: list[str] = []
        # Within \begin_deeper we collect items; emit them as a sub-list.
        items_itemize: list[str] = []
        items_enum: list[str] = []
        misc: list[str] = []
        list_type: str | None = None
        while self.i < len(self.lines):
            line = self.lines[self.i]
            if line.startswith(end_token):
                self.i += 1
                break
            if line.startswith("\\begin_layout "):
                layout = line[len("\\begin_layout "):].strip()
                self.i += 1
                body = self._parse_inline_until("\\end_layout").strip()
                self.i += 1
                if layout == "Itemize":
                    if list_type and list_type != "itemize":
                        out.append(self._flush_list(list_type, items_itemize, items_enum))
                        items_itemize, items_enum = [], []
                    list_type = "itemize"
                    items_itemize.append(body)
                elif layout == "Enumerate":
                    if list_type and list_type != "enumerate":
                        out.append(self._flush_list(list_type, items_itemize, items_enum))
                        items_itemize, items_enum = [], []
                    list_type = "enumerate"
                    items_enum.append(body)
                else:
                    if list_type:
                        out.append(self._flush_list(list_type, items_itemize, items_enum))
                        items_itemize, items_enum = [], []
                        list_type = None
                    out.append(self._wrap_layout(layout, body))
            elif line.startswith("\\begin_deeper"):
                self.i += 1
                nested = self._parse_block_until("\\end_deeper", in_list=True)
                # attach nested to last item
                if list_type == "itemize" and items_itemize:
                    items_itemize[-1] += "\n" + nested
                elif list_type == "enumerate" and items_enum:
                    items_enum[-1] += "\n" + nested
                else:
                    out.append(nested)
            else:
                self.i += 1
        if list_type:
            out.append(self._flush_list(list_type, items_itemize, items_enum))
        return "\n\n".join(p for p in out if p.strip())

    @staticmethod
    def _flush_list(kind: str, itemize: list[str], enum: list[str]) -> str:
        if kind == "itemize":
            inner = "\n".join(f"\\item {it}" for it in itemize)
            return f"\\begin{{itemize}}\n{inner}\n\\end{{itemize}}"
        else:
            inner = "\n".join(f"\\item {it}" for it in enum)
            return f"\\begin{{enumerate}}\n{inner}\n\\end{{enumerate}}"

    # ---- inline (within a layout) ---------------------------------------

    def _parse_inline_until(self, end_token: str) -> str:
        """Parse text + insets until end_token at this depth. Returns LaTeX string."""
        # Text formatting state (toggled by \series, \emph, \family, etc.)
        st = {
            "bold": False,
            "emph": False,
            "noun": False,
            "family": "default",  # default | sans | typewriter | roman
            "size": "default",
            "bar": "default",
            "color": "inherit",
            "strikeout": "default",
        }
        out_parts: list[str] = []

        def flush_format_open(closing: list[str]):
            """Close any pending state when we emit text after a state change."""
            return

        while self.i < len(self.lines):
            line = self.lines[self.i]
            stripped = line.rstrip("\n")

            if stripped == end_token or stripped.startswith(end_token + " "):
                # caller advances past end_token
                break

            if stripped.startswith("\\begin_inset"):
                inset_out = self._parse_inset()
                # If the inset returned a block-level construct, close any
                # open inline formatting (\textbf{, \emph{, ...) first so we
                # don't end up with \emph{\begin{tabular}...}.
                if re.search(
                    r"\\begin\{(tabular|longtable|figure|table|itemize|enumerate|verbatim|quote|equation|align)\b",
                    inset_out,
                ):
                    out_parts.append(self._format_close_all(st))
                out_parts.append(inset_out)
                continue

            # Text formatting toggles
            m = re.match(r"\\(series|emph|family|size|bar|color|noun|strikeout|lang)\s+(\S+)", stripped)
            if m:
                key, val = m.group(1), m.group(2)
                # We translate by closing any open and re-opening as needed inline
                out_parts.append(self._format_toggle(st, key, val))
                self.i += 1
                continue

            # Alignment / spacing / paragraph spacing — ignore
            if (
                stripped.startswith("\\align ")
                or stripped.startswith("\\paragraph_spacing")
                or stripped.startswith("\\paragraph_indentation")
                or stripped.startswith("\\labelwidthstring")
                or stripped == "\\noindent"
                or stripped == "\\indent"
                or stripped == "\\added_space_top"
                or stripped == "\\added_space_bottom"
                or stripped.startswith("\\added_space_")
                or stripped.startswith("\\start_of_appendix")
                or stripped.startswith("\\leftindent")
                or stripped.startswith("\\change_")
            ):
                self.i += 1
                continue

            # \backslash on its own line = literal backslash in text
            if stripped == "\\backslash":
                out_parts.append("\\textbackslash{}")
                self.i += 1
                continue

            # \\SpecialChar foo
            if stripped.startswith("\\SpecialChar"):
                replaced = SPECIAL_CHARS.get(stripped, "")
                out_parts.append(replaced)
                self.i += 1
                continue

            # \\InsetSpace ~
            if stripped.startswith("\\InsetSpace"):
                rest = stripped[len("\\InsetSpace"):].strip()
                if rest in ("~", "\\space{}"):
                    out_parts.append("~")
                else:
                    out_parts.append(" ")
                self.i += 1
                continue

            # Comment / unknown directive
            if stripped.startswith("\\"):
                # unknown directive line — skip it
                self.i += 1
                continue

            # Plain text line
            out_parts.append(self._escape_text_line(line))
            self.i += 1

        # Close any still-open formatting
        out_parts.append(self._format_close_all(st))
        return "".join(out_parts)

    # ---- formatting toggles ---------------------------------------------

    @staticmethod
    def _format_toggle(st: dict, key: str, val: str) -> str:
        """Emit LaTeX to close/open formatting based on state change."""
        out = ""
        if key == "series":
            on = (val == "bold")
            if on != st["bold"]:
                out += "}" if st["bold"] else "\\textbf{"
                st["bold"] = on
        elif key == "emph":
            on = (val == "on")
            if on != st["emph"]:
                out += "}" if st["emph"] else "\\emph{"
                st["emph"] = on
        elif key == "noun":
            on = (val == "on")
            if on != st["noun"]:
                out += "}" if st["noun"] else "\\textsc{"
                st["noun"] = on
        elif key == "family":
            new = "default" if val == "default" else val
            if new != st["family"]:
                # close previous family wrapper
                if st["family"] == "sans":
                    out += "}"
                elif st["family"] == "typewriter":
                    out += "}"
                elif st["family"] == "roman":
                    out += "}"
                # open new
                if new == "sans":
                    out += "\\textsf{"
                elif new == "typewriter":
                    out += "\\texttt{"
                elif new == "roman":
                    out += "\\textrm{"
                st["family"] = new
        # size, bar, color, strikeout, lang: ignored visually (rare, low payoff)
        return out

    @staticmethod
    def _format_close_all(st: dict) -> str:
        out = ""
        if st["bold"]:
            out += "}"
            st["bold"] = False
        if st["emph"]:
            out += "}"
            st["emph"] = False
        if st["noun"]:
            out += "}"
            st["noun"] = False
        if st["family"] in ("sans", "typewriter", "roman"):
            out += "}"
            st["family"] = "default"
        return out

    # ---- text escaping --------------------------------------------------

    @staticmethod
    def _escape_text_line(line: str) -> str:
        """Escape LaTeX special chars in plain text content from LyX layout body.

        LyX stores text as UTF-8 with no LaTeX escaping. We must escape:
            #, $, %, &, _, {, }, ~, ^, \\
        Note: LyX uses `\\backslash` on its own line for literal backslash.
        Here we just process the rest of the chars.
        """
        # Strip trailing \n only (line came in with newline)
        text = line.rstrip("\n")
        text = text.replace("\\", "\\textbackslash{}")  # but LyX never emits \ in text body except via \backslash directive (handled separately)
        # wait — actually LyX text lines do not contain backslashes in content; they appear only as LyX directives starting at column 0.
        # So we revert that and instead trust no backslashes in body.
        return _latex_escape(line.rstrip("\n"))

    # ---- inset parsing --------------------------------------------------

    def _parse_inset(self) -> str:
        line = self.lines[self.i].rstrip("\n")
        kind = line[len("\\begin_inset "):].strip()
        self.i += 1
        # Formula insets often have content inline on the begin line:
        #   \begin_inset Formula $\phi=1$
        # In that case `kind` already contains "Formula $\phi=1$" — split.
        if kind.startswith("Formula"):
            return self._parse_formula_inset(kind[len("Formula"):].strip())
        if kind.startswith("FormulaMacro"):
            return self._consume_inset_silently()
        if kind.startswith("ERT"):
            return self._parse_ert_inset()
        if kind.startswith("Graphics"):
            return self._parse_graphics_inset()
        if kind.startswith("Float "):
            float_kind = kind.split()[1]  # figure | table
            return self._parse_float_inset(float_kind)
        if kind.startswith("Caption"):
            return self._parse_caption_inset()
        if kind.startswith("CommandInset"):
            return self._parse_command_inset(kind)
        if kind.startswith("Tabular"):
            return self._parse_tabular_inset()
        if kind.startswith("Text"):
            return self._parse_text_inset()
        if kind.startswith("Box"):
            return self._parse_box_inset()
        if kind.startswith("Note"):
            return self._consume_inset_silently()
        if kind.startswith("Branch"):
            return self._parse_branch_inset()
        if kind.startswith("Newline"):
            self._consume_inset_silently()
            return " \\\\\n"
        if kind.startswith("Newpage"):
            self._consume_inset_silently()
            return "\n\\clearpage\n"
        if kind.startswith("VSpace"):
            self._consume_inset_silently()
            return ""
        if kind.startswith("space "):
            # \begin_inset space ~  ... \end_inset
            self._consume_inset_silently()
            return "~"
        if kind.startswith("Quotes"):
            self._consume_inset_silently()
            return self._quotes_to_char(kind[len("Quotes "):])
        if kind.startswith("Flex URL"):
            return self._parse_flex_url_inset()
        if kind.startswith("Flex"):
            # Generic Flex — pass-through inner text
            return self._parse_flex_generic()
        if kind.startswith("listings"):
            return self._parse_listings_inset()
        if kind.startswith("Foot"):
            return self._parse_foot_inset()
        if kind.startswith("Marginal"):
            return self._consume_inset_silently()
        if kind.startswith("script"):
            return self._parse_script_inset(kind)
        if kind.startswith("Argument"):
            return self._consume_inset_silently()

        # Unknown — drop silently but warn once
        self._warn_unknown_inset(kind)
        self._consume_inset_silently()
        return ""

    _seen_unknown_insets: set[str] = set()

    def _warn_unknown_inset(self, kind: str):
        head = kind.split()[0] if kind else ""
        if head not in self._seen_unknown_insets:
            self._seen_unknown_insets.add(head)
            self.warnings.append(f"Unknown inset: {kind}")

    # ---- inset implementations ------------------------------------------

    def _consume_inset_silently(self) -> str:
        depth = 1
        while self.i < len(self.lines) and depth > 0:
            line = self.lines[self.i].rstrip("\n")
            if line.startswith("\\begin_inset"):
                depth += 1
            elif line == "\\end_inset":
                depth -= 1
                if depth == 0:
                    self.i += 1
                    return ""
            self.i += 1
        return ""

    @staticmethod
    def _quotes_to_char(quote_code: str) -> str:
        # eld = English left double, erd = right; sld/srd = swedish (also "")
        m = {
            "eld": "“", "erd": "”", "els": "‘", "ers": "’",
            "sld": "”", "srd": "”", "sls": "’", "srs": "’",
            "fld": "«", "frd": "»",
            "ald": "”", "ard": "”",
            "gld": "„", "grd": "“",
        }
        return m.get(quote_code.strip(), '"')

    def _parse_formula_inset(self, inline_content: str) -> str:
        """Formula content can be on the begin line (inline like `$\phi=1$`)
        or span multiple lines (e.g. \\begin{equation}...\\end{equation}).
        We collect everything until \\end_inset and emit as-is."""
        parts: list[str] = []
        if inline_content:
            parts.append(inline_content)
        depth = 1
        while self.i < len(self.lines) and depth > 0:
            line = self.lines[self.i]
            stripped = line.rstrip("\n")
            if stripped.startswith("\\begin_inset"):
                depth += 1
                parts.append(stripped)
            elif stripped == "\\end_inset":
                depth -= 1
                if depth == 0:
                    self.i += 1
                    break
                parts.append(stripped)
            else:
                parts.append(stripped)
            self.i += 1
        joined = "\n".join(parts).strip()
        # Heuristic: if it doesn't start with $ or \[ or \begin, wrap as inline math
        if not (joined.startswith("$") or joined.startswith("\\[") or joined.startswith("\\begin")):
            joined = "$" + joined + "$"
        # Keep formulas as-is for pandoc to handle.
        return " " + joined + " "

    def _parse_ert_inset(self) -> str:
        """ERT (Evil Red Text) is raw LaTeX. Body is in nested
        `\\begin_layout Plain Layout` ... `\\end_layout`. The actual LaTeX is
        joined from those text lines; `\\backslash` in LyX = literal backslash."""
        depth = 1
        out: list[str] = []
        in_layout = False
        while self.i < len(self.lines) and depth > 0:
            line = self.lines[self.i]
            stripped = line.rstrip("\n")
            if stripped.startswith("\\begin_inset"):
                depth += 1
                self.i += 1
                continue
            if stripped == "\\end_inset":
                depth -= 1
                if depth == 0:
                    self.i += 1
                    break
                self.i += 1
                continue
            if stripped.startswith("\\begin_layout"):
                in_layout = True
                self.i += 1
                continue
            if stripped == "\\end_layout":
                in_layout = False
                out.append("\n")
                self.i += 1
                continue
            if not in_layout:
                self.i += 1
                continue
            # Translate `\backslash` -> `\`
            if stripped == "\\backslash":
                out.append("\\")
                self.i += 1
                continue
            # Status / formatting directives we don't want
            if stripped.startswith("\\") and not stripped.startswith("\\backslash"):
                # Skip metadata directives in ERT
                self.i += 1
                continue
            # Append raw text (no escaping — ERT is already LaTeX)
            out.append(line.rstrip("\n"))
            self.i += 1
        return "".join(out)

    def _parse_graphics_inset(self) -> str:
        """Read filename, scale/width settings, emit \\includegraphics."""
        filename = ""
        opts: list[str] = []
        depth = 1
        while self.i < len(self.lines) and depth > 0:
            line = self.lines[self.i].rstrip("\n")
            if line.startswith("\\begin_inset"):
                depth += 1
            elif line == "\\end_inset":
                depth -= 1
                if depth == 0:
                    self.i += 1
                    break
            else:
                m = re.match(r"\s*filename\s+(.+)$", line)
                if m:
                    filename = m.group(1).strip()
                m = re.match(r"\s*width\s+(\S+)", line)
                if m:
                    w = m.group(1)
                    # Convert LyX widths: "100text%" -> "\\textwidth" approx
                    w = w.replace("text%", "\\textwidth").replace("col%", "\\columnwidth").replace("page%", "\\paperwidth")
                    if w.endswith("\\textwidth") or w.endswith("\\columnwidth") or w.endswith("\\paperwidth"):
                        try:
                            num = float(w.split("\\")[0])
                            unit = "\\" + w.split("\\")[1]
                            opts.append(f"width={num/100:.3f}{unit}")
                        except Exception:
                            opts.append(f"width={w}")
                    else:
                        opts.append(f"width={w}")
                m = re.match(r"\s*scale\s+(\d+)", line)
                if m:
                    opts.append(f"scale={int(m.group(1))/100:.2f}")
            self.i += 1
        if not filename:
            return ""
        opt_str = "[" + ",".join(opts) + "]" if opts else ""
        return f"\\includegraphics{opt_str}{{{filename}}}"

    def _parse_float_inset(self, kind: str) -> str:
        """Float figure / Float table: collect inner content, wrap in environment.

        Float insets contain metadata (placement, alignment, status) followed by
        a sequence of nested `\\begin_layout Plain Layout` blocks. Read all
        layouts until the matching `\\end_inset`.
        """
        out: list[str] = []
        while self.i < len(self.lines):
            line = self.lines[self.i].rstrip("\n")
            if line == "\\end_inset":
                self.i += 1
                break
            if line.startswith("\\begin_layout "):
                out.append(self._parse_layout())
                continue
            # metadata or blank — skip
            self.i += 1
        body = "\n".join(p for p in out if p.strip())
        env = "figure" if kind == "figure" else "table"
        return f"\\begin{{{env}}}[H]\n\\centering\n{body}\n\\end{{{env}}}"

    def _parse_caption_inset(self) -> str:
        # Caption holds nested layouts; concatenate their text bodies into \\caption{}
        depth = 1
        out: list[str] = []
        while self.i < len(self.lines):
            line = self.lines[self.i].rstrip("\n")
            if line == "\\end_inset":
                self.i += 1
                break
            if line.startswith("\\begin_layout "):
                out.append(self._parse_layout())
            else:
                self.i += 1
        body = " ".join(p for p in out if p.strip()).strip()
        return f"\\caption{{{body}}}"

    def _parse_command_inset(self, kind: str) -> str:
        """Generic CommandInset: read key=val params until \\end_inset, emit LaTeX."""
        cmd = kind.split()[1] if len(kind.split()) > 1 else ""  # ref, label, href, citation, include, toc, line
        params: dict[str, str] = {}
        latex_command = ""
        while self.i < len(self.lines):
            line = self.lines[self.i].rstrip("\n")
            if line == "\\end_inset":
                self.i += 1
                break
            m = re.match(r'^(\w+)\s+"(.*)"$', line)
            if m:
                params[m.group(1)] = m.group(2)
            else:
                m = re.match(r"^LatexCommand\s+(\S+)$", line)
                if m:
                    latex_command = m.group(1)
            self.i += 1

        if cmd == "ref":
            ref = params.get("reference", "")
            return f"\\ref{{{ref}}}"
        if cmd == "label":
            name = params.get("name", "")
            return f"\\label{{{name}}}"
        if cmd == "href":
            target = params.get("target", "")
            name = params.get("name", "")
            if name:
                return f"\\href{{{target}}}{{{name}}}"
            return f"\\url{{{target}}}"
        if cmd == "citation":
            key = params.get("key", "")
            return f"\\cite{{{key}}}"
        if cmd == "include":
            fn = params.get("filename", "")
            base = re.sub(r"\.tex$", "", fn)
            return f"\\include{{{base}}}"
        if cmd == "toc":
            return "\\tableofcontents"
        if cmd == "line":
            return ""  # horizontal rule — drop, pandoc has no LaTeX equivalent it likes
        return ""

    def _parse_tabular_inset(self) -> str:
        """Parse a LyX tabular into a LaTeX longtable.

        LyX tabular xml-like structure:
            <lyxtabular version="3" rows="N" columns="M">
            <features ...>
            <column alignment="..." valignment="..." width="...">...
            <row>
              <cell ...>
                \\begin_inset Text
                \\begin_layout Plain Layout
                  ...content...
                \\end_layout
                \\end_inset
              </cell> (no closing tag)
              <cell ...>...</cell>
            </row>
            ...
            </lyxtabular>
            \\end_inset
        """
        rows: list[list[str]] = []
        col_aligns: list[str] = []
        n_cols = 0
        current_row: list[str] = []
        current_cell_lines: list[str] = []
        in_cell = False

        depth = 1  # the outer Tabular inset
        while self.i < len(self.lines):
            line = self.lines[self.i]
            stripped = line.rstrip("\n")
            if stripped == "\\end_inset" and depth == 1:
                self.i += 1
                break
            if stripped.startswith("<lyxtabular"):
                m = re.search(r'columns="(\d+)"', stripped)
                if m:
                    n_cols = int(m.group(1))
                self.i += 1
                continue
            if stripped.startswith("</lyxtabular>"):
                self.i += 1
                continue
            if stripped.startswith("<features"):
                self.i += 1
                continue
            if stripped.startswith("<column"):
                m = re.search(r'alignment="(\w+)"', stripped)
                a = m.group(1) if m else "left"
                col_aligns.append({"left": "l", "center": "c", "right": "r", "block": "p{3cm}"}.get(a, "l"))
                self.i += 1
                continue
            if stripped.startswith("<row"):
                current_row = []
                self.i += 1
                continue
            if stripped.startswith("</row>"):
                rows.append(current_row)
                self.i += 1
                continue
            if stripped.startswith("<cell"):
                # Begin collecting content of this cell — call the parser recursively.
                self.i += 1
                # The cell wraps `\\begin_inset Text` ... `\\end_inset` then `</cell>` (closing tag missing in LyX, the next `<cell` or `</row>` ends it).
                cell_text = self._parse_tabular_cell()
                current_row.append(cell_text)
                continue
            if stripped.startswith("</cell>"):
                self.i += 1
                continue
            self.i += 1

        if not rows:
            return ""
        if not col_aligns:
            col_aligns = ["l"] * (n_cols or len(rows[0]))
        col_spec = "".join(col_aligns)
        body_lines: list[str] = []
        for r in rows:
            cells = [c.replace("\n", " ").strip() for c in r]
            # pad if needed
            while len(cells) < len(col_aligns):
                cells.append("")
            body_lines.append(" & ".join(cells) + " \\\\")
        body = "\n".join(body_lines)
        return f"\\begin{{tabular}}{{{col_spec}}}\n\\toprule\n{body}\n\\bottomrule\n\\end{{tabular}}"

    def _parse_tabular_cell(self) -> str:
        """Parse a single tabular cell: an optional `\\begin_inset Text` containing layout(s)."""
        out: list[str] = []
        # Read until we see `</cell>` or the next `<cell` or `</row>` at depth 0.
        depth = 0
        while self.i < len(self.lines):
            line = self.lines[self.i].rstrip("\n")
            if line.startswith("</cell>"):
                self.i += 1
                break
            if line.startswith("<cell") or line.startswith("</row>"):
                break
            if line.startswith("\\begin_inset Text"):
                self.i += 1
                # collect layouts until matching \end_inset
                inner_depth = 1
                while self.i < len(self.lines) and inner_depth > 0:
                    l2 = self.lines[self.i].rstrip("\n")
                    if l2.startswith("\\begin_inset"):
                        inner_depth += 1
                        # parse the inset normally so we don't lose nested formulas etc.
                        out.append(self._parse_inset())
                        # _parse_inset advanced i past its own \end_inset
                        # but we incremented inner_depth on the \begin_inset line — undo
                        inner_depth -= 1
                        continue
                    if l2 == "\\end_inset":
                        inner_depth -= 1
                        self.i += 1
                        if inner_depth == 0:
                            break
                        continue
                    if l2.startswith("\\begin_layout "):
                        out.append(self._parse_layout())
                        continue
                    self.i += 1
                continue
            self.i += 1
        return " ".join(p for p in out if p.strip())

    def _parse_text_inset(self) -> str:
        """Inline Text inset (rare outside tabulars). Concatenate inner layouts."""
        out: list[str] = []
        while self.i < len(self.lines):
            line = self.lines[self.i].rstrip("\n")
            if line == "\\end_inset":
                self.i += 1
                break
            if line.startswith("\\begin_layout "):
                out.append(self._parse_layout())
                continue
            self.i += 1
        return " ".join(p for p in out if p.strip())

    def _parse_box_inset(self) -> str:
        """Box (Frameless, Boxed, Framed, etc.) — emit a framed environment."""
        out: list[str] = []
        # Skip metadata until first \begin_layout
        while self.i < len(self.lines):
            line = self.lines[self.i].rstrip("\n")
            if line == "\\end_inset":
                self.i += 1
                break
            if line.startswith("\\begin_layout "):
                out.append(self._parse_layout())
                continue
            self.i += 1
        body = "\n\n".join(p for p in out if p.strip())
        return f"\\begin{{quote}}\n{body}\n\\end{{quote}}"

    def _parse_branch_inset(self) -> str:
        # Branch inset: include only if branch is selected. We'll include the body unconditionally.
        out: list[str] = []
        while self.i < len(self.lines):
            line = self.lines[self.i].rstrip("\n")
            if line == "\\end_inset":
                self.i += 1
                break
            if line.startswith("\\begin_layout "):
                out.append(self._parse_layout())
                continue
            self.i += 1
        return "\n\n".join(p for p in out if p.strip())

    def _parse_flex_url_inset(self) -> str:
        # Flex URL contains a Plain Layout with the URL text
        out: list[str] = []
        while self.i < len(self.lines):
            line = self.lines[self.i].rstrip("\n")
            if line == "\\end_inset":
                self.i += 1
                break
            if line.startswith("\\begin_layout "):
                out.append(self._parse_layout())
                continue
            self.i += 1
        url = " ".join(out).strip()
        return f"\\url{{{url}}}"

    def _parse_flex_generic(self) -> str:
        out: list[str] = []
        while self.i < len(self.lines):
            line = self.lines[self.i].rstrip("\n")
            if line == "\\end_inset":
                self.i += 1
                break
            if line.startswith("\\begin_layout "):
                out.append(self._parse_layout())
                continue
            self.i += 1
        return " ".join(p for p in out if p.strip())

    def _parse_listings_inset(self) -> str:
        """listings inset: extract verbatim text inside Plain Layout(s)."""
        out: list[str] = []
        in_layout = False
        while self.i < len(self.lines):
            line = self.lines[self.i].rstrip("\n")
            if line == "\\end_inset":
                self.i += 1
                break
            if line.startswith("\\begin_layout"):
                in_layout = True
                self.i += 1
                continue
            if line == "\\end_layout":
                in_layout = False
                out.append("\n")
                self.i += 1
                continue
            if line.startswith("\\begin_inset") or line.startswith("status "):
                self.i += 1
                continue
            if line.startswith("lstparams"):
                self.i += 1
                continue
            if not in_layout:
                self.i += 1
                continue
            # treat \backslash as a literal backslash
            if line == "\\backslash":
                out.append("\\")
            elif line.startswith("\\"):
                self.i += 1
                continue
            else:
                out.append(line)
            self.i += 1
        code = "".join(out)
        return f"\n\\begin{{verbatim}}\n{code}\n\\end{{verbatim}}\n"

    def _parse_foot_inset(self) -> str:
        """Footnote: \\footnote{body}."""
        out: list[str] = []
        while self.i < len(self.lines):
            line = self.lines[self.i].rstrip("\n")
            if line == "\\end_inset":
                self.i += 1
                break
            if line.startswith("\\begin_layout "):
                out.append(self._parse_layout())
                continue
            self.i += 1
        body = " ".join(p for p in out if p.strip())
        return f"\\footnote{{{body}}}"

    def _parse_script_inset(self, kind: str) -> str:
        """script superscript / subscript."""
        out: list[str] = []
        is_super = "superscript" in kind
        while self.i < len(self.lines):
            line = self.lines[self.i].rstrip("\n")
            if line == "\\end_inset":
                self.i += 1
                break
            if line.startswith("\\begin_layout "):
                out.append(self._parse_layout())
                continue
            self.i += 1
        body = "".join(out).strip()
        if is_super:
            return f"\\textsuperscript{{{body}}}"
        return f"\\textsubscript{{{body}}}"


# ---------------------------------------------------------------------------
# LaTeX text escaping helper
# ---------------------------------------------------------------------------

_LATEX_ESCAPES = {
    "\\": "\\textbackslash{}",
    "&": r"\&",
    "%": r"\%",
    "$": r"\$",
    "#": r"\#",
    "_": r"\_",
    "{": r"\{",
    "}": r"\}",
    "~": r"\textasciitilde{}",
    "^": r"\textasciicircum{}",
}


def _latex_escape(s: str) -> str:
    # Backslashes never appear in LyX content lines (handled via \backslash directive),
    # so we don't escape them here to avoid double-escaping math we already passed through.
    out = []
    for ch in s:
        if ch in "&%$#_{}":
            out.append(_LATEX_ESCAPES[ch])
        elif ch == "~":
            out.append(r"\textasciitilde{}")
        elif ch == "^":
            out.append(r"\textasciicircum{}")
        else:
            out.append(ch)
    return "".join(out)


# ---------------------------------------------------------------------------
# Pipeline steps
# ---------------------------------------------------------------------------

def step_lyx_to_latex() -> str:
    print(f"[1/8] Parsing LyX -> LaTeX: {LYX_FILE.name}")
    with open(LYX_FILE, encoding="utf-8") as f:
        lines = f.readlines()
    parser = LyxParser(lines)
    body = parser.parse()
    latex = _LATEX_PREAMBLE + "\n\\begin{document}\n" + body + "\n\\end{document}\n"
    print(f"  - body length: {len(body):,} chars")
    if parser.warnings:
        print(f"  - {len(parser.warnings)} unique warnings (sampled):")
        for w in parser.warnings[:10]:
            print(f"      {w}")
    return latex


_LATEX_PREAMBLE = r"""\documentclass{article}
\usepackage{amsmath,amssymb}
\usepackage[version=4]{mhchem}
\usepackage{graphicx}
\usepackage{hyperref}
\usepackage{booktabs}
\usepackage{longtable}
"""


def step_flatten_includes(latex: str) -> str:
    print(f"[2/8] Flattening {len(INCLUDED_TEX)} \\include directives")
    def _read(name: str) -> str:
        p = SOURCE_DIR / f"{name}.tex"
        if not p.exists():
            print(f"  ! missing: {p}")
            return f"% missing include: {name}\n"
        for enc in ("utf-8", "latin-1", "cp1252"):
            try:
                with open(p, encoding=enc) as f:
                    return f.read()
            except UnicodeDecodeError:
                continue
        with open(p, encoding="utf-8", errors="replace") as f:
            return f.read()

    def repl(m):
        base = m.group(1)
        return f"\n% --- begin include: {base} ---\n" + _read(base) + f"\n% --- end include: {base} ---\n"

    out = re.sub(r"\\include\{([^}]+)\}", repl, latex)
    # Also handle nested \input{...} inside the included files
    for _ in range(3):
        prev = out
        out = re.sub(
            r"\\input\{([^}]+)\}",
            lambda m: _read(re.sub(r"\.tex$", "", m.group(1))) if (SOURCE_DIR / (m.group(1) if m.group(1).endswith(".tex") else m.group(1) + ".tex")).exists() else m.group(0),
            out,
        )
        if out == prev:
            break
    return out


def step_expand_macros(latex: str) -> str:
    print("[3/8] Expanding custom macros")
    # \CRm and \SIidx and \Ksp are defined in our preamble, so pandoc with --interpret-macros
    # would handle them. As an extra safety net, expand them textually here too.
    latex = re.sub(r"\\CRm(\b|\{\})", r"\\mathrm{CR}", latex)
    latex = re.sub(r"\\SIidx(\b|\{\})", r"\\mathrm{SI}", latex)
    latex = re.sub(r"\\Ksp(\b|\{\})", r"K_{\\mathrm{sp}}", latex)
    return latex


def step_pandoc_to_md(latex: str) -> str:
    print("[4/8] Pandoc: LaTeX -> Markdown")
    BUILD_DIR.mkdir(parents=True, exist_ok=True)
    tex_path = BUILD_DIR / "flat.tex"
    with open(tex_path, "w", encoding="utf-8") as f:
        f.write(latex)
    md_path = BUILD_DIR / "user_guide.md"
    cmd = [
        PANDOC,
        "--from=latex",
        "--to=gfm+tex_math_dollars+raw_html+pipe_tables+attributes",
        "--mathjax",
        "--wrap=none",
        f"--extract-media={(BUILD_DIR / 'extracted').as_posix()}",
        str(tex_path),
        "-o",
        str(md_path),
    ]
    res = subprocess.run(cmd, capture_output=True, text=True)
    if res.returncode != 0:
        print("Pandoc stderr:")
        print(res.stderr[:4000])
        raise SystemExit(f"pandoc failed (exit {res.returncode})")
    if res.stderr:
        # warnings are fine; show first few
        warn_lines = res.stderr.strip().splitlines()
        print(f"  - pandoc emitted {len(warn_lines)} warning lines")
    with open(md_path, encoding="utf-8") as f:
        md = f.read()
    print(f"  - markdown length: {len(md):,} chars")
    return md


def step_split_sections(md: str) -> tuple[list[dict], dict[str, tuple[str, str]]]:
    """Split Markdown into sections. Returns (sections_list, label_map).

    A section is delimited by a `# Title` (Part) or `## Title` (Section).
    For the help system we split at `# ` and `## ` boundaries; subsections
    stay within their parent page.
    """
    print("[5/8] Splitting Markdown into sections")
    # Convert pandoc anchor style {#sec:foo} on heading lines to standard
    sections: list[dict] = []
    label_map: dict[str, tuple[str, str]] = {}
    current_part: str | None = None
    current: dict | None = None
    nav_idx = 0

    def flush():
        nonlocal current
        if current and current["body"].strip():
            sections.append(current)
        current = None

    # In our pandoc output, `\part{}` -> H1 (# ) and `\section{}` -> H3 (### ).
    # Split pages on those two levels; leave h4+ inside their parent page.
    for line in md.splitlines():
        m = re.match(r"^(#{1,3}) (.+?)(?:\s*\{#([^}]+)(?:\s+[^}]*)?\})?\s*$", line)
        if m:
            level = len(m.group(1))
            if level == 2:
                # h2 isn't used as a page boundary; treat as in-page heading
                if current is None:
                    current = {"nav_idx": 0, "level": 0, "part": None, "title": "Introduction", "anchor": None, "body": ""}
                current["body"] += line + "\n"
                continue
            title = m.group(2).strip()
            anchor = m.group(3)
            if level == 1:
                # Treat Part as a logical section too — can't have empty pages
                flush()
                current_part = title
                nav_idx += 1
                current = {
                    "nav_idx": nav_idx,
                    "level": 1,
                    "part": title,
                    "title": title,
                    "anchor": anchor,
                    "body": "",
                }
                if anchor:
                    label_map[anchor] = (_slug_filename(nav_idx, title), anchor)
                continue
            if level == 3:
                flush()
                nav_idx += 1
                current = {
                    "nav_idx": nav_idx,
                    "level": 2,
                    "part": current_part,
                    "title": title,
                    "anchor": anchor,
                    "body": "",
                }
                if anchor:
                    label_map[anchor] = (_slug_filename(nav_idx, title), anchor)
                continue
        if current is None:
            # preamble before first heading -> index page
            current = {
                "nav_idx": 0,
                "level": 0,
                "part": None,
                "title": "Introduction",
                "anchor": None,
                "body": "",
            }
        current["body"] += line + "\n"

    flush()
    print(f"  - emitted {len(sections)} section pages")
    # Also collect labels embedded inside body content via \label{...} that pandoc preserves as {#name} on headings
    # and as raw `\label{name}` in passthroughs. Index those too.
    return sections, label_map


def _slug_filename(nav_idx: int, title: str) -> str:
    slug = re.sub(r"[^a-zA-Z0-9]+", "-", title.lower()).strip("-")
    slug = slug[:50]
    return f"{nav_idx:02d}-{slug}.md"


def step_copy_images() -> list[Path]:
    print("[6/8] Copying images and rewriting paths")
    img_root = DOCS_DIR / "images"
    img_root.mkdir(parents=True, exist_ok=True)
    copied: list[Path] = []
    _IMAGE_DIR_PREFIXES = ("screens", "imagens", "snaps")
    for entry in SOURCE_DIR.iterdir():
        if entry.is_dir() and any(entry.name.startswith(p) for p in _IMAGE_DIR_PREFIXES):
            dest = img_root / entry.name
            if dest.exists():
                shutil.rmtree(dest)
            shutil.copytree(entry, dest)
            copied.append(dest)
    # Also copy the pandoc-extracted media (if any)
    extracted = BUILD_DIR / "extracted"
    if extracted.exists():
        for sub in extracted.iterdir():
            dest = img_root / sub.name
            if dest.exists():
                shutil.rmtree(dest)
            shutil.copytree(sub, dest)
            copied.append(dest)
    print(f"  - copied {len(copied)} image directories")
    return copied


def step_rewrite_paths_and_refs(sections: list[dict], label_map: dict[str, tuple[str, str]]) -> None:
    """Rewrite image paths and \\ref{} cross-references in each section body."""
    # Collect labels emitted as `[]{#name}` or `\\label{name}` from pandoc raw
    # Also reflect every section's own anchor.
    extra_labels: dict[str, tuple[str, str]] = {}
    for s in sections:
        if s["anchor"]:
            extra_labels[s["anchor"]] = (_slug_filename(s["nav_idx"], s["title"]), s["anchor"])
        # find inline `\label{name}` in the body (raw passthrough)
        for m in re.finditer(r"\\label\{([^}]+)\}", s["body"]):
            extra_labels[m.group(1)] = (_slug_filename(s["nav_idx"], s["title"]), m.group(1))
        # find pandoc-emitted bracket anchors: `[]{#anchor}` or `{#anchor}` after headings
        for m in re.finditer(r"\{#([^}]+)\}", s["body"]):
            extra_labels.setdefault(m.group(1), (_slug_filename(s["nav_idx"], s["title"]), m.group(1)))
        # find <a id="..."> anchors emitted for equation labels
        for m in re.finditer(r'<a id="([^"]+)"></a>', s["body"]):
            extra_labels.setdefault(m.group(1), (_slug_filename(s["nav_idx"], s["title"]), m.group(1)))

    label_map.update(extra_labels)

    for s in sections:
        body = s["body"]

        # Convert pandoc's GFM math output to pymdownx.arithmatex-compatible syntax.
        # Display: ```math\n<eq>\n``` (with optional space, optional indent) -> $$ ... $$
        def _math_repl(m):
            inner = m.group(1).strip()
            # Strip surrounding equation/equation* env so MathJax processes it as a $$ block
            inner = re.sub(r"^\\begin\{equation\*?\}\s*", "", inner)
            inner = re.sub(r"\s*\\end\{equation\*?\}$", "", inner)
            # Capture \label{...} so we can emit an HTML anchor for cross-references
            labels = re.findall(r"\\label\{([^}]*)\}", inner)
            inner = re.sub(r"\\label\{[^}]*\}", "", inner)
            inner = re.sub(r"\\(nonumber|notag)\b", "", inner)
            # Collapse blank/whitespace-only lines that would break MathJax
            inner = re.sub(r"\n[ \t]*\n+", "\n", inner)
            inner = "\n".join(line.rstrip() for line in inner.splitlines())
            anchor_html = "".join(f'<a id="{lbl}"></a>' for lbl in labels)
            # Blank line BETWEEN the anchor and the math block so arithmatex's
            # block-math detector treats `\[ ... \]` as its own paragraph.
            sep = "\n\n" if anchor_html else ""
            return "\n\n" + anchor_html + sep + "\\[\n" + inner.strip() + "\n\\]\n\n"

        body = re.sub(
            r"^[ \t]*```\s*math\s*\n(.+?)\n[ \t]*```\s*$",
            _math_repl,
            body,
            flags=re.DOTALL | re.MULTILINE,
        )
        # Inline: $`<eq>`$ -> $<eq>$
        body = re.sub(r"\$`([^`]+)`\$", r"$\1$", body)

        # Strip residual <span class="roman"></span> empty wrappers between equations
        body = re.sub(r'<span class="roman"></span>\s*', "", body)

        # Unwrap raw <div class="..."> AND <div id="..."> wrappers from pandoc
        # so the markdown content inside (links, tables, lists) gets parsed
        # instead of treated as raw HTML. The pattern uses lazy whitespace on
        # both sides so empty divs match without spanning to a later closing tag.
        # We preserve any id= as an HTML anchor before the unwrapped content
        # so intra-page references (e.g. Table 28 → #tab:ion_params) still work.
        def _unwrap_div(m):
            attrs = m.group(1) or ""
            inner = m.group(2)
            id_m = re.search(r'\bid="([^"]+)"', attrs)
            anchor = f'<a id="{id_m.group(1)}"></a>' if id_m else ""
            return f"\n\n{anchor}\n\n{inner}\n\n"

        body = re.sub(
            r'<div((?=\s)[^>]*?(?:class="(?:center|description|quote|figure)"|id="[^"]+")[^>]*)>\s*?(.*?)\s*?</div>',
            _unwrap_div,
            body,
            flags=re.DOTALL,
        )

        # Rewrite image paths: ![alt](screensXX/foo.png) -> ![alt](images/screensXX/foo.png)
        # Also handles imagens*, imagens16/, imagens17/, snaps/ directories.
        # Normalize uppercase file extensions (e.g. .PNG -> .png) so MkDocs
        # strict-mode path resolution (case-sensitive even on Windows) doesn't
        # flag them as missing.
        _img_pfx = r"(?:screens[\w]*|imagens[\w]*|snaps)"

        def _rewrite_img_md(m: re.Match) -> str:
            path = m.group(2)
            # Lowercase the file extension only
            stem, _, ext = path.rpartition(".")
            path = f"{stem}.{ext.lower()}" if ext else path
            return m.group(1) + "images/" + path + m.group(3)

        body = re.sub(
            r"(!\[[^\]]*\]\()(" + _img_pfx + r"/[^)\s]+)(\))",
            _rewrite_img_md,
            body,
        )

        def _rewrite_img_html(m: re.Match) -> str:
            path = m.group(2)
            stem, _, ext = path.rpartition(".")
            path = f"{stem}.{ext.lower()}" if ext else path
            return m.group(1) + "images/" + path + m.group(3)

        # Pandoc may also emit <img src="screens.../..."/> (or imagens/snaps)
        body = re.sub(
            r'(<img[^>]+src=")(' + _img_pfx + r'/[^"]+)(")',
            _rewrite_img_html,
            body,
        )

        # Pandoc emits <span class="image placeholder" data-original-image-src="...">.
        # Convert to markdown ![](path) so mkdocs resolves URLs correctly under
        # use_directory_urls. We also unwrap surrounding <figure>...<figcaption>
        # blocks into markdown image+caption paragraphs.
        def _norm_src(src: str) -> str:
            # Lowercase the file extension (.PNG -> .png) so MkDocs strict-mode
            # path resolution (case-sensitive even on Windows) doesn't flag them.
            stem, dot, ext = src.rpartition(".")
            src_normalized = f"{stem}{dot}{ext.lower()}" if dot else src
            if src_normalized.startswith(("images/", "http", "/", "data:")):
                return src_normalized
            return "images/" + src_normalized

        # Unwrap <figure ...>...<img...>...<figcaption>cap</figcaption></figure>
        def _figure_repl(m):
            wrapper = m.group(0)
            inner = m.group(1)
            id_m = re.search(r'<figure[^>]*\bid="([^"]+)"', wrapper)
            anchor = f'<a id="{id_m.group(1)}"></a>' if id_m else ""
            src_m = re.search(r'data-original-image-src="([^"]+)"', inner)
            if not src_m:
                src_m = re.search(r'<img[^>]+src="([^"]+)"', inner)
            cap_m = re.search(r'<figcaption[^>]*>(.*?)</figcaption>', inner, re.DOTALL)
            if not src_m:
                return ""  # nothing to render
            src = _norm_src(src_m.group(1))
            cap = cap_m.group(1).strip() if cap_m else ""
            cap = re.sub(r"\s+", " ", cap)
            return (f"\n\n{anchor}\n![{cap}]({src})\n\n*{cap}*\n\n" if cap
                    else f"\n\n{anchor}\n![]({src})\n\n")

        body = re.sub(r"<figure[^>]*>(.*?)</figure>", _figure_repl, body, flags=re.DOTALL)

        # Any remaining bare placeholders (not inside a figure) -> markdown image
        body = re.sub(
            r'<span class="image placeholder" data-original-image-src="([^"]+)"[^>]*></span>',
            lambda m: f"![]({_norm_src(m.group(1))})",
            body,
        )

        # Rewrite \ref{name} to Markdown links
        def _ref_repl(m):
            label = m.group(1)
            if label in label_map:
                target_file, anchor = label_map[label]
                # Same file -> just anchor
                if target_file == _slug_filename(s["nav_idx"], s["title"]):
                    return f"[§](#{anchor})"
                return f"[§]({target_file}#{anchor})"
            return f"§"  # broken ref

        body = re.sub(r"\\ref\{([^}]+)\}", _ref_repl, body)

        # Drop stray \label{} (already captured above) so they don't show as text
        body = re.sub(r"\\label\{[^}]+\}", "", body)

        # Drop stray \cite{...} since we don't ship the bibliography
        body = re.sub(r"\\cite\{([^}]+)\}", r"[\1]", body)

        # Drop residual LaTeX environments pandoc may have passed through that aren't useful
        body = re.sub(r"\\begin\{(figure|table)\}\[H\]", "", body)
        body = re.sub(r"\\end\{(figure|table)\}", "", body)
        body = re.sub(r"\\centering", "", body)

        # Strip pandoc-emitted attribute lists on links: `{reference-type="ref" ...}`
        body = re.sub(r'\{reference-type="[^"]*"[^}]*\}', "", body)

        # Replace dead `[\[label\]](#label)` references (anchors that don't exist
        # anywhere) with a neutral placeholder so the broken bracket text doesn't
        # leak into the rendered prose.
        def _dead_ref_repl(m):
            anchor = m.group(2)
            if anchor in label_map:
                return m.group(0)
            if anchor.startswith("eq:"):
                return "(eq.)"
            if anchor.startswith("fig:"):
                return "(fig.)"
            if anchor.startswith("tab:"):
                return "(tab.)"
            return ""

        body = re.sub(
            r"\[\\\[([^\]]+)\\\]\]\(#([^)]+)\)",
            _dead_ref_repl,
            body,
        )

        s["body"] = body


def step_write_section_files(sections: list[dict]) -> None:
    print("[7/8] Writing section markdown files")
    DOCS_DIR.mkdir(parents=True, exist_ok=True)
    # Wipe existing per-section files (but not images/ or javascripts/)
    for f in DOCS_DIR.glob("*.md"):
        f.unlink()
    for s in sections:
        if s["nav_idx"] == 0:
            fname = "index.md"
        else:
            fname = _slug_filename(s["nav_idx"], s["title"])
        path = DOCS_DIR / fname
        # Front matter: tell mkdocs the page title
        # Heading: if not in body, add one
        body = s["body"].lstrip("\n")
        if not body.startswith(f"# {s['title']}") and s["title"] != "Introduction":
            body = f"# {s['title']}\n\n" + body
        with open(path, "w", encoding="utf-8") as f:
            f.write(body)
    print(f"  - wrote {len(sections)} files to {DOCS_DIR}")


def step_write_mkdocs_yml(sections: list[dict]) -> None:
    print("[8/8] Generating mkdocs.yml")
    nav_lines = []
    nav_lines.append("  - Home: index.md")

    # Group sections by their LyX `\part{...}`. Each Part becomes a YAML
    # nav group containing its sections; sections with no Part stay flat.
    current_part = None
    for s in sections:
        if s["nav_idx"] == 0:
            continue
        fname = _slug_filename(s["nav_idx"], s["title"])
        part = s.get("part")
        is_part_page = (s["level"] == 1)

        if is_part_page:
            # A Part heading that also has its own body — open a new group and
            # add the page itself as the group's index/overview entry.
            nav_lines.append(f"  - {_yaml_str(s['title'])}:")
            nav_lines.append(f"    - Overview: {fname}")
            current_part = s["title"]
            continue

        if part:
            # Section under a Part. Open the group lazily on first child.
            if part != current_part:
                nav_lines.append(f"  - {_yaml_str(part)}:")
                current_part = part
            nav_lines.append(f"    - {_yaml_str(s['title'])}: {fname}")
        else:
            nav_lines.append(f"  - {_yaml_str(s['title'])}: {fname}")
            current_part = None

    yml = f"""site_name: DWSIM User Guide
site_description: User guide for the DWSIM open-source chemical process simulator
docs_dir: docs
site_dir: site
use_directory_urls: true

theme:
  name: material
  font: false                # disable Google Fonts; use system Helvetica Neue
  features:
    - navigation.instant
    - navigation.tracking
    - navigation.top
    - navigation.indexes
    - navigation.expand
    - search.highlight
    - search.suggest
    - content.code.copy
    - toc.follow
  palette:
    - media: "(prefers-color-scheme: light)"
      scheme: default
      primary: indigo
      accent: indigo
      toggle:
        icon: material/brightness-7
        name: Switch to dark mode
    - media: "(prefers-color-scheme: dark)"
      scheme: slate
      primary: indigo
      accent: indigo
      toggle:
        icon: material/brightness-4
        name: Switch to light mode

plugins:
  - search

markdown_extensions:
  - admonition
  - attr_list
  - md_in_html
  - tables
  - toc:
      permalink: true
  - pymdownx.details
  - pymdownx.superfences
  - pymdownx.arithmatex:
      generic: true
      tex_inline_wrap: ['\\(', '\\)']
      tex_block_wrap: ['\\[', '\\]']

extra_css:
  - stylesheets/extra.css

extra_javascript:
  - javascripts/mathjax.js
  - https://polyfill.io/v3/polyfill.min.js?features=es6
  - https://cdn.jsdelivr.net/npm/mathjax@3/es5/tex-mml-chtml.js

nav:
{chr(10).join(nav_lines)}
"""
    with open(OUTPUT_DIR / "mkdocs.yml", "w", encoding="utf-8") as f:
        f.write(yml)


def _yaml_str(s: str) -> str:
    """Quote a YAML string if needed."""
    if re.search(r"[:\[\]\{\},&\*#\?\|<>=!%@`'\"]", s):
        return '"' + s.replace('"', '\\"') + '"'
    return s


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def step_prune_unreferenced_images() -> None:
    """Delete images under docs/images that no generated page references.

    step_copy_images copies whole screens*/imagens*/snaps/ trees, but the guide
    references only a subset; the rest is dead weight in the shipped help. This
    prunes them so the payload stays small (roughly halves the image size).
    """
    print("[opt] Pruning unreferenced images")
    img_root = DOCS_DIR / "images"
    if not img_root.exists():
        return

    ref_re = re.compile(r"images/([A-Za-z0-9_./%-]+\.(?:png|jpg|jpeg|gif|svg))", re.IGNORECASE)
    referenced: set[str] = set()
    for md in DOCS_DIR.glob("*.md"):
        text = md.read_text(encoding="utf-8", errors="ignore")
        for m in ref_re.finditer(text):
            referenced.add(m.group(1).replace("%20", " ").replace("\\", "/"))

    removed = 0
    freed = 0
    for f in img_root.rglob("*"):
        if not f.is_file():
            continue
        rel = f.relative_to(img_root).as_posix()
        if rel not in referenced:
            freed += f.stat().st_size
            f.unlink()
            removed += 1

    # Drop directories left empty after the prune.
    for d in sorted((p for p in img_root.rglob("*") if p.is_dir()), reverse=True):
        try:
            d.rmdir()
        except OSError:
            pass

    print(f"  - removed {removed} unreferenced images ({freed/1024/1024:.1f} MB freed)")


def step_shrink_images() -> None:
    """Run pngquant in-place over docs/images/screens*/. Skips if not installed.

    Re-running is idempotent: pngquant's --skip-if-larger leaves smaller files
    untouched. Cuts the help payload from ~57 MB down to ~10–15 MB.
    """
    print("[opt] Shrinking PNGs with pngquant")
    import shutil as _sh
    pq = _sh.which("pngquant")
    if not pq:
        print("  - pngquant not found on PATH; skipping (install: scoop install pngquant)")
        return
    img_root = DOCS_DIR / "images"
    pngs = list(img_root.rglob("*.png"))
    print(f"  - {len(pngs)} PNGs to process")
    # Process in batches to avoid Windows command-line length limits
    BATCH = 200
    for i in range(0, len(pngs), BATCH):
        batch = pngs[i : i + BATCH]
        cmd = [pq, "--quality=65-85", "--skip-if-larger", "--strip", "-f", "--ext=.png"] + [str(p) for p in batch]
        subprocess.run(cmd, capture_output=True)
    total = sum(p.stat().st_size for p in pngs)
    print(f"  - new total size: {total/1024/1024:.1f} MB")


def step_install_assistant_knowledge() -> None:
    """Copy dist/dwsim-help/assistant-knowledge/*.md into the dwsim-assistant
    repo's knowledge/user_guide/ folder, replacing whatever .md files were
    there. Idempotent — safe to re-run after each --portable build.
    """
    print("[opt] Installing knowledge into dwsim-assistant")
    src_dir = DIST_DIR / "assistant-knowledge"
    if not src_dir.exists():
        print(f"  ! source not found: {src_dir}")
        print(f"    run with --portable first to generate it")
        return

    if not ASSISTANT_KNOWLEDGE_DIR.parent.exists():
        print(f"  ! dwsim-assistant repo not found at expected path:")
        print(f"      {ASSISTANT_KNOWLEDGE_DIR.parent}")
        print(f"    edit ASSISTANT_KNOWLEDGE_DIR in build.py if your local")
        print(f"    clone lives elsewhere.")
        return

    ASSISTANT_KNOWLEDGE_DIR.mkdir(parents=True, exist_ok=True)

    # Wipe only .md files; preserve any other files the assistant may keep
    # there (e.g. README.md if added later, or hand-curated additions).
    removed = 0
    for f in ASSISTANT_KNOWLEDGE_DIR.glob("*.md"):
        f.unlink()
        removed += 1

    copied = 0
    for f in sorted(src_dir.glob("*.md")):
        shutil.copy2(f, ASSISTANT_KNOWLEDGE_DIR / f.name)
        copied += 1

    print(f"  - removed {removed} existing .md, copied {copied} new")
    print(f"  - target: {ASSISTANT_KNOWLEDGE_DIR}")
    print(f"  - restart dwsim-assistant for changes to take effect")


def step_export_assistant_knowledge() -> None:
    """Export cleaned-prose markdown for the dwsim-assistant RAG knowledge base.

    The dwsim-assistant (Python) already has BM25 search built into
    `server.py` — it scans `<exe_dir>/knowledge/**/*.md` at startup, splits on
    H1-H3 headings, and ranks via rank_bm25. So integration here is just
    "drop clean .md files into the right folder."

    What we need to clean from the help source:
      - MathJax `\\[...\\]` and `\\(...\\)`           (noise; not searchable)
      - Pandoc artifacts: `<a id>`, `{reference-type}`, `<!-- -->`
      - Raw HTML wrappers (`<div>`, `<figure>`, etc.)
      - Image syntax (no value for an LLM)
      - `*Caption*` italics emitted under figures

    What we need to PRESERVE:
      - Heading hierarchy (so the splitter picks up section boundaries)
      - Inline code, lists, tables, links to other pages

    Heading remap so the assistant's H1-H3 splitter creates one chunk per
    user-guide section instead of one chunk per page:
        Source H1 (page title) -> H1
        Source H4 (Section)    -> H2
        Source H5 (Subsection) -> H3
        Source H6              -> H4 (stays inside parent chunk)
    Output goes to dist/dwsim-help/assistant-knowledge/.
    """
    print("[opt] Exporting cleaned markdown for dwsim-assistant knowledge base")
    out_dir = DIST_DIR / "assistant-knowledge"
    if out_dir.exists():
        shutil.rmtree(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    n_files = 0
    n_chars = 0
    for md_file in sorted(DOCS_DIR.glob("*.md")):
        if md_file.name == "index.md":
            continue
        text = md_file.read_text(encoding="utf-8")
        cleaned = _clean_for_assistant_kb(text)
        if not cleaned.strip():
            continue
        out = out_dir / md_file.name
        out.write_text(cleaned, encoding="utf-8")
        n_files += 1
        n_chars += len(cleaned)
    print(f"  - exported {n_files} files, {n_chars/1024:.1f} kB total")
    print(f"  - destination on the build machine:")
    print(f"      {out_dir}")
    print(f"  - copy contents to <dwsim-assistant>/knowledge/user_guide/")


def _clean_for_assistant_kb(text: str) -> str:
    """Strip noise that hurts RAG; preserve heading structure + prose."""
    out = text

    # Drop pandoc-emitted HTML anchors and attribute lists
    out = re.sub(r'<a id="[^"]*"></a>', "", out)
    out = re.sub(r'\{reference-type="[^"]*"[^}]*\}', "", out)
    out = re.sub(r"\{#[^}]+\}", "", out)               # heading anchor attrs
    out = re.sub(r"<!--\s*-->", "", out)
    out = re.sub(r"<!--.*?-->", "", out, flags=re.DOTALL)

    # Drop image syntax (LLMs can't see the screenshots anyway)
    out = re.sub(r"!\[[^\]]*\]\([^)]+\)", "", out)
    # Drop the italic caption line we emit below each figure
    out = re.sub(r"^\*[^*]+?\*\s*$", "", out, flags=re.MULTILINE)

    # Drop display math entirely; keep inline math text-only (strip delimiters)
    out = re.sub(r"\\\[[\s\S]*?\\\]", "", out)
    out = re.sub(r"\\\(([^)]*?)\\\)", r"\1", out)

    # Drop residual raw HTML wrappers (div, figure, span class), keep contents
    out = re.sub(r"</?(?:div|figure|figcaption|span)[^>]*>", "", out)

    # Demote source H4/H5/H6 by 2 levels so the assistant's H1-H3 splitter
    # treats each user-guide section as its own chunk. Process from H6 down
    # to H4 to avoid cascade rewriting.
    out = re.sub(r"^###### ", "#### ",  out, flags=re.MULTILINE)
    out = re.sub(r"^##### ",  "### ",   out, flags=re.MULTILINE)
    out = re.sub(r"^#### ",   "## ",    out, flags=re.MULTILINE)

    # Collapse 3+ blank lines to 2
    out = re.sub(r"\n{3,}", "\n\n", out)
    return out.strip() + "\n"


def _chunk_markdown_for_embedding() -> list[dict]:
    """Walk docs/*.md, return list of chunks suitable for embedding."""
    chunks: list[dict] = []
    for md_file in sorted(DOCS_DIR.glob("*.md")):
        if md_file.name == "index.md":
            continue
        text = md_file.read_text(encoding="utf-8")
        page_url = md_file.stem + ".html"

        # Page title = first H1 line
        m = re.search(r"^# +(.+?)$", text, re.MULTILINE)
        page_title = m.group(1).strip() if m else md_file.stem

        # Split by H4 (####) and H5 (#####) — these are the semantic sub-units
        # for sections. Falls back to splitting the whole page as one chunk.
        parts = re.split(r"(?m)^(#{2,6})\s+(.+?)(?:\s*\{[^}]*\})?\s*$", text)
        # Pattern: text_before, [hashes, heading, body]*
        if len(parts) <= 1:
            cleaned = _clean_for_embedding(text)
            if len(cleaned) >= 50:
                chunks.append({
                    "id": page_url,
                    "page_url": page_url,
                    "page_title": page_title,
                    "heading": page_title,
                    "anchor": "",
                    "text": cleaned[:2000],
                })
            continue

        # Walk (hashes, heading, body) tuples
        cur_heading = page_title
        cur_anchor = ""
        cur_body = parts[0]
        flush = lambda: chunks.append({
            "id": f"{page_url}#{cur_anchor}" if cur_anchor else page_url,
            "page_url": page_url,
            "page_title": page_title,
            "heading": cur_heading,
            "anchor": cur_anchor,
            "text": _clean_for_embedding(cur_body)[:2000],
        }) if len(_clean_for_embedding(cur_body)) >= 50 else None
        flush()
        for i in range(1, len(parts), 3):
            cur_heading = parts[i + 1].strip()
            cur_anchor = re.sub(r"[^a-z0-9]+", "-", cur_heading.lower()).strip("-")
            cur_body = parts[i + 2] if i + 2 < len(parts) else ""
            flush()
    return chunks


def _clean_for_embedding(text: str) -> str:
    """Strip markdown/HTML/math noise; return plain prose."""
    # Remove fenced code blocks
    text = re.sub(r"```[\s\S]*?```", " ", text)
    # Remove display math \[ ... \]
    text = re.sub(r"\\\[[\s\S]*?\\\]", " ", text)
    # Remove inline math $...$, \(...\)
    text = re.sub(r"\\\([^)]*?\\\)", " ", text)
    text = re.sub(r"\$[^$\n]+\$", " ", text)
    # Remove HTML tags
    text = re.sub(r"<[^>]+>", " ", text)
    # Remove markdown links/images but keep link text
    text = re.sub(r"!\[[^\]]*\]\([^)]+\)", " ", text)
    text = re.sub(r"\[([^\]]+)\]\([^)]+\)", r"\1", text)
    # Strip remaining markdown emphasis
    text = re.sub(r"[*_`#>~|]", " ", text)
    # Collapse whitespace
    text = re.sub(r"\s+", " ", text).strip()
    return text


def step_bundle_fonts() -> None:
    """Download Inter (SIL OFL, free) into dist/assets/vendor/fonts/ and emit
    a CSS @font-face block. Helvetica Neue itself is proprietary and can't be
    redistributed — Inter is the standard free substitute used as a fallback
    when a system doesn't have Helvetica Neue installed.

    Files come from the Google Fonts API in WOFF2 (best compression, supported
    in WebView2/Edge, modern browsers). Idempotent — skips if already cached.
    """
    print("[opt] Bundling Inter font (Helvetica Neue substitute)")
    import urllib.request

    fonts_dir = DIST_DIR / "assets" / "vendor" / "fonts"
    fonts_dir.mkdir(parents=True, exist_ok=True)

    # Inter weights we use: Regular (400), Medium (500), SemiBold (600), Bold (700).
    # WOFF2 files served by GitHub release of rsms/inter (v4.0).
    base = "https://github.com/rsms/inter/raw/v4.0/docs/font-files/"
    files = {
        "Inter-Regular.woff2":  ("Inter", 400, "normal"),
        "Inter-Italic.woff2":   ("Inter", 400, "italic"),
        "Inter-Medium.woff2":   ("Inter", 500, "normal"),
        "Inter-SemiBold.woff2": ("Inter", 600, "normal"),
        "Inter-Bold.woff2":     ("Inter", 700, "normal"),
    }

    for fname in files:
        out = fonts_dir / fname
        if out.exists() and out.stat().st_size > 0:
            print(f"  - cached: {fname}")
            continue
        try:
            req = urllib.request.Request(base + fname, headers={"User-Agent": "Mozilla/5.0"})
            with urllib.request.urlopen(req, timeout=20) as resp:
                out.write_bytes(resp.read())
            print(f"  - downloaded: {fname} ({out.stat().st_size/1024:.1f} kB)")
        except Exception as e:
            print(f"  ! failed to fetch {fname}: {e}")
            return

    # Emit a CSS file with @font-face declarations and override the Material
    # CSS variables to put Inter at the top of the fallback chain.
    face_blocks = []
    for fname, (family, weight, style) in files.items():
        face_blocks.append(
            "@font-face {\n"
            f"  font-family: '{family}';\n"
            f"  font-style: {style};\n"
            f"  font-weight: {weight};\n"
            "  font-display: swap;\n"
            f"  src: url('vendor/fonts/{fname}') format('woff2');\n"
            "}"
        )
    fonts_css = "\n\n".join(face_blocks) + "\n\n" + (
        ":root {\n"
        "  /* Helvetica Neue stays first for users that have it; Inter is the\n"
        "     bundled fallback that ships with DWSIM. */\n"
        "  --md-text-font: 'Helvetica Neue', 'Inter', 'Helvetica', 'Arial', sans-serif !important;\n"
        "}\n"
    )
    css_path = DIST_DIR / "assets" / "vendor" / "fonts.css"
    css_path.write_text(fonts_css, encoding="utf-8")
    print(f"  - wrote font CSS: {css_path}")

    # Inject the local font CSS into every HTML page (just before </head>) so
    # it loads before the page is painted. Idempotent: skip if already present.
    n = 0
    for html in DIST_DIR.glob("*.html"):
        text = html.read_text(encoding="utf-8")
        if "vendor/fonts.css" in text:
            continue
        link = '<link rel="stylesheet" href="assets/vendor/fonts.css">'
        text = text.replace("</head>", link + "</head>", 1)
        html.write_text(text, encoding="utf-8")
        n += 1
    print(f"  - injected font CSS into {n} pages")


def step_localize_cdn_assets() -> None:
    """Download CDN-hosted scripts (iframe-worker, MathJax) into dist/ and
    rewrite every HTML page to reference the local copies. Required so the
    shipped bundle works on a machine with no internet access.
    """
    print("[opt] Localizing CDN assets")
    import urllib.request

    asset_dir = DIST_DIR / "assets" / "vendor"
    asset_dir.mkdir(parents=True, exist_ok=True)

    targets = [
        # (CDN url, local relative path under dist/, glob match in src= for replacement)
        (
            "https://unpkg.com/iframe-worker/shim",
            "assets/vendor/iframe-worker.shim.js",
            r"https://unpkg\.com/iframe-worker/shim",
        ),
        # MathJax: use the "SVG full" bundle. SVG output inlines glyph paths
        # so no separate font WOFF files need to be fetched — perfect for
        # file:// shipping. The "full" variant bundles all TeX extensions
        # (including mhchem) so no dynamic extension XHR either.
        (
            "https://cdn.jsdelivr.net/npm/mathjax@3/es5/tex-svg-full.js",
            "assets/vendor/mathjax/tex-svg-full.js",
            r"https://cdn\.jsdelivr\.net/npm/mathjax@3/es5/tex-mml-chtml\.js",
        ),
    ]

    for url, rel_path, _pattern in targets:
        out = DIST_DIR / rel_path
        out.parent.mkdir(parents=True, exist_ok=True)
        if out.exists() and out.stat().st_size > 0:
            print(f"  - cached: {rel_path}")
            continue
        try:
            req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
            with urllib.request.urlopen(req, timeout=15) as resp:
                out.write_bytes(resp.read())
            print(f"  - downloaded: {rel_path} ({out.stat().st_size/1024:.1f} kB)")
        except Exception as e:
            print(f"  ! failed to fetch {url}: {e}")
            print("    (run again with internet to populate local cache)")
            return

    # MathJax loads its own components dynamically. Pin output to CommonHTML
    # using mathjax.js config; CHTML font assets must also live locally.
    # The single tex-mml-chtml.js bundle includes everything needed for our
    # use (no external font fetches at runtime when configured below).

    # Drop polyfill.io (modern WebView2/Edge needs no ES6 polyfill).
    polyfill_pat = re.compile(r'\s*<script src="https://polyfill\.io/[^"]+"></script>')

    # Patch every HTML page
    n = 0
    for html in DIST_DIR.glob("*.html"):
        text = html.read_text(encoding="utf-8")
        orig = text
        for _url, rel_path, pattern in targets:
            text = re.sub(pattern, rel_path, text)
        text = polyfill_pat.sub("", text)
        if text != orig:
            html.write_text(text, encoding="utf-8")
            n += 1
    print(f"  - patched {n} HTML pages")


def step_inject_offline_search() -> None:
    """[Deprecated — replaced by mkdocs-material's built-in `offline` plugin,
    which is enabled in mkdocs.portable.yml. Retained as a fallback.]

    Make MkDocs Material's search work under file:// by replacing its
    fetch-based loader with a script-tag-loaded JS index plus a small shim
    that drives the same `.md-search-result` UI.
    """
    print("[opt] Wiring offline search")
    import json
    idx_json = DIST_DIR / "search" / "search_index.json"
    if not idx_json.exists():
        print("  ! search index not found; skipping")
        return

    raw = json.loads(idx_json.read_text(encoding="utf-8"))
    docs = raw.get("docs", [])
    # Trim each doc's text to keep payload reasonable (~1 kB per doc)
    slim = []
    for d in docs:
        text = re.sub(r"\s+", " ", d.get("text", ""))
        slim.append({
            "location": d.get("location", ""),
            "title": d.get("title", ""),
            "text": text[:1500],
        })
    out_js = DIST_DIR / "search" / "search_index.js"
    out_js.write_text(
        "window.__DWSIM_SEARCH = " + json.dumps(slim, ensure_ascii=False) + ";",
        encoding="utf-8",
    )
    print(f"  - wrote search index: {len(slim)} docs, {out_js.stat().st_size/1024:.1f} kB")

    shim_js = r"""// offline-search.js — file://-friendly substring search backed by
// window.__DWSIM_SEARCH. Hijacks Material's search input and renders into
// the existing .md-search-result__list element.
(function () {
  function ready(fn) {
    if (document.readyState !== 'loading') fn();
    else document.addEventListener('DOMContentLoaded', fn);
  }
  ready(function () {
    var input = document.querySelector('.md-search__input');
    var resultList = document.querySelector('.md-search-result__list');
    var resultMeta = document.querySelector('.md-search-result__meta');
    if (!input || !resultList || !window.__DWSIM_SEARCH) return;

    // Compute the path prefix to convert relative `location` (from search index,
    // e.g. "05-welcome-screen.html") to a path that resolves from THIS page.
    // Under use_directory_urls=false all pages live at the dist root.
    var pageDir = location.pathname.replace(/[^/]*$/, '');

    function score(doc, terms) {
      var hay = (doc.title + '\n' + doc.text).toLowerCase();
      var s = 0;
      for (var i = 0; i < terms.length; i++) {
        var t = terms[i];
        if (!t) continue;
        var idx = hay.indexOf(t);
        if (idx < 0) return 0;
        s += 100 - Math.min(idx, 100);
        if (doc.title.toLowerCase().indexOf(t) >= 0) s += 200;
      }
      return s;
    }

    function snippet(text, terms) {
      var lower = text.toLowerCase();
      var pos = -1;
      for (var i = 0; i < terms.length; i++) {
        var p = lower.indexOf(terms[i]);
        if (p >= 0) { pos = p; break; }
      }
      if (pos < 0) return text.slice(0, 200);
      var start = Math.max(0, pos - 50);
      var end = Math.min(text.length, pos + 200);
      var snip = (start > 0 ? '…' : '') + text.slice(start, end) + (end < text.length ? '…' : '');
      var re = new RegExp('(' + terms.map(function (t) { return t.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'); }).join('|') + ')', 'gi');
      return snip.replace(re, '<mark>$1</mark>');
    }

    function render(query) {
      resultList.innerHTML = '';
      var q = query.trim().toLowerCase();
      if (q.length < 2) {
        if (resultMeta) resultMeta.textContent = 'Type to search';
        return;
      }
      var terms = q.split(/\s+/);
      var hits = [];
      for (var i = 0; i < window.__DWSIM_SEARCH.length; i++) {
        var doc = window.__DWSIM_SEARCH[i];
        var s = score(doc, terms);
        if (s > 0) hits.push({ doc: doc, score: s });
      }
      hits.sort(function (a, b) { return b.score - a.score; });
      hits = hits.slice(0, 30);

      if (resultMeta)
        resultMeta.textContent = hits.length === 0
          ? 'No matching results'
          : hits.length + ' result' + (hits.length === 1 ? '' : 's') + ' for "' + query + '"';

      hits.forEach(function (h) {
        var li = document.createElement('li');
        li.className = 'md-search-result__item';
        var a = document.createElement('a');
        a.className = 'md-search-result__link';
        a.href = pageDir + h.doc.location;
        a.innerHTML =
          '<article class="md-search-result__article md-typeset">' +
          '<h1 class="md-search-result__title">' + escapeHtml(h.doc.title || '(untitled)') + '</h1>' +
          '<p class="md-search-result__teaser">' + snippet(h.doc.text, terms) + '</p>' +
          '</article>';
        li.appendChild(a);
        resultList.appendChild(li);
      });
    }

    function escapeHtml(s) {
      return s.replace(/[&<>"']/g, function (c) {
        return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c];
      });
    }

    var debounceTimer;
    input.addEventListener('input', function () {
      clearTimeout(debounceTimer);
      var q = input.value;
      debounceTimer = setTimeout(function () { render(q); }, 80);
    });

    // Run once in case the input has a prefilled value
    if (input.value) render(input.value);
  });
})();
"""
    shim_path = DIST_DIR / "javascripts" / "offline-search.js"
    shim_path.parent.mkdir(parents=True, exist_ok=True)
    shim_path.write_text(shim_js, encoding="utf-8")

    inject_tags = (
        '<script src="search/search_index.js"></script>'
        '<script src="javascripts/offline-search.js"></script>'
    )
    n_pages = 0
    for html in DIST_DIR.glob("*.html"):
        text = html.read_text(encoding="utf-8")
        if "offline-search.js" in text:
            continue
        if "</body>" in text:
            text = text.replace("</body>", inject_tags + "</body>", 1)
            html.write_text(text, encoding="utf-8")
            n_pages += 1
    print(f"  - injected search shim into {n_pages} pages")


def step_build_portable() -> None:
    """Build a `file://`-friendly static help bundle into dist/dwsim-help/.

    Uses a generated mkdocs override config that sets `use_directory_urls: false`
    so links resolve correctly when opened directly from disk and from a
    WebView2 virtual host. Output is self-contained and ready to bundle with
    DWSIM installer.
    """
    print("[opt] Building portable bundle for shipping")
    portable_yml = OUTPUT_DIR / "mkdocs.portable.yml"
    base_yml = (OUTPUT_DIR / "mkdocs.yml").read_text(encoding="utf-8")
    # Force flat HTML filenames and pin site_dir to dist/.
    portable = re.sub(r"^use_directory_urls: .*$", "use_directory_urls: false", base_yml, flags=re.MULTILINE)
    # Relative to the config file (OUTPUT_DIR), so the generated file is portable across machines/CI.
    portable = re.sub(r"^site_dir: .*$", "site_dir: dist/dwsim-help", portable, flags=re.MULTILINE)
    # Strip features that require a real HTTP origin (XHR/fetch is blocked
    # under file://, which leaves the layout half-rendered).
    portable = re.sub(r"^\s*-\s*navigation\.instant\s*$\n?", "", portable, flags=re.MULTILINE)
    portable = re.sub(r"^\s*-\s*navigation\.tracking\s*$\n?", "", portable, flags=re.MULTILINE)
    # Add Material's built-in `offline` plugin — it inlines the search index
    # into a <script> tag so search works under file:// without any custom shim.
    # Must come AFTER `search:` in the plugins list (which we already have).
    if "- offline" not in portable:
        portable = re.sub(r"^(plugins:\s*\n(?:\s*-\s*\S+\s*\n)+)", r"\1  - offline\n", portable, count=1, flags=re.MULTILINE)
    # Inject portable.css overrides (footer hide, sidebar scrolling, etc.)
    if "extra_css:" not in portable:
        portable += "\nextra_css:\n  - stylesheets/portable.css\n"
    elif "stylesheets/portable.css" not in portable:
        portable = re.sub(r"^extra_css:\s*\n", "extra_css:\n  - stylesheets/portable.css\n", portable, count=1, flags=re.MULTILINE)
    portable_yml.write_text(portable, encoding="utf-8")

    if DIST_DIR.exists():
        shutil.rmtree(DIST_DIR)
    DIST_DIR.parent.mkdir(parents=True, exist_ok=True)

    res = subprocess.run(
        ["mkdocs", "build", "-f", str(portable_yml), "--clean"],
        capture_output=True, text=True, cwd=OUTPUT_DIR,
    )
    if res.returncode != 0:
        print("mkdocs stderr:")
        print(res.stderr[-2000:])
        raise SystemExit(f"mkdocs build failed (exit {res.returncode})")

    total = sum(p.stat().st_size for p in DIST_DIR.rglob("*") if p.is_file())
    print(f"  - bundle written to: {DIST_DIR}")
    print(f"  - bundle size:       {total/1024/1024:.1f} MB")
    print(f"  - entry point:       {DIST_DIR / 'index.html'}")


def main():
    import argparse
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description="Build the DWSIM help system from LyX source.")
    ap.add_argument("--shrink-images", action="store_true",
                    help="Run pngquant over docs/images/ to reduce ship size (~3–5x smaller)")
    ap.add_argument("--portable", action="store_true",
                    help="After building, emit a flat-URL bundle in dist/dwsim-help/ for shipping with DWSIM")
    ap.add_argument("--ship", action="store_true",
                    help="Shortcut for --shrink-images --portable --install-assistant-knowledge")
    ap.add_argument("--install-assistant-knowledge", action="store_true",
                    dest="install_assistant_knowledge",
                    help="Copy assistant-knowledge/*.md into dwsim-assistant/knowledge/user_guide/, "
                         "replacing any existing .md files there.")
    ap.add_argument("--skip-convert", action="store_true",
                    help="Skip the LyX->Markdown conversion; only run shrink/portable on existing docs/")
    args = ap.parse_args()

    if args.ship:
        args.shrink_images = True
        args.portable = True
        args.install_assistant_knowledge = True

    if not args.skip_convert:
        if not LYX_FILE.exists():
            raise SystemExit(f"Source LyX not found: {LYX_FILE}")
        if not Path(PANDOC).exists():
            raise SystemExit(f"Pandoc not found at {PANDOC}")

        BUILD_DIR.mkdir(parents=True, exist_ok=True)
        DOCS_DIR.mkdir(parents=True, exist_ok=True)

        latex = step_lyx_to_latex()
        latex = step_flatten_includes(latex)
        latex = step_expand_macros(latex)

        md = step_pandoc_to_md(latex)
        sections, label_map = step_split_sections(md)
        step_copy_images()
        step_rewrite_paths_and_refs(sections, label_map)
        step_write_section_files(sections)
        step_prune_unreferenced_images()
        step_write_mkdocs_yml(sections)

    if args.shrink_images:
        step_shrink_images()
    if args.portable:
        step_build_portable()
        step_localize_cdn_assets()
        step_bundle_fonts()
        step_export_assistant_knowledge()
    if args.install_assistant_knowledge:
        step_install_assistant_knowledge()

    print()
    print("Done. Next:")
    print("  mkdocs serve              # local preview (pretty URLs)")
    if args.portable:
        print(f"  start {DIST_DIR / 'index.html'}   # open shippable bundle")


if __name__ == "__main__":
    main()
