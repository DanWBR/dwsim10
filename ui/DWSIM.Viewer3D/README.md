# DWSIM 3D

DWSIM 3D is the 3D view of a flowsheet: a Godot 4.7 program (GDScript) that DWSIM starts from
**CAD Tools > 3D Designer/Viewer**. DWSIM builds the scene from the plant model of the SolidCraft
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
into every package.

The release templates are built from the Godot source with `templates/custom.py`: it leaves out
the parts the window does not use (physics, navigation, XR, networking, video, Direct3D 12 and
the runtime glTF and texture compressors) and optimizes the binary for size with full link-time
optimization. The workflow `viewer3d-templates.yml`, run by hand when Godot or the profile
changes, builds them for every platform and publishes them on the release
`viewer3d-templates-<godot version>`; `templates/install.sh` then puts them over the official
templates before the export, and it keeps the official ones if that release cannot be reached.
With them, the builds are about 62 MB on Windows x64 and 55 MB on Linux x64 (about 22 MB of that
is textures, models and sounds), against 126 MB and 92 MB with the official templates. Windows
runs on Vulkan, and the OpenGL 3 renderer is still the fallback everywhere.

To build a template locally, check out the Godot tag, copy `custom.py` next to `SConstruct` and
run, for example, `scons platform=windows target=template_release arch=x86_64 profile=custom.py
d3d12=no accesskit=no` (the platform defaults override those two in the profile). On Linux, use
Godot's buildroot toolchain `godot-2023.08.x-4`, so the template needs glibc 2.28 like the
official one.

## Controls

With the mouse over an item or a line, the window tints it, draws a box at its limits (seen
through the other items too) and shows its tag and size (width x depth x height). Left click selects (Ctrl adds), a double click opens the item's editor, Shift+F frames it, right drag turns the view, middle
drag (or Shift + right drag) moves the view, and the wheel zooms. Keys 1 to 4 are the isometric views,
5 is the plan, 6 and 7 the front and right side, Q and E turn the view by 90 degrees, F fits the
plant, I, R and G show or hide the insulation, pipe rack and grid, and F1 the card of shortcuts.
Only the selected items show their tags, so that the tags do not crowd the plant; L (or All labels
in the panel, which the window keeps between runs) shows the tags of every item, and
`--all-labels` does the same for one run (pictures).

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

A unit operation that draws itself shows its own 3D model inside its block. A pipe network with
plant positions on its nodes is one: its pipes, hydrants, monitors, pumps, reservoirs, protected
tanks and buildings appear where its designer puts them, centred on the block. That model is
read-only here. A click on any part of it selects the network, a drag moves and T turns the whole
network, and a double click opens the network's own editor, where every change is made; the 3D view
follows it. A network without plant positions stays a block. (DWSIM finds the operation's
BuildPlant3DModel() method by reflection, so the extension does not need the CAD Tools to load.)

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
- The window draws the scene in high dynamic range and tone maps it with ACES (exposure 0.9 by day,
  1.3 at night), which keeps the sunlit concrete near white. Lamps, lit windows,
  the warning lights, flames, the welding arc and its sparks, and alarms are brighter than the lit
  ground, so they bloom (Medium and High); the sunlit concrete stays below the bloom threshold.
- HDR display: if the system can show the window in HDR (Windows with HDR turned on, for example),
  this box sends the picture to the display in high dynamic range. Otherwise the box is greyed out.

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
a storage corner, fire hydrants round the pad, extinguishers by pumps and compressors, lamp posts,
and woods on the grass outside. O shows or hides the site. `--capture-zoom=<k>` takes k times more
in the pictures and `--capture-clean` leaves the panels out.

## Building the site

U opens the build bar at the bottom of the window, in the manner of SimCity 3000. It has seven
categories: Buildings, Roads, Fences, Nature, Vehicles, Safety and Utilities. When you click a
category, a grid of its objects opens above the bar, each with a picture made in the window, its
name and its estimated cost. When you pick an object, a ghost of it follows the mouse on a 1 m grid.
The ghost is green where the object fits and red where it would stand on the equipment (or within
1 m of it) or on another object. R turns it a quarter turn and Shift+R turns it back. A click
builds it, and the tool stays for the next one until you press Esc or click the right button.
Some kinds have several looks (office buildings, factory halls, sheds, containers, trees, autumn
trees, small trees, pines, palms, bushes, flowers, stones, rocks, tall rocks): the bar deals one at each placement, as SimCity does, and V shows
the next one before you click.

The catalog holds 60 kinds. The buildings are a control room, an administration building, a
maintenance workshop, a warehouse with a loading dock, an electrical substation with a fenced yard
of transformers, a canteen with parasols, a guardhouse, an office building, a factory hall, a plant
office and a storage shed. Roads and paving have roads, footpaths, asphalt, concrete, gravel and
parking lots. Fences and barriers have chain-link fences, concrete walls, gates, road barriers and
tank bunds. Nature has trees (18 looks, in two greens), autumn trees (9), small trees and young
pines (9), pines (12), palms (6), rows of trees, hedges, bushes, flowers, stones (14), rocks (6),
tall rocks (10), lawns and woods; the woods mix 15 kinds of tree. Vehicles has cars, delivery vans, trucks, a fire truck and a front loader. Safety has
fire hydrants, extinguishers, safety showers, assembly points, windsocks and traffic cones.
Utilities has lamp posts, flagpoles, LPG tanks, electrical cabinets, drums, crates, shipping
containers, dumpsters, parasol tables, a water tower, solar arrays, a wind turbine whose rotor turns
while the plant runs, and power lines (a pole every 35 m with three wires). The buildings made in
the window carry their names over the door, and their windows light up at night.
Once an object is built, you drag it to move it, T turns it and Del demolishes it, and Ctrl+Z
undoes each step.

