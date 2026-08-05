using System;
using System.Collections.Generic;
using DWSIM.PhaseEquilibriumData.Core;

namespace DWSIM.PhaseEquilibriumData.Index
{
    /// <summary>
    /// Fluent query builder over <see cref="QueryExecutor"/>. Immutable - each chained
    /// method returns a new instance, so builders can be safely reused as templates.
    /// </summary>
    /// <example>
    /// <code>
    /// using var index = new ThermoMLIndex(dbPath);
    /// var results = index.CreateQuery()
    ///     .ForBinaryByNames("ethanol", "water")
    ///     .OfType(EquilibriumType.VLE_Isobaric)
    ///     .InTemperatureRangeK(300, 400)
    ///     .Take(20)
    ///     .Execute();
    /// </code>
    /// </example>
    public sealed class PhaseEqQuery
    {
        private enum KeyMode { None, Cas, Name, InChIKey }

        private readonly QueryExecutor _exec;
        private readonly KeyMode _mode;
        private readonly string? _k1;
        private readonly string? _k2;
        private readonly EquilibriumType? _type;
        private readonly (double Min, double Max)? _tRangeK;
        private readonly (double Min, double Max)? _pRangeKPa;
        private readonly int _limit;

        internal PhaseEqQuery(QueryExecutor exec)
            : this(exec, KeyMode.None, null, null, null, null, null, 50) { }

        private PhaseEqQuery(
            QueryExecutor exec,
            KeyMode mode,
            string? k1,
            string? k2,
            EquilibriumType? type,
            (double Min, double Max)? tRangeK,
            (double Min, double Max)? pRangeKPa,
            int limit)
        {
            _exec = exec;
            _mode = mode;
            _k1 = k1;
            _k2 = k2;
            _type = type;
            _tRangeK = tRangeK;
            _pRangeKPa = pRangeKPa;
            _limit = limit;
        }

        /// <summary>Match both components by CAS number (exact).</summary>
        public PhaseEqQuery ForBinary(string cas1, string cas2)
            => new PhaseEqQuery(_exec, KeyMode.Cas, cas1, cas2, _type, _tRangeK, _pRangeKPa, _limit);

        /// <summary>Match both components by common/IUPAC name (case-insensitive exact, then contains).</summary>
        public PhaseEqQuery ForBinaryByNames(string name1, string name2)
            => new PhaseEqQuery(_exec, KeyMode.Name, name1, name2, _type, _tRangeK, _pRangeKPa, _limit);

        /// <summary>Match both components by standard InChIKey (case-insensitive).</summary>
        public PhaseEqQuery ForBinaryByInChIKey(string inchiKey1, string inchiKey2)
            => new PhaseEqQuery(_exec, KeyMode.InChIKey, inchiKey1, inchiKey2, _type, _tRangeK, _pRangeKPa, _limit);

        public PhaseEqQuery OfType(EquilibriumType type)
            => new PhaseEqQuery(_exec, _mode, _k1, _k2, type, _tRangeK, _pRangeKPa, _limit);

        public PhaseEqQuery AnyType()
            => new PhaseEqQuery(_exec, _mode, _k1, _k2, null, _tRangeK, _pRangeKPa, _limit);

        public PhaseEqQuery InTemperatureRangeK(double minK, double maxK)
            => new PhaseEqQuery(_exec, _mode, _k1, _k2, _type, (minK, maxK), _pRangeKPa, _limit);

        public PhaseEqQuery InPressureRangeKPa(double minKPa, double maxKPa)
            => new PhaseEqQuery(_exec, _mode, _k1, _k2, _type, _tRangeK, (minKPa, maxKPa), _limit);

        public PhaseEqQuery Take(int limit)
            => new PhaseEqQuery(_exec, _mode, _k1, _k2, _type, _tRangeK, _pRangeKPa, limit);

        public IReadOnlyList<PhaseEquilibriumDataset> Execute()
        {
            if (_mode == KeyMode.None || string.IsNullOrWhiteSpace(_k1) || string.IsNullOrWhiteSpace(_k2))
                throw new InvalidOperationException("Call ForBinary, ForBinaryByNames, or ForBinaryByInChIKey before Execute.");

            return _mode switch
            {
                KeyMode.Cas => _exec.SearchBinary(_k1!, _k2!, _type, _tRangeK, _pRangeKPa, _limit),
                KeyMode.Name => _exec.SearchBinaryByNames(_k1!, _k2!, _type, _tRangeK, _pRangeKPa, _limit),
                KeyMode.InChIKey => _exec.SearchBinaryByInChIKey(_k1!, _k2!, _type, _tRangeK, _pRangeKPa, _limit),
                _ => throw new InvalidOperationException("Unreachable.")
            };
        }
    }
}
