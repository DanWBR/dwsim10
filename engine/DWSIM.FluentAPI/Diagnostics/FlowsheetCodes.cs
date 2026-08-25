using System.Collections.Generic;

namespace DWSIM.Automation.FluentAPI.Diagnostics
{
    /// <summary>
    /// The codes <see cref="FlowsheetDiagnostics"/> emits, so the tools, the catalogue and the
    /// documentation cannot drift apart from the findings themselves.
    /// </summary>
    public static class FlowsheetCodes
    {
        public const string EmptyFlowsheet = "EMPTY_FLOWSHEET";
        public const string NoCompounds = "NO_COMPOUNDS";
        public const string NoPropertyPackage = "NO_PROPERTY_PACKAGE";
        public const string DuplicateTag = "DUPLICATE_TAG";
        public const string StreamDangling = "STREAM_DANGLING";
        public const string EnergyStreamHalfConnected = "ENERGY_STREAM_HALF_CONNECTED";
        public const string UnitUnconnected = "UNIT_UNCONNECTED";
        public const string UnitNoFeed = "UNIT_NO_FEED";
        public const string UnitNoProduct = "UNIT_NO_PRODUCT";
        public const string FeedNoPressure = "FEED_NO_PRESSURE";
        public const string FeedNoTemperature = "FEED_NO_TEMPERATURE";
        public const string FeedNoFlow = "FEED_NO_FLOW";
        public const string FeedNoComposition = "FEED_NO_COMPOSITION";
        public const string FeedCompositionNotNormalised = "FEED_COMPOSITION_NOT_NORMALISED";
        public const string RecycleNoEstimate = "RECYCLE_NO_ESTIMATE";
        public const string LogicalTargetMissing = "LOGICAL_TARGET_MISSING";
        public const string SolverException = "SOLVER_EXCEPTION";
        public const string InfiniteLoop = "INFINITE_LOOP";
        public const string NotConverged = "NOT_CONVERGED";
        public const string StreamNotFinite = "STREAM_NOT_FINITE";
        public const string NegativeFlow = "NEGATIVE_FLOW";
        public const string UnitHadNoEffect = "UNIT_HAD_NO_EFFECT";

        /// <summary>Every code, mapped to a one-line explanation.</summary>
        public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
        {
            { EmptyFlowsheet, "The flowsheet has no objects." },
            { NoCompounds, "The flowsheet has no compounds, so no stream can carry anything." },
            { NoPropertyPackage, "The flowsheet has no property package, so nothing can be flashed." },
            { DuplicateTag, "Two or more objects share a tag, so addressing one by tag is ambiguous." },
            { StreamDangling, "A stream is connected to nothing at either end." },
            { EnergyStreamHalfConnected, "An energy stream is attached at one end only." },
            { UnitUnconnected, "A unit operation has nothing connected to it." },
            { UnitNoFeed, "A unit operation has no feed, so it has nothing to process." },
            { UnitNoProduct, "A unit operation has no product, so its result has nowhere to go." },
            { FeedNoPressure, "A boundary feed has no pressure." },
            { FeedNoTemperature, "A boundary feed has no temperature." },
            { FeedNoFlow, "A boundary feed carries no flow." },
            { FeedNoComposition, "Every compound in a boundary feed is at zero." },
            { FeedCompositionNotNormalised, "The mole fractions of a boundary feed do not sum to 1." },
            { RecycleNoEstimate, "A recycle starts from a zero estimate." },
            { LogicalTargetMissing, "An adjust or specification does not name both the object it reads and the one it writes." },
            { SolverException, "The solver raised an exception." },
            { InfiniteLoop, "The solver found a cycle with no recycle to tear it." },
            { NotConverged, "A unit operation did not solve." },
            { StreamNotFinite, "A stream carries a flow that is not a finite number." },
            { NegativeFlow, "A stream carries a negative flow." },
            { UnitHadNoEffect, "A unit operation left its stream unchanged, so its specification is not being read." }
        };
    }
}
