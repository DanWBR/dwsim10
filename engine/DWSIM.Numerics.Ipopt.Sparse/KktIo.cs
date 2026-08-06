// Binary serialization for captured KKT systems. The native "spy" linear-solver
// (see native/IpManagedDumpSolverInterface.*) writes these records for every
// factorization Ipopt performs on a real problem; the managed replay engine reads
// them back to check inertia agreement and measure the size distribution offline,
// without any native/managed interop.
//
// Format (little-endian, matching Windows x64 and .NET BinaryReader/Writer):
//   magic   : 4 bytes  'I' 'K' 'K' 'T'
//   version : int32    = 1
//   records : Record*   (read until end of stream)
// Record:
//   n, nnz, nrhs, requestedNegEVals, nativeNegEVals, checkNegEVals(0/1) : 6 x int32
//   irn[nnz], jcn[nnz]        : int32   (0-based, lower triangle)
//   values[nnz]               : float64
//   rhs[nrhs*n]               : float64
//
// There is no record count: records are appended until EOF, so a live capture can
// stream them out as they occur (see native/IpKktDumpSolverInterface.cpp).

using System;
using System.Collections.Generic;
using System.IO;

namespace DWSIM.Numerics.Ipopt.Sparse
{
    /// <summary>One captured symmetric KKT system plus what the native solver reported.</summary>
    public sealed class KktRecord
    {
        public int N;
        public int[] Irn = Array.Empty<int>();
        public int[] Jcn = Array.Empty<int>();
        public double[] Values = Array.Empty<double>();

        public int Nrhs;
        public double[] Rhs = Array.Empty<double>();

        /// <summary>Negative eigenvalues Ipopt asked the solver to match, or -1 if unused.</summary>
        public int RequestedNegEVals = -1;

        /// <summary>Negative eigenvalues the native solver (e.g. MA57) reported, or -1 if unknown.</summary>
        public int NativeNegEVals = -1;

        public bool CheckNegEVals;

        public int Nnz => Values.Length;
    }

    /// <summary>Reader/writer for a file of <see cref="KktRecord"/>s.</summary>
    public static class KktDump
    {
        private const int Version = 1;

        public static void WriteAll(Stream stream, IReadOnlyList<KktRecord> records)
        {
            using var w = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
            w.Write((byte)'I'); w.Write((byte)'K'); w.Write((byte)'K'); w.Write((byte)'T');
            w.Write(Version);
            foreach (var r in records)
            {
                w.Write(r.N);
                w.Write(r.Nnz);
                w.Write(r.Nrhs);
                w.Write(r.RequestedNegEVals);
                w.Write(r.NativeNegEVals);
                w.Write(r.CheckNegEVals ? 1 : 0);
                for (int k = 0; k < r.Nnz; k++) w.Write(r.Irn[k]);
                for (int k = 0; k < r.Nnz; k++) w.Write(r.Jcn[k]);
                for (int k = 0; k < r.Nnz; k++) w.Write(r.Values[k]);
                int rhsLen = r.Nrhs * r.N;
                for (int k = 0; k < rhsLen; k++) w.Write(r.Rhs[k]);
            }
        }

        public static List<KktRecord> ReadAll(Stream stream)
        {
            using var rd = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
            if (rd.ReadByte() != 'I' || rd.ReadByte() != 'K' || rd.ReadByte() != 'K' || rd.ReadByte() != 'T')
                throw new InvalidDataException("Not an IKKT dump.");
            int version = rd.ReadInt32();
            if (version != Version)
                throw new InvalidDataException($"Unsupported IKKT version {version}.");

            var list = new List<KktRecord>();
            while (rd.BaseStream.Position < rd.BaseStream.Length)
            {
                var r = new KktRecord();
                r.N = rd.ReadInt32();
                int nnz = rd.ReadInt32();
                r.Nrhs = rd.ReadInt32();
                r.RequestedNegEVals = rd.ReadInt32();
                r.NativeNegEVals = rd.ReadInt32();
                r.CheckNegEVals = rd.ReadInt32() != 0;

                r.Irn = new int[nnz];
                r.Jcn = new int[nnz];
                r.Values = new double[nnz];
                for (int k = 0; k < nnz; k++) r.Irn[k] = rd.ReadInt32();
                for (int k = 0; k < nnz; k++) r.Jcn[k] = rd.ReadInt32();
                for (int k = 0; k < nnz; k++) r.Values[k] = rd.ReadDouble();

                int rhsLen = r.Nrhs * r.N;
                r.Rhs = new double[rhsLen];
                for (int k = 0; k < rhsLen; k++) r.Rhs[k] = rd.ReadDouble();

                list.Add(r);
            }
            return list;
        }
    }
}
