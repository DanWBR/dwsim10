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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using Newtonsoft.Json.Linq;
using ObjectType = DWSIM.Interfaces.Enums.GraphicObjects.ObjectType;

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>
    /// Keeps a flowsheet and the DWSIM 3D window in step over a local connection (see
    /// <see cref="SceneProtocol"/>). The link listens on 127.0.0.1 at a free port, lets in one
    /// window that knows its token, sends it the scene and then every change, and carries out the
    /// window's commands on the flowsheet with the same calls the 2D drawing uses.
    /// </summary>
    /// <remarks>
    /// The link reads the flowsheet only on the interface thread (through
    /// <see cref="IFlowsheet.RunCodeOnUIThread"/>), and one lock keeps the reading of changes and
    /// the commands apart, so a headless flowsheet, which runs that code in place, is safe too.
    /// Topology (objects, connections, tags, selection) is the same in both views; each view keeps
    /// its own positions, and the 3D ones live in the saved layout (see <see cref="PlantLayout"/>).
    /// </remarks>
    public sealed class FlowsheetLink : IDisposable
    {
        readonly IFlowsheet fs;
        readonly object gate = new object();
        readonly object sendLock = new object();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly Dictionary<string, TaskCompletionSource<byte[]>> captures = new Dictionary<string, TaskCompletionSource<byte[]>>();

        TcpListener listener;
        Thread acceptThread, pollThread;
        TcpClient client;
        StreamWriter writer;
        volatile bool running;

        JObject scene;
        PlantModel model;
        string fingerprint, results, selection;
        int nextRef;

        /// <summary>
        /// Opens the editor of an object for the window (see <see cref="EditorHost"/>); called on
        /// the interface thread, false when it could not. Null: the window cannot open editors.
        /// </summary>
        public Func<string, bool> EditHandler;

        /// <summary>How often the link looks for changes, ms.</summary>
        public int PollInterval = 500;

        /// <summary>How long a call on the interface thread may take before the link gives up on it, ms.</summary>
        public int UiTimeout = 30000;

        public FlowsheetLink(IFlowsheet flowsheet)
        {
            fs = flowsheet ?? throw new ArgumentNullException(nameof(flowsheet));
            Token = NewToken();
        }

        public IFlowsheet Flowsheet => fs;

        /// <summary>The port the link listens on (0 until started).</summary>
        public int Port { get; private set; }

        /// <summary>The secret the window must send first; it gets it on its command line.</summary>
        public string Token { get; }

        public bool IsConnected => client != null;

        /// <summary>The scene the window has now (the last one sent).</summary>
        public JObject Scene { get { lock (gate) return scene; } }

        public event Action<string> Log;

        /// <summary>Every message the link sends, connected window or not (for tests and logs).</summary>
        public event Action<JObject> MessageSent;
        public event EventHandler Connected, Disconnected;

        /// <summary>Starts listening and looking for changes.</summary>
        public void Start()
        {
            if (running) return;
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            running = true;
            acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "DWSIM 3D link" };
            acceptThread.Start();
            pollThread = new Thread(PollLoop) { IsBackground = true, Name = "DWSIM 3D sync" };
            pollThread.Start();
        }

        public void Stop()
        {
            if (!running) return;
            running = false;
            try { Send(SceneProtocol.Message(SceneProtocol.Bye)); } catch (Exception) { }
            try { listener?.Stop(); } catch (Exception) { }
            DropClient();
            wake.Set();
            lock (captures)
            {
                foreach (var tcs in captures.Values) tcs.TrySetCanceled();
                captures.Clear();
            }
        }

        public void Dispose() => Stop();

        #region Connection

        void AcceptLoop()
        {
            while (running)
            {
                TcpClient candidate;
                try { candidate = listener.AcceptTcpClient(); }
                catch (Exception) { break; }

                if (client != null)
                {
                    // one window at a time
                    candidate.Close();
                    continue;
                }
                var t = new Thread(() => Serve(candidate)) { IsBackground = true, Name = "DWSIM 3D window" };
                t.Start();
            }
        }

        void Serve(TcpClient c)
        {
            try
            {
                c.NoDelay = true;
                var stream = c.GetStream();
                var reader = new StreamReader(stream, new UTF8Encoding(false));
                var w = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

                c.ReceiveTimeout = 10000;
                var hello = SceneProtocol.Decode(reader.ReadLine());
                if (hello == null || SceneProtocol.TypeOf(hello) != SceneProtocol.Hello || (string)hello["token"] != Token)
                {
                    Write(w, Error("the window did not say hello with the right token", SceneProtocol.Hello));
                    c.Close();
                    return;
                }
                if ((int?)hello["version"] != SceneProtocol.Version)
                {
                    Write(w, Error("the window speaks version " + hello["version"] + ", DWSIM speaks " + SceneProtocol.Version, SceneProtocol.Hello));
                    c.Close();
                    return;
                }
                c.ReceiveTimeout = 0;

                lock (sendLock)
                {
                    client = c;
                    writer = w;
                }
                var welcome = SceneProtocol.Message(SceneProtocol.Welcome);
                welcome["version"] = SceneProtocol.Version;
                welcome["name"] = PlantModelExtender.ModelName(fs);
                welcome["palette"] = PaletteWithExtensions();
                Send(welcome);
                Refresh();
                Connected?.Invoke(this, EventArgs.Empty);

                string line;
                while (running && (line = reader.ReadLine()) != null)
                {
                    var message = SceneProtocol.Decode(line);
                    if (message == null)
                    {
                        Send(Error("not a message: " + Shorten(line), null));
                        continue;
                    }
                    if (SceneProtocol.TypeOf(message) == SceneProtocol.Bye) break;
                    Handle(message);
                }
            }
            catch (Exception ex)
            {
                if (running) Report("the 3D window connection dropped: " + ex.Message);
            }
            finally
            {
                var was = client == c;
                if (was) DropClient();
                else try { c.Close(); } catch (Exception) { }
                if (was) Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        void DropClient()
        {
            lock (sendLock)
            {
                try { client?.Close(); } catch (Exception) { }
                client = null;
                writer = null;
            }
        }

        /// <summary>Sends a message to the window, when one is connected.</summary>
        public void Send(JObject message)
        {
            try { MessageSent?.Invoke(message); } catch (Exception) { }
            lock (sendLock)
            {
                if (writer == null) return;
                try { Write(writer, message); }
                catch (Exception ex) { Report("could not send to the 3D window: " + ex.Message); }
            }
        }

        static void Write(StreamWriter w, JObject message) => w.Write(SceneProtocol.Encode(message));

        #endregion

        #region Changes from the flowsheet

        void PollLoop()
        {
            while (running)
            {
                wake.WaitOne(PollInterval);
                if (!running) break;
                if (client == null) continue;
                try { Sync(); }
                catch (Exception ex) { Report("could not bring the 3D scene up to date: " + ex.Message); }
            }
        }

        /// <summary>
        /// Brings the window up to date now: sends what changed, when anything did. It waits for the
        /// interface thread, so it must not be called from it (see <see cref="Refresh"/>).
        /// </summary>
        public void Sync()
        {
            JObject delta = null, select = null, values = null;
            OnUi(() =>
            {
                lock (gate)
                {
                    // While a dynamic simulation runs only the values change: they go without the
                    // geometry. Otherwise new results may resize the equipment and the lines, so the
                    // scene is built again.
                    var dynamic = false;
                    try { dynamic = fs.DynamicMode; } catch (Exception) { }
                    var now = FlowsheetFingerprint.Structure(fs);
                    var res = FlowsheetFingerprint.Results(fs);
                    if (scene == null || now != fingerprint || !dynamic && res != results)
                    {
                        var m = SceneBuilder.Model(fs);
                        var next = SceneBuilder.FromModel(m, fs);
                        delta = scene == null ? Full(next) : SceneDiff.Delta(scene, next);
                        scene = next;
                        model = m;
                        fingerprint = now;
                        results = res;
                        values = SceneBuilder.Values(fs, next);
                    }
                    else if (res != results)
                    {
                        values = SceneBuilder.Values(fs, scene);
                        results = res;
                    }
                    var sel = string.Join("\n", Selection.Get(fs));
                    if (sel != selection)
                    {
                        selection = sel;
                        select = SceneProtocol.Message(SceneProtocol.Select);
                        select["ids"] = new JArray(Selection.Get(fs));
                    }
                }
            });
            if (delta != null) Send(delta);
            if (values != null) Send(values);
            if (select != null) Send(select);
        }

        /// <summary>
        /// Sends the whole scene and the selection again (a window that just came in, or one that
        /// asks), from the link's own thread. Safe to call from the interface thread, unlike
        /// <see cref="Sync"/>, which waits for that thread.
        /// </summary>
        public void Refresh()
        {
            lock (gate)
            {
                scene = null;
                selection = null;
            }
            wake.Set();
        }

        static JObject Full(JObject s)
        {
            var m = SceneProtocol.Message(SceneProtocol.SceneFull);
            m["scene"] = s;
            return m;
        }

        /// <summary>Asks the window for a picture of a view ("IsoSE", "Plan" and so on); the task ends with the PNG.</summary>
        public Task<byte[]> Capture(string view, int width = 1280, int height = 800, int timeoutMs = 30000)
        {
            var tcs = new TaskCompletionSource<byte[]>();
            string id;
            lock (captures)
            {
                id = "cap" + (++nextRef);
                captures[id] = tcs;
            }
            var m = SceneProtocol.Message(SceneProtocol.Capture);
            m["ref"] = id;
            m["view"] = view;
            m["width"] = width;
            m["height"] = height;
            Send(m);
            var cts = new CancellationTokenSource(timeoutMs);
            cts.Token.Register(() =>
            {
                lock (captures) captures.Remove(id);
                tcs.TrySetException(new TimeoutException("the 3D window sent no picture of " + view));
            });
            return tcs.Task;
        }

        #endregion

        #region Commands from the window

        /// <summary>Carries out one command of the window and answers it. Public so a test can drive the link without a socket.</summary>
        public JObject Handle(JObject message)
        {
            var type = SceneProtocol.TypeOf(message);
            JObject answer;
            if (type == SceneProtocol.CaptureResult)
            {
                Captured(message);
                return null;
            }
            try
            {
                answer = null;
                OnUi(() =>
                {
                    lock (gate) answer = Execute(type, message);
                });
            }
            catch (Exception ex)
            {
                answer = Error(ex.InnerException?.Message ?? ex.Message, type);
            }
            if (answer == null) answer = SceneProtocol.Message(SceneProtocol.Ack);
            if (message["ref"] != null) answer["ref"] = message["ref"].DeepClone();
            if (answer["for"] == null) answer["for"] = type;
            Send(answer);
            if (SceneProtocol.TypeOf(answer) != SceneProtocol.Error) wake.Set();
            return answer;
        }

        JObject Execute(string type, JObject m)
        {
            switch (type)
            {
                case SceneProtocol.Place: return Place(m);
                case SceneProtocol.LayoutReset: return ResetLayout(m);
                case SceneProtocol.Undo: return Undo(true);
                case SceneProtocol.Redo: return Undo(false);
                case SceneProtocol.Add: return Add(m);
                case SceneProtocol.Connect: return Connect(m);
                case SceneProtocol.Disconnect: return Disconnect(m);
                case SceneProtocol.Delete: return Delete(m);
                case SceneProtocol.Rename: return Rename(m);
                case SceneProtocol.Select: return Select(m);
                case SceneProtocol.Edit: return Edit(m);
                case SceneProtocol.Flyby: return FlybyPlan();
                case SceneProtocol.SceneFull:
                    scene = null;
                    selection = null;
                    return null;
                default: return Error("unknown command '" + type + "'", type);
            }
        }

        /// <summary>
        /// Moves or turns items: one item (id, x, y, z, rot) or several at once ("items", an array
        /// of those), which undo as one step. Lengths in mm, the turn in degrees (quarter turns).
        /// </summary>
        JObject Place(JObject m)
        {
            var list = m["items"] as JArray ?? new JArray(m.DeepClone());
            var places = new List<KeyValuePair<string, Placement>>();
            var site = SiteLayout.Load(fs);
            foreach (var t in list.OfType<JObject>())
            {
                var id = (string)t["id"];
                var isSite = SiteLayout.IsSite(id) && site.Any(b => b.Id == id);
                if (id == null || !isSite && !fs.SimulationObjects.ContainsKey(id)) return Error("there is no object '" + id + "'", SceneProtocol.Place);
                double x, y, z;
                if (!Finite(t["x"], out x) || !Finite(t["y"], out y) || !Finite(t["z"] ?? 0, out z))
                    return Error("the place of '" + id + "' is not a number", SceneProtocol.Place);
                if (z < MinElevation || z > MaxElevation)
                    return Error("an elevation must lie between " + MinElevation / 1000 + " and " + MaxElevation / 1000 + " m", SceneProtocol.Place);
                places.Add(new KeyValuePair<string, Placement>(id, new Placement
                {
                    X = Math.Round(x),
                    Y = Math.Round(y),
                    Z = Math.Round(z),
                    Rot = QuarterTurn((double?)t["rot"] ?? 0)
                }));
            }
            if (places.Count == 0) return Error("nothing to place", SceneProtocol.Place);
            var layout = PlantLayout.Load(fs);
            Remember();
            foreach (var kv in places)
            {
                var building = site.FirstOrDefault(b => b.Id == kv.Key);
                if (building != null)
                {
                    building.X = kv.Value.X;
                    building.Y = kv.Value.Y;
                    building.Z = kv.Value.Z;
                    building.Rot = kv.Value.Rot;
                }
                else layout[kv.Key] = kv.Value;
            }
            PlantLayout.Save(fs, layout);
            SiteLayout.Save(fs, site);
            return null;
        }

        JObject ResetLayout(JObject m)
        {
            var layout = PlantLayout.Load(fs);
            var ids = m["ids"] as JArray;
            Remember();
            if (ids == null) layout.Clear();
            else foreach (var id in ids) layout.Remove((string)id);
            PlantLayout.Save(fs, layout);
            return null;
        }

        /// <summary>The lowest and highest elevation an item may be placed at, mm.</summary>
        public const double MinElevation = -50000, MaxElevation = 300000;

        // The window's Ctrl+Z takes back the last change made in the window: a move (the 3D layout
        // before it) or a change of the flowsheet (the objects and connections before it, taken
        // with the snapshot the 2D undo uses, and the layout). It keeps its own history; the 2D
        // drawing keeps its own too, since the commands also register their 2D snapshots.
        const int UndoDepth = 100;

        sealed class Step
        {
            public string Layout;
            public string Site;
            public System.Xml.Linq.XDocument Flowsheet;
        }

        readonly List<Step> undo = new List<Step>(), redo = new List<Step>();

        void Remember(bool flowsheet = false)
        {
            undo.Add(Capture(flowsheet));
            if (undo.Count > UndoDepth) undo.RemoveAt(0);
            redo.Clear();
        }

        Step Capture(bool flowsheet)
        {
            var step = new Step { Layout = LayoutText(), Site = SiteLayout.Text(fs) };
            if (flowsheet)
                try { step.Flowsheet = fs.GetSnapshot(SnapshotType.ObjectAddedOrRemoved); }
                catch (Exception) { step.Flowsheet = null; }
            return step;
        }

        JObject Undo(bool back)
        {
            var from = back ? undo : redo;
            var to = back ? redo : undo;
            if (from.Count == 0) return Error(back ? "there is nothing to undo" : "there is nothing to redo", back ? SceneProtocol.Undo : SceneProtocol.Redo);
            var step = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            to.Add(Capture(step.Flowsheet != null));
            if (step.Flowsheet != null)
            {
                fs.RestoreSnapshot(step.Flowsheet, SnapshotType.ObjectAddedOrRemoved);
                try { fs.UpdateInterface(); } catch (Exception) { }
            }
            var props = fs.ExtraProperties as IDictionary<string, object>;
            if (props != null)
            {
                if (step.Layout == null) props.Remove(LayoutKey);
                else props[LayoutKey] = step.Layout;
            }
            SiteLayout.SetText(fs, step.Site);
            return null;
        }

        const string LayoutKey = "SolidCraftLayout";

        string LayoutText()
        {
            var props = fs.ExtraProperties as IDictionary<string, object>;
            object value;
            return props != null && props.TryGetValue(LayoutKey, out value) && value != null ? value.ToString() : null;
        }

        static bool Finite(JToken t, out double value)
        {
            value = 0;
            if (t == null || (t.Type != JTokenType.Float && t.Type != JTokenType.Integer)) return false;
            value = (double)t;
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        /// <summary>The built-in palette and, after it, the unit operations of the extensions DWSIM loaded.</summary>
        public JArray PaletteWithExtensions()
        {
            var palette = Palette.ToJson();
            JArray extra = null;
            OnUi(() =>
            {
                extra = new JArray();
                var available = fs.AvailableExternalUnitOperations;
                if (available == null) return;
                foreach (var kv in available.OrderBy(k => k.Value?.Name ?? k.Key, StringComparer.OrdinalIgnoreCase))
                {
                    if (kv.Value == null) continue;
                    string name;
                    try { name = string.IsNullOrWhiteSpace(kv.Value.Name) ? kv.Key : kv.Value.Name; }
                    catch (Exception) { name = kv.Key; }
                    extra.Add(new JObject { ["group"] = "Extensions", ["name"] = name, ["objectType"] = "External", ["external"] = kv.Key });
                }
            });
            foreach (var e in extra) palette.Add(e);
            return palette;
        }

        /// <summary>
        /// Adds a unit operation of an extension, known by its key in AvailableExternalUnitOperations:
        /// a new instance of its class, placed through the drawing surface the way each interface's
        /// palette does it (FlowsheetBase.AddObjectToSurface, or the Windows surface's own).
        /// </summary>
        ISimulationObject AddExternal(string key, int x, int y, string tag)
        {
            IExternalUnitOperation template;
            if (fs.AvailableExternalUnitOperations == null || !fs.AvailableExternalUnitOperations.TryGetValue(key, out template) || template == null)
                throw new InvalidOperationException("there is no extension unit operation '" + key + "'");
            var instance = (IExternalUnitOperation)Activator.CreateInstance(template.GetType());
            var before = new HashSet<string>(fs.SimulationObjects.Keys);
            var target = (object)fs;
            var method = FindAdd(target.GetType());
            if (method == null)
            {
                var type = fs.GetType();
                target = type.GetField("FormSurface")?.GetValue(fs) ?? type.GetProperty("FormSurface")?.GetValue(fs);
                method = target == null ? null : FindAdd(target.GetType());
            }
            if (method == null) throw new InvalidOperationException("this interface cannot add extension unit operations from the 3D view");
            var args = method.GetParameters().Select(p =>
            {
                switch (p.Name)
                {
                    case "type": return (object)ObjectType.External;
                    case "x": return x;
                    case "y": return y;
                    case "tag": return tag ?? "";
                    case "id": return "";
                    case "uoobj": return instance;
                    default: return p.HasDefaultValue ? p.DefaultValue : (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null);
                }
            }).ToArray();
            method.Invoke(target, args);
            return fs.SimulationObjects.Values.FirstOrDefault(o => !before.Contains(o.Name));
        }

        static System.Reflection.MethodInfo FindAdd(Type t) => t.GetMethods().FirstOrDefault(mi =>
            mi.Name == "AddObjectToSurface" && mi.GetParameters().Any(p => p.Name == "uoobj"));

        JObject Add(JObject m)
        {
            var requested = (string)m["objectType"] ?? "";
            if (SiteLayout.IsSite(requested)) return AddSite(requested.Substring(SiteLayout.Prefix.Length), m);
            ObjectType type;
            var external = (string)m["external"];
            if (external != null) type = ObjectType.External;
            else if (!Enum.TryParse((string)m["objectType"] ?? "", true, out type) || type == ObjectType.Nenhum || type == ObjectType.External)
                return Error("unknown object type '" + m["objectType"] + "'", SceneProtocol.Add);

            var x = (double?)m["x"] ?? 0;
            var y = (double?)m["y"] ?? 0;
            var z = (double?)m["z"] ?? 0;
            // a logical block dropped on the ground floats at the badge height
            if (SceneBuilder.Floats(type) && z == 0) z = SceneBuilder.BadgeHeight;
            double cx, cy;
            if (model != null) SceneBuilder.ToCanvas(model, x, y, out cx, out cy);
            else { cx = 100; cy = 100; }
            FreeSpot(ref cx, ref cy);

            Remember(true);
            var obj = external != null
                ? AddExternal(external, (int)Math.Round(cx), (int)Math.Round(cy), (string)m["tag"])
                : fs.AddObject(type, (int)Math.Round(cx), (int)Math.Round(cy), (string)m["tag"] ?? "");
            if (obj == null) return Error("DWSIM did not add a " + type, SceneProtocol.Add);

            if (m["z"] != null || m["rot"] != null || m["x"] != null)
            {
                var layout = PlantLayout.Load(fs);
                layout[obj.Name] = new Placement { X = Math.Round(x), Y = Math.Round(y), Z = Math.Round(z), Rot = QuarterTurn((double?)m["rot"] ?? 0) };
                PlantLayout.Save(fs, layout);
            }
            var answer = SceneProtocol.Message(SceneProtocol.Added);
            answer["id"] = obj.Name;
            answer["tag"] = obj.GraphicObject?.Tag;
            return answer;
        }

        JObject Connect(JObject m)
        {
            var from = Graphic((string)m["from"]);
            var to = Graphic((string)m["to"]);
            var fromPort = (int?)m["fromPort"];
            var toPort = (int?)m["toPort"];
            if (m["from"] != null && from == null || m["to"] != null && to == null || from == null && to == null)
                return Error("the ends must be objects of the flowsheet", SceneProtocol.Connect);
            if (from == to) return Error("an object cannot connect to itself", SceneProtocol.Connect);
            if ((bool?)m["energy"] == true) return ConnectEnergy(from, fromPort, to, toPort, (string)m["tag"]);
            if (from == null || to == null) return Open(from, fromPort, to, toPort, (string)m["tag"]);

            var fromStream = IsStream(from);
            var toStream = IsStream(to);
            Remember(true);
            Snapshot(SnapshotType.ObjectAddedOrRemoved);

            if (fromStream || toStream)
            {
                var fp = fromPort ?? FreePort(from.OutputConnectors);
                var tp = toPort ?? FreePort(to.InputConnectors);
                if (fp < 0) return Error(from.Tag + " has no free outlet", SceneProtocol.Connect);
                if (tp < 0) return Error(to.Tag + " has no free inlet", SceneProtocol.Connect);
                fs.ConnectObjects(from, to, fp, tp);
                return null;
            }

            // two unit operations: a new material stream between them, as the 2D drawing does
            var outlet = fromPort ?? FreePort(from.OutputConnectors);
            var inlet = toPort ?? FreePort(to.InputConnectors);
            if (outlet < 0) return Error(from.Tag + " has no free outlet", SceneProtocol.Connect);
            if (inlet < 0) return Error(to.Tag + " has no free inlet", SceneProtocol.Connect);
            var sx = (from.X + from.Width / 2.0 + to.X + to.Width / 2.0) / 2 - 10;
            var sy = (from.Y + from.Height / 2.0 + to.Y + to.Height / 2.0) / 2 - 10;
            var stream = fs.AddObject(ObjectType.MaterialStream, (int)Math.Round(sx), (int)Math.Round(sy), (string)m["tag"] ?? "");
            if (stream?.GraphicObject == null) return Error("DWSIM did not add the stream", SceneProtocol.Connect);
            fs.ConnectObjects(from, stream.GraphicObject, outlet, 0);
            fs.ConnectObjects(stream.GraphicObject, to, 0, inlet);
            var answer = SceneProtocol.Message(SceneProtocol.Added);
            answer["id"] = stream.Name;
            answer["tag"] = stream.GraphicObject.Tag;
            return answer;
        }

        /// <summary>
        /// A stream from a nozzle to the battery limit: a product stream on an outlet (from), or a
        /// feed stream on an inlet (to), drawn next to the icon in 2D.
        /// </summary>
        JObject Open(IGraphicObject from, int? fromPort, IGraphicObject to, int? toPort, string tag)
        {
            var unit = from ?? to;
            if (IsStream(unit)) return Error("a stream needs a unit operation at its other end", SceneProtocol.Connect);
            var list = from != null ? from.OutputConnectors : to.InputConnectors;
            var port = (from != null ? fromPort : toPort) ?? FreePort(list);
            if (port < 0) return Error(unit.Tag + (from != null ? " has no free outlet" : " has no free inlet"), SceneProtocol.Connect);
            if (port >= list.Count || list[port].IsAttached) return Error("that nozzle of " + unit.Tag + " is taken", SceneProtocol.Connect);
            double sx = from != null ? unit.X + unit.Width + 40 : unit.X - 60;
            double sy = unit.Y + unit.Height / 2.0 - 10;
            FreeSpot(ref sx, ref sy);
            Remember(true);
            Snapshot(SnapshotType.ObjectAddedOrRemoved);
            var stream = fs.AddObject(ObjectType.MaterialStream, (int)Math.Round(sx), (int)Math.Round(sy), tag ?? "");
            if (stream?.GraphicObject == null) return Error("DWSIM did not add the stream", SceneProtocol.Connect);
            if (from != null) fs.ConnectObjects(from, stream.GraphicObject, port, 0);
            else fs.ConnectObjects(stream.GraphicObject, to, 0, port);
            var answer = SceneProtocol.Message(SceneProtocol.Added);
            answer["id"] = stream.Name;
            answer["tag"] = stream.GraphicObject.Tag;
            return answer;
        }

        /// <summary>
        /// An energy stream: from an energy outlet of a unit (an outlet connector of the energy kind,
        /// or its energy connector, port -1) to an energy inlet of another, or from either to the
        /// battery limit. DWSIM's own connection rules decide which units give or take energy.
        /// </summary>
        JObject ConnectEnergy(IGraphicObject from, int? fromPort, IGraphicObject to, int? toPort, string tag)
        {
            if (from != null && IsStream(from) || to != null && IsStream(to)) return Error("an energy stream joins unit operations", SceneProtocol.Connect);
            if (from != null)
            {
                var fp = fromPort ?? -1;
                var cp = fp < 0 ? from.EnergyConnector : (fp < from.OutputConnectors.Count ? from.OutputConnectors[fp] : null);
                if (cp == null || cp.IsAttached) return Error(from.Tag + " has no free energy outlet there", SceneProtocol.Connect);
            }
            if (to != null)
            {
                var tp = toPort ?? FreePort(to.InputConnectors, true);
                if (tp < 0 || tp >= to.InputConnectors.Count || to.InputConnectors[tp].IsAttached) return Error(to.Tag + " has no free energy inlet there", SceneProtocol.Connect);
                toPort = tp;
            }
            var a = from ?? to;
            var b = to ?? from;
            double sx = (a.X + a.Width / 2.0 + b.X + b.Width / 2.0) / 2 - 10;
            double sy = Math.Max(a.Y + a.Height, b.Y + b.Height) + 30;
            if (from == null) sx = to.X - 60;
            if (to == null) sx = from.X + from.Width + 40;
            FreeSpot(ref sx, ref sy);
            Remember(true);
            Snapshot(SnapshotType.ObjectAddedOrRemoved);
            var es = fs.AddObject(ObjectType.EnergyStream, (int)Math.Round(sx), (int)Math.Round(sy), tag ?? "");
            if (es?.GraphicObject == null) return Error("DWSIM did not add the energy stream", SceneProtocol.Connect);
            try
            {
                if (from != null) fs.ConnectObjects(from, es.GraphicObject, fromPort ?? -1, 0);
                if (to != null) fs.ConnectObjects(es.GraphicObject, to, 0, toPort.Value);
            }
            catch (Exception ex)
            {
                // DWSIM refused one end: take the stream back out, as nothing was made
                try { fs.DeleteSelectedObject(this, EventArgs.Empty, es.GraphicObject, false, false); } catch (Exception) { }
                undo.RemoveAt(undo.Count - 1);
                return Error(ex.InnerException?.Message ?? ex.Message, SceneProtocol.Connect);
            }
            var answer = SceneProtocol.Message(SceneProtocol.Added);
            answer["id"] = es.Name;
            answer["tag"] = es.GraphicObject.Tag;
            return answer;
        }

        JObject Disconnect(JObject m)
        {
            var from = Graphic((string)m["from"]);
            var to = Graphic((string)m["to"]);
            if (from == null || to == null) return Error("both ends must be objects of the flowsheet", SceneProtocol.Disconnect);
            Remember(true);
            Snapshot(SnapshotType.ObjectAddedOrRemoved);
            fs.DisconnectObjects(from, to);
            return null;
        }

        JObject Delete(JObject m)
        {
            var ids = (m["ids"] as JArray)?.Select(t => (string)t).ToList() ?? new List<string>();
            var siteIds = ids.Where(SiteLayout.IsSite).ToList();
            if (siteIds.Count > 0)
            {
                var site = SiteLayout.Load(fs);
                var gone = siteIds.Where(id => !site.Any(b => b.Id == id)).ToList();
                if (gone.Count > 0) return Error("there is no building '" + string.Join("', '", gone) + "'", SceneProtocol.Delete);
                Remember(ids.Count > siteIds.Count);
                site.RemoveAll(b => siteIds.Contains(b.Id));
                SiteLayout.Save(fs, site);
                ids = ids.Where(id => !SiteLayout.IsSite(id)).ToList();
                if (ids.Count == 0) return null;
                foreach (var id in ids)
                {
                    var go = Graphic(id);
                    if (go != null) fs.DeleteSelectedObject(this, EventArgs.Empty, go, false, false);
                }
                var lay = PlantLayout.Load(fs);
                if (ids.Count(id => lay.Remove(id)) > 0) PlantLayout.Save(fs, lay);
                return null;
            }
            var missing = ids.Where(id => Graphic(id) == null).ToList();
            if (missing.Count > 0) return Error("there is no object '" + string.Join("', '", missing) + "'", SceneProtocol.Delete);
            if (ids.Count == 0) return Error("nothing to delete", SceneProtocol.Delete);
            Remember(true);
            foreach (var id in ids)
            {
                var go = Graphic(id);
                if (go != null) fs.DeleteSelectedObject(this, EventArgs.Empty, go, false, false);
            }
            var layout = PlantLayout.Load(fs);
            if (ids.Count(id => layout.Remove(id)) > 0) PlantLayout.Save(fs, layout);
            return null;
        }

        JObject Rename(JObject m)
        {
            var go = Graphic((string)m["id"]);
            var tag = ((string)m["tag"] ?? "").Trim();
            if (go == null) return Error("there is no object '" + m["id"] + "'", SceneProtocol.Rename);
            if (tag.Length == 0) return Error("a tag cannot be empty", SceneProtocol.Rename);
            if (fs.GraphicObjects.Values.Any(g => g != go && g != null && !g.IsConnector && string.Equals(g.Tag, tag, StringComparison.Ordinal)))
                return Error("another object is already called " + tag, SceneProtocol.Rename);
            Remember(true);
            Snapshot(SnapshotType.ObjectData, go.Owner);
            go.Tag = tag;
            return null;
        }

        /// <summary>A building of the site (a control room): no object of the simulation, a place in Plant3DSite.</summary>
        JObject AddSite(string kind, JObject m)
        {
            if (!SiteLayout.Kinds.Any(k => k.Kind == kind)) return Error("unknown building '" + kind + "'", SceneProtocol.Add);
            double x, y;
            if (!Finite(m["x"] ?? 0, out x) || !Finite(m["y"] ?? 0, out y)) return Error("the place is not a number", SceneProtocol.Add);
            var site = SiteLayout.Load(fs);
            Remember();
            var id = SiteLayout.NewId(site, kind);
            site.Add(new SiteLayout.Building { Id = id, Kind = kind, X = Math.Round(x), Y = Math.Round(y), Z = 0, Rot = QuarterTurn((double?)m["rot"] ?? 0) });
            SiteLayout.Save(fs, site);
            var answer = SceneProtocol.Message(SceneProtocol.Added);
            answer["id"] = id;
            answer["tag"] = SiteLayout.NameOf(kind);
            return answer;
        }

        /// <summary>The items of the scene the flyby visits, in process order.</summary>
        JObject FlybyPlan()
        {
            var shown = new HashSet<string>((scene?["items"] as JArray ?? new JArray()).OfType<JObject>()
                .Where(i => (bool?)i["badge"] != true && (bool?)i["inst"] != true).Select(i => (string)i["id"]));
            var plan = SceneProtocol.Message(SceneProtocol.FlybyPlan);
            plan["ids"] = new JArray(ProcessOrder.Of(fs, shown));
            return plan;
        }

        JObject Edit(JObject m)
        {
            var id = (string)m["id"];
            if (SiteLayout.IsSite(id)) return Error("a building of the site has no editor", SceneProtocol.Edit);
            if (id == null || !fs.SimulationObjects.ContainsKey(id)) return Error("there is no object '" + id + "'", SceneProtocol.Edit);
            var handler = EditHandler;
            if (handler == null || !handler(id)) return Error("DWSIM cannot open this editor from the 3D view", SceneProtocol.Edit);
            return null;
        }

        JObject Select(JObject m)
        {
            var ids = (m["ids"] as JArray)?.Select(t => (string)t).Where(id => id != null && fs.GraphicObjects.ContainsKey(id)).ToList() ?? new List<string>();
            Selection.Set(fs, ids);
            selection = string.Join("\n", Selection.Get(fs));
            return null;
        }

        void Captured(JObject m)
        {
            var id = (string)m["ref"];
            TaskCompletionSource<byte[]> tcs;
            lock (captures)
            {
                if (id == null || !captures.TryGetValue(id, out tcs)) return;
                captures.Remove(id);
            }
            try { tcs.TrySetResult(Convert.FromBase64String((string)m["png"] ?? "")); }
            catch (FormatException ex) { tcs.TrySetException(ex); }
        }

        #endregion

        #region Helpers

        IGraphicObject Graphic(string id)
        {
            IGraphicObject go;
            return id != null && fs.GraphicObjects.TryGetValue(id, out go) && go != null && !go.IsConnector ? go : null;
        }

        static bool IsStream(IGraphicObject go) => go.ObjectType == ObjectType.MaterialStream || go.ObjectType == ObjectType.EnergyStream;

        static int FreePort(IEnumerable<IConnectionPoint> ports, bool energy = false)
        {
            if (ports == null) return -1;
            var k = 0;
            foreach (var cp in ports)
            {
                if (cp != null && !cp.IsAttached && (cp.Type == DWSIM.Interfaces.Enums.GraphicObjects.ConType.ConEn) == energy) return k;
                k++;
            }
            return -1;
        }

        /// <summary>Moves a canvas point right until no icon covers it, so a new object does not land on another one.</summary>
        void FreeSpot(ref double cx, ref double cy)
        {
            var boxes = fs.GraphicObjects.Values.Where(g => g != null && !g.IsConnector)
                .Select(g => new { g.X, g.Y, g.Width, g.Height }).ToList();
            for (var tries = 0; tries < 200; tries++)
            {
                var x = cx;
                var y = cy;
                if (!boxes.Any(b => x + 20 > b.X - 10 && x - 20 < b.X + b.Width + 10 && y + 20 > b.Y - 10 && y - 20 < b.Y + b.Height + 10)) return;
                cx += 40;
            }
        }

        static int QuarterTurn(double degrees)
        {
            var q = (int)Math.Round(degrees / 90.0) * 90;
            return ((q % 360) + 360) % 360;
        }

        void Snapshot(SnapshotType type, ISimulationObject obj = null)
        {
            try { fs.RegisterSnapshot(type, obj); }
            catch (Exception) { /* a headless flowsheet keeps no undo */ }
        }

        static JObject Error(string message, string forType)
        {
            var e = SceneProtocol.Message(SceneProtocol.Error);
            e["message"] = message;
            if (forType != null) e["for"] = forType;
            return e;
        }

        /// <summary>
        /// Runs code on the interface thread and waits for it. A headless flowsheet runs it in place;
        /// the desktop ones post it, so the link waits on an event.
        /// </summary>
        void OnUi(Action act)
        {
            Exception error = null;
            using (var done = new ManualResetEventSlim(false))
            {
                fs.RunCodeOnUIThread(() =>
                {
                    try { act(); }
                    catch (Exception ex) { error = ex; }
                    finally { done.Set(); }
                });
                if (!done.Wait(UiTimeout)) throw new TimeoutException("the interface thread did not answer");
            }
            if (error != null) throw new InvalidOperationException(error.Message, error);
        }

        void Report(string text)
        {
            try { Log?.Invoke(text); } catch (Exception) { }
        }

        static string Shorten(string s) => s.Length > 80 ? s.Substring(0, 80) + "..." : s;

        static string NewToken()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return string.Concat(bytes.Select(b => b.ToString("x2")));
        }

        #endregion

        /// <summary>
        /// The selection of the 2D drawing, read and set through the drawing surface the flowsheet
        /// hands out (its SelectedObjects and SelectedObject members). A flowsheet without a surface
        /// has an empty selection.
        /// </summary>
        public static class Selection
        {
            public static List<string> Get(IFlowsheet fs)
            {
                var result = new List<string>();
                var surface = Surface(fs);
                var dict = surface?.GetType().GetProperty("SelectedObjects")?.GetValue(surface) as IDictionary;
                if (dict == null) return result;
                foreach (var v in dict.Values)
                {
                    var go = v as IGraphicObject;
                    if (go != null && !go.IsConnector && go.Name != null) result.Add(go.Name);
                }
                result.Sort(StringComparer.Ordinal);
                return result;
            }

            public static void Set(IFlowsheet fs, IList<string> ids)
            {
                var surface = Surface(fs);
                if (surface == null) return;
                var type = surface.GetType();
                var dict = type.GetProperty("SelectedObjects")?.GetValue(surface) as IDictionary;
                if (dict == null) return;
                dict.Clear();
                IGraphicObject first = null;
                foreach (var id in ids)
                {
                    IGraphicObject go;
                    if (!fs.GraphicObjects.TryGetValue(id, out go) || go == null) continue;
                    dict[go.Name] = go;
                    if (first == null) first = go;
                }
                var single = type.GetProperty("SelectedObject");
                if (single != null && single.CanWrite) single.SetValue(surface, first);
                try { fs.UpdateInterface(); } catch (Exception) { }
            }

            static object Surface(IFlowsheet fs)
            {
                try { return fs.GetSurface(); }
                catch (Exception) { return null; }
            }
        }
    }
}
