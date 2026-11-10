# dwsim-dexpi

Converts a [DEXPI](https://dexpi.org/) model into the reading that DWSIM's
*Import Flowsheet from Drawing* tool opens, so a flowsheet can be built from a DEXPI file without
the AI Assistant.

It reads DEXPI 2.0 and 2.0.1 (XML and JSON) and Proteus XML (DEXPI 1.3) through
[pyDEXPI](https://github.com/process-intelligence-research/pyDEXPI).

| DEXPI model | Reading | What is taken |
| --- | --- | --- |
| Process model | PFD | process steps as equipment, streams with temperature, pressure, mass flow and mass composition, the diagram's layout |
| Plant model (P&ID) | P&ID | equipment, control and safety valves, the piping between them with line numbers, the diagram's layout |

The diagram is rendered as the picture the import shows the items on (pyDEXPI's PNG export, the
`resvg` package; without it the reading has no picture).

Block valves, check valves, reducers and other in-line pieces are passed through, dead-end
branches (drains, instrument taps) are left out and tees become mixers or splitters.

## Use

```bash
pip install -r requirements.txt
python dwsim_dexpi.py model.xml -o model.reading.json
```

Python 3.12 or newer. DWSIM runs the program by itself when a `.xml` file is chosen in the
import: it looks for `dwsim-dexpi` (a frozen executable) or `dwsim_dexpi.py` in the folder named
by the `DWSIM_DEXPI_HOME` environment variable, in `extenders/dexpi` and `tools/dwsim-dexpi`
under DWSIM's folder, and on the `PATH`. The script runs with the first Python 3.12 or newer among: the
one named by `DWSIM_DEXPI_PYTHON`, the ones installed for the user or the computer, the one on the
`PATH` and the one of DWSIM's settings.

## License

The program is part of DWSIM (GPL v3 or later) and is a separate program: DWSIM only runs it and
reads the file it writes. pyDEXPI is licensed under the GNU Affero General Public License v3, so
a copy of this program distributed together with pyDEXPI (a frozen executable, for instance) is
conveyed under the AGPL v3 as a whole.
