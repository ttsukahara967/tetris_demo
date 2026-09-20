package ai

// Weights of the evaluation function. To tune the AI, change only these constants.
// The initial values are a combination widely used by this kind of heuristic AI.
const (
	WeightAggregateHeight = -0.510066 // total height of the stack (lower is better)
	WeightLines           = 0.760666  // number of lines that can be cleared (more is better)
	WeightHoles           = -0.35663  // number of holes (fewer is better)
	WeightBumpiness       = -0.184483 // sum of height differences between adjacent columns (smaller is better)

	// Penalty when the next piece cannot be placed anywhere (a dead end).
	PenaltyNoPlacement = -1000.0
)

// Weights is the full set of evaluation weights.
type Weights struct {
	AggregateHeight float64
	Lines           float64
	Holes           float64
	Bumpiness       float64
}

// DefaultWeights is the default set of weights, built from the constants above.
var DefaultWeights = Weights{
	AggregateHeight: WeightAggregateHeight,
	Lines:           WeightLines,
	Holes:           WeightHoles,
	Bumpiness:       WeightBumpiness,
}
