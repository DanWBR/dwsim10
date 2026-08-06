# DWSIM.Automation.FluentAPI — Python (pythonnet)

`DWSIM.Automation.FluentAPI.dll` is a regular .NET assembly, so it can be driven from Python via [pythonnet](https://pythonnet.github.io/).

## Setup

```bash
pip install pythonnet
```

In your script, point pythonnet at the DWSIM Debug/Release output folder (where `DWSIM.Automation.FluentAPI.dll` was built — by default `DWSIM/bin/x64/Debug/`):

```python
import sys, clr

DWSIM_BIN = r"C:\Users\danie\source\repos\DanWBR\DWSIM_Private\DWSIM\bin\x64\Debug"
sys.path.append(DWSIM_BIN)
clr.AddReference("DWSIM.Automation.FluentAPI")

from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, License
from DWSIM.Automation.FluentAPI import Q  # extension methods .Kelvin() etc. as static helpers
```

Because pythonnet does not surface C# extension methods as instance methods, call them as static helpers:

```python
T = Q.Kelvin(300.0)
P = Q.Bar(10.0)
m = Q.KgPerSecond(100.0)
```

## Patron-key activation (unlocks DWSIMPlus components)

```python
ok = License.Activate("you@example.com", "YOUR-PATRON-KEY")
print("activated:", ok, "as", License.Email)
```

`Activate` loads `DWSIM.Support.dll` from the same folder via reflection and calls
`DWSIM.Support.Vodka.Cat()` to validate online. It throws `FileNotFoundException`
if the Support DLL is missing (open-source build), and returns `False` if the
key is invalid. Once activated, `License.IsActivated` stays `True` for the
process and Plus-only Fluent surface (added later) is unlocked.

## Hello-flowsheet

See `examples/01_mixer.py`, `examples/02_conv_reactor.py`, `examples/03_distillation.py`.
