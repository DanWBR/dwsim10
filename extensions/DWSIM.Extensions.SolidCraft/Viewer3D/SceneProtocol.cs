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

        /// <summary>{ids}: removes objects from the simulation.</summary>
        public const string Delete = "delete";

        /// <summary>{id, tag}: gives an object a new tag.</summary>
        public const string Rename = "rename";

        /// <summary>{view, png}: the picture asked with <see cref="Capture"/>, PNG in base 64.</summary>
        public const string CaptureResult = "capture.result";

        // DWSIM to the window, and both ways

        /// <summary>{version, name}: the window was let in.</summary>
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