A road, a footpath, a fence, a wall, a hedge or a row of trees is a line. If you press and drag,
the line runs straight from where you pressed to where you let go. If you click instead, each click
adds a point, and a double click or Enter finishes the line; Backspace takes the last point back
and Shift holds the run to steps of 45 degrees. Paving, gravel, a lawn, woods, a parking lot or a
tank bund is an area: drag a rectangle, or click the corners of an outline and finish with a double
click or Enter. While you draw, the window shows the object as it will be, with its length or area
and its estimated cost in the hint, and paints it red if it would cross the equipment (DWSIM is
then not asked to build it).

Every object of the catalog has an estimated cost: each, per metre of a line, or per square metre
of an area (a point object scaled up costs its scale squared, since it covers more ground). The
grid shows the cost of each kind, the hint shows the cost of what you are about to build, and the
tip over a built object shows its cost. The bar shows the cost of the whole site, as SimCity shows
the funds of the city, and Budget opens a panel with the count and the cost of each category.
DWSIM works the budget out (SiteCatalog.Budget) and sends it with the scene. The costs are rough
estimates in US dollars of 2026, for planning, not a quote. The ground the terrain brushes moved
counts too, as a line of earthworks (the cut and fill volume times a cost per m3), and the panel
shows the embodied CO2e of the site, from the concrete, steel and asphalt each kind of object
takes and the diesel the earthworks burn.

DWSIM keeps a summary of the site in the flowsheet (key `Plant3DSiteSummary`): the cost of each
category, the earthworks, the materials and the CO2e. The techno-economic and life cycle tools of
DWSIM Plus read it. In the TEA, a case can take Auxiliary Buildings (the buildings), Site
Preparation (roads, paving, fences, nature and earthworks) and Site Facilities (safety, utilities
and vehicles) from the site, brought to the date, place and currency of the case, instead of the
percentages of the equipment cost. In the LCA, a study can include the construction of the site
(its concrete, steel, asphalt and earthworks diesel) spread over the years the site serves.

Terrain, the last category, shapes the ground round the plant with brushes: Raise builds it up,
Lower digs it down, Level brings it to the height where you press, Smooth evens out bumps, Pond
digs a bowl and fills it with water up to the height where you press, and Drain takes the water
away. Hold the left button and move over the ground; the brush comes in three sizes (4, 8 and
14 m) and its ring turns red over the platform of the plant (the equipment, the pad and the gravel
round it, 15 m out), which stays flat. The ground turns from grass to bare earth as it gets
steeper. The relief is a grid with one height every metre, about 110 m beyond the plant, made the
first time a brush touches the ground; each stroke goes to DWSIM as one step (the terrain message),
which keeps it with the flowsheet (key `Plant3DTerrain`) and undoes it with Ctrl+Z. The objects of
the site stand on the relief: buildings and trees at the height of the ground where they stand,
roads, fences and power lines following the slopes, and paving, lawns and woods laid over them.

Building and demolishing look and sound as in SimCity 3000. When something is built (an object of
the site, or equipment from the palette), it rises out of the ground with a small bounce, a cloud of
dust and a thud. When it goes, it sinks into the ground under the sound of a bulldozer, with dust,
rubble and a crunch. The terrain brushes rumble like earthmoving machines and throw up dust where
they work, and the buttons of the build bar click. A change of many objects at once (a scene that
loads, the site adopted) passes quietly. The sounds are made in the window, like the other sounds
of the plant, and follow the sound setting.

Demolish is the bulldozer. When you click an object of the site, it goes; when you drag a
rectangle, every object of the site that the rectangle touches goes, a road or a fence if any part
of it crosses the rectangle, and an area if the rectangle lies inside it. The bulldozer leaves the
equipment alone; Del takes equipment out of the flowsheet.

Until the bar is opened for the first time, the window draws the site above by itself, from the
bounds of the plant. When the bar opens, that site becomes objects of the flowsheet (DWSIM keeps
them in its extra properties, key `Plant3DSite`, and the window sends them with `site.adopt`), so
the user can move, turn or demolish the fence, the cars or the woods like anything built later. A
flowsheet that was never edited this way keeps the site the window draws.

The samples come with their site already made. `-- --bake-site [--bake-out=<folder>]` does it:
once DWSIM has sent the scene, the window adopts the site it draws by itself together with the
buildings of a plant in front of the gate, on both sides of a longer road (a guardhouse, the
administration building with flags, a parking lot, the canteen, a lawn with the assembly point,
the maintenance workshop, the warehouse with containers and the substation with its power line),
with lamp posts and rows of trees along the road, safety showers and a windsock on the plant, and
the woods kept clear of the buildings. When DWSIM sends the adopted site back, the window saves
IsoSE.png in the folder, writes bake.txt (ok, or what went wrong) and quits. A flowsheet whose site
was already adopted keeps it.

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
