//    Copyright 2026 Daniel Wagner O. de Medeiros
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>
    /// The messages between DWSIM and the DWSIM 3D window. Each message is one JSON object on one
    /// line (UTF-8, ended by a line feed) with a "type". A message may carry a "ref", which the
    /// answer to it repeats, so the sender can match the two.
    /// </summary>
    /// <remarks>
    /// The window opens the connection and sends <see cref="Hello"/> with the token it got on its
    /// command line. DWSIM answers <see cref="Welcome"/>, then the whole scene
    /// (<see cref="SceneFull"/>) and, from then on, only what changed (<see cref="SceneDelta"/>).
    /// Lengths are in mm, in a frame with Z up and Y toward the back of the plot plan.
    /// </remarks>
    public static class SceneProtocol
    {
        /// <summary>The version of the messages; a window that speaks another one is turned away.</summary>
        public const int Version = 1;

        // the window to DWSIM

        /// <summary>{token, version}: the first message of the window.</summary>
        public const string Hello = "hello";

        /// <summary>
        /// {id, x, y, z, rot}, or {items: [those]}: the window moved or turned items (rot in degrees,
        /// quarter turns); several items at once undo as one step.
        /// </summary>
        public const string Place = "place";

        /// <summary>Takes back the last change of the 3D layout (place or layout.reset).</summary>
        public const string Undo = "undo";

        /// <summary>Makes again the change the last undo took back.</summary>
        public const string Redo = "redo";

        /// <summary>{ids}: the items go back to the place the flowsheet drawing gives them; no ids for all of them.</summary>
        public const string LayoutReset = "layout.reset";

        /// <summary>
        /// {objectType, tag, x, y, z, rot}, or {external, ...} for a unit operation of an extension
        /// (its key in the palette): a new object, placed in 3D; DWSIM answers <see cref="Added"/>.
        /// </summary>
        public const string Add = "add";

        /// <summary>
        /// {from, to, fromPort, toPort, energy}: joins two objects; between two unit operations DWSIM
        /// puts a new stream (an energy stream with energy: true). With one end only, the stream goes
        /// to the battery limit: a feed or a product.
        /// </summary>
        public const string Connect = "connect";

        /// <summary>{from, to}: takes the connection between two objects apart.</summary>
        public const string Disconnect = "disconnect";

        /// <summary>Asks for the tour of a flyby; DWSIM answers <see cref="FlybyPlan"/>.</summary>
        public const string Flyby = "flyby";

        /// <summary>{ids}: the items of the scene in the order the material goes through them (see <see cref="ProcessOrder"/>).</summary>
        public const string FlybyPlan = "flyby.plan";

        /// <summary>{id}: opens the object's editor in a floating window of DWSIM.</summary>
        public const string Edit = "edit";

        /// <summary>
        /// {kind, x, y, z, rot, variant, scale} for a point object of the site, {kind, points: [[x, y], ...]}
        /// for a line or an area (see <see cref="SiteCatalog"/>), or {items: [those]} for several at
        /// once, which undo as one step; DWSIM answers <see cref="Added"/> with "id" and "ids".
        /// </summary>
        public const string SiteAdd = "site.add";

        /// <summary>
        /// {ids} and/or {rect: [x0, y0, x1, y1]}: the bulldozer takes away objects of the site, by id
        /// or all those that touch the rectangle; the answer lists them in "removed". Equipment stays.
        /// </summary>
        public const string SiteRemove = "site.remove";

        /// <summary>
        /// {items: [as in site.add]}: the site the window drew by itself round the plant becomes
        /// objects of the site, once; from then on the window draws the site from its objects.
        /// </summary>
        public const string SiteAdopt = "site.adopt";

        /// <summary>{terrain: {x0, y0, cell, nx, ny, h, w}} or {terrain: null}: the relief of the ground (see <see cref="SiteLayout.Terrain"/>).</summary>
        public const string Terrain = "terrain";

        /// <summary>
        /// {path}: imports a 3D model of the plant (glTF, FBX, OBJ, STL, PLY, 3D DWF) as the scenery of
        /// the flowsheet; DWSIM reads it in the background, answers at once and tells how it went with
        /// <see cref="ModelStatus"/>.
        /// </summary>
        public const string ModelImport = "model.import";

        /// <summary>
        /// Proposes again the flowsheet objects for the equipment groups of the plant model (after the
        /// flowsheet changed); runs apart, like <see cref="ModelImport"/>.
        /// </summary>
        public const string ModelSuggest = "model.suggest";

        /// <summary>
        /// Asks for every group of the plant model (window to DWSIM, no fields), or brings them (DWSIM
        /// to the window: {groups: [{id, name, tag, role, kind, islands, c, length, diameter, user,
        /// object, suggested, score, why}], units: [{name, tag, kind}]}); DWSIM sends them again after
        /// each change of the groups.
        /// </summary>
        public const string ModelGroups = "model.groups";

        /// <summary>{group, object}: the group stands for the flowsheet object (null for none); {group, accept: true} takes the proposal.</summary>
        public const string ModelAssign = "model.assign";

        /// <summary>{group, role}: equipment, piping, structure, scenery or hidden.</summary>
        public const string ModelRole = "model.role";

        /// <summary>{groups}: the groups become one.</summary>
        public const string ModelMerge = "model.merge";

        /// <summary>{group, islands}: those islands of the group become a group of their own.</summary>
        public const string ModelSplit = "model.split";

        /// <summary>{group, objectType}: DWSIM adds a flowsheet object for the group (by default of the group's kind), where the group stands, and the group stands for it.</summary>
        public const string ModelCreate = "model.create";

        /// <summary>The flowsheet stops using its 3D model of the plant (the file and the cache stay).</summary>
        public const string ModelRemove = "model.remove";

        /// <summary>{ids}: removes objects from the simulation.</summary>
        public const string Delete = "delete";

        /// <summary>{id, tag}: gives an object a new tag.</summary>
        public const string Rename = "rename";

        /// <summary>{view, png}: the picture asked with <see cref="Capture"/>, PNG in base 64.</summary>
        public const string CaptureResult = "capture.result";

        // DWSIM to the window, and both ways

        /// <summary>{version, name, palette, site}: the window was let in; "site" is the catalog of <see cref="SiteCatalog"/>.</summary>
        public const string Welcome = "welcome";

        /// <summary>{scene}: the whole scene (see <see cref="SceneBuilder"/>).</summary>
        public const string SceneFull = "scene.full";

        /// <summary>{upsert, remove, lines, rack, name, warnings}: what changed in the scene (see <see cref="SceneDiff"/>).</summary>
        public const string SceneDelta = "scene.delta";

        /// <summary>
        /// {items, lines, alarms, errors}: new values without new geometry, sent while a dynamic
        /// simulation runs (see <see cref="SceneBuilder.Values"/>).
        /// </summary>
        public const string Values = "values";

        /// <summary>{ids}: the selection, both ways.</summary>
        public const string Select = "select";

        /// <summary>{view, width, height}: asks the window for a picture of a view.</summary>
        public const string Capture = "capture";

        /// <summary>{id}: the object a <see cref="Add"/> or <see cref="Connect"/> made.</summary>
        public const string Added = "added";

        /// <summary>{state: importing, done or failed, message}: how an import of a 3D model of the plant goes.</summary>
        public const string ModelStatus = "model.status";

        /// <summary>The command went through.</summary>
        public const string Ack = "ack";

        /// <summary>{message, for}: the command did not go through.</summary>
        public const string Error = "error";

        /// <summary>The other side is closing the connection.</summary>
        public const string Bye = "bye";

        public static JObject Message(string type) => new JObject { ["type"] = type };

        /// <summary>The message as one line of text, line feed included.</summary>
        public static string Encode(JObject message) => message.ToString(Formatting.None) + "\n";

        /// <summary>One line read back as a message; null when the line holds no message with a type.</summary>
        public static JObject Decode(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            try
            {
                var o = JToken.Parse(line) as JObject;
                return o != null && o["type"]?.Type == JTokenType.String ? o : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static string TypeOf(JObject message) => (string)message?["type"] ?? "";
    }
}
