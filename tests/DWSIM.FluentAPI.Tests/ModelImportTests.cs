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
using System.IO.Compression;
using System.Linq;
using System.Text;
using DWSIM.Automation.FluentAPI;
using DWSIM.Extensions.SolidCraft;
using DWSIM.Extensions.SolidCraft.Viewer3D;
using DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// The model import of the 3D window, phase M0: the readers (glTF, GLB, OBJ, STL, PLY, 3D DWF, and
    /// FBX through the window when it is there), the unit and axis normalization, the islands of a
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
            m.Parts.Add(Cylinder("C-101", new[] { 0.0, 0, 0 }, new[] { 0.0, 0, 20 }, 1.0, 0));
            m.Parts.Add(Cylinder("D-102", new[] { 8.0, 0, 2 }, new[] { 14.0, 0, 2 }, 1.0, 0));
            m.Parts.Add(Box("P-103", new[] { 18.0, -0.5, 0 }, new[] { 19.5, 0.5, 1 }, 1));
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
            Assert.That(m.Parts.Select(p => p.Name), Is.EqualTo(new[] { "C-101", "D-102", "P-103", "L-1", "L-2" }));
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

        // ---- M5: IFC, and STEP, IGES, Rhino and FreeCAD through SolidCraft ----

        /// <summary>
        /// A small IFC4 plant in millimetres: a drum (V-101, an extruded circle, blue), a pump (P-102,
        /// a CSG block through a mapped item) inside a skid assembly, a line with a bend (a swept disk),
        /// a beam with an I profile and a slab as a faceted brep.
        /// </summary>
        const string IfcPlant = @"ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [ReferenceView]'),'2;1');
