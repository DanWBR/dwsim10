using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DWSIM.PureCompoundData.Core
{
    public interface IDataSource
    {
        string Name { get; }
        bool IsOffline { get; }
        Task<IReadOnlyList<PureCompoundRecord>> SearchAsync(CompoundQuery query, CancellationToken ct);
    }
}
