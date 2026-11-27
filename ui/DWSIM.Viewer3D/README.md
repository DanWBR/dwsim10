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
into every package. The builds are about 126 MB on Windows x64 and 92 MB on Linux x64 (the engine
and about 21 MB of textures, models and sounds), and some 100 MB per architecture on macOS.

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

## A living plant

- Dashes run along every line in the direction of flow, at a pace that follows the velocity of its
  stream (A stops all the animation).
- While a dynamic simulation runs, DWSIM sends only the new values (a `values` message), without
  building the geometry again: the info panel, the value labels (V) and the pace of the dashes
  follow them. Outside dynamics new results build the scene again, since they may resize the
  equipment and the lines.
- A gauge in alarm blinks in red together with the object it measures; an object with an error is
  tinted orange.
- A fired heater (a heater above 5 MW) smokes from its stack and glows at its burners. A unit
  operation of an extension whose name says flare burns with a flame and smoke on its tip, and
  one that says cooling tower gives off a plume of steam.
- Pumps hum, compressors and expanders whine, burners and flares roar; the sounds are made by the
  window itself, and the ones nearest the middle of the view are heard, from where the camera looks.

`--dwsim-messages=<file>` feeds the window, after the scene, a JSON array of messages as DWSIM
would send them (values, a selection), and `--audio-driver Dummy` (a Godot option) runs it without
any sound reaching the speakers.

## Flyby videos

B plays a flyby in the window (B or Esc stops it): a perspective camera opens on the whole plant,
flies in an arc to each unit in the order the material goes through them (DWSIM works it out from
the feeds to the products; a recycle does not take the tour back), closes in from an angle no
other item hides, turns slowly around it while a card shows its tag, kind and main values, and
ends on the whole plant again.

CAD Tools > Record 3D Flyby Video (DWSIM 3D) records it: a window of its own runs in Godot's Movie
Maker mode (`--write-movie`, every frame drawn at a fixed rate, the sound mixed in and nothing
played aloud) with `-- --flyby-record`, and quits at the end. "Flyby stop" in the panel sets how
long the camera stays on each unit (1 to 30 s) so the cards can be read, both for B and for the
recordings; `--flyby-hold=<s>` sets it for one run. The video is
1920 x 1080 (the size the window starts with, which is why the project is that size and the
window people work in shrinks itself when it opens). It goes to the folder of the 3D model next
to the simulation, as MP4 (H.264, about 6 Mbit/s) when ffmpeg is on the PATH, else as the AVI.

## The site and the materials

The equipment keeps the sizes and colours the simulation gives it, with surface detail on top
(scratched paint, bare metal grain, ribbed cladding on insulation; off at Low quality), concrete
foundations under it and caged ladders up tall columns and vessels. Around the plant stand a
chain-link fence with a gate and an access road, parked vehicles and a fire truck, a front loader,
a storage corner, fire hydrants round the pad, extinguishers by pumps and compressors, and trees
on the grass outside. O shows or hides the site. `--capture-zoom=<k>` takes k times more in the
pictures and `--capture-clean` leaves the panels out.

## The control room and the rounds

The palette has a Site group with a Control Room. When you place one, it becomes a building of the
site: it moves, turns, goes with Del and undoes like any item, and the flowsheet keeps it in its
extra properties (key `Plant3DSite`), apart from the equipment layout. It has no editor and does
not take part in the simulation. If the flowsheet doesn't have a control room, the window draws one
by the road on its own.

Every 20 to 45 s an operator or an engineer comes out of the control room door, walks round the
equipment to two to four units (the ones in alarm first), stops by each one with a clipboard and
writes a note about it: the alarm, the error, or its temperature, pressure, power or duty. A bubble
over the person shows the note, and then the person walks back in. At most four people are out at
once.

Every 35 to 70 s a maintenance team comes out too, walks to a unit (the ones in alarm or in error
first), sets four cones and red tape round the place of the job, and works there for 30 to 50 s:
welding (a blue arc that comes and goes, sparks and the crackle of the arc, with a second person on
fire watch holding an extinguisher), grinding (a fan of orange sparks and the whine of the disc),
hammering (the swing of the hammer and the clank of each blow) or tightening flange bolts (the
clicks of a ratchet). Then the team takes the cones away and walks back in. At most two jobs go on
at once, and the sounds are made in the window, like the sounds of the plant.

Everyone on the plant wears PPE: a hard hat (white for engineers, the yellow one of the worker
model for operators and maintenance), safety glasses, gloves and a high-visibility vest with
reflective bands. The welder wears a welding mask, a leather apron and leather gloves; whoever
grinds wears a face shield over the glasses; whoever grinds or hammers wears ear muffs. The people
come in seven skin tones, from very light to very dark, dealt in turn so that every group of seven
has one of each.

K shows the Rounds and maintenance panel with the last notes and jobs. `--rounds` opens that panel
at start, `--capture-after=<s>` waits s seconds before the pictures so that the rounds and the jobs
have started, and `--capture-focus=<tag>` centres the pictures on a unit.

## Licences

The window is part of DWSIM (GPL 3). The Godot Engine is MIT licensed; `licenses/` holds its
licence and the notices of the third-party code it includes, and every export carries them.
Only CC0 assets go into this project (see `assets/CREDITS.md`).
