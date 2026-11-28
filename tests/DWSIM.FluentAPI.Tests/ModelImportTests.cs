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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DWSIM.Automation.FluentAPI;
using DWSIM.Extensions.SolidCraft.Viewer3D;
using DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport;
using NUnit.Framework;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// The model import of the 3D window, phase M0: the readers (glTF, GLB, OBJ, STL, PLY, and FBX
    /// through the window when it is there), the unit and axis normalization, the islands of a
    /// model merged into one mesh, the GLB cache and the Plant3DModel key. Every model is made
    /// here: a small plant of a column, a drum, a pump and two pipes that touch them.
    /// </summary>
    [TestFixture]
    public class ModelImportTests
    {
        string _dir;
        string _cacheRoot;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dwsim-model-import-" + Guid.NewGuid().ToString("N"));
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

        const string Png1x1 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

        // ---- the synthetic plant, in metres with Z up ----

        /// <summary>A closed cylinder between two points, as one part.</summary>
        static MeshPart Cylinder(string name, double[] a, double[] b, double r, int material, int n = 16)
        {
            var axis = new[] { b[0] - a[0], b[1] - a[1], b[2] - a[2] };
            var len = Math.Sqrt(axis.Sum(x => x * x));
            var w = axis.Select(x => x / len).ToArray();
            var helper = Math.Abs(w[2]) < 0.9 ? new[] { 0.0, 0, 1 } : new[] { 1.0, 0, 0 };
            var u = Cross(w, helper); var ul = Math.Sqrt(u.Sum(x => x * x)); u = u.Select(x => x / ul).ToArray();
            var v = Cross(w, u);
            var pos = new List<float>();
            var idx = new List<int>();
            for (var end = 0; end < 2; end++)
            {
                var c = end == 0 ? a : b;
                for (var i = 0; i < n; i++)
                {
                    var t = 2 * Math.PI * i / n;
                    for (var k = 0; k < 3; k++) pos.Add((float)(c[k] + r * (Math.Cos(t) * u[k] + Math.Sin(t) * v[k])));
                }
            }
            pos.AddRange(a.Select(x => (float)x));
            pos.AddRange(b.Select(x => (float)x));
            int ca = 2 * n, cb = 2 * n + 1;
            for (var i = 0; i < n; i++)
            {
                int i0 = i, i1 = (i + 1) % n, j0 = n + i, j1 = n + (i + 1) % n;
                idx.AddRange(new[] { i0, i1, j1, i0, j1, j0, ca, i1, i0, cb, j0, j1 });
            }
            return new MeshPart { Name = name, Material = material, Positions = pos.ToArray(), Indices = idx.ToArray() };
        }

        /// <summary>A box as one part, with 24 vertices (4 per face), as CAD writes flat faces.</summary>
        static MeshPart Box(string name, double[] min, double[] max, int material)
        {
            var pos = new List<float>();
            var idx = new List<int>();
            Action<double[][]> face = q =>
            {
                var at = pos.Count / 3;
                foreach (var p in q) pos.AddRange(p.Select(x => (float)x));
                idx.AddRange(new[] { at, at + 1, at + 2, at, at + 2, at + 3 });
            };
            double x0 = min[0], y0 = min[1], z0 = min[2], x1 = max[0], y1 = max[1], z1 = max[2];
            face(new[] { new[] { x0, y0, z0 }, new[] { x0, y1, z0 }, new[] { x1, y1, z0 }, new[] { x1, y0, z0 } });
            face(new[] { new[] { x0, y0, z1 }, new[] { x1, y0, z1 }, new[] { x1, y1, z1 }, new[] { x0, y1, z1 } });
            face(new[] { new[] { x0, y0, z0 }, new[] { x1, y0, z0 }, new[] { x1, y0, z1 }, new[] { x0, y0, z1 } });
            face(new[] { new[] { x0, y1, z0 }, new[] { x0, y1, z1 }, new[] { x1, y1, z1 }, new[] { x1, y1, z0 } });
            face(new[] { new[] { x0, y0, z0 }, new[] { x0, y0, z1 }, new[] { x0, y1, z1 }, new[] { x0, y1, z0 } });
            face(new[] { new[] { x1, y0, z0 }, new[] { x1, y1, z0 }, new[] { x1, y1, z1 }, new[] { x1, y0, z1 } });
            return new MeshPart { Name = name, Material = material, Positions = pos.ToArray(), Indices = idx.ToArray() };
        }

        static double[] Cross(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };

        /// <summary>
        /// The column C-1 (2 m by 20 m), the drum D-1 lying down, the pump P-1 and two pipes from the
        /// column to the drum and from the drum to the pump, which touch the bodies without sharing
        /// a vertex: five solids.
        /// </summary>
        static ImportedModel Plant()
        {
            var m = new ImportedModel { Format = "test", Up = UpAxis.Z, Units = ModelUnits.Meters };
            m.Materials.Add(new ModelMaterial { Name = "steel", Color = new[] { 0.6f, 0.6f, 0.62f, 1f }, Metallic = 0.8f, Roughness = 0.4f });
            m.Materials.Add(new ModelMaterial { Name = "painted", Color = new[] { 0.2f, 0.4f, 0.8f, 1f } });
            m.Parts.Add(Cylinder("C-1", new[] { 0.0, 0, 0 }, new[] { 0.0, 0, 20 }, 1.0, 0));
            m.Parts.Add(Cylinder("D-1", new[] { 8.0, 0, 2 }, new[] { 14.0, 0, 2 }, 1.0, 0));
            m.Parts.Add(Box("P-1", new[] { 18.0, -0.5, 0 }, new[] { 19.5, 0.5, 1 }, 1));
            m.Parts.Add(Cylinder("L-1", new[] { 1.0, 0, 15 }, new[] { 8.0, 0, 2.5 }, 0.1, 0));
            m.Parts.Add(Cylinder("L-2", new[] { 14.0, 0, 1.5 }, new[] { 18.0, 0, 0.5 }, 0.08, 0));
            return m;
        }

        static ImportedModel Copy(ImportedModel m)
        {
            var c = new ImportedModel { Format = m.Format, Up = m.Up, Units = m.Units, Materials = m.Materials };
            foreach (var p in m.Parts)
                c.Parts.Add(new MeshPart { Name = p.Name, Material = p.Material, Positions = (float[])p.Positions.Clone(), UV = (float[])p.UV?.Clone(), Indices = (int[])p.Indices.Clone() });
            return c;
        }

        /// <summary>The model in the frame of a file: scaled, and with Y up when asked (the inverse of the normalization).</summary>
        static ImportedModel ToFileFrame(ImportedModel plant, double unitsPerMetre, bool yUp)
        {
            var m = Copy(plant);
            foreach (var p in m.Parts)
                for (var i = 0; i < p.Positions.Length; i += 3)
                {
                    double x = p.Positions[i], y = p.Positions[i + 1], z = p.Positions[i + 2];
                    if (yUp) { var t = z; z = -y; y = t; }
                    p.Positions[i] = (float)(x * unitsPerMetre); p.Positions[i + 1] = (float)(y * unitsPerMetre); p.Positions[i + 2] = (float)(z * unitsPerMetre);
                }
            return m;
        }

        static string F(double d) => d.ToString("R", CultureInfo.InvariantCulture);

        static void WriteObj(ImportedModel m, string path, bool oneObject)
        {
            var sb = new StringBuilder();
            var mtl = Path.ChangeExtension(path, ".mtl");
            sb.AppendLine("mtllib " + Path.GetFileName(mtl));
            var at = 1;
            if (oneObject) sb.AppendLine("o Plant");
            foreach (var p in m.Parts)
            {
                if (!oneObject) sb.AppendLine("o " + p.Name);
                sb.AppendLine("usemtl " + m.Materials[p.Material].Name);
                for (var i = 0; i < p.Positions.Length; i += 3)
                    sb.AppendLine("v " + F(p.Positions[i]) + " " + F(p.Positions[i + 1]) + " " + F(p.Positions[i + 2]));
                for (var t = 0; t < p.Indices.Length; t += 3)
                    sb.AppendLine("f " + (p.Indices[t] + at) + " " + (p.Indices[t + 1] + at) + " " + (p.Indices[t + 2] + at));
                at += p.VertexCount;
            }
            File.WriteAllText(path, sb.ToString());
            var mt = new StringBuilder();
            foreach (var mat in m.Materials)
            {
                mt.AppendLine("newmtl " + mat.Name);
                mt.AppendLine("Kd " + F(mat.Color[0]) + " " + F(mat.Color[1]) + " " + F(mat.Color[2]));
            }
            File.WriteAllText(mtl, mt.ToString());
        }

        /// <summary>A binary STL: three corners per triangle, no vertex shared.</summary>
        static void WriteStl(ImportedModel m, string path)
        {
            var tris = m.Parts.Sum(p => p.TriangleCount);
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write(new byte[80]);
                w.Write((uint)tris);
                foreach (var p in m.Parts)
                    for (var t = 0; t < p.Indices.Length; t += 3)
                    {
                        w.Write(0f); w.Write(0f); w.Write(0f);
                        for (var k = 0; k < 3; k++)
                        {
                            var i = p.Indices[t + k] * 3;
                            w.Write(p.Positions[i]); w.Write(p.Positions[i + 1]); w.Write(p.Positions[i + 2]);
                        }
                        w.Write((ushort)0);
                    }
            }
        }

        static void WritePly(ImportedModel m, string path, bool binary)
        {
            var verts = m.Parts.Sum(p => p.VertexCount);
            var faces = m.Parts.Sum(p => p.TriangleCount);
            var head = "ply\nformat " + (binary ? "binary_little_endian" : "ascii") + " 1.0\ncomment made by the DWSIM tests\nelement vertex " + verts +
                "\nproperty float x\nproperty float y\nproperty float z\nproperty uchar red\nproperty uchar green\nproperty uchar blue\nelement face " + faces +
                "\nproperty list uchar int vertex_indices\nend_header\n";
            using (var f = File.Create(path))
            {
                var hb = Encoding.ASCII.GetBytes(head);
                f.Write(hb, 0, hb.Length);
                var w = new BinaryWriter(f);
                var text = new StringBuilder();
                foreach (var p in m.Parts)
                    for (var i = 0; i < p.Positions.Length; i += 3)
                        if (binary) { w.Write(p.Positions[i]); w.Write(p.Positions[i + 1]); w.Write(p.Positions[i + 2]); w.Write((byte)200); w.Write((byte)10); w.Write((byte)10); }
                        else text.Append(F(p.Positions[i]) + " " + F(p.Positions[i + 1]) + " " + F(p.Positions[i + 2]) + " 200 10 10\n");
                var at = 0;
                foreach (var p in m.Parts)
                {
                    for (var t = 0; t < p.Indices.Length; t += 3)
                        if (binary) { w.Write((byte)3); w.Write(p.Indices[t] + at); w.Write(p.Indices[t + 1] + at); w.Write(p.Indices[t + 2] + at); }
                        else text.Append("3 " + (p.Indices[t] + at) + " " + (p.Indices[t + 1] + at) + " " + (p.Indices[t + 2] + at) + "\n");
                    at += p.VertexCount;
                }
                w.Flush();
                var tb = Encoding.ASCII.GetBytes(text.ToString());
                f.Write(tb, 0, tb.Length);
            }
        }

        /// <summary>The boxes of the islands, rounded to cm and sorted, to compare with the solids of the plant.</summary>
        static List<string> Boxes(IEnumerable<Island> islands) =>
            islands.Select(i => string.Join(",", i.Min.Concat(i.Max).Select(x => Math.Round(x, 2).ToString("0.00", CultureInfo.InvariantCulture)))).OrderBy(s => s, StringComparer.Ordinal).ToList();

        static List<string> PlantBoxes()
        {
            var plant = Plant();
            return Boxes(ModelGeometry.Islands(plant));
        }

        // ---- the tests ----

        [Test]
        public void EverySolidOfThePlantIsAnIsland()
        {
            var islands = ModelGeometry.Islands(Plant());
            Assert.That(islands.Count, Is.EqualTo(5), "the column, the drum, the pump and the two pipes");
            var column = islands.Single(i => i.Part == 0);
            Assert.That(column.Max[2] - column.Min[2], Is.EqualTo(20).Within(1e-4));
            Assert.That(column.Centroid[2], Is.EqualTo(10).Within(0.01));
            Assert.That(column.Area, Is.EqualTo(2 * Math.PI * 1 * 20 + 2 * Math.PI).Within(4.0), "the area of a 16-sided cylinder is near that of the cylinder");
        }

        [Test]
        public void APlantMergedIntoOneMeshSplitsIntoItsSolids()
        {
            var plant = Plant();
            // every part of the same material in one primitive: what an export as one object gives
            var glb = Path.Combine(_dir, "merged.glb");
            GlbWriter.WriteProp(plant, "Plant", glb);
            var read = GltfReader.Read(glb);
            Assert.That(read.Parts.Count, Is.EqualTo(2), "one primitive per material");
            Assert.That(read.Parts.All(p => p.Name.StartsWith("Plant")), Is.True);
            ModelGeometry.Normalize(read, new Placement3D { Scale = 1, Up = UpAxis.Y });
            var islands = ModelGeometry.Islands(read);
            Assert.That(islands.Count, Is.EqualTo(5));
            Assert.That(Boxes(islands), Is.EqualTo(PlantBoxes()), "the islands are the solids, in place");
        }

        [Test]
        public void TouchingSolidsStayApartAndAnUnweldedSolidStaysWhole()
        {
            var m = new ImportedModel();
            m.Parts.Add(Box("a", new[] { 0.0, 0, 0 }, new[] { 1.0, 1, 1 }, -1));
            var b = Box("b", new[] { 1.0, 0, 0 }, new[] { 2.0, 1, 1 }, -1);
            // the second box goes into the same part, face to face with the first
            var p = m.Parts[0];
            var at = p.VertexCount;
            p.Positions = p.Positions.Concat(b.Positions).ToArray();
            p.Indices = p.Indices.Concat(b.Indices.Select(i => i + at)).ToArray();
            Assert.That(ModelGeometry.Islands(m).Count, Is.EqualTo(2), "two boxes that only touch are two islands");

            // a box written as STL writes it, three corners per triangle, is still one island
            var stl = Path.Combine(_dir, "box.stl");
            var one = new ImportedModel();
            one.Parts.Add(Box("c", new[] { 0.0, 0, 0 }, new[] { 1.0, 1, 1 }, -1));
            WriteStl(one, stl);
            var read = StlReader.Read(stl);
            Assert.That(read.Parts[0].VertexCount, Is.EqualTo(36));
            Assert.That(ModelGeometry.Islands(read).Count, Is.EqualTo(1));
        }

        [Test]
        public void GlbKeepsNamesMaterialsTexturesAndPlaces()
        {
            var plant = Plant();
            plant.Materials[1].Texture = Convert.FromBase64String(Png1x1);
            plant.Materials[1].TextureMime = "image/png";
            plant.Materials[1].AlphaMode = "MASK";
            plant.Materials[1].DoubleSided = true;
            var pump = plant.Parts[2];
            pump.UV = new float[pump.VertexCount * 2];
            for (var i = 0; i < pump.UV.Length; i++) pump.UV[i] = (i % 4) / 4f;

            var islands = ModelGeometry.Islands(plant);
            var glb = Path.Combine(_dir, "islands.glb");
            GlbWriter.WriteIslands(plant, islands, glb);
            var read = GltfReader.Read(glb);

            Assert.That(read.Up, Is.EqualTo(UpAxis.Y));
            Assert.That(read.Units, Is.EqualTo(ModelUnits.Meters));
            Assert.That(read.Parts.Select(p => p.Name), Is.EqualTo(Enumerable.Range(0, 5).Select(i => "island_" + i)));
            var readPump = read.Parts[2];
            var mat = read.Materials[readPump.Material];
            Assert.That(mat.Name, Is.EqualTo("painted"));
            Assert.That(mat.Color[2], Is.EqualTo(0.8f).Within(1e-4));
            Assert.That(mat.Texture, Is.EqualTo(plant.Materials[1].Texture), "the texture is kept byte by byte");
            Assert.That(mat.AlphaMode, Is.EqualTo("MASK"));
            Assert.That(mat.DoubleSided, Is.True);
            Assert.That(readPump.UV, Is.Not.Null);
            Assert.That(read.Materials[read.Parts[0].Material].Metallic, Is.EqualTo(0.8f).Within(1e-4));

            ModelGeometry.Normalize(read, new Placement3D { Scale = 1, Up = UpAxis.Y });
            Assert.That(Boxes(ModelGeometry.Islands(read)), Is.EqualTo(PlantBoxes()), "Y up in the file comes back as Z up");
        }

        [Test]
        public void GltfNodeTransformsAreApplied()
        {
            // a unit triangle under a parent moved 10 m along X and a child turned 90 degrees about Y (Y up)
            var bin = new List<byte>();
            foreach (var f in new[] { 0f, 0, 0, 1, 0, 0, 0, 1, 0 }) bin.AddRange(BitConverter.GetBytes(f));
            var json = @"{""asset"":{""version"":""2.0""},""scene"":0,""scenes"":[{""nodes"":[0]}],
""nodes"":[{""name"":""Area"",""translation"":[10,0,0],""children"":[1]},{""name"":""Tri"",""rotation"":[0,0.70710678,0,0.70710678],""scale"":[2,2,2],""mesh"":0}],
""meshes"":[{""primitives"":[{""attributes"":{""POSITION"":0}}]}],
""accessors"":[{""bufferView"":0,""componentType"":5126,""count"":3,""type"":""VEC3""}],
""bufferViews"":[{""buffer"":0,""byteLength"":36}],
""buffers"":[{""byteLength"":36,""uri"":""data:application/octet-stream;base64," + Convert.ToBase64String(bin.ToArray()) + @"""}]}";
            var path = Path.Combine(_dir, "nodes.gltf");
            File.WriteAllText(path, json);
            var m = GltfReader.Read(path);
            Assert.That(m.Parts.Count, Is.EqualTo(1));
            Assert.That(m.Parts[0].Name, Is.EqualTo("Area/Tri"));
            var p = m.Parts[0].Positions;
            // the corner (1, 0, 0) turned about Y goes to (0, 0, -1), times 2, plus 10 on X
            Assert.That(p[3], Is.EqualTo(10).Within(1e-5));
            Assert.That(p[5], Is.EqualTo(-2).Within(1e-5));
            Assert.That(p[7], Is.EqualTo(2).Within(1e-5), "the corner (0, 1, 0) stays on Y, times 2");
        }

        [Test]
        public void ObjKeepsObjectsAndMaterialsAndGuessesMillimetres()
        {
            var obj = Path.Combine(_dir, "plant.obj");
            WriteObj(ToFileFrame(Plant(), 1000, true), obj, false);
            var m = ObjReader.Read(obj);
            Assert.That(m.Parts.Select(p => p.Name), Is.EqualTo(new[] { "C-1", "D-1", "P-1", "L-1", "L-2" }));
            Assert.That(m.Materials.Select(x => x.Name), Is.EqualTo(new[] { "steel", "painted" }));
            Assert.That(m.Materials[m.Parts[2].Material].Color[2], Is.EqualTo(0.8f).Within(1e-4));
            Assert.That(m.Units, Is.EqualTo(ModelUnits.Unknown));
            Assert.That(ModelGeometry.GuessScale(m), Is.EqualTo(0.001), "a plant about 20 000 units across is in millimetres");
            ModelGeometry.Normalize(m, new Placement3D { Scale = 0.001, Up = UpAxis.Y });
            Assert.That(Boxes(ModelGeometry.Islands(m)), Is.EqualTo(PlantBoxes()));

            // the same plant as one object: one part per material, and the same islands
            var one = Path.Combine(_dir, "one.obj");
            WriteObj(ToFileFrame(Plant(), 1000, true), one, true);
            var m1 = ObjReader.Read(one);
            Assert.That(m1.Parts.Select(p => p.Name).Distinct(), Is.EqualTo(new[] { "Plant" }));
            ModelGeometry.Normalize(m1, new Placement3D { Scale = 0.001, Up = UpAxis.Y });
            Assert.That(Boxes(ModelGeometry.Islands(m1)), Is.EqualTo(PlantBoxes()));
        }

        [Test]
        public void StlAndPlyInMillimetresWithZUp()
        {
            var file = ToFileFrame(Plant(), 1000, false);
            var stl = Path.Combine(_dir, "plant.stl");
            WriteStl(file, stl);
            var ply = Path.Combine(_dir, "plant.ply");
            WritePly(file, ply, false);
            var plyBin = Path.Combine(_dir, "plant_bin.ply");
            WritePly(file, plyBin, true);
            foreach (var path in new[] { stl, ply, plyBin })
            {
                var m = ModelImporter.Read(path);
                Assert.That(m.Parts.Count, Is.EqualTo(1), path);
                Assert.That(m.TriangleCount, Is.EqualTo(Plant().TriangleCount), path);
                ModelGeometry.Normalize(m, new Placement3D { Scale = ModelGeometry.GuessScale(m), Up = UpAxis.Z });
                Assert.That(Boxes(ModelGeometry.Islands(m)), Is.EqualTo(PlantBoxes()), path);
            }
        }

        [Test]
        public void TheUnitIsGuessedFromTheSize()
        {
            var plant = Plant();
            Assert.That(ModelGeometry.GuessScale(ToFileFrame(plant, 1, false)), Is.EqualTo(1));
            var cm = ToFileFrame(plant, 100, false); cm.Units = ModelUnits.Unknown;
            Assert.That(ModelGeometry.GuessScale(cm), Is.EqualTo(0.01));
            Assert.That(ModelGeometry.GuessScale(ToFileFrame(plant, 1000, false)), Is.EqualTo(0.001));
            // a tree 9 m tall drawn in centimetres, as a prop
            var tree = new ImportedModel();
            tree.Parts.Add(Cylinder("trunk", new[] { 0.0, 0, 0 }, new[] { 0.0, 0, 900 }, 20, -1));
            Assert.That(ModelGeometry.GuessScale(tree, prop: true), Is.EqualTo(0.01));
            // glTF says metres, and a plant 20 m across keeps them
            var gltf = ToFileFrame(plant, 1, true);
            gltf.Units = ModelUnits.Meters;
            Assert.That(ModelGeometry.GuessScale(gltf), Is.EqualTo(1));
        }

        [Test]
        public void ThePlacementTurnsAndMovesTheModel()
        {
            var m = new ImportedModel();
            m.Parts.Add(Box("b", new[] { 1.0, 0, 0 }, new[] { 2.0, 1, 1 }, -1));
            ModelGeometry.Normalize(m, new Placement3D { Scale = 1, Up = UpAxis.Z, Rotation = 90, Offset = new[] { 100.0, 0, 5 } });
            double[] min, max;
            m.Bounds(out min, out max);
            Assert.That(min[0], Is.EqualTo(99).Within(1e-4));
            Assert.That(max[0], Is.EqualTo(100).Within(1e-4));
            Assert.That(min[1], Is.EqualTo(1).Within(1e-4));
            Assert.That(max[1], Is.EqualTo(2).Within(1e-4));
            Assert.That(min[2], Is.EqualTo(5).Within(1e-4));

            ModelGeometry.CenterOnBase(m);
            m.Bounds(out min, out max);
            Assert.That(min[0] + max[0], Is.EqualTo(0).Within(1e-4));
            Assert.That(min[2], Is.EqualTo(0).Within(1e-6), "a prop stands on its base at the origin");
        }

        [Test]
        public void TheImportWritesACacheAndReadsItBack()
        {
            var glb = Path.Combine(_dir, "plant.glb");
            GlbWriter.WriteProp(Plant(), "Plant", glb);
            var first = ModelImporter.Import(glb);
            Assert.That(first.FromCache, Is.False);
            Assert.That(first.Islands.Count, Is.EqualTo(5));
            Assert.That(File.Exists(first.GlbPath) && File.Exists(first.IndexPath), Is.True);
            Assert.That(first.CacheFolder, Does.StartWith(ModelImporter.CacheRoot));
            Assert.That(first.Reference.Sha256, Has.Length.EqualTo(64));

            // the cached model has one node per island, with Z up in the plant turned to Y up
            var cached = GltfReader.Read(first.GlbPath);
            Assert.That(cached.Parts.Count, Is.EqualTo(5));

            var again = ModelImporter.Import(glb, first.Reference);
            Assert.That(again.FromCache, Is.True, "the same file and placement read the cache");
            Assert.That(again.Islands.Select(i => i.Centroid[2]), Is.EqualTo(first.Islands.Select(i => Math.Round(i.Centroid[2], 5))));

            var other = first.Reference;
            other.Place = new Placement3D { Scale = 1, Up = other.Place.Up, Rotation = 90 };
            var turned = ModelImporter.Import(glb, other);
            Assert.That(turned.FromCache, Is.False, "another placement makes another cache");
            Assert.That(turned.CacheFolder, Is.Not.EqualTo(first.CacheFolder));
        }

        [Test]
        public void TheFlowsheetKeepsTheReferenceAndTheGroups()
        {
            var fs = Flowsheet.Create("model import");
            var glb = Path.Combine(_dir, "plant.glb");
            GlbWriter.WriteProp(Plant(), "Plant", glb);
            var import = ModelImporter.Import(glb, null, Path.Combine(_dir, "case.dwxmz"));
            Assert.That(import.Reference.RelativePath, Is.EqualTo("plant.glb"));
            var model = new PlantModelImport { Model = import.Reference };
            model.Groups.Add(new ModelGroup { Id = "g1", Islands = { 0 }, Class = "column", Object = "C-1-id", Role = "equipment" });
            ModelStore.Save(fs.Inner, model);

            var back = ModelStore.Load(fs.Inner);
            Assert.That(back.Model.Sha256, Is.EqualTo(import.Reference.Sha256));
            Assert.That(back.Model.Place.Up, Is.EqualTo(import.Reference.Place.Up));
            Assert.That(back.Groups.Single().Object, Is.EqualTo("C-1-id"));
            Assert.That(ModelImporter.Locate(back.Model, Path.Combine(_dir, "case.dwxmz")), Is.EqualTo(Path.GetFullPath(glb)));

            // a project folder that was moved: the absolute path is gone, the relative one finds the file
            var moved = Path.Combine(_dir, "moved");
            Directory.CreateDirectory(moved);
            File.Copy(glb, Path.Combine(moved, "plant.glb"));
            back.Model.Path = Path.Combine(_dir, "gone", "plant.glb");
            Assert.That(ModelImporter.Locate(back.Model, Path.Combine(moved, "case.dwxmz")), Is.EqualTo(Path.Combine(moved, "plant.glb")));

            ModelStore.Save(fs.Inner, null);
            Assert.That(ModelStore.Load(fs.Inner), Is.Null);
        }

        [Test]
        public void ReadingAnUnknownFormatSaysWhichOnesItReads()
        {
            var path = Path.Combine(_dir, "plant.skp");
            File.WriteAllText(path, "x");
            var ex = Assert.Throws<NotSupportedException>(() => ModelImporter.Read(path));
            Assert.That(ex.Message, Does.Contain("glTF"));
        }

        const string FbxCubes = @"; FBX 7.4.0 project file
