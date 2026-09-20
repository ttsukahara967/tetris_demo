// Package ai is the CPU's thinking routine: a heuristic evaluation function plus exhaustive search.
package ai

import "tetris-ai-server/internal/tetris"

// Features are the evaluation terms extracted from a board.
type Features struct {
	AggregateHeight int // sum of the heights of all columns
	Holes           int // number of empty cells below a block
	Bumpiness       int // sum of absolute height differences between adjacent columns
	Lines           int // number of lines cleared by this piece
}

// ExtractFeatures computes the evaluation terms from board b (after placing the piece and clearing lines) and the number of lines cleared.
func ExtractFeatures(b tetris.Board, lines int) Features {
	f := Features{Lines: lines}
	var heights [tetris.Width]int
	for c := 0; c < tetris.Width; c++ {
		top := -1
		for r := 0; r < tetris.Height; r++ {
			if b[r][c] {
				if top < 0 {
					top = r
				}
			} else if top >= 0 {
				f.Holes++
			}
		}
		if top >= 0 {
			heights[c] = tetris.Height - top
		}
		f.AggregateHeight += heights[c]
	}
	for c := 0; c+1 < tetris.Width; c++ {
		d := heights[c] - heights[c+1]
		if d < 0 {
			d = -d
		}
		f.Bumpiness += d
	}
	return f
}

// Score multiplies each term by its weight and sums them. A larger value means a better board.
func (w Weights) Score(f Features) float64 {
	return w.AggregateHeight*float64(f.AggregateHeight) +
		w.Lines*float64(f.Lines) +
		w.Holes*float64(f.Holes) +
		w.Bumpiness*float64(f.Bumpiness)
}
