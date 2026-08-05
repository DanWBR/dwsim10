using System;
using System.Collections.Generic;
using DWSIM.PureCompoundData.Core;

namespace DWSIM.PureCompoundData.Index
{
    /// <summary>
    /// Fluent query builder over <see cref="QueryExecutor"/>. Immutable - each chained
    /// method returns a new instance.
    /// </summary>
    /// <example>
    /// <code>
    /// using var index = new PureCompoundIndex(dbPath);
    /// var records = index.CreateQuery()
    ///     .ForCompound("64-17-5")
    ///     .OfCategory(PropertyCategory.VaporPressure)
    ///     .InTemperatureRangeK(273, 373)
    ///     .Take(20)
    ///     .Execute();
    /// </code>
    /// </example>
    public sealed class PureCompoundQuery
    {
        private enum KeyMode { None, Cas, Name, InChIKey }

        private readonly QueryExecutor _exec;
        private readonly KeyMode _mode;
        private readonly string? _key;
        private readonly PropertyCategory? _category;
        private readonly (double Min, double Max)? _tRangeK;
        private readonly int _limit;

        internal PureCompoundQuery(QueryExecutor exec)
            : this(exec, KeyMode.None, null, null, null, 100) { }

        private PureCompoundQuery(
            QueryExecutor exec,
            KeyMode mode,
            string? key,
            PropertyCategory? category,
            (double Min, double Max)? tRangeK,
            int limit)
        {
            _exec = exec;
            _mode = mode;
            _key = key;
            _category = category;
            _tRangeK = tRangeK;
            _limit = limit;
        }

        public PureCompoundQuery ForCompound(string cas)
            => new PureCompoundQuery(_exec, KeyMode.Cas, cas, _category, _tRangeK, _limit);

        public PureCompoundQuery ForCompoundByName(string name)
            => new PureCompoundQuery(_exec, KeyMode.Name, name, _category, _tRangeK, _limit);

        public PureCompoundQuery ForCompoundByInChIKey(string inchiKey)
            => new PureCompoundQuery(_exec, KeyMode.InChIKey, inchiKey, _category, _tRangeK, _limit);

        public PureCompoundQuery OfCategory(PropertyCategory category)
            => new PureCompoundQuery(_exec, _mode, _key, category, _tRangeK, _limit);

        public PureCompoundQuery AnyCategory()
            => new PureCompoundQuery(_exec, _mode, _key, null, _tRangeK, _limit);

        public PureCompoundQuery InTemperatureRangeK(double minK, double maxK)
            => new PureCompoundQuery(_exec, _mode, _key, _category, (minK, maxK), _limit);

        public PureCompoundQuery Take(int limit)
            => new PureCompoundQuery(_exec, _mode, _key, _category, _tRangeK, limit);

        public IReadOnlyList<PureCompoundRecord> Execute()
        {
            if (_mode == KeyMode.None || string.IsNullOrWhiteSpace(_key))
                throw new InvalidOperationException("Call ForCompound, ForCompoundByName, or ForCompoundByInChIKey before Execute.");

            return _mode switch
            {
                KeyMode.Cas => _exec.SearchByCas(_key!, _category, _tRangeK, _limit),
                KeyMode.Name => _exec.SearchByName(_key!, _category, _tRangeK, _limit),
                KeyMode.InChIKey => _exec.SearchByInChIKey(_key!, _category, _tRangeK, _limit),
                _ => throw new InvalidOperationException("Unreachable.")
            };
        }
    }
}
