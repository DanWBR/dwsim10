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

With the mouse over an item or a line, the window tints it, draws a box at its limits (seen
through the other items too) and shows its tag and size (width x depth x height). Left click selects (Ctrl adds), a double click opens the item's editor, Shift+F frames it, right drag turns the view, middle
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
undo and redo the changes made in the window, in a history that DWSIM keeps apart from the 2D one.

## Building the flowsheet

The palette on the left (P shows or hides it) lists the built-in equipment that has a body in the
plant model; a click on one and then on the ground adds it there (Shift keeps the tool for more),
and the 2D drawing gets the new icon next to the others. The Connect tool (C) shows the nozzles of
every item, green for outlets, blue for inlets and grey for the ones in use: a click on a nozzle and
then on a nozzle of another item joins them with a new material stream, and a click on the ground
instead makes a feed (on an inlet) or a product (on an outlet). Delete removes the selection, and
the Tag field of the Place panel renames an item. Ctrl+Z takes back these changes too, one by one.
The palette also lists the logical blocks (adjust, specification, recycle, energy recycle, PID
controller) and, under Extensions, the unit operations of the extensions DWSIM loaded, which the
plant model draws as a block of their size. Energy nozzles are orange: joining an energy outlet (an
energy connector, or an outlet of the energy kind) to an energy inlet makes an energy stream, and
DWSIM's own rules decide which units give or take energy.

In the plant, a logical block is a spinning sphere with two rings, floating 3 m up on a thin pole;
a controller is a giant old computer on the ground, with a green screen, blinking lamps and turning
tape reels; an energy stream is a spinning golden bolt with dashed links to the nozzles it joins.
A turns the animation on and off.

A double click (or Enter) on an item asks DWSIM to open its editor in a floating window that stays
above the 3D view: the cross-platform interface builds the same editor it docks, in a window of
its own, and the Windows interface floats the object's editor form.

`-- --selftest-item=<object name> --selftest-out=<folder>` runs a self-test of the editing once
DWSIM has sent the scene: it drags the item 6 m along X, turns it, raises it twice and undoes the
last step, with input events fed to the window itself, and saves a picture after each step (the
first one with the mouse over the item). With `--selftest-item=build` it adds a cooler from the
palette, gives it a feed and a product from its nozzles, renames it, deletes it and undoes that,
and with `edit:<tag>` it double-clicks that item. DWSIM_VIEWER3D_SELFTEST and
DWSIM_VIEWER3D_SELFTEST_OUT ask for the same in a window that DWSIM starts itself.

## Surroundings

The plant stands on a concrete pad, inside a band of gravel, on grass. The panel at the bottom left
sets the hour (or lets a day pass in 4 minutes), the weather, the quality of the picture and the
sound; the window keeps them between runs (user://environment.cfg). N jumps between noon and night,
W steps through the weather and M turns the sound off and on.

- The sun follows the hour at the equinox for a latitude of 22.9 S (the plot plan's Y is north),
  with a physical sky; the moon and the stars light the night.
- At night the lamp posts around the plant come on, and a red warning light blinks on top of every
  item taller than 15 m.
- Cloudy, Rain, Storm (with lightning), Snow and Fog dim the sun and veil the view; rain darkens
  and wets the ground, snow whitens it, and the wind leans the rain and the snow.
- The ambient sounds follow the hour and the weather: wind, birds by day, crickets by night, rain
  and storm.
- Quality: Low (no ambient occlusion or glow, small shadows, fewer drops; the start setting with
  the OpenGL renderer), Medium, High (4x MSAA, indirect light, large shadows).

`--time=<hour> --weather=<name> --quality=<name> --mute` set them for one run (pictures, tests).
The textures and sounds, all CC0, are listed in `assets/CREDITS.md`.

## Licences

The window is part of DWSIM (GPL 3). The Godot Engine is MIT licensed; `licenses/` holds its
licence and the notices of the third-party code it includes, and every export carries them.
Only CC0 assets go into this project (see `assets/CREDITS.md`).
