using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DWSIM.Extensions.SolidCraft.Viewer3D;
using DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport;
using DWSIM.Automation.FluentAPI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Plant models that come as one continuous surface (a scan, or a mesh a program fused and
    /// remeshed): a synthetic plant is drawn as a distance field and meshed by marching
    /// tetrahedra, which welds every object to the slab and to each other in one surface.
    /// </summary>
    [TestFixture]
    public class ModelSurfaceTests
    {
        string _dir;
        string _cacheRoot;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dwsim-model-surface-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _cacheRoot = ModelImporter.CacheRoot;
            ModelImporter.CacheRoot = Path.Combine(_dir, "cache");
        }

        [TearDown]
        public void TearDown()
        {
            ModelImporter.CacheRoot = _cacheRoot;
            try { Directory.Delete(_dir, true); } catch { }
        }

        // ---- the distance field of the plant, metres, Z up ----

        static double Box(double x, double y, double z, double[] min, double[] max)
        {
            double cx = (min[0] + max[0]) / 2, cy = (min[1] + max[1]) / 2, cz = (min[2] + max[2]) / 2;
            double qx = Math.Abs(x - cx) - (max[0] - min[0]) / 2, qy = Math.Abs(y - cy) - (max[1] - min[1]) / 2, qz = Math.Abs(z - cz) - (max[2] - min[2]) / 2;
            double ox = Math.Max(qx, 0), oy = Math.Max(qy, 0), oz = Math.Max(qz, 0);
            return Math.Sqrt(ox * ox + oy * oy + oz * oz) + Math.Min(Math.Max(qx, Math.Max(qy, qz)), 0);
        }

        /// <summary>A closed cylinder along an axis (0 x, 1 y, 2 z), from a to b on it, round the point (u, v) of the other two.</summary>
        static double Cylinder(double x, double y, double z, int axis, double a, double b, double u, double v, double r)
        {
            var p = new[] { x, y, z };
            var along = p[axis];
            var o = new double[2];
            var k = 0;
            for (var d = 0; d < 3; d++) if (d != axis) o[k++] = p[d];
            var dr = Math.Sqrt((o[0] - u) * (o[0] - u) + (o[1] - v) * (o[1] - v)) - r;
            var dh = Math.Abs(along - (a + b) / 2) - (b - a) / 2;
            double ox = Math.Max(dr, 0), oy = Math.Max(dh, 0);
            return Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(dr, dh), 0);
        }

        /// <summary>
        /// The plant: a slab 16 m by 12 m; a vertical drum V1 of 2 m by 5 m on it; a horizontal drum
        /// V2 of 1.6 m by 4 m on two saddles; a line of 0.5 m from the side of V1 into the top of V2;
        /// a pump skid of 1.2 m.
        /// </summary>
        static double Plant(double x, double y, double z)
        {
            var d = Box(x, y, z, new[] { -8.0, -6, -0.3 }, new[] { 8.0, 6, 0 });
            d = Math.Min(d, Cylinder(x, y, z, 2, 0, 5, -4, 0, 1.0));
            d = Math.Min(d, Cylinder(x, y, z, 0, 1.5, 5.5, 2.5, 1.8, 0.8));
            d = Math.Min(d, Box(x, y, z, new[] { 2.1, 1.9, 0 }, new[] { 2.5, 3.1, 1.4 }));
            d = Math.Min(d, Box(x, y, z, new[] { 4.5, 1.9, 0 }, new[] { 4.9, 3.1, 1.4 }));
            d = Math.Min(d, Cylinder(x, y, z, 0, -4, 3.5, 0, 3, 0.25));
            d = Math.Min(d, Cylinder(x, y, z, 1, 0, 2.5, 3.5, 3, 0.25));
            d = Math.Min(d, Cylinder(x, y, z, 2, 1.8, 3, 3.5, 2.5, 0.25));
            d = Math.Min(d, Box(x, y, z, new[] { -1.0, -4, 0 }, new[] { 0.2, -3.4, 0.8 }));
            return d;
        }

        static readonly int[][] Kuhn = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };

        /// <summary>
        /// The surface where the field is zero, by marching tetrahedra on a grid of the step h (the
        /// six tetrahedra of each cube along its long diagonal, so that the neighbours' faces
        /// match): one welded surface, its faces turned outward. The noise moves each vertex up to
        /// that far, as a scan would.
        /// </summary>
        static ImportedModel Mesh(Func<double, double, double, double> field, double[] min, double[] max, double h, double noise = 0, int seed = 1)
        {
            int nx = (int)Math.Ceiling((max[0] - min[0]) / h), ny = (int)Math.Ceiling((max[1] - min[1]) / h), nz = (int)Math.Ceiling((max[2] - min[2]) / h);
            Func<int, int, int, int> id = (i, j, k) => (k * (ny + 1) + j) * (nx + 1) + i;
            var f = new double[(nx + 1) * (ny + 1) * (nz + 1)];
            for (var k = 0; k <= nz; k++)
                for (var j = 0; j <= ny; j++)
                    for (var i = 0; i <= nx; i++)
                        f[id(i, j, k)] = field(min[0] + i * h, min[1] + j * h, min[2] + k * h);
            Func<int, double[]> at = g =>
            {
                int i = g % (nx + 1), j = g / (nx + 1) % (ny + 1), k = g / ((nx + 1) * (ny + 1));
                return new[] { min[0] + i * h, min[1] + j * h, min[2] + k * h };
            };
            var pos = new List<float>();
            var idx = new List<int>();
            var onEdge = new Dictionary<long, int>();
            Func<int, int, int> vertex = (a, b) =>
            {
                var key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                int vi;
                if (onEdge.TryGetValue(key, out vi)) return vi;
                double fa = f[a], fb = f[b];
                var t = fa / (fa - fb);
                double[] pa = at(a), pb = at(b);
                vi = pos.Count / 3;
                for (var d = 0; d < 3; d++) pos.Add((float)(pa[d] + t * (pb[d] - pa[d])));
                onEdge[key] = vi;
                return vi;
            };
            for (var k = 0; k < nz; k++)
                for (var j = 0; j < ny; j++)
                    for (var i = 0; i < nx; i++)
                    {
                        var corners = new int[8];
                        var inside = 0;
                        for (var c = 0; c < 8; c++)
                        {
                            corners[c] = id(i + (c & 1), j + (c >> 1 & 1), k + (c >> 2 & 1));
                            if (f[corners[c]] < 0) inside++;
                        }
                        if (inside == 0 || inside == 8) continue;
                        foreach (var perm in Kuhn)
                        {
                            var g = new int[4];
                            var cell = new[] { i, j, k };
                            g[0] = id(cell[0], cell[1], cell[2]);
                            for (var s = 0; s < 3; s++)
                            {
                                cell[perm[s]]++;
                                g[s + 1] = id(cell[0], cell[1], cell[2]);
                            }
                            var ins = g.Where(x => f[x] < 0).ToList();
                            var outs = g.Where(x => f[x] >= 0).ToList();
                            if (ins.Count == 0 || outs.Count == 0) continue;
                            var tris = new List<int[]>();
                            if (ins.Count == 1) tris.Add(outs.Select(o => vertex(ins[0], o)).ToArray());
                            else if (ins.Count == 3) tris.Add(ins.Select(o => vertex(o, outs[0])).ToArray());
                            else
                            {
                                int q0 = vertex(ins[0], outs[0]), q1 = vertex(ins[0], outs[1]), q2 = vertex(ins[1], outs[1]), q3 = vertex(ins[1], outs[0]);
                                tris.Add(new[] { q0, q1, q2 });
                                tris.Add(new[] { q0, q2, q3 });
                            }
                            // outward: from the inside corners to the outside ones
                            var dir = new double[3];
                            foreach (var o in outs) { var p = at(o); for (var d = 0; d < 3; d++) dir[d] += p[d] / outs.Count; }
                            foreach (var o in ins) { var p = at(o); for (var d = 0; d < 3; d++) dir[d] -= p[d] / ins.Count; }
                            foreach (var t in tris)
                            {
                                var nrm = Normal(pos, t[0], t[1], t[2]);
                                if (nrm[0] * dir[0] + nrm[1] * dir[1] + nrm[2] * dir[2] < 0) { var x = t[1]; t[1] = t[2]; t[2] = x; }
                                idx.AddRange(t);
                            }
                        }
                    }
            if (noise > 0)
            {
                var rnd = new Random(seed);
                for (var q = 0; q < pos.Count; q++) pos[q] += (float)((rnd.NextDouble() * 2 - 1) * noise);
            }
            var m = new ImportedModel { Format = "test", Up = UpAxis.Z, Units = ModelUnits.Meters };
            m.Parts.Add(new MeshPart { Name = "scan", Positions = pos.ToArray(), Indices = idx.ToArray() });
            return m;
        }

        static double[] Normal(List<float> p, int a, int b, int c)
        {
            double ux = p[3 * b] - p[3 * a], uy = p[3 * b + 1] - p[3 * a + 1], uz = p[3 * b + 2] - p[3 * a + 2];
            double wx = p[3 * c] - p[3 * a], wy = p[3 * c + 1] - p[3 * a + 1], wz = p[3 * c + 2] - p[3 * a + 2];
            return new[] { uy * wz - uz * wy, uz * wx - ux * wz, ux * wy - uy * wx };
        }

        internal static ImportedModel FusedPlant(double noise = 0) =>
            Mesh(Plant, new[] { -8.31, -6.31, -0.51 }, new[] { 8.31, 6.31, 5.31 }, 0.12, noise);

        static SegmentGroup Near(List<SegmentGroup> groups, double x, double y) =>
            groups.Where(g => g.Role == "equipment")
                .OrderBy(g => Math.Pow(g.Center[0] - x, 2) + Math.Pow(g.Center[1] - y, 2)).FirstOrDefault();

        ModelImportResult Import(ImportedModel model, string name)
        {
            var glb = Path.Combine(_dir, name);
            GlbWriter.WriteParts(model, glb);
            return ModelImporter.Import(glb);
        }

        [TestCase(0.0)]
        [TestCase(0.005)]
        [TestCase(0.01)]
        public void AFusedPlantSplitsIntoTheFloorTheVesselsThePipeAndThePump(double noise)
        {
            var model = FusedPlant(noise);
            var copy = new ImportedModel();
            copy.Parts.Add(new MeshPart { Name = "scan", Positions = (float[])model.Parts[0].Positions.Clone(), Indices = model.Parts[0].Indices });
            Assert.That(ModelGeometry.Islands(copy).Count(i => i.Area > 1), Is.EqualTo(1), "the plant is one surface, as a scan gives it");

            var r = Import(model, "fused.glb");
            // the first import stands the slab on the ground
            var dz = r.Reference.Place.Offset[2];
            Assert.That(dz, Is.EqualTo(0.3).Within(0.02));
            Assert.That(r.Warnings.Any(w => w.Contains("continuous surface")), Is.True);
            var floor = r.Islands.OrderByDescending(i => i.Area).First();
            Assert.That(floor.Shape.Class, Is.EqualTo(ShapeClass.Ground), "the slab is the ground");

            var groups = ModelSegmentation.Groups(r);
            var v1 = Near(groups, -4, 0);
            var v2 = Near(groups, 3.5, 2.5);
            var pump = Near(groups, -0.4, -3.7);
            Assert.That(v1, Is.Not.Null);
            Assert.That(v2, Is.Not.Null);
            Assert.That(new[] { v1, v2, pump }.Distinct().Count(), Is.EqualTo(3), "three pieces of equipment, apart");
            Assert.That(v1.Body, Is.EqualTo(ShapeClass.VerticalVessel));
            Assert.That(v1.Diameter, Is.EqualTo(2.0).Within(0.2));
            Assert.That(v1.Length, Is.EqualTo(5.0).Within(0.4));
            if (noise == 0)
            {
                Assert.That(v2.Body, Is.EqualTo(ShapeClass.HorizontalVessel));
                Assert.That(v2.Diameter, Is.EqualTo(1.6).Within(0.2));
                Assert.That(v2.Length, Is.EqualTo(4.0).Within(0.4));
            }
            else
            {
                // the noise hides the shallow crease under the saddles: they stay in the drum's
                // region, which the review cuts off with a plane
                Assert.That(v2.Max[0] - v2.Min[0], Is.EqualTo(4.0).Within(0.4));
            }
            Assert.That(v2.Min[2], Is.LessThan(dz + 0.1), "the saddles go with the drum");
            Assert.That(Math.Abs(pump.Center[0] + 0.4) < 0.3 && Math.Abs(pump.Center[1] + 3.7) < 0.3, Is.True, "the pump skid is a machine of its own");

            var line = groups.Where(g => g.Role == "piping").OrderByDescending(g => g.Islands.Sum(i => r.Islands[i].Area)).FirstOrDefault();
            Assert.That(line, Is.Not.Null, "the line is piping");
            Assert.That(v1.Neighbours, Does.Contain(v2.Id), "the line joins the two drums");
        }

        [Test]
        public void APlaneCutsAnIslandAndTheCutIsMadeAgainAtTheNextImport()
        {
            var model = FusedPlant();
            var glb = Path.Combine(_dir, "cut.glb");
            GlbWriter.WriteParts(model, glb);
            var r = ModelImporter.Import(glb);
            var v1 = Near(ModelSegmentation.Groups(r), -4, 0);
            var body = v1.Islands.OrderByDescending(i => r.Islands[i].Area).First();
            var count = r.Islands.Count;
            var dz = r.Reference.Place.Offset[2];

            var reference = r.Reference;
            reference.Cuts = new List<SurfaceCut> { new SurfaceCut { Islands = new List<int> { body }, Point = new[] { -4.0, 0, 3.4 + dz }, Normal = new[] { 0.0, 0, 1 } } };
            var again = ModelImporter.Import(glb, reference);
            Assert.That(again.FromCache, Is.False, "a cut makes another cache");
            Assert.That(again.Islands.Count, Is.EqualTo(count + 1), "the cut makes one island more, at the end");
            var low = again.Islands[body];
            var high = again.Islands[count];
            Assert.That(low.Max[2], Is.LessThan(3.5 + dz));
            Assert.That(high.Min[2], Is.GreaterThan(3.3 + dz));
            Assert.That(low.Area + high.Area, Is.EqualTo(r.Islands[body].Area).Within(1e-3 * r.Islands[body].Area));
            for (var i = 0; i < count; i++)
                if (i != body) Assert.That(again.Islands[i].Area, Is.EqualTo(r.Islands[i].Area).Within(1e-6), "the other islands keep their numbers");

            var cached = ModelImporter.Import(glb, reference);
            Assert.That(cached.FromCache, Is.True);
            Assert.That(cached.Islands.Count, Is.EqualTo(count + 1));
            // the two courses of the shell are still one drum
            var v1again = Near(ModelSegmentation.Groups(cached), -4, 0);
            Assert.That(v1again.Islands, Does.Contain(body).And.Contain(count));
            Assert.That(v1again.Length, Is.EqualTo(5.0).Within(0.4));
        }

        [Test]
        public void TheReviewCutsThroughTheLinkAndUndoes()
        {
            var fs = Flowsheet.Create("cut").WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables);
            fs.AddMaterialStream("1").At(300.Kelvin(), 101325.0.Pascal()).WithMassFlow(1.KgPerSecond());
            var glb = Path.Combine(_dir, "link.glb");
            GlbWriter.WriteParts(FusedPlant(), glb);
            var link = new FlowsheetLink(fs.Inner);
            var sent = new List<JObject>();
            link.MessageSent += m => { lock (sent) sent.Add(m); };
            link.Handle(new JObject { ["type"] = SceneProtocol.ModelImport, ["path"] = glb });
            var deadline = DateTime.Now.AddSeconds(120);
            while (DateTime.Now < deadline && link.ImportingModel) System.Threading.Thread.Sleep(50);
            Assert.That(ModelStore.Load(fs.Inner), Is.Not.Null);
            link.ModelEditsInline = true;

            var index = ModelImporter.Cached(ModelStore.Load(fs.Inner).Model);
            var count = index.Islands.Count;
            var v1 = Near(ModelSegmentation.Groups(index), -4, 0);
            var body = v1.Islands.OrderByDescending(i => index.Islands[i].Area).First();
            var answer = link.Handle(new JObject
            {
                ["type"] = SceneProtocol.ModelCut,
                ["islands"] = new JArray(body),
                ["point"] = new JArray(-4.0, 0.0, 2.5),
                ["normal"] = new JArray(0.0, 0.0, 1.0)
            });
            Assert.That((string)answer["type"], Is.EqualTo(SceneProtocol.Ack));
            List<JObject> copy;
            lock (sent) copy = sent.ToList();
            var error = copy.LastOrDefault(m => (string)m["type"] == SceneProtocol.Error);
            Assert.That(error, Is.Null, (string)error?["message"]);
            var payload = copy.Last(m => (string)m["type"] == SceneProtocol.ModelGroups);
            Assert.That(payload["groups"].SelectMany(g => g["islands"]).Select(t => (int)t), Does.Contain(count), "the new island is in the groups");
            var stored = ModelStore.Load(fs.Inner);
            Assert.That(stored.Model.Cuts, Has.Count.EqualTo(1));
            Assert.That(stored.Model.Cuts[0].Islands, Is.EqualTo(new[] { body }));
            link.Sync();
            Assert.That(File.Exists((string)link.Scene["model"]["islands"]), Is.True, "the window gets the islands of the cut model");

            link.Handle(new JObject { ["type"] = SceneProtocol.Undo });
            Assert.That(ModelStore.Load(fs.Inner).Model.Cuts, Is.Null, "the window's undo takes the cut back");

            var refused = link.Handle(new JObject { ["type"] = SceneProtocol.ModelCut, ["islands"] = new JArray(body), ["point"] = new JArray(0.0, 0.0, 0.0) });
            Assert.That((string)refused["type"], Is.EqualTo(SceneProtocol.Error), "a cut without its plane says why");
        }
    }
}
