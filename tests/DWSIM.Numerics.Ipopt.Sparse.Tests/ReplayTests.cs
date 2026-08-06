using System.IO;
using Xunit;

namespace DWSIM.Numerics.Ipopt.Sparse.Tests
{
    public class ReplayTests
    {
        [Fact]
        public void DumpRoundTripsExactly()
        {
            var records = SyntheticKkt.Generate(count: 25, seed: 7);

            using var ms = new MemoryStream();
            KktDump.WriteAll(ms, records);
            ms.Position = 0;
            var back = KktDump.ReadAll(ms);

            Assert.Equal(records.Count, back.Count);
            for (int i = 0; i < records.Count; i++)
            {
                Assert.Equal(records[i].N, back[i].N);
                Assert.Equal(records[i].Nnz, back[i].Nnz);
                Assert.Equal(records[i].Nrhs, back[i].Nrhs);
                Assert.Equal(records[i].NativeNegEVals, back[i].NativeNegEVals);
                Assert.Equal(records[i].RequestedNegEVals, back[i].RequestedNegEVals);
                Assert.Equal(records[i].CheckNegEVals, back[i].CheckNegEVals);
                Assert.Equal(records[i].Irn, back[i].Irn);
                Assert.Equal(records[i].Jcn, back[i].Jcn);
                Assert.Equal(records[i].Values, back[i].Values);
                Assert.Equal(records[i].Rhs, back[i].Rhs);
            }
        }

        [Theory]
        [InlineData(LinearSolverKind.Sparse)]
        [InlineData(LinearSolverKind.Dense)]
        [InlineData(LinearSolverKind.Auto)]
        public void ReplayAgreesWithKnownInertiaAndSolves(LinearSolverKind kind)
        {
            var records = SyntheticKkt.Generate(count: 300, seed: 42);
            var report = ReplayEngine.Run(records, new ReplayOptions { Kind = kind });

            // Every synthetic system is quasidefinite with a known inertia, so the
            // managed solver must agree on all of them and never report singular.
            Assert.Equal(records.Count, report.Records);
            Assert.Equal(report.Comparable, report.InertiaAgreements);
            Assert.Equal(0, report.ManagedWrong);
            Assert.Equal(0, report.ManagedSingular);
            Assert.True(report.MaxSolveResidual < 1e-7, $"residual {report.MaxSolveResidual:E3}");
            Assert.True(report.AgreementRate > 0.999);
        }

        [Fact]
        public void ReportTracksSizeDistribution()
        {
            var records = SyntheticKkt.Generate(count: 100, seed: 3);
            var report = ReplayEngine.Run(records);

            int bucketSum = 0;
            foreach (var b in report.SizeBuckets) bucketSum += b;
            Assert.Equal(records.Count, bucketSum);
            Assert.True(report.MaxN >= report.MinN);
            Assert.True(report.TotalNnz > 0);
        }
    }
}
