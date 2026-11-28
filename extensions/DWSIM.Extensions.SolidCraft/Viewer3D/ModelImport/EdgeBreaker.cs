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

using System.Collections.Generic;

namespace DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport
{
    /// <summary>
    /// Decodes the connectivity of a triangle mesh compressed with Edgebreaker (Rossignac, 1999),
    /// one opcode per triangle, as the shells of HSF streams keep it. The decoder keeps the loop of
    /// vertices that bounds the part of the mesh already decoded, as a linked list, and walks it:
    /// <list type="bullet">
    /// <item>C makes a new vertex;</item>
    /// <item>R and L close the triangle with the next or the previous vertex of the loop;</item>
    /// <item>E closes a loop of three;</item>
    /// <item>S splits the loop in two, and the right part is decoded first.</item>
    /// </list>
    /// Where an S lands on the loop, and how long the first loop of each component is, come from
    /// the opcodes themselves: a branch that ends in E needs a loop of three, C adds a vertex, L
    /// and R take one away.
    /// </summary>
    /// <remarks>
    /// The opcode values (C 0, L 1, E 2, R 3, S 4) and the layout of the shells were found from
    /// DWF files of plant models, for interoperability; they were checked against the points of
    /// every shell of those files (manifold and consistently oriented meshes). The dummy vertices
    /// that close holes and the duplicated vertices where a mesh touches itself are numbered like
    /// the others here; the shell's table says which they are (see DwfReader).
    /// </remarks>
    public static class EdgeBreaker
    {
        public const byte C = 0, L = 1, E = 2, R = 3, S = 4;

        /// <summary>
        /// The triangles (three vertex numbers each) and the number of vertices of the opcodes, or
        /// false when the opcodes do not make a mesh. Each component starts with the vertices of its
        /// first loop, numbered on from those before it, and every C numbers one more vertex.
        /// </summary>
        public static bool Decode(byte[] ops, int start, int count, out List<int> triangles, out int vertices)
        {
            triangles = new List<int>(count * 3);
            vertices = 0;
            var end = start + count;
            var memo = new Dictionary<int, long>();

            // the loop as linked lists; the vertex count is not known before, so they grow
            var next = new List<int>();
            var prev = new List<int>();
            System.Action<int> ensure = n => { while (next.Count <= n) { next.Add(-1); prev.Add(-1); } };

            var i = start;
            var stack = new Stack<int[]>();
            while (i < end)
            {
                int length, after;
                if (!Need(ops, i, end, memo, 0, out length, out after) || length < 2) return false;
                var first = vertices;
                vertices += length;
                ensure(vertices);
                for (var k = 0; k < length; k++)
                {
                    next[first + k] = first + (k + 1) % length;
                    prev[first + (k + 1) % length] = first + k;
                }
                int a = first, b = next[first];
                stack.Clear();
                var open = true;
                while (open)
                {
                    if (i >= end) return false;
                    var op = ops[i++];
                    int v;
                    switch (op)
                    {
                        case C:
                            v = vertices++;
                            ensure(vertices);
                            triangles.Add(a); triangles.Add(b); triangles.Add(v);
                            next[a] = v; prev[v] = a; next[v] = b; prev[b] = v;
                            a = v;
                            break;
                        case R:
                            v = next[b];
                            if (v < 0 || v == a) return false;
                            triangles.Add(a); triangles.Add(b); triangles.Add(v);
                            next[a] = v; prev[v] = a;
                            b = v;
                            break;
                        case L:
                            v = prev[a];
                            if (v < 0 || v == b) return false;
                            triangles.Add(a); triangles.Add(b); triangles.Add(v);
                            next[v] = b; prev[b] = v;
                            a = v;
                            break;
                        case E:
                            v = next[b];
                            if (v < 0 || prev[a] != v) return false;
                            triangles.Add(a); triangles.Add(b); triangles.Add(v);
                            if (stack.Count == 0) { open = false; break; }
                            // back to the left loop that the S before left waiting
                            var left = stack.Pop();
                            a = left[0];
                            var tip = left[1];
                            var w = left[2];
                            next[a] = tip; prev[tip] = a; next[tip] = w; prev[w] = tip;
                            b = tip;
                            break;
                        case S:
                            {
                                int sub, unused;
                                if (!Need(ops, i, end, memo, 0, out sub, out unused) || sub < 3) return false;
                                v = b;
                                for (var k = 0; k < sub - 1; k++)
                                {
                                    v = next[v];
                                    if (v < 0 || v == a) return false;
                                }
                                triangles.Add(a); triangles.Add(b); triangles.Add(v);
                                // the left loop (a, v, w ...) waits; the right loop is b ... v
                                stack.Push(new[] { a, v, next[v] });
                                next[v] = b; prev[b] = v;
                                a = v;
                                break;
                            }
                        default:
                            return false;
                    }
                }
            }
            return true;
        }

        /// <summary>The loop length the branch that starts at op j needs, and the op after the E that ends it.</summary>
        static bool Need(byte[] ops, int j, int end, Dictionary<int, long> memo, int depth, out int length, out int after)
        {
            long cached;
            if (memo.TryGetValue(j, out cached)) { length = (int)(cached >> 32); after = (int)(cached & 0xFFFFFFFF); return true; }
            length = 0; after = 0;
            if (depth > 20000) return false;
            var d = 0;
            var k = j;
            while (k < end)
            {
                var op = ops[k++];
                if (op == C) d++;
                else if (op == L || op == R) d--;
                else if (op == E)
                {
                    length = 3 - d;
                    after = k;
                    memo[j] = ((long)length << 32) | (uint)after;
                    return true;
                }
                else if (op == S)
                {
                    int sub, subAfter;
                    if (!Need(ops, k, end, memo, depth + 1, out sub, out subAfter)) return false;
                    k = subAfter;
                    d += 1 - sub;
                }
                else return false;
            }
            return false;
        }
    }
}
