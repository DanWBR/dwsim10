using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DWSIM.PhaseEquilibriumData.Core
{
    public interface IDataSource
    {
        string Name { get; }
        bool IsOffline { get; }
        Task<IReadOnlyList<PhaseEquilibriumDataset>> SearchAsync(CompoundQuery query, CancellationToken ct);
    }
}
