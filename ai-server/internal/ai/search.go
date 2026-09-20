package ai

import (
	"errors"
	"fmt"

	"tetris-ai-server/internal/tetris"
)

// ErrNoPlacement is returned when the current piece cannot be placed anywhere (dead end).
var ErrNoPlacement = errors.New("no valid placement")

// Result is the outcome of a search.
type Result struct {
	Placement tetris.Placement
	Score     float64
	Reasoning string
}

// BestMove evaluates every way to place cur and returns the highest-scoring one.
// If next is non-nil, the evaluation of the best follow-up placement of next is added to each candidate (2-piece lookahead).
// Ties go to the candidate found first (smaller rotation, then smaller x), so the result is deterministic.
func BestMove(b tetris.Board, cur tetris.Piece, next *tetris.Piece, w Weights) (Result, error) {
	var (
		best    Result
		bestF   Features
		bestAny bool
	)
	for _, pl := range tetris.Placements(cur) {
		b1, lines1, ok := b.Drop(cur, pl.Rotation, pl.X)
		if !ok {
			continue
		}
		f1 := ExtractFeatures(b1, lines1)
		score := w.Score(f1)
		if next != nil {
			score += bestFollowUp(b1, *next, w)
		}
		if !bestAny || score > best.Score {
			best = Result{Placement: pl, Score: score}
			bestF = f1
			bestAny = true
		}
	}
	if !bestAny {
		return Result{}, ErrNoPlacement
	}
	best.Reasoning = fmt.Sprintf(
		"piece=%s rot=%d x=%d: lines=%d holes=%d height=%d bumpiness=%d score=%.3f lookahead=%t",
		cur, best.Placement.Rotation, best.Placement.X,
		bestF.Lines, bestF.Holes, bestF.AggregateHeight, bestF.Bumpiness, best.Score, next != nil,
	)
	return best, nil
}

// bestFollowUp returns the best evaluation of placing next on board b. If it cannot be placed, it is penalized heavily.
func bestFollowUp(b tetris.Board, next tetris.Piece, w Weights) float64 {
	best, found := 0.0, false
	for _, pl := range tetris.Placements(next) {
		b2, lines2, ok := b.Drop(next, pl.Rotation, pl.X)
		if !ok {
			continue
		}
		if s := w.Score(ExtractFeatures(b2, lines2)); !found || s > best {
			best, found = s, true
		}
	}
	if !found {
		return PenaltyNoPlacement
	}
	return best
}
