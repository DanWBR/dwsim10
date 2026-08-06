# IntelliSense for DWSIM.Automation.FluentAPI in Python

C# XML doc comments live in `DWSIM.Automation.FluentAPI.xml` (built next to the
DLL when you compile in Debug or Release). pythonnet doesn't read them by
default, but two helpers in this folder bridge the gap:

| | What it does | Where it shows up |
|---|---|---|
| `dwsim_fluent_help.py` | Reads the XML at runtime; offers `doc()`, `signature()`, `patch_docstrings()`. | `help()`, Jupyter `?`, IPython `obj?` |
| `generate_pyi_stubs.py` | Emits `.pyi` stub files from the assembly + XML. | VS Code (Pylance) hover + completion, PyCharm |

You can use one, the other, or both.

---

## 1. Runtime help (works in any REPL / Jupyter)

```python
import sys, clr
sys.path.append(r"C:\path\to\DWSIM\bin\x64\Debug")
clr.AddReference("DWSIM.Automation.FluentAPI")
from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages

# Drop dwsim_fluent_help.py somewhere on sys.path:
from dwsim_fluent_help import patch_docstrings, doc
patch_docstrings("DWSIM.Automation.FluentAPI")

help(Flowsheet)            # → C# class summary
doc(Flowsheet.AddHeater)   # → "Adds a Heater unit operation tagged tag and ..."
```

`patch_docstrings` walks every public type and sets `__doc__` on classes and
properties, so Jupyter's `Shift+Tab` overlay and `help()` start showing the C#
summaries immediately.

The CLI form is handy too:

```bash
python dwsim_fluent_help.py reactor   # grep documented members for "reactor"
```

---

## 2. `.pyi` stubs for VS Code / PyCharm

Stubs let the IDE display IntelliSense **before** Python runs (no need to
import the assembly first). Generate them once after each FluentAPI build:

```bash
pip install pythonnet
python generate_pyi_stubs.py \
    --asm DWSIM.Automation.FluentAPI \
    --bin "C:\path\to\DWSIM\bin\x64\Debug" \
    --out  ".\stubs"
```

You'll get a tree like:

```
stubs/
└── DWSIM-stubs/
    ├── py.typed
    ├── Automation/
    │   └── FluentAPI/
    │       ├── __init__.pyi              # Flowsheet, License, Quantity, Q, ...
    │       ├── Builders/
    │       │   ├── __init__.pyi          # MaterialStreamBuilder, HeaterBuilder, ...
    │       │   ├── Bioprocess/__init__.pyi
    │       │   ├── Refining/__init__.pyi
    │       │   └── ...
    │       └── ...
```

### VS Code (Pylance)

Add to `.vscode/settings.json`:

```jsonc
{
  "python.analysis.stubPath": "${workspaceFolder}/stubs",
  "python.analysis.typeCheckingMode": "basic"
}
```

Hover and completion now show the C# `<summary>` text:

```
Flowsheet.AddHeater(tag: str) -> HeaterBuilder
Adds a Heater unit operation tagged tag and returns its fluent builder.
```

### PyCharm

Right-click the `stubs/` folder → **Mark Directory as → Sources Root**.
PyCharm picks up `DWSIM-stubs` automatically.

### Notes / limitations

* Stubs are built by reflecting the loaded assembly, so transitively-loaded
  Plus assemblies (LCA, TEA, refining UOs, …) are included only if their DLLs
  are findable from the `--bin` folder. The generator calls
  `Flowsheet.RegisterAssemblyResolver()` for you.
* Generic methods come through with their CLR-style `` `1 `` markers stripped
  but parameter types fall back to `Any` for any non-built-in type. Tighten
  the `_PY_TYPE` map at the top of `generate_pyi_stubs.py` if you want
  better hints.
* The `.pyi` files are *type stubs*, not runtime modules — they don't replace
  `clr.AddReference("DWSIM.Automation.FluentAPI")` in your scripts; they only
  feed the IDE.

---

## Combine both

For the best DX, drop the runtime helper into your project AND ship the
stubs. The stubs power IDE IntelliSense; the runtime helper makes `help()`
and Jupyter's `?` work too.