FBXHeaderExtension:  {
	FBXHeaderVersion: 1003
	FBXVersion: 7400
}
GlobalSettings:  {
	Version: 1000
	Properties70:  {
		P: ""UpAxis"", ""int"", ""Integer"", """",1
		P: ""UpAxisSign"", ""int"", ""Integer"", """",1
		P: ""FrontAxis"", ""int"", ""Integer"", """",2
		P: ""FrontAxisSign"", ""int"", ""Integer"", """",1
		P: ""CoordAxis"", ""int"", ""Integer"", """",0
		P: ""CoordAxisSign"", ""int"", ""Integer"", """",1
		P: ""UnitScaleFactor"", ""double"", ""Number"", """",100
	}
}
Objects:  {
	Geometry: 1000, ""Geometry::Cube"", ""Mesh"" {
		Vertices: *24 {
			a: -1,-1,-1,1,-1,-1,1,1,-1,-1,1,-1,-1,-1,1,1,-1,1,1,1,1,-1,1,1
		}
		PolygonVertexIndex: *24 {
			a: 0,3,2,-2,4,5,6,-8,0,1,5,-5,1,2,6,-6,2,3,7,-7,3,0,4,-8
		}
		GeometryVersion: 124
	}
	Model: 2000, ""Model::CubeA"", ""Mesh"" {
		Version: 232
		Properties70:  {
		}
	}
	Model: 2001, ""Model::CubeB"", ""Mesh"" {
		Version: 232
		Properties70:  {
			P: ""Lcl Translation"", ""Lcl Translation"", """", ""A"",5,0,0
		}
	}
	Material: 3000, ""Material::Red"", """" {
		Version: 102
		ShadingModel: ""phong""
		Properties70:  {
			P: ""DiffuseColor"", ""Color"", """", ""A"",0.8,0.1,0.1
		}
	}
}
Connections:  {
	C: ""OO"",1000,2000
	C: ""OO"",1000,2001
	C: ""OO"",3000,2000
	C: ""OO"",3000,2001
	C: ""OO"",2000,0
	C: ""OO"",2001,0
}
";

        /// <summary>
        /// FBX goes through the 3D window (Godot reads it at run time), so this runs only where the
        /// window is found: an installation with the viewer3d folder, or DWSIM_VIEWER3D and DWSIM_GODOT
        /// set to the project folder and the Godot editor.
        /// </summary>
        [Test]
        public void FbxIsConvertedByTheWindow()
        {
            string exe, prefix;
            Assume.That(ViewerLauncher.Resolve(out exe, out prefix), Is.True, "the 3D window is not here");
            var fbx = Path.Combine(_dir, "cubes.fbx");
            File.WriteAllText(fbx, FbxCubes);
            var m = ModelImporter.Read(fbx);
            Assert.That(m.Format, Is.EqualTo("fbx"));
            Assert.That(m.Parts.Count, Is.EqualTo(2));
            Assert.That(m.Parts.Select(p => p.Name.Split('/').Last()), Is.EquivalentTo(new[] { "CubeA", "CubeB" }));
            ModelGeometry.Normalize(m, new Placement3D { Scale = 1, Up = UpAxis.Y });
            var islands = ModelGeometry.Islands(m);
            Assert.That(islands.Count, Is.EqualTo(2));
            var sizes = islands.Select(i => Math.Round(i.Size[0], 3)).Distinct().ToList();
            Assert.That(sizes.Count, Is.EqualTo(1), "the two cubes are the same size");
            var gap = Math.Abs(islands[1].Centroid[0] - islands[0].Centroid[0]);
            Assert.That(gap / sizes[0], Is.EqualTo(2.5).Within(1e-3), "the second cube is 5 units away, 2.5 sizes of a 2-unit cube");
            Assert.That(m.Materials[m.Parts[0].Material].Color[0], Is.GreaterThan(m.Materials[m.Parts[0].Material].Color[1]), "the red material is kept");
        }
    }
}