FILE_NAME('plant.ifc','2026-11-28T10:00:00',(''),(''),'','','');
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);
#2=IFCSIUNIT(*,.PLANEANGLEUNIT.,$,.RADIAN.);
#3=IFCUNITASSIGNMENT((#1,#2));
#4=IFCPROJECT('0p',$,'Plant',$,$,$,$,$,#3);
#10=IFCCARTESIANPOINT((0.,0.,0.));
#11=IFCAXIS2PLACEMENT3D(#10,$,$);
#12=IFCLOCALPLACEMENT($,#11);
#13=IFCSITE('0s',$,'Site',$,$,#12,$,$,.ELEMENT.,$,$,$,$,$);
#14=IFCRELAGGREGATES('0r',$,$,$,#4,(#13));
/* the drum */
#20=IFCCARTESIANPOINT((5000.,0.,0.));
#21=IFCAXIS2PLACEMENT3D(#20,$,$);
#22=IFCLOCALPLACEMENT(#12,#21);
#23=IFCCIRCLEPROFILEDEF(.AREA.,$,$,1000.);
#24=IFCDIRECTION((0.,0.,1.));
#25=IFCEXTRUDEDAREASOLID(#23,$,#24,3000.);
#26=IFCSHAPEREPRESENTATION($,'Body','SweptSolid',(#25));
#27=IFCPRODUCTDEFINITIONSHAPE($,$,(#26));
#28=IFCTANK('0t',$,'Separador',$,$,#22,#27,'V-101',.NOTDEFINED.);
#29=IFCCOLOURRGB($,0.,0.,1.);
#30=IFCSURFACESTYLERENDERING(#29,0.,$,$,$,$,$,$,.NOTDEFINED.);
#31=IFCSURFACESTYLE($,.BOTH.,(#30));
#32=IFCSTYLEDITEM(#25,(#31),$);
/* the pump, in a skid */
#40=IFCCARTESIANPOINT((10000.,0.,0.));
#41=IFCAXIS2PLACEMENT3D(#40,$,$);
#42=IFCLOCALPLACEMENT(#12,#41);
#43=IFCBLOCK(#11,1000.,500.,600.);
#44=IFCCSGSOLID(#43);
#45=IFCSHAPEREPRESENTATION($,'Body','CSG',(#44));
#46=IFCREPRESENTATIONMAP(#11,#45);
#47=IFCCARTESIANTRANSFORMATIONOPERATOR3D($,$,#10,1.,$);
#48=IFCMAPPEDITEM(#46,#47);
#49=IFCSHAPEREPRESENTATION($,'Body','MappedRepresentation',(#48));
#50=IFCPRODUCTDEFINITIONSHAPE($,$,(#49));
#51=IFCPUMP('0u',$,'P-102 booster',$,$,#42,#50,$,.NOTDEFINED.);
#52=IFCELEMENTASSEMBLY('0k',$,'SKID-200',$,$,$,$,$,.FACTORY.,.NOTDEFINED.);
#53=IFCRELAGGREGATES('0a',$,$,$,#52,(#51));
/* the line between them, with a bend */
#60=IFCCARTESIANPOINT((6000.,0.,500.));
#61=IFCCARTESIANPOINT((8000.,0.,500.));
#62=IFCCARTESIANPOINT((8000.,0.,300.));
#63=IFCPOLYLINE((#60,#61,#62));
#64=IFCSWEPTDISKSOLID(#63,50.,$,$,$);
#65=IFCSHAPEREPRESENTATION($,'Body','AdvancedSweptSolid',(#64));
#66=IFCPRODUCTDEFINITIONSHAPE($,$,(#65));
#67=IFCPIPESEGMENT('0l',$,'Line 2""-P-001',$,$,#12,#66,$,.RIGIDSEGMENT.);
/* a beam along x and a slab */
#70=IFCISHAPEPROFILEDEF(.AREA.,$,$,200.,300.,10.,15.,$,$,$);
#71=IFCDIRECTION((1.,0.,0.));
#72=IFCCARTESIANPOINT((0.,5000.,3000.));
#73=IFCAXIS2PLACEMENT3D(#72,#71,$);
#74=IFCEXTRUDEDAREASOLID(#70,#73,#24,4000.);
#75=IFCSHAPEREPRESENTATION($,'Body','SweptSolid',(#74));
#76=IFCPRODUCTDEFINITIONSHAPE($,$,(#75));
#77=IFCBEAM('0b',$,'Beam',$,$,#12,#76,$,$);
#80=IFCCARTESIANPOINT((0.,-5000.,0.));
#81=IFCCARTESIANPOINT((1000.,-5000.,0.));
#82=IFCCARTESIANPOINT((1000.,-4000.,0.));
#83=IFCCARTESIANPOINT((0.,-4000.,0.));
#84=IFCCARTESIANPOINT((0.,-5000.,1000.));
#85=IFCCARTESIANPOINT((1000.,-5000.,1000.));
#86=IFCCARTESIANPOINT((1000.,-4000.,1000.));
#87=IFCCARTESIANPOINT((0.,-4000.,1000.));
#89=IFCPOLYLOOP((#80,#83,#82,#81));
#90=IFCFACE((#91));
#91=IFCFACEOUTERBOUND(#92,.T.);
#92=IFCPOLYLOOP((#84,#85,#86,#87));
#93=IFCFACE((#94));
#94=IFCFACEOUTERBOUND(#95,.T.);
#95=IFCPOLYLOOP((#80,#81,#85,#84));
#96=IFCFACE((#97));
#97=IFCFACEOUTERBOUND(#98,.T.);
#98=IFCPOLYLOOP((#81,#82,#86,#85));
#99=IFCFACE((#100));
#100=IFCFACEOUTERBOUND(#101,.T.);
#101=IFCPOLYLOOP((#82,#83,#87,#86));
#102=IFCFACE((#103));
#103=IFCFACEOUTERBOUND(#104,.T.);
#104=IFCPOLYLOOP((#83,#80,#84,#87));
#105=IFCCLOSEDSHELL((#90,#93,#96,#99,#102,#106));
#106=IFCFACE((#107));
#107=IFCFACEOUTERBOUND(#89,.T.);
#108=IFCFACETEDBREP(#105);
#109=IFCSHAPEREPRESENTATION($,'Body','Brep',(#108));
#110=IFCPRODUCTDEFINITIONSHAPE($,$,(#109));
#111=IFCSLAB('0c',$,'Slab \X2\00E9\X0\',$,$,#12,#110,$,.FLOOR.);
/* an opening is never drawn */
#120=IFCOPENINGELEMENT('0o',$,'Opening',$,$,#12,#110,$,.OPENING.);
ENDSEC;
END-ISO-10303-21;
";

        static double Volume(MeshPart p)
        {
            double v = 0;
            var a = p.Positions;
            for (var k = 0; k + 2 < p.Indices.Length; k += 3)
            {
                int i = 3 * p.Indices[k], j = 3 * p.Indices[k + 1], l = 3 * p.Indices[k + 2];
                v += a[i] * (a[j + 1] * a[l + 2] - a[j + 2] * a[l + 1]) - a[i + 1] * (a[j] * a[l + 2] - a[j + 2] * a[l]) + a[i + 2] * (a[j] * a[l + 1] - a[j + 1] * a[l]);
            }
            return v / 6;
        }

        static double[][] BoundsOf(MeshPart p)
        {
            var min = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
            var max = new[] { double.MinValue, double.MinValue, double.MinValue };
            for (var i = 0; i + 2 < p.Positions.Length; i += 3)
                for (var k = 0; k < 3; k++) { min[k] = Math.Min(min[k], p.Positions[i + k]); max[k] = Math.Max(max[k], p.Positions[i + k]); }
            return new[] { min, max };
        }

        [Test]
        public void IfcElementsComeWithTheirPlacesShapesRolesAndTags()
        {
            var path = Path.Combine(_dir, "plant.ifc");
            File.WriteAllText(path, IfcPlant);
            var m = ModelImporter.Read(path);
            Assert.That(m.Format, Is.EqualTo("ifc"));
            Assert.That(m.Units, Is.EqualTo(ModelUnits.Meters), "the millimetres of the file come in as metres");
            Assert.That(m.Up, Is.EqualTo(UpAxis.Z));
            Assert.That(m.Parts.Count, Is.EqualTo(5), "the opening is left out: " + string.Join(", ", m.Parts.Select(p => p.Name)));
            Assert.That(m.Warnings, Is.Empty);

            var drum = m.Parts.Single(p => p.Role == "equipment" && p.Path.Last().Contains("V-101"));
            Assert.That(drum.Path, Is.EqualTo(new[] { "plant", "V-101 Separador" }), "the tag goes in front of a name without one");
            var b = BoundsOf(drum);
            Assert.That(new[] { b[0][0], b[1][0], b[0][2], b[1][2] }, Is.EqualTo(new[] { 4.0, 6.0, 0.0, 3.0 }).Within(1e-3));
            Assert.That(Volume(drum), Is.EqualTo(Math.PI * 3).Within(Math.PI * 3 * 0.02), "a closed drum that faces out");
            var color = m.Materials[drum.Material].Color;
            Assert.That(color[2], Is.EqualTo(1f).Within(1e-3));
            Assert.That(color[0], Is.EqualTo(0f).Within(1e-3));

            var pump = m.Parts.Single(p => p.Path.Last() == "P-102 booster");
            Assert.That(pump.Path, Is.EqualTo(new[] { "plant", "SKID-200", "P-102 booster" }), "the skid that holds the pump is in its path");
            Assert.That(pump.Role, Is.EqualTo("equipment"));
            b = BoundsOf(pump);
            Assert.That(b[0].Concat(b[1]), Is.EqualTo(new[] { 10.0, 0, 0, 11, 0.5, 0.6 }).Within(1e-3), "the block goes through the mapped item and the placement");
            Assert.That(Volume(pump), Is.EqualTo(0.3).Within(1e-4));

            var pipe = m.Parts.Single(p => p.Role == "piping");
            b = BoundsOf(pipe);
            Assert.That(new[] { b[0][0], b[1][0], b[0][2], b[1][2] }, Is.EqualTo(new[] { 6.0, 8.05, 0.3, 0.55 }).Within(1e-3), "the swept disk turns down at the bend");
            Assert.That(Volume(pipe), Is.EqualTo(Math.PI * 0.05 * 0.05 * 2.2).Within(Math.PI * 0.05 * 0.05 * 2.2 * 0.06));

            var beam = m.Parts.Single(p => p.Path.Last() == "Beam");
            Assert.That(beam.Role, Is.EqualTo("structure"));
            b = BoundsOf(beam);
            Assert.That(b[0].Concat(b[1]), Is.EqualTo(new[] { 0.0, 4.9, 2.85, 4, 5.1, 3.15 }).Within(1e-3), "the profile stands across the beam's axis");
            var area = 2 * 0.2 * 0.015 + (0.3 - 2 * 0.015) * 0.01;
            Assert.That(Volume(beam), Is.EqualTo(area * 4).Within(area * 4 * 1e-3));

            var slab = m.Parts.Single(p => p.Path.Last() == "Slab é");
            Assert.That(slab.Role, Is.EqualTo("structure"));
            Assert.That(Volume(slab), Is.EqualTo(1.0).Within(1e-6), "the brep is closed and faces out");

            // the importer groups by the elements and their roles
            var result = ModelImporter.Import(path);
            var groups = ModelSegmentation.Groups(result);
            Assert.That(groups.Single(g => g.Tag == "V-101").Role, Is.EqualTo("equipment"));
            Assert.That(groups.Single(g => g.Name == "SKID-200").Role, Is.EqualTo("equipment"));
            Assert.That(groups.Count(g => g.Role == "piping"), Is.EqualTo(1));
            Assert.That(groups.Count(g => g.Role == "structure"), Is.EqualTo(2));
            var cached = ModelImporter.Cached(result.Reference);
            Assert.That(cached.PartRoles, Is.EqualTo(result.PartRoles), "the roles are kept in the cache");
        }

        [Test]
        public void IfcInFeetWithDegreesAndArcs()
        {
            // a ring of 1 ft radius swept round a quarter circle given in degrees, as IFC2x3 files have it
            var text = @"ISO-10303-21;
HEADER;FILE_SCHEMA(('IFC2X3'));ENDSEC;
DATA;
#1=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
#2=IFCMEASUREWITHUNIT(IFCLENGTHMEASURE(0.3048),#1);
#3=IFCCONVERSIONBASEDUNIT(#9,.LENGTHUNIT.,'FOOT',#2);
#4=IFCSIUNIT(*,.PLANEANGLEUNIT.,$,.RADIAN.);
#5=IFCMEASUREWITHUNIT(IFCPLANEANGLEMEASURE(0.0174532925199433),#4);
#6=IFCCONVERSIONBASEDUNIT(#9,.PLANEANGLEUNIT.,'DEGREE',#5);
#7=IFCUNITASSIGNMENT((#3,#6));
#8=IFCPROJECT('p',$,'P',$,$,$,$,$,#7);
#9=IFCDIMENSIONALEXPONENTS(1,0,0,0,0,0,0);
#10=IFCCARTESIANPOINT((0.,0.,0.));
#11=IFCAXIS2PLACEMENT3D(#10,$,$);
#12=IFCLOCALPLACEMENT($,#11);
#20=IFCCIRCLE(#11,10.);
#21=IFCTRIMMEDCURVE(#20,(IFCPARAMETERVALUE(0.)),(IFCPARAMETERVALUE(90.)),.T.,.PARAMETER.);
#22=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#21);
#23=IFCCOMPOSITECURVE((#22),.F.);
#24=IFCSWEPTDISKSOLID(#23,1.,$,$,$);
#25=IFCSHAPEREPRESENTATION($,'Body','SweptSolid',(#24));
#26=IFCPRODUCTDEFINITIONSHAPE($,$,(#25));
#27=IFCPIPEFITTING('e',$,'Elbow',$,$,#12,#26,$);
ENDSEC;
END-ISO-10303-21;
";
            var path = Path.Combine(_dir, "elbow.ifc");
            File.WriteAllText(path, text);
            var m = ModelImporter.Read(path);
            var elbow = m.Parts.Single();
            Assert.That(elbow.Role, Is.EqualTo("piping"));
            var b = BoundsOf(elbow);
            Assert.That(b[1][0], Is.EqualTo(11 * 0.3048).Within(0.01), "10 ft of bend radius and 1 ft of pipe radius");
            Assert.That(b[1][1], Is.EqualTo(11 * 0.3048).Within(0.01), "the quarter turn ends on the y axis");
            Assert.That(b[0][0], Is.GreaterThan(-0.31), "the turn stays in the first quadrant");
            var expected = Math.PI * 0.3048 * 0.3048 * (Math.PI / 2 * 10 * 0.3048);
            Assert.That(Volume(elbow), Is.EqualTo(expected).Within(expected * 0.05));
        }

        /// <summary>A glTF file with the tree SolidCraft writes for an imported STEP assembly.</summary>
        static void WriteCadTree(string glb)
        {
            var m = new ImportedModel { Format = "test", Up = UpAxis.Z, Units = ModelUnits.Meters };
            m.Materials.Add(new ModelMaterial());
            m.Parts.Add(Cylinder("shell", new[] { 0.0, 0, 0 }, new[] { 0.0, 0, 6 }, 1.0, 0));
            m.Parts.Add(Box("casing", new[] { 5.0, 0, 0 }, new[] { 6.0, 1, 1 }, 0));
            m.Parts.Add(Box("beam", new[] { -5.0, -5, 5 }, new[] { 5.0, -4.8, 5.2 }, 0));
            GlbWriter.WriteParts(m, glb);
            // the parts again, under the products and the occurrences of a SolidCraft design
            var bytes = File.ReadAllBytes(glb);
            var jsonLength = BitConverter.ToInt32(bytes, 12);
            var doc = JObject.Parse(Encoding.UTF8.GetString(bytes, 20, jsonLength));
            var nodes = (JArray)doc["nodes"];
            var meshNodes = nodes.Count;
            Func<string, int[], int> add = (name, children) =>
            {
                nodes.Add(new JObject { ["name"] = name, ["children"] = new JArray(children) });
                return nodes.Count - 1;
            };
            var vessel = add("V-101:1", new[] { 0 });
            var area = add("Area 100:1", new[] { vessel });
            var pump = add("P-201:1", new[] { 1 });
            var rack = add("Pipe rack:1", new[] { 2 });
            var file = add("plant:1", new[] { area, pump, rack });
            var design = add("plant", new[] { file });
            doc["scenes"] = new JArray(new JObject { ["nodes"] = new JArray(design) });
            doc["scene"] = 0;
            var json = Encoding.UTF8.GetBytes(doc.ToString(Newtonsoft.Json.Formatting.None));
            var pad = (4 - json.Length % 4) % 4;
            var binStart = 20 + jsonLength;
            var bin = bytes.Skip(binStart).ToArray();
            using (var f = File.Create(glb))
            using (var w = new BinaryWriter(f))
            {
                w.Write(0x46546C67); w.Write(2); w.Write(12 + 8 + json.Length + pad + bin.Length);
                w.Write(json.Length + pad); w.Write(0x4E4F534A); w.Write(json); for (var k = 0; k < pad; k++) w.Write((byte)0x20);
                w.Write(bin);
            }
            Assert.That(meshNodes, Is.EqualTo(3));
        }

        [Test]
        public void CadFilesGoThroughSolidCraftAndKeepTheirProducts()
        {
            var converter = ModelImporter.CadConverter;
            try
            {
                string seen = null;
                ModelImporter.CadConverter = (cad, glb) => { seen = cad; WriteCadTree(glb); return true; };
                var step = Path.Combine(_dir, "plant.step");
                File.WriteAllText(step, "ISO-10303-21;");
                var m = ModelImporter.Read(step);
                Assert.That(seen, Is.EqualTo(step));
                Assert.That(m.Format, Is.EqualTo("step"));
                Assert.That(m.SourcePath, Is.EqualTo(step));
                Assert.That(m.Parts.Select(p => string.Join("/", p.Path)), Is.EquivalentTo(new[] { "plant/V-101/shell", "plant/P-201/casing", "plant/Pipe rack/beam" }),
                    "the design and the occurrence numbers go, and the tagged vessel comes up from its area");

                var result = ModelImporter.Import(step);
                var groups = ModelSegmentation.Groups(result);
                Assert.That(groups.Single(g => g.Name == "V-101").Role, Is.EqualTo("equipment"));
                Assert.That(groups.Select(g => g.Name), Is.EquivalentTo(new[] { "V-101", "P-201", "beam" }), "a rack is structure, and its members are its groups, as in a DWF");
                Assert.That(groups.Single(g => g.Name == "beam").Role, Is.EqualTo("structure"));
            }
            finally { ModelImporter.CadConverter = converter; }
        }

        [Test]
        public void ACadFileSolidCraftCannotConvertSaysWhatToDo()
        {
            var converter = ModelImporter.CadConverter;
            try
            {
                ModelImporter.CadConverter = (cad, glb) => false;
                var igs = Path.Combine(_dir, "plant.igs");
                File.WriteAllText(igs, "x");
                var ex = Assert.Throws<InvalidOperationException>(() => ModelImporter.Read(igs));
                Assert.That(ex.Message, Does.Contain("SolidCraft"));
            }
            finally { ModelImporter.CadConverter = converter; }
        }

        /// <summary>The real conversion, where SolidCraft is installed: a STEP file it writes (two boxes made solids) comes back with its products.</summary>
        [Test]
        public void SolidCraftConvertsAStepFile()
        {
            var folder = SolidCraftRunner.FindInstallFolder();
            Assume.That(folder, Is.Not.Null, "SolidCraft is not installed");
            // a STEP file of two products, as SolidCraft's own writer makes it from a mesh design
            var m = new ImportedModel { Format = "test", Up = UpAxis.Z, Units = ModelUnits.Meters };
            m.Materials.Add(new ModelMaterial());
            m.Parts.Add(Box("V-301", new[] { 0.0, 0, 0 }, new[] { 1.0, 1, 2 }, 0));
            m.Parts.Add(Box("P-302", new[] { 3.0, 0, 0 }, new[] { 4.0, 1, 1 }, 0));
            var glb = Path.Combine(_dir, "two.glb");
            GlbWriter.WriteParts(m, glb);
            var step = Path.Combine(_dir, "two.step");
            var script = Path.Combine(_dir, "two.json");
            File.WriteAllText(script, new JObject
            {
                ["commands"] = new JArray
                {
                    new JObject { ["command"] = "file.insert_mesh", ["params"] = new JObject { ["path"] = glb } },
                    new JObject { ["command"] = "mesh.convert", ["params"] = new JObject { ["body"] = "V-301" } },
                    new JObject { ["command"] = "mesh.convert", ["params"] = new JObject { ["body"] = "P-302" } },
                    new JObject { ["command"] = "file.export", ["params"] = new JObject { ["path"] = step } }
                }
            }.ToString());
            var cli = Path.Combine(folder, Environment.OSVersion.Platform == PlatformID.Win32NT ? "solidcraft-cli.exe" : "solidcraft-cli");
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(cli, "run \"" + script + "\" --quiet") { UseShellExecute = false, CreateNoWindow = true });
            p.WaitForExit(120000);
            Assume.That(File.Exists(step), Is.True, "this SolidCraft could not write the STEP file of the test");

            var read = ModelImporter.Read(step);
            Assert.That(read.Format, Is.EqualTo("step"));
            Assert.That(read.Parts.Count, Is.EqualTo(2));
            Assert.That(read.Parts.Select(x => x.Path[1]), Is.EquivalentTo(new[] { "V-301", "P-302" }), "each product is the top of its branch: " + string.Join(", ", read.Parts.Select(x => x.Name)));
            Assert.That(read.Parts.All(x => x.Path != null && x.Path.Length >= 2 && !x.Path.Any(s => System.Text.RegularExpressions.Regex.IsMatch(s, @":\d+$"))), Is.True,
                string.Join(", ", read.Parts.Select(x => x.Name)));
            ModelGeometry.Normalize(read, new Placement3D { Scale = 1, Up = UpAxis.Y });
            double[] min, max;
            read.Bounds(out min, out max);
            Assert.That(max[2] - min[2], Is.EqualTo(2.0).Within(0.01), "the drum keeps its 2 m height through STEP");
            Assert.That(max[0] - min[0], Is.EqualTo(4.0).Within(0.01), "and the pump stays 3 m away");
        }
            // ---- 3D DWF: the Edgebreaker decoder and a package made here ----

        /// <summary>
        /// The opcodes of an open tube of n vertices per ring and r rings, the last ring closed round
        /// one dummy vertex (numbered last): the first ring is the first loop, each ring after it is
        /// C C (R C)^(n-2) R R, and the cap is C R^(n-2) E.
        /// </summary>
        static byte[] TubeOps(int n, int r)
        {
            var ops = new List<byte>();
            for (var k = 1; k < r; k++)
            {
                ops.Add(EdgeBreaker.C); ops.Add(EdgeBreaker.C);
                for (var j = 0; j < n - 2; j++) { ops.Add(EdgeBreaker.R); ops.Add(EdgeBreaker.C); }
                ops.Add(EdgeBreaker.R); ops.Add(EdgeBreaker.R);
            }
            ops.Add(EdgeBreaker.C);
            for (var j = 0; j < n - 2; j++) ops.Add(EdgeBreaker.R);
            ops.Add(EdgeBreaker.E);
            return ops.ToArray();
        }

        static float[] TubePoints(int n, int r)
        {
            var p = new List<float>();
            for (var k = 0; k < r; k++)
                for (var j = 0; j < n; j++)
                {
                    var t = 2 * Math.PI * j / n;
                    p.Add((float)Math.Cos(t)); p.Add((float)Math.Sin(t)); p.Add(k);
                }
            return p.ToArray();
        }

        [Test]
        public void EdgeBreakerDecodesASquareAndATube()
        {
            List<int> tris;
            int verts;
            Assert.That(EdgeBreaker.Decode(new[] { EdgeBreaker.R, EdgeBreaker.E }, 0, 2, out tris, out verts), Is.True);
            Assert.That(verts, Is.EqualTo(4), "R then E: a loop of four closed by two triangles");
            Assert.That(tris, Is.EqualTo(new[] { 0, 1, 2, 0, 2, 3 }));

            const int n = 6, r = 4;
            var ops = TubeOps(n, r);
            Assert.That(EdgeBreaker.Decode(ops, 0, ops.Length, out tris, out verts), Is.True);
            Assert.That(verts, Is.EqualTo(n * r + 1), "the rings and the dummy");
            Assert.That(tris.Count / 3, Is.EqualTo(ops.Length));
            var pts = TubePoints(n, r);
            var edges = new Dictionary<long, int>();
            var real = 0;
            for (var t = 0; t < tris.Count; t += 3)
            {
                if (tris[t] == n * r || tris[t + 1] == n * r || tris[t + 2] == n * r) continue;
                real++;
                for (var k = 0; k < 3; k++)
                {
                    int a = tris[t + k], b = tris[t + (k + 1) % 3];
                    double dx = pts[3 * a] - pts[3 * b], dy = pts[3 * a + 1] - pts[3 * b + 1], dz = pts[3 * a + 2] - pts[3 * b + 2];
                    Assert.That(Math.Sqrt(dx * dx + dy * dy + dz * dz), Is.LessThan(1.5), "every edge joins neighbours of the tube");
                    var key = ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
                    int c;
                    edges.TryGetValue(key, out c);
                    edges[key] = c + 1;
                }
            }
            Assert.That(real, Is.EqualTo(2 * n * (r - 1)), "the tube without the cap round the dummy");
            Assert.That(edges.Values.Max(), Is.LessThanOrEqualTo(2), "manifold");

            Assert.That(EdgeBreaker.Decode(new[] { EdgeBreaker.R, EdgeBreaker.R }, 0, 2, out tris, out verts), Is.False, "a branch must end in E");
            Assert.That(EdgeBreaker.Decode(new byte[] { 7 }, 0, 1, out tris, out verts), Is.False, "an unknown opcode");
        }

        /// <summary>Writes the opcodes of a W3D stream.</summary>
        class W3d
        {
            readonly MemoryStream _s = new MemoryStream();
            readonly BinaryWriter _w;
            public W3d() { _w = new BinaryWriter(_s); }
            public W3d Op(char c) { _w.Write((byte)c); return this; }
            public W3d B(params byte[] b) { _w.Write(b); return this; }
            public W3d F(params float[] f) { foreach (var x in f) _w.Write(x); return this; }
            public W3d I(params int[] i) { foreach (var x in i) _w.Write(x); return this; }
            public W3d Open(string name) { Op('('); _w.Write((byte)name.Length); _w.Write(Encoding.ASCII.GetBytes(name)); return this; }
            public W3d Object(string id) { Open(id).Op('q').Op('U'); _w.Write((ushort)4); _w.Write(Encoding.ASCII.GetBytes("node")); return this; }
            public W3d Close() => Op(')');
            public byte[] Bytes() { _w.Flush(); return _s.ToArray(); }
        }

        /// <summary>
        /// A 3D DWF package as Navisworks writes it: the "(DWF V06.00)" header, then a zip whose offsets
        /// count from the start of the file, with the tree of objects and the W3D stream.
        /// </summary>
        static void WriteDwf(string path, string objectsXml, byte[] opcodes)
        {
            byte[] w3d;
            using (var ms = new MemoryStream())
            {
                var head = Encoding.ASCII.GetBytes(";; HSF V12.15 ");
                ms.Write(head, 0, head.Length);
                ms.WriteByte(0);
                ms.WriteByte((byte)'Z');
                using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(opcodes, 0, opcodes.Length);
                w3d = ms.ToArray();
            }
            using (var f = File.Create(path))
            {
                var header = Encoding.ASCII.GetBytes("(DWF V06.00)");
                f.Write(header, 0, header.Length);
                using (var zip = new ZipArchive(f, ZipArchiveMode.Create, true))
                {
                    Action<string, byte[]> add = (name, data) =>
                    {
                        using (var e = zip.CreateEntry(name).Open()) e.Write(data, 0, data.Length);
                    };
                    add("manifest.xml", Encoding.UTF8.GetBytes("<Manifest/>"));
                    add("com.autodesk.dwf.eModel_1/descriptor.xml", Encoding.UTF8.GetBytes("<eModel:Space xmlns:eModel=\"DWF-eModel:1.0\"><eModel:Units type=\"m\"/></eModel:Space>"));
                    add("com.autodesk.dwf.eModel_1/objects.xml", Encoding.UTF8.GetBytes(objectsXml));
                    add("com.autodesk.dwf.eModel_1/geometry.w3d", w3d);
                }
            }
        }

        static string Obj(string id, string name, string children = null) =>
            "<Object id=\"" + id + "\"" + (children == null ? "" : " children=\"" + children + " \"") + "><Properties id=\"p" + id + "\"><Property name=\"_name\" value=\"" + name + "\" category=\"hidden\"/></Properties></Object>";

        [Test]
        public void DwfObjectsComeWithTheirNamesPlacesAndShapes()
        {
            var xml = "<SpaceObjectDefinition><Objects>" + Obj("1001", "Plant", "1002 1003 1004 1005 1006 1007") + Obj("1002", "/V-1") + Obj("1003", "/SLAB") +
                Obj("1004", "/E-1") + Obj("1005", "/ODD") + Obj("1006", "/P-1") + Obj("1007", "/H-1") + "</Objects></SpaceObjectDefinition>";
            var s = new W3d();
            s.Op('}').B(1).F(0, 0, 10, 0, 0, 0, 0, 1, 0, 5, 5).B(7).B(Encoding.ASCII.GetBytes("default"));
            s.Object("1001");
            // a vessel: moved 10 m along x, red, a cylinder 2 m tall with both caps and a disk of 1 m under it
            s.Object("1002").Open("").Op('q').Op('%').F(1, 0, 0, 0, 1, 0, 0, 0, 1, 10, 0, 0).Op('"').B(7).B(1, 0).B(255, 0, 0)
                .Op('Y').F(0, 0, 0, 0, 0, 2, 0.5f).B(3)
                .Op('C').F(1, 0, -1, 0, 1, -1, -1, 0, -1).B(1).F(0, 0, -1)
                .Close().Close();
            // a slab: a face shell of a square (two triangles), with a normals record
            s.Object("1003").Op('S').B(0x1A, 0).I(4).F(0, 0, 0, 4, 0, 0, 4, 3, 0, 0, 3, 0)
                .B(1).I(9).B(8, 3, 0, 1, 2, 3, 0, 2, 3)
                .B(1, 7, 12).I(2).B(0, 0).B(0)
                .Close();
            // a tube: an Edgebreaker shell of 6 x 4 points, the cap round a dummy (numbered 24)
            const int n = 6, r = 4;
            var ops = TubeOps(n, r);
            var padded = (ops.Length + 3) & ~3;
            var table = new[] { 8, 1, n * r };
            var size = 24 + padded + 4 * table.Length;
            s.Object("1004").Op('S').B(0x5A, 0, 5).I(size).B(2, 0, 0, 0).I(ops.Length, 4 * table.Length, 0, n * r, 0)
                .B(ops).B(new byte[padded - ops.Length]).I(table).F(TubePoints(n, r)).B(0)
                .Close();
            // a record the reader does not know, with bytes that mean nothing
            s.Object("1005").Op('!').B(9, 9, 9, 9).Close();
            // the reader goes on from the next object
            s.Object("1006").Op('Y').F(0, 0, 0, 1, 0, 0, 0.2f).B(0).Close();
            // two squares decoded apart (R E twice, vertices 0-3 and 4-7); vertices 4 and 7 are
            // duplicates of points 1 and 2, so the squares share an edge: table mask 0x10, four
            // pair ints, pairs (delta 4, point 1) and (delta 3, point 2)
            var squares = new[] { EdgeBreaker.R, EdgeBreaker.E, EdgeBreaker.R, EdgeBreaker.E };
            var handles = new[] { 0x10, 4, 4, 1, 3, 2 };
            s.Object("1007").Op('S').B(0x5A, 0, 5).I(24 + 4 + 4 * handles.Length).B(2, 0, 0, 0).I(squares.Length, 4 * handles.Length, 0, 6, 0)
                .B(squares).I(handles).F(0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 2, 0, 0, 2, 1, 0).B(0)
                .Close();
            s.Close();

            var path = Path.Combine(_dir, "plant.dwf");
            WriteDwf(path, xml, s.Bytes());
            var m = ModelImporter.Read(path);
            var stats = DwfReader.LastStats;

            Assert.That(m.Format, Is.EqualTo("dwf"));
            Assert.That(m.Units, Is.EqualTo(ModelUnits.Meters));
            Assert.That(m.Up, Is.EqualTo(UpAxis.Z));
            Assert.That(stats.Objects, Is.EqualTo(7));
            Assert.That(stats.Cylinders, Is.EqualTo(2));
            Assert.That(stats.Circles, Is.EqualTo(1));
            Assert.That(stats.FaceShells, Is.EqualTo(1));
            Assert.That(stats.EdgeBreakerShells, Is.EqualTo(2));
            Assert.That(stats.HandleShells, Is.EqualTo(1));
            Assert.That(stats.SkippedShells, Is.EqualTo(0));
            Assert.That(stats.Resyncs, Is.EqualTo(1), "the unknown record is skipped once");
            Assert.That(m.Warnings.Any(w => w.Contains("skipped")), Is.True);

            var vessel = m.Parts.Single(p => p.Name == "/V-1");
            Assert.That(vessel.Path, Is.EqualTo(new[] { "Plant", "/V-1" }));
            Assert.That(vessel.ObjectId, Is.EqualTo("1002"));
            Assert.That(m.Materials[vessel.Material].Color[0], Is.EqualTo(1f).Within(1e-6));
            Assert.That(m.Materials[vessel.Material].Color[1], Is.EqualTo(0f).Within(1e-6));
            var v = new ImportedModel { Parts = { vessel } };
            double[] min, max;
            v.Bounds(out min, out max);
            Assert.That(min[0], Is.EqualTo(9).Within(1e-4), "the disk of 1 m round x = 10");
            Assert.That(max[0], Is.EqualTo(11).Within(1e-4));
            Assert.That(min[2], Is.EqualTo(-1).Within(1e-4));
            Assert.That(max[2], Is.EqualTo(2).Within(1e-4));

            Assert.That(m.Parts.Single(p => p.Name == "/SLAB").TriangleCount, Is.EqualTo(2));
            var tube = m.Parts.Single(p => p.Name == "/E-1");
            Assert.That(tube.TriangleCount, Is.EqualTo(2 * n * (r - 1)), "the dummy's cap is left out");
            Assert.That(tube.VertexCount, Is.EqualTo(n * r));
            Assert.That(m.Parts.Any(p => p.Name == "/P-1"), Is.True, "the object after the unknown record is read");
            Assert.That(m.Parts.Any(p => p.Name == "/ODD"), Is.False);

            // the duplicates take the points their pairs name: two squares side by side, one island
            var joined = m.Parts.Single(p => p.Name == "/H-1");
            Assert.That(joined.TriangleCount, Is.EqualTo(4));
            Assert.That(joined.VertexCount, Is.EqualTo(6), "the six points, the duplicates do not add any");
            var h = new ImportedModel { Parts = { joined } };
            h.Bounds(out min, out max);
            Assert.That(max[0] - min[0], Is.EqualTo(2).Within(1e-6));
            Assert.That(ModelGeometry.Islands(h).Count, Is.EqualTo(1), "the shared edge joins the squares");

            // the import splits the objects into islands as for any model
            var import = ModelImporter.Import(path);
            Assert.That(import.Islands.Count, Is.GreaterThanOrEqualTo(4));
        }
            // ---- M1: the link imports a plant model for the 3D window ----

        static JObject Wait(List<JObject> sent, string type, int seconds = 60)
        {
            var until = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < until)
            {
                lock (sent)
                {
                    var m = sent.LastOrDefault(x => (string)x["type"] == type && (string)x["state"] != "importing");
                    if (m != null) return m;
                }
                System.Threading.Thread.Sleep(50);
            }
            return null;
        }

        [Test]
        public void TheLinkImportsAPlantModelForTheWindowAndUndoesIt()
        {
            var fs = Flowsheet.Create("plant model").WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables);
            fs.AddMaterialStream("1").At(300.Kelvin(), 101325.0.Pascal()).WithMassFlow(1.KgPerSecond());
            // the plant somewhere far from the origin, as site coordinates put it
            var far = Plant();
            foreach (var p in far.Parts)
                for (var i = 0; i < p.Positions.Length; i += 3) { p.Positions[i] += 1000; p.Positions[i + 1] += 2000; p.Positions[i + 2] += 60; }
            var glb = Path.Combine(_dir, "far.glb");
            GlbWriter.WriteProp(far, "Plant", glb);

            var link = new FlowsheetLink(fs.Inner);
            var sent = new List<JObject>();
            link.MessageSent += m => { lock (sent) sent.Add(m); };
            var answer = link.Handle(new JObject { ["type"] = SceneProtocol.ModelImport, ["path"] = glb });
            Assert.That((string)answer["type"], Is.EqualTo(SceneProtocol.Ack), "the import starts at once and runs apart");
            var status = Wait(sent, SceneProtocol.ModelStatus);
            Assert.That(status, Is.Not.Null, "DWSIM tells how the import went");
            Assert.That((string)status["state"], Is.EqualTo("done"), (string)status["message"]);
            Assert.That((string)status["message"], Does.Contain("triangles"));

            var stored = ModelStore.Load(fs.Inner);
            Assert.That(stored, Is.Not.Null);
            Assert.That(stored.Groups.Count(g => g.Role == "equipment"), Is.EqualTo(3), "the column, the drum and the pump are found at import");
            Assert.That((string)status["message"], Does.Contain("equipment"));
            Assert.That(stored.Model.Place.Offset[0], Is.EqualTo(-1000 - 9.25).Within(0.01), "the middle of the plan goes to the origin");
            Assert.That(stored.Model.Place.Offset[2], Is.EqualTo(-60).Within(0.01), "the lowest point goes to the ground");

            link.Sync();
            var info = link.Scene["model"] as JObject;
            Assert.That(info, Is.Not.Null);
            Assert.That((string)info["name"], Is.EqualTo("far.glb"));
            Assert.That(File.Exists((string)info["glb"]), Is.True, "the window gets the view of the cache");
            var view = GltfReader.Read((string)info["glb"]);
            double[] min, max;
            view.Bounds(out min, out max);
            Assert.That(min[1], Is.EqualTo(0).Within(1e-3), "in the view file (Y up) the plant stands on the ground");
            Assert.That(view.Parts.All(p => p.Name.StartsWith("tile_")), Is.True);

            Assert.That((string)link.Handle(new JObject { ["type"] = SceneProtocol.Undo })["type"], Is.EqualTo(SceneProtocol.Ack));
            Assert.That(ModelStore.Load(fs.Inner), Is.Null, "the window's undo takes the import back");
            link.Handle(new JObject { ["type"] = SceneProtocol.Redo });
            Assert.That(ModelStore.Load(fs.Inner), Is.Not.Null);

            link.Handle(new JObject { ["type"] = SceneProtocol.ModelRemove });
            Assert.That(ModelStore.Load(fs.Inner), Is.Null);
            link.Sync();
            Assert.That(link.Scene["model"]?.Type ?? JTokenType.Null, Is.EqualTo(JTokenType.Null));

            var missing = link.Handle(new JObject { ["type"] = SceneProtocol.ModelImport, ["path"] = Path.Combine(_dir, "none.dwf") });
            Assert.That((string)missing["type"], Is.EqualTo(SceneProtocol.Error));
        }

        [Test]
        public void TheViewFileCutsTheModelInTilesWithSharpEdges()
        {
            var plant = Plant();
            var path = Path.Combine(_dir, "view.glb");
            GlbWriter.WriteTiles(plant, path, tile: 5);
            var view = GltfReader.Read(path);
            Assert.That(view.Parts.Count, Is.GreaterThan(2), "the plant is about 20 m long: several tiles");
            Assert.That(view.TriangleCount, Is.EqualTo(plant.TriangleCount));
            // a box with its eight corners shared, as a decoded shell has them, keeps flat faces:
            // each corner splits into one vertex per face
            var box = new ImportedModel();
            box.Parts.Add(new MeshPart
            {
                Name = "b",
                Positions = new float[] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 1, 1, 0, 1, 1, 1, 1, 0, 1, 1 },
                Indices = new[] { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7 }
            });
            var bp = Path.Combine(_dir, "box.glb");
            GlbWriter.WriteProp(box, "box", bp);
            var read = GltfReader.Read(bp);
            Assert.That(read.Parts[0].VertexCount, Is.EqualTo(24), "eight corners, three faces each");
        }
            // ---- M2: shapes, groups and the matching with the flowsheet ----

        /// <summary>The plant as one mesh, through an import (islands and their shapes in the cache).</summary>
        ModelImportResult MergedPlant()
        {
            var glb = Path.Combine(_dir, "merged-plant.glb");
            GlbWriter.WriteProp(Plant(), "Plant", glb);
            return ModelImporter.Import(glb);
        }

        [Test]
        public void IslandsAreClassifiedByShape()
        {
            var plant = Plant();
            var islands = ModelGeometry.Islands(plant);
            var shapes = IslandShapes.Classify(plant, islands);
            Assert.That(shapes[0].Class, Is.EqualTo(ShapeClass.Column), "C-1, 2 m by 20 m, standing");
            Assert.That(shapes[0].Radius, Is.EqualTo(1).Within(0.05));
            Assert.That(shapes[0].Length, Is.EqualTo(20).Within(0.1));
            Assert.That(shapes[1].Class, Is.EqualTo(ShapeClass.HorizontalVessel), "D-1, lying");
            Assert.That(shapes[2].Class, Is.EqualTo(ShapeClass.Box), "P-1");
            Assert.That(shapes[3].Class, Is.EqualTo(ShapeClass.Pipe), "L-1");
            Assert.That(shapes[4].Class, Is.EqualTo(ShapeClass.Pipe), "L-2");

            // the cache keeps them
            var r = MergedPlant();
            var again = ModelImporter.Cached(r.Reference);
            Assert.That(again.Islands.Select(i => i.Shape.Class).OrderBy(c => c), Is.EqualTo(shapes.Select(s => s.Class).OrderBy(c => c)));
        }

        [Test]
        public void AMeshWithoutATreeIsGroupedByItsShapes()
        {
            var groups = ModelSegmentation.Groups(MergedPlant());
            var eq = groups.Where(g => g.Role == "equipment").ToList();
            Assert.That(eq.Count, Is.EqualTo(3), "the column, the drum and the pump");
            var column = eq.Single(g => g.Category == "column");
            var drum = eq.Single(g => g.Category == "vessel");
            var pump = eq.Single(g => g.Body == ShapeClass.Box);
            Assert.That(column.Vertical, Is.True);
            Assert.That(column.Length, Is.EqualTo(20).Within(0.2));
            Assert.That(drum.Vertical, Is.False);
            Assert.That(groups.Count(g => g.Role == "piping"), Is.EqualTo(2), "the two lines");
            Assert.That(column.Neighbours, Is.EquivalentTo(new[] { drum.Id }), "L-1 joins the column and the drum");
            Assert.That(drum.Neighbours, Is.EquivalentTo(new[] { column.Id, pump.Id }));
        }

        [Test]
        public void AModelWithATreeIsGroupedByItsTagsAndWords()
        {
            var m = new ImportedModel { Format = "test", Up = UpAxis.Z, Units = ModelUnits.Meters };
            m.Materials.Add(new ModelMaterial());
            Action<MeshPart, string[]> add = (p, path) => { p.Path = path; p.Name = path.Last(); m.Parts.Add(p); };
            add(Cylinder("shell", new[] { 0.0, 0, 0 }, new[] { 0.0, 0, 6 }, 1.0, 0), new[] { "Plant", "/AREA/EQUIPAMENTOS", "/AREA/VASOS", "/V-101", "/V-101/CORPO" });
            add(Cylinder("nozzle", new[] { 1.0, 0, 3 }, new[] { 1.4, 0, 3 }, 0.1, 0), new[] { "Plant", "/AREA/EQUIPAMENTOS", "/AREA/VASOS", "/V-101", "/V-101/BOCAIS" });
            add(Box("pump", new[] { 5.0, 0, 0 }, new[] { 6.0, 1, 1 }, 0), new[] { "Plant", "/AREA/EQUIPAMENTOS", "/AREA/BOMBAS", "/P-102A", "/P-102A/CORPO" });
            add(Cylinder("pipe", new[] { 1.4, 0, 3 }, new[] { 5.0, 0, 0.5 }, 0.1, 0), new[] { "Plant", "/AREA/TUBULACAO", "/AREA/LINHAS-P", "/4\"-P-001", "/4\"-P-001/B1", "TUBE 1 of BRANCH" });
            add(Box("beam", new[] { -5.0, -5, 5 }, new[] { 5.0, -4.8, 5.2 }, 0), new[] { "Plant", "/AREA/ESTRUTURAS", "/AREA/PIPE-RACK", "/PR-1" });
            add(Box("slab", new[] { -10.0, -10, -0.2 }, new[] { 10.0, 10, 0 }, 0), new[] { "Plant", "/AREA/CIVIL", "/AREA/PISOS", "/PISO-1" });
            var path = Path.Combine(_dir, "tree.glb");
            // the tree goes through the importer as a DWF would bring it: built here on the result
            var islands = ModelGeometry.Islands(m);
            var shapes = IslandShapes.Classify(m, islands);
            for (var i = 0; i < islands.Count; i++) islands[i].Shape = shapes[i];
            var index = new ModelImportResult { Islands = islands, Parts = m.Parts.Count, PartNames = m.Parts.Select(p => p.Name).ToList(), PartPaths = m.Parts.Select(p => p.Path).ToList() };
            var groups = ModelSegmentation.Groups(index);

            var v = groups.Single(g => g.Tag == "V-101");
            Assert.That(v.Role, Is.EqualTo("equipment"));
            Assert.That(v.Islands.Count, Is.EqualTo(2), "the shell and its nozzle are one vessel");
            Assert.That(v.Category, Is.EqualTo("vessel"));
            var p = groups.Single(g => g.Tag == "P-102A");
            Assert.That(p.Category, Is.EqualTo("pump"));
            Assert.That(groups.Single(g => g.Role == "piping").Name, Is.EqualTo("/4\"-P-001"));
            Assert.That(groups.Single(g => g.Role == "structure").Name, Is.EqualTo("/PR-1"));
            Assert.That(groups.Single(g => g.Role == "scenery").Name, Is.EqualTo("/PISO-1"));
            Assert.That(v.Neighbours, Is.EquivalentTo(new[] { p.Id }), "the line joins the vessel and the pump");
        }

        [Test]
        public void TheMatchingTrustsTagsAndProposesOnlyClearCasesWithoutThem()
        {
            var groups = ModelSegmentation.Groups(MergedPlant());
            var eq = groups.Where(g => g.Role == "equipment").ToList();
            var column = eq.Single(g => g.Category == "column");
            var drum = eq.Single(g => g.Category == "vessel");
            var pump = eq.Single(g => g.Body == ShapeClass.Box);
            // the canvas turned a quarter turn from the plan, with a second pump that nothing joins, far off
            var units = new List<FlowsheetUnit>
            {
                new FlowsheetUnit { Name = "a", Tag = "T-10", Category = "column", X = 0, Y = 0, Length = 18, Diameter = 2.2, Vertical = true },
                new FlowsheetUnit { Name = "b", Tag = "V-20 (separador)", Category = "vessel", X = 0, Y = -110, Length = 5, Diameter = 2, Vertical = false },
                new FlowsheetUnit { Name = "c", Tag = "B-30", Category = "pump", X = 0, Y = -190, Length = 1.5, Diameter = 1 },
                new FlowsheetUnit { Name = "d", Tag = "B-99", Category = "pump", X = 400, Y = 300, Length = 1.5, Diameter = 1 }
            };
            units[0].Neighbours.Add("b");
            units[1].Neighbours.Add("a");
            units[1].Neighbours.Add("c");
            units[2].Neighbours.Add("b");
            var right = new Dictionary<string, string> { [column.Id] = "a", [drum.Id] = "b", [pump.Id] = "c" };

            // without tags, what is proposed is right, and the far pump is never proposed
            foreach (var p in ModelMatching.Suggest(groups, units))
                Assert.That(p.Object, Is.EqualTo(right[p.Group]), p.Group + ": " + p.Why);

            // with tags: the same tag, a tag read from a name with words, a tag of the same family; and
            // a flowsheet that draws the drum as two objects of the same tag
            column.Tag = "T-10";
            drum.Tag = "V-20";
            pump.Tag = "B-30A";
            units.Add(new FlowsheetUnit { Name = "e", Tag = "V-20 (lado b)", Category = "vessel", X = 10, Y = -120, Length = 5, Diameter = 2 });
            var s = ModelMatching.Suggest(groups, units).ToDictionary(x => x.Group);
            Assert.That(s[column.Id].Object, Is.EqualTo("a"));
            Assert.That(s[column.Id].Why, Does.Contain("same tag"));
            Assert.That(new[] { s[drum.Id].Object }.Concat(s[drum.Id].Also), Is.EquivalentTo(new[] { "b", "e" }), "V-20 is read from the names, and both objects go with the drum");
            Assert.That(s[pump.Id].Object, Is.EqualTo("c"));
            Assert.That(s[pump.Id].Why, Does.Contain("similar tag"));
            Assert.That(s.Values.Any(x => x.Object == "d"), Is.False);
            units.RemoveAt(units.Count - 1);

            // an object the user chose is not proposed again, and the lines from it still count
            var t = ModelMatching.Suggest(groups, units, new Dictionary<string, string> { [drum.Id] = "b" }).ToDictionary(x => x.Group);
            Assert.That(t.ContainsKey(drum.Id), Is.False);
            Assert.That(t.Values.Any(x => x.Object == "b"), Is.False);
            Assert.That(t[pump.Id].Why, Does.Contain("joined like the streams"), "the drum the user chose joins the pump to B-30");
        }

        [Test]
        public void AModelNamedByPartsGivesTheGroupsTheirTags()
        {
            var glb = Path.Combine(_dir, "parts.glb");
            GlbWriter.WriteParts(Plant(), glb);
            var groups = ModelSegmentation.Groups(ModelImporter.Import(glb));
            var tags = groups.Where(g => g.Role == "equipment").Select(g => g.Tag).ToList();
            Assert.That(tags, Is.EquivalentTo(new[] { "C-101", "D-102", "P-103" }));
            Assert.That(groups.Single(g => g.Tag == "D-102").Category, Is.EqualTo("vessel"));
            Assert.That(groups.Single(g => g.Tag == "P-103").Category, Is.EqualTo("pump"));
        }

        [Test]
        public void HungarianFindsTheBestAssignment()
        {
            var a = ModelMatching.Hungarian(new double[,] { { 1, 9, 2 }, { 8, 7, 1 }, { 2, 8, 9 } });
            Assert.That(a, Is.EqualTo(new[] { 1, 0, 2 }), "9 + 8 + 9");
            var wide = ModelMatching.Hungarian(new double[,] { { 1, 5, 3, 4 } });
            Assert.That(wide, Is.EqualTo(new[] { 1 }));
            var tall = ModelMatching.Hungarian(new double[,] { { 1 }, { 6 }, { 3 } });
            Assert.That(tall, Is.EqualTo(new[] { -1, 0, -1 }));
        }

        [Test]
        public void FlowsheetUnitsHaveKindsAndNeighboursThroughValves()
        {
            var fs = Flowsheet.Create("units").WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables);
            var feed = fs.AddMaterialStream("1").At(300.Kelvin(), 101325.0.Pascal()).WithMassFlow(1.KgPerSecond());
            var s2 = fs.AddMaterialStream("2");
            var s3 = fs.AddMaterialStream("3");
            var s4 = fs.AddMaterialStream("4");
            var s5 = fs.AddMaterialStream("5");
            fs.AddPump("P-1").ConnectFeed(feed, 0).ConnectProduct(s2, 0);
            fs.AddValve("FV-1").ConnectFeed(s2, 0).ConnectProduct(s3, 0);
            fs.AddSeparator("V-1").ConnectFeed(s3, 0).ConnectProduct(s4, 0).ConnectProduct(s5, 1);
            fs.AutoLayout();
            var units = ModelMatching.Units(fs.Inner, SceneBuilder.Model(fs.Inner));
            Assert.That(units.Select(u => u.Tag), Is.EquivalentTo(new[] { "P-1", "V-1" }), "the valve is not equipment one matches");
            var pump = units.Single(u => u.Tag == "P-1");
            var vessel = units.Single(u => u.Tag == "V-1");
            Assert.That(pump.Category, Is.EqualTo("pump"));
            Assert.That(vessel.Category, Is.EqualTo("vessel"));
            Assert.That(vessel.Length, Is.GreaterThan(0));
            Assert.That(pump.Neighbours, Is.EquivalentTo(new[] { vessel.Name }), "the stream reaches the vessel through the valve");
        }
            // ---- M3: the review of the groups through the link ----

        static JObject LastOf(List<JObject> sent, string type)
        {
            lock (sent) return sent.LastOrDefault(x => (string)x["type"] == type);
        }

        static JObject GroupOf(JObject payload, Func<JObject, bool> where) => payload["groups"].OfType<JObject>().FirstOrDefault(where);

        [Test]
        public void TheReviewAssignsJoinsSplitsCreatesAndUndoes()
        {
            var fs = Flowsheet.Create("review").WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables);
            var feed = fs.AddMaterialStream("1").At(300.Kelvin(), 101325.0.Pascal()).WithMassFlow(1.KgPerSecond());
            var s2 = fs.AddMaterialStream("2");
            var s3 = fs.AddMaterialStream("3");
            var s4 = fs.AddMaterialStream("4");
            fs.AddSeparator("D-102").ConnectFeed(feed, 0).ConnectProduct(s2, 0).ConnectProduct(s3, 1);
            fs.AddPump("P-103").ConnectFeed(s3, 0).ConnectProduct(s4, 0);
            fs.AutoLayout();
            var glb = Path.Combine(_dir, "review.glb");
            GlbWriter.WriteParts(Plant(), glb);

            var link = new FlowsheetLink(fs.Inner);
            var sent = new List<JObject>();
            link.MessageSent += m => { lock (sent) sent.Add(m); };
            link.Handle(new JObject { ["type"] = SceneProtocol.ModelImport, ["path"] = glb });
            Assert.That((string)Wait(sent, SceneProtocol.ModelStatus)["state"], Is.EqualTo("done"));
            link.ModelEditsInline = true;
            Func<string, JObject, JObject> edit = (type, fields) =>
            {
                fields["type"] = type;
                var a = link.Handle(fields);
                Assert.That((string)a["type"], Is.EqualTo(SceneProtocol.Ack), type);
                var err = LastOf(sent, SceneProtocol.Error);
                var last = LastOf(sent, SceneProtocol.ModelGroups);
                Assert.That(err == null || sent.IndexOf(err) < sent.IndexOf(last), Is.True, type + ": " + err?["message"]);
                return last;
            };
            Func<string, string> id = tag => fs.Inner.GetFlowsheetSimulationObject(tag).Name;

            var payload = edit(SceneProtocol.ModelGroups, new JObject());
            Assert.That(payload["groups"].Count(), Is.EqualTo(5), "the three equipment groups and the two lines");
            Assert.That(payload["units"].Select(u => (string)u["tag"]), Is.EquivalentTo(new[] { "D-102", "P-103" }));
            var column = GroupOf(payload, g => (string)g["kind"] == "column");
            var drum = GroupOf(payload, g => (string)g["kind"] == "vessel");
            var pump = GroupOf(payload, g => (string)g["role"] == "equipment" && (string)g["kind"] != "column" && (string)g["kind"] != "vessel");
            var line = payload["groups"].OfType<JObject>().Where(g => (string)g["role"] == "piping").OrderByDescending(g => (double)g["c"][0]).First();
            Assert.That((string)drum["suggested"], Is.EqualTo(id("D-102")), "the drum is proposed for the separator of the same tag");

            // the user takes the proposal; the undo of the window takes it back
            payload = edit(SceneProtocol.ModelAssign, new JObject { ["group"] = drum["id"], ["accept"] = true });
            Assert.That((string)GroupOf(payload, g => (string)g["id"] == (string)drum["id"])["object"], Is.EqualTo(id("D-102")));
            Assert.That(ModelStore.Load(fs.Inner).Groups.Single(g => g.Id == (string)drum["id"]).Object, Is.EqualTo(id("D-102")));
            link.Handle(new JObject { ["type"] = SceneProtocol.Undo });
            Assert.That(ModelStore.Load(fs.Inner).Groups.Single(g => g.Id == (string)drum["id"]).Object, Is.Null);
            link.Handle(new JObject { ["type"] = SceneProtocol.Redo });

            // the separator goes where the drum is, and stays there
            link.Sync();
            var drumGroup = ModelStore.Load(fs.Inner).Groups.Single(g => g.Id == (string)drum["id"]);
            var placed = PlantLayout.Load(fs.Inner)[id("D-102")];
            Assert.That(placed.X, Is.EqualTo(Math.Round((drumGroup.Min[0] + drumGroup.Max[0]) / 2 * 1000)).Within(1));
            Assert.That(placed.Z, Is.EqualTo(Math.Round(drumGroup.Min[2] * 1000)).Within(1), "on the bottom of the drum");
            var item = link.Scene["items"].OfType<JObject>().Single(i => (string)i["id"] == id("D-102"));
            Assert.That(item["model"], Is.Not.Null, "the window shows the model's drum for the separator");
            var refused = link.Handle(new JObject { ["type"] = SceneProtocol.Place, ["id"] = id("D-102"), ["x"] = 0, ["y"] = 0 });
            Assert.That((string)refused["type"], Is.EqualTo(SceneProtocol.Error));
            Assert.That((string)refused["message"], Does.Contain("plant model"));

            // the pump too: the stream between them runs in the model's pipes
            edit(SceneProtocol.ModelAssign, new JObject { ["group"] = pump["id"], ["accept"] = true });
            link.Sync();
            var stream = link.Scene["lines"].OfType<JObject>().Single(l => (string)l["id"] == id("3"));
            Assert.That((bool?)stream["replaced"], Is.True);

            // a line is hidden: it becomes a group of the user's, with its islands
            payload = edit(SceneProtocol.ModelRole, new JObject { ["group"] = line["id"], ["role"] = "hidden" });
            var hidden = GroupOf(payload, g => (string)g["role"] == "hidden");
            Assert.That(hidden, Is.Not.Null);
            Assert.That((bool?)hidden["user"], Is.True);
            Assert.That(hidden["islands"].Select(t => (int)t), Is.EquivalentTo(line["islands"].Select(t => (int)t)));

            // the drum and the pump joined, then the pump split off again
            payload = edit(SceneProtocol.ModelMerge, new JObject { ["groups"] = new JArray(drum["id"], pump["id"]) });
            var joined = GroupOf(payload, g => g["islands"].Count() == drum["islands"].Count() + pump["islands"].Count());
            Assert.That(joined, Is.Not.Null, "one group holds the drum and the pump");
            Assert.That((string)joined["object"], Is.EqualTo(id("D-102")), "the join keeps the object the drum stood for");
            payload = edit(SceneProtocol.ModelSplit, new JObject { ["group"] = joined["id"], ["islands"] = pump["islands"].DeepClone() });
            Assert.That(GroupOf(payload, g => g["islands"].Select(t => (int)t).OrderBy(x => x).SequenceEqual(pump["islands"].Select(t => (int)t).OrderBy(x => x))), Is.Not.Null);

            // the column has no object in the flowsheet: DWSIM adds one where the column stands
            var before = fs.Inner.SimulationObjects.Count;
            payload = edit(SceneProtocol.ModelCreate, new JObject { ["group"] = column["id"] });
            Assert.That(fs.Inner.SimulationObjects.Count, Is.EqualTo(before + 1));
            var made = (string)GroupOf(payload, g => (string)g["id"] == (string)column["id"])["object"];
            Assert.That(made, Is.Not.Null);
            Assert.That(fs.Inner.SimulationObjects[made].GraphicObject.ObjectType.ToString(), Is.EqualTo("ShortcutColumn"));
            Assert.That(PlantLayout.Load(fs.Inner).ContainsKey(made), Is.True, "the new object stands where the model's column is");
            var missing = payload["links"].OfType<JObject>().FirstOrDefault(l => (string)l["from"] == made || (string)l["to"] == made);
            Assert.That(missing, Is.Not.Null, "a pipe joins the column and the drum, no stream does: the review offers it");
            link.Handle(new JObject { ["type"] = SceneProtocol.Undo });
            Assert.That(fs.Inner.SimulationObjects.ContainsKey(made), Is.False, "one undo takes back the object and the choice");
            Assert.That(ModelStore.Load(fs.Inner).Groups.Any(g => g.Object == made), Is.False);

            // a wrong request says why
            link.Handle(new JObject { ["type"] = SceneProtocol.ModelAssign, ["group"] = "nope", ["object"] = null });
            Assert.That((string)LastOf(sent, SceneProtocol.Error)["message"], Does.Contain("no group"));
        }
    }
}
