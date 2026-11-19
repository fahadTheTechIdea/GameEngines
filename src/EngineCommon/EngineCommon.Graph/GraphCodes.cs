namespace EngineCommon.Graph;

/// <summary>The named limits of the graph library: every size and magnitude is bounded so that the arithmetic below cannot overflow, and a request over a limit is refused with a code.</summary>
public static class GraphLimits
{
    public const int MaxNodes = 200_000;
    public const int MaxEdges = 2_000_000;
    /// <summary>The largest absolute edge weight (a cost, a distance or an affinity).</summary>
    public const long MaxWeight = 1_000_000_000;
    /// <summary>The largest edge capacity.</summary>
    public const long MaxCapacity = 1_000_000_000;
    public const int MaxRounds = 1_000;
    public const int MaxIterations = 10_000;
    public const int MaxAugmentations = 1_000_000;
    public const int MaxTop = 1_024;
    /// <summary>Passed as the flow limit to ask for the maximum flow.</summary>
    public const long NoFlowLimit = long.MaxValue;
}

/// <summary>The refusal codes of the graph library. An empty text is "accepted"; every code is reached by a test.</summary>
public static class GraphCodes
{
    public const string Ok = "";
    public const string ArgumentMissing = "GRAPH_ARGUMENT_MISSING";
    public const string NodeCount = "GRAPH_NODE_COUNT";
    public const string EdgeCount = "GRAPH_EDGE_COUNT";
    public const string NodeRange = "GRAPH_NODE_RANGE";
    public const string WeightRange = "GRAPH_WEIGHT_RANGE";
    public const string WeightsLength = "GRAPH_WEIGHTS_LENGTH";
    public const string CapacityRange = "GRAPH_CAPACITY_RANGE";
    public const string NegativeWeight = "GRAPH_NEGATIVE_WEIGHT";
    public const string SourceRange = "GRAPH_SOURCE_RANGE";
    public const string SinkRange = "GRAPH_SINK_RANGE";
    public const string SameSourceSink = "GRAPH_SAME_SOURCE_SINK";
    public const string DepthRange = "GRAPH_DEPTH_RANGE";
    public const string BellmanFordTooLarge = "GRAPH_BELLMAN_FORD_TOO_LARGE";
    public const string FlowLimitRange = "GRAPH_FLOW_LIMIT_RANGE";
    public const string AugmentationsRange = "GRAPH_AUGMENTATIONS_RANGE";
    public const string RoundsRange = "GRAPH_ROUNDS_RANGE";
    public const string LabelsLength = "GRAPH_LABELS_LENGTH";
    public const string LabelRange = "GRAPH_LABEL_RANGE";
    public const string PreTrustLength = "GRAPH_PRETRUST_LENGTH";
    public const string PreTrustNegative = "GRAPH_PRETRUST_NEGATIVE";
    public const string PreTrustSum = "GRAPH_PRETRUST_SUM";
    public const string AlphaRange = "GRAPH_ALPHA_RANGE";
    public const string IterationsRange = "GRAPH_ITERATIONS_RANGE";
    public const string EpsilonRange = "GRAPH_EPSILON_RANGE";
    public const string TopRange = "GRAPH_TOP_RANGE";
    public const string Cycle = "GRAPH_CYCLE";
}
