# DWSIM 3D

DWSIM 3D is the 3D view of a flowsheet: a Godot 4.7 program (GDScript) that DWSIM starts from
**CAD Tools > 3D View (DWSIM 3D)**. DWSIM builds the scene from the plant model of the SolidCraft
extension (equipment sized from the simulation, routed lines, pipe rack) and sends it over a local
connection; the window draws it and sends the user's edits back. The DWSIM side lives in
`extensions/DWSIM.Extensions.SolidCraft/Viewer3D` (protocol in `SceneProtocol.cs`).

## Running it while working on it

Point DWSIM at this folder and at a Godot 4.7 editor, and the menu command runs the project in the
editor instead of an exported build:

```
DWSIM_VIEWER3D=<this folder>
DWSIM_GODOT=<path to Godot_v4.7.2-stable_win64.exe>
```

`DWSIM_VIEWER3D` may also point at an exported executable. Without DWSIM, the window draws a saved
scene, and with a capture folder it saves pictures of the views and quits:

```
godot --path . -- --dwsim-scene=scene.json --capture-dir=out --views=IsoSE,Plan --size=1400x900
```

## Exporting

`export.sh <godot> <out> [rid ...]` exports the window into `<out>/<rid>/viewer3d`, the folder
DWSIM looks in next to its own executable (macOS: `DWSIM.app/Contents/Helpers`). It needs the
export templates of the same Godot version. The release workflows run it once and copy the result
into every package. The builds are about 105 MB on Windows x64, 71 MB on Linux x64 and 82 MB per
architecture on macOS.

## Controls

Left click selects (Ctrl adds), a double click frames the item, right drag turns the view, middle
drag (or Shift + right drag) moves the view, and the wheel zooms. Keys 1 to 4 are the isometric views,
5 is the plan, 6 and 7 the front and right side, Q and E turn the view by 90 degrees, F fits the
plant, L, I, R and G show or hide the labels, insulation, pipe rack and grid, and F1 the card of
shortcuts.

## Editing the layout

Dragging an item moves it over the ground on a 0.5 m grid (Alt drags freely), the arrow keys nudge
the selection 0.5 m (Shift: 0.1 m) along the plot plan axis closest to the screen direction,
Page Up and Page Down raise and lower it, T and Shift+T turn it by a quarter turn, and the Place
panel takes exact coordinates and lines several items up. Each change goes to DWSIM as one `place`
command and lands in the flowsheet's layout, the one the SolidCraft model reads. Ctrl+Z and Ctrl+Y
undo and redo in a history of the 3D layout that DWSIM keeps apart from the flowsheet's own.

`-- --selftest-item=<object name> --selftest-out=<folder>` runs a self-test of the editing once
DWSIM has sent the scene: it drags the item 6 m along X, turns it, raises it twice and undoes the
last step, with input events fed to the window itself, and saves a picture after each step.

## Licences

The window is part of DWSIM (GPL 3). The Godot Engine is MIT licensed; `licenses/` holds its
licence and the notices of the third-party code it includes, and every export carries them.
Only CC0 assets go into this project.
