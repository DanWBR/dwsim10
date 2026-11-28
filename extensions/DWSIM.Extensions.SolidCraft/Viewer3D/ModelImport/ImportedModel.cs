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

namespace DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport
{
    /// <summary>The axis that points up in a model file.</summary>
    public enum UpAxis { Unknown, Y, Z }

    /// <summary>The length unit of a model file, when the format says it.</summary>
    public enum ModelUnits { Unknown, Meters, Centimeters, Millimeters, Feet, Inches }

    /// <summary>A material as the importer keeps it: the base colour and its texture, and how it is see-through.</summary>
    public class ModelMaterial
    {
        public string Name;
        /// <summary>The base colour, linear RGBA from 0 to 1.</summary>
        public float[] Color = { 0.7f, 0.7f, 0.7f, 1f };
        /// <summary>The base colour texture as a PNG or JPEG file, or null.</summary>
        public byte[] Texture;
        /// <summary>"image/png" or "image/jpeg".</summary>
        public string TextureMime;
        /// <summary>OPAQUE, MASK (cut out, as leaves are) or BLEND.</summary>
        public string AlphaMode = "OPAQUE";
        public float AlphaCutoff = 0.5f;
        public bool DoubleSided;
        public float Metallic;
        public float Roughness = 0.8f;
    }

    /// <summary>
    /// One mesh of the file in the frame of the file (its own unit and up axis), with the node
    /// transforms applied: the corners of the triangles, their texture coordinates and the material.
    /// </summary>
    public class MeshPart
    {
        /// <summary>The path of the node in the file ("Plant/Area 1/V-101"), or the group name.</summary>
        public string Name;
        /// <summary>The index of the material in <see cref="ImportedModel.Materials"/>, or -1.</summary>
        public int Material = -1;
        /// <summary>The names from the root of the file's object tree down to this part, when the file has a tree (DWF).</summary>
        public string[] Path;
        /// <summary>The id of the object in the file's tree, when it has one.</summary>
        public string ObjectId;
        /// <summary>The role the file says the part has (equipment, piping, structure or scenery: an IFC class does), or null.</summary>
        public string Role;
        /// <summary>x, y, z of each vertex.</summary>
        public float[] Positions = new float[0];
        /// <summary>u, v of each vertex (v down, as glTF has it), or null.</summary>
        public float[] UV;
        /// <summary>Three vertex indices per triangle, counter-clockwise seen from outside.</summary>
        public int[] Indices = new int[0];

        public int VertexCount => Positions.Length / 3;
        public int TriangleCount => Indices.Length / 3;
    }

    /// <summary>A model file as read: its meshes and materials, and what the format says of its unit and up axis.</summary>
    public class ImportedModel
    {
        public string SourcePath;
        /// <summary>gltf, obj, stl, ply, fbx, dwf, ifc, step, stp, igs, iges, 3dm or fcstd.</summary>
        public string Format;
        public UpAxis Up = UpAxis.Unknown;
        public ModelUnits Units = ModelUnits.Unknown;
        public List<MeshPart> Parts = new List<MeshPart>();
        public List<ModelMaterial> Materials = new List<ModelMaterial>();
        /// <summary>What the reader passed over (a texture of a format it cannot keep, a primitive it does not draw).</summary>
        public List<string> Warnings = new List<string>();

        public int TriangleCount
        {
            get { var n = 0; foreach (var p in Parts) n += p.TriangleCount; return n; }
        }

        public int VertexCount
        {
            get { var n = 0; foreach (var p in Parts) n += p.VertexCount; return n; }
        }

        /// <summary>The box round every vertex, in the frame the model is in now; false when it has none.</summary>
        public bool Bounds(out double[] min, out double[] max)
        {
            min = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
            max = new[] { double.MinValue, double.MinValue, double.MinValue };
            var any = false;
            foreach (var p in Parts)
            {
                var a = p.Positions;
                for (var i = 0; i + 2 < a.Length; i += 3)
                {
                    any = true;
                    for (var k = 0; k < 3; k++)
                    {
                        if (a[i + k] < min[k]) min[k] = a[i + k];
                        if (a[i + k] > max[k]) max[k] = a[i + k];
                    }
                }
            }
            return any;
        }

        /// <summary>Adds a material and returns its index.</summary>
        public int AddMaterial(ModelMaterial m)
        {
            Materials.Add(m);
            return Materials.Count - 1;
        }
    }

    /// <summary>Builds a <see cref="MeshPart"/> from vertices and triangles added one by one.</summary>
    internal class PartBuilder
    {
        public readonly List<float> Positions = new List<float>();
        public List<float> UV;
        public readonly List<int> Indices = new List<int>();

        public int Add(double x, double y, double z)
        {
            Positions.Add((float)x); Positions.Add((float)y); Positions.Add((float)z);
            if (UV != null) { UV.Add(0f); UV.Add(0f); }
            return Positions.Count / 3 - 1;
        }

        public int Add(double x, double y, double z, double u, double v)
        {
            if (UV == null)
            {
                UV = new List<float>(Positions.Count / 3 * 2 + 2);
                for (var i = 0; i < Positions.Count / 3; i++) { UV.Add(0f); UV.Add(0f); }
            }
            Positions.Add((float)x); Positions.Add((float)y); Positions.Add((float)z);
            UV.Add((float)u); UV.Add((float)v);
            return Positions.Count / 3 - 1;
        }

        public void Triangle(int a, int b, int c)
        {
            if (a == b || b == c || a == c) return;
            Indices.Add(a); Indices.Add(b); Indices.Add(c);
        }

        /// <summary>A polygon as a fan from its first corner.</summary>
        public void Polygon(IList<int> corners)
        {
            for (var i = 1; i + 1 < corners.Count; i++) Triangle(corners[0], corners[i], corners[i + 1]);
        }

        public bool Empty => Indices.Count == 0;

        public MeshPart Build(string name, int material)
        {
            return new MeshPart
            {
                Name = name,
                Material = material,
                Positions = Positions.ToArray(),
                UV = UV == null ? null : UV.ToArray(),
                Indices = Indices.ToArray()
            };
        }
    }
}
