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
        string fingerprint, selection;
        int nextRef;

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
            JObject delta = null, select = null;
            OnUi(() =>
            {
                lock (gate)
                {
                    var now = FlowsheetFingerprint.Compute(fs);
                    if (scene == null || now != fingerprint)
                    {
                        var m = SceneBuilder.Model(fs);
                        var next = SceneBuilder.FromModel(m, fs);
                        delta = scene == null ? Full(next) : SceneDiff.Delta(scene, next);
                        scene = next;
                        model = m;
                        fingerprint = now;
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
                case SceneProtocol.Add: return Add(m);
                case SceneProtocol.Connect: return Connect(m);
                case SceneProtocol.Disconnect: return Disconnect(m);
                case SceneProtocol.Delete: return Delete(m);
                case SceneProtocol.Rename: return Rename(m);
                case SceneProtocol.Select: return Select(m);
                case SceneProtocol.SceneFull:
                    scene = null;
                    selection = null;
                    return null;
                default: return Error("unknown command '" + type + "'", type);
            }
        }

        JObject Place(JObject m)
        {
            var id = (string)m["id"];
            if (id == null || !fs.SimulationObjects.ContainsKey(id)) return Error("there is no object '" + id + "'", SceneProtocol.Place);
            var layout = PlantLayout.Load(fs);
            layout[id] = new Placement
            {
                X = Math.Round((double?)m["x"] ?? 0),
                Y = Math.Round((double?)m["y"] ?? 0),
                Z = Math.Round((double?)m["z"] ?? 0),
                Rot = QuarterTurn((double?)m["rot"] ?? 0)
            };
            Snapshot(SnapshotType.ObjectLayout);
            PlantLayout.Save(fs, layout);
            return null;
        }

        JObject ResetLayout(JObject m)
        {
            var layout = PlantLayout.Load(fs);
            var ids = m["ids"] as JArray;
            Snapshot(SnapshotType.ObjectLayout);
            if (ids == null) layout.Clear();
            else foreach (var id in ids) layout.Remove((string)id);
            PlantLayout.Save(fs, layout);
            return null;
        }

        JObject Add(JObject m)
        {
            ObjectType type;
            if (!Enum.TryParse((string)m["objectType"] ?? "", true, out type) || type == ObjectType.Nenhum)
                return Error("unknown object type '" + m["objectType"] + "'", SceneProtocol.Add);

            var x = (double?)m["x"] ?? 0;
            var y = (double?)m["y"] ?? 0;
            double cx, cy;
            if (model != null) SceneBuilder.ToCanvas(model, x, y, out cx, out cy);
            else { cx = 100; cy = 100; }
            FreeSpot(ref cx, ref cy);

            var obj = fs.AddObject(type, (int)Math.Round(cx), (int)Math.Round(cy), (string)m["tag"] ?? "");
            if (obj == null) return Error("DWSIM did not add a " + type, SceneProtocol.Add);

            if (m["z"] != null || m["rot"] != null || m["x"] != null)
            {
                var layout = PlantLayout.Load(fs);
                layout[obj.Name] = new Placement { X = Math.Round(x), Y = Math.Round(y), Z = Math.Round((double?)m["z"] ?? 0), Rot = QuarterTurn((double?)m["rot"] ?? 0) };
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
            if (from == null || to == null) return Error("both ends must be objects of the flowsheet", SceneProtocol.Connect);
            if (from == to) return Error("an object cannot connect to itself", SceneProtocol.Connect);

            var fromPort = (int?)m["fromPort"];
            var toPort = (int?)m["toPort"];
            var fromStream = IsStream(from);
            var toStream = IsStream(to);
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

        JObject Disconnect(JObject m)
        {
            var from = Graphic((string)m["from"]);
            var to = Graphic((string)m["to"]);
            if (from == null || to == null) return Error("both ends must be objects of the flowsheet", SceneProtocol.Disconnect);
            Snapshot(SnapshotType.ObjectAddedOrRemoved);
            fs.DisconnectObjects(from, to);
            return null;
        }

        JObject Delete(JObject m)
        {
            var ids = (m["ids"] as JArray)?.Select(t => (string)t).ToList() ?? new List<string>();
            var missing = ids.Where(id => Graphic(id) == null).ToList();
            if (missing.Count > 0) return Error("there is no object '" + string.Join("', '", missing) + "'", SceneProtocol.Delete);
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
            Snapshot(SnapshotType.ObjectData, go.Owner);
            go.Tag = tag;
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

        static int FreePort(IEnumerable<IConnectionPoint> ports)
        {
            if (ports == null) return -1;
            var k = 0;
            foreach (var cp in ports)
            {
                if (cp != null && !cp.IsAttached && cp.Type != DWSIM.Interfaces.Enums.GraphicObjects.ConType.ConEn) return k;
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
