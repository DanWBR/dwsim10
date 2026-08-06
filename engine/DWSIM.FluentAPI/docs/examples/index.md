# Examples

Each example below shows the same flowsheet in **Python**, **C#** and
**VB.NET**. The Python versions ship as runnable scripts under
`DWSIM.FluentAPI/python/examples/` (01–08); the C# and VB.NET versions in
each page are direct ports of the same call sequence.

| # | Case | Category | Patron key |
|---|---|---|---|
| [01](01-mixer.md) | Mixer (2 streams of water) | Core | – |
| [02](02-conversion-reactor.md) | Steam-reforming conversion reactor | Reactors | – |
| [03](03-distillation.md) | Shortcut distillation (ethanol/water) | Columns | – |
| [04](04-bioprocess-train.md) | Bioprocess pretreatment train | Bioprocess | – |
| [05](05-anaerobic-digester.md) | Anaerobic digester | Bioprocess | – |
| [06](06-refining-train.md) | Refining train (CDU + FCC + reformer) | Refining | ✓ |
| [07](07-lca-tea.md) | Full simulation + LCA + TEA | Plus | ✓ |
| [08](08-wrap-existing-flowsheet.md) | Script an open DWSIM session | Integration | – |
| [09](09-heat-exchanger.md) | Heat exchanger (LMTD/UA) | Core | – |
| [10](10-pump-compressor-train.md) | Pump + compressor train | Core | – |
| [11](11-rigorous-distillation.md) | Rigorous distillation column | Columns | – |
| [12](12-equilibrium-reactor.md) | Equilibrium / Gibbs reactor | Reactors | – |
| [13](13-electrolyte-reverse-osmosis.md) | RO + ion exchange | Electrolyte | ✓ |
| [14](14-water-electrolyzer.md) | Water electrolyzer (H₂ production) | Clean energy | – |
| [15](15-pem-fuel-cell.md) | PEM fuel cell | Clean energy | – |
| [16](16-recycle-loop.md) | Flowsheet with recycle loop | Solver | – |
| [17](17-dynamic-simulation.md) | Dynamic simulation with PID level control | Dynamics | – |

**Conventions used in every example:**

- Python preamble (`sys.path.append(DWSIM_BIN)`, `clr.AddReference(...)`)
  is omitted from the snippet itself but assumed — see
  [Installation](../getting-started/installation.md).
- C# snippets use top-level statements style; the actual program needs the
  usual `class Program { static void Main() { ... } }` boilerplate.
- VB.NET snippets target Module Program / Sub Main equivalent.
