using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using DWSIM.PhaseEquilibriumData.Core;

namespace DWSIM.PhaseEquilibriumData.Sources.Enrichment
{
    /// <summary>
    /// Enriches <see cref="Citation"/> records by looking up their DOI in Crossref.
    /// Only fields missing from the input citation are filled in - existing values are
    /// preserved. In-memory cache ensures each DOI is resolved at most once per
    /// enricher instance.
    /// </summary>
    public sealed class CitationEnricher
    {
        private readonly CrossrefClient _client;
        private readonly ConcurrentDictionary<string, Task<Citation?>> _cache =
            new ConcurrentDictionary<string, Task<Citation?>>();

        public CitationEnricher(CrossrefClient client)
        {
            _client = client;
        }

        public async Task<Citation> EnrichAsync(Citation input, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(input.Doi)) return input;
            var fetched = await _cache
                .GetOrAdd(input.Doi!.Trim(), d => _client.GetByDoiAsync(d, ct))
                .ConfigureAwait(false);
            if (fetched == null) return input;

            return new Citation(
                Doi: input.Doi,
                Title: string.IsNullOrWhiteSpace(input.Title) ? fetched.Title : input.Title,
                Authors: (input.Authors == null || input.Authors.Count == 0) ? fetched.Authors : input.Authors,
                Journal: string.IsNullOrWhiteSpace(input.Journal) ? fetched.Journal : input.Journal,
                Year: input.Year ?? fetched.Year,
                Volume: input.Volume ?? fetched.Volume,
                Pages: string.IsNullOrWhiteSpace(input.Pages) ? fetched.Pages : input.Pages);
        }
    }
}
