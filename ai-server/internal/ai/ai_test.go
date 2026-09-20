package ai

import (
	"errors"
	"math"
	"math/rand"
	"strings"
	"testing"

	"tetris-ai-server/internal/tetris"
)

func TestExtractFeatures(t *testing.T) {
	t.Run("empty", func(t *testing.T) {
		if f := ExtractFeatures(tetris.Board{}, 0); f != (Features{}) {
			t.Errorf("got %+v", f)
		}
	})
	t.Run("single block bottom-left", func(t *testing.T) {
		var b tetris.Board
		b[tetris.Height-1][0] = true
		want := Features{AggregateHeight: 1, Bumpiness: 1}
		if f := ExtractFeatures(b, 0); f != want {
			t.Errorf("got %+v, want %+v", f, want)
		}
	})
	t.Run("hole under a block", func(t *testing.T) {
		var b tetris.Board
		b[tetris.Height-2][0] = true // the cell directly below (bottom row) is empty = a hole
		want := Features{AggregateHeight: 2, Holes: 1, Bumpiness: 2}
		if f := ExtractFeatures(b, 0); f != want {
			t.Errorf("got %+v, want %+v", f, want)
		}
	})
	t.Run("multiple holes in a column", func(t *testing.T) {
		var b tetris.Board
		b[tetris.Height-4][3] = true
		b[tetris.Height-1][3] = true // the 2 cells in between are holes
		if f := ExtractFeatures(b, 0); f.Holes != 2 || f.AggregateHeight != 4 {
			t.Errorf("got %+v", f)
		}
	})
	t.Run("flat floor has no bumpiness", func(t *testing.T) {
		var b tetris.Board
		for c := 0; c < tetris.Width; c++ {
			b[tetris.Height-1][c] = true
		}
		want := Features{AggregateHeight: 10}
		if f := ExtractFeatures(b, 0); f != want {
			t.Errorf("got %+v, want %+v", f, want)
		}
	})
	t.Run("lines passthrough", func(t *testing.T) {
		if f := ExtractFeatures(tetris.Board{}, 3); f.Lines != 3 {
			t.Errorf("Lines = %d", f.Lines)
		}
	})
}

func TestScoreOrdering(t *testing.T) {
	w := DefaultWeights
	base := Features{AggregateHeight: 20, Holes: 1, Bumpiness: 4}
	better := []Features{
		{AggregateHeight: 10, Holes: 1, Bumpiness: 4}, // lower
		{AggregateHeight: 20, Holes: 0, Bumpiness: 4}, // fewer holes
		{AggregateHeight: 20, Holes: 1, Bumpiness: 2}, // smoother surface
		{AggregateHeight: 20, Holes: 1, Bumpiness: 4, Lines: 1},
	}
	for i, f := range better {
		if w.Score(f) <= w.Score(base) {
			t.Errorf("case %d: %+v should score higher than %+v", i, f, base)
		}
	}
}

func TestScoreUsesWeights(t *testing.T) {
	w := Weights{AggregateHeight: 1, Lines: 10, Holes: 100, Bumpiness: 1000}
	got := w.Score(Features{AggregateHeight: 1, Lines: 2, Holes: 3, Bumpiness: 4})
	if want := 1.0 + 20 + 300 + 4000; math.Abs(got-want) > 1e-9 {
		t.Errorf("got %v, want %v", got, want)
	}
}

func TestBestMoveClearsLine(t *testing.T) {
	var b tetris.Board
	for c := 4; c < tetris.Width; c++ {
		b[tetris.Height-1][c] = true
	}
	res, err := BestMove(b, tetris.I, nil, DefaultWeights)
	if err != nil {
		t.Fatal(err)
	}
	if res.Placement != (tetris.Placement{X: 0, Rotation: 0}) {
		t.Errorf("placement = %+v, want x=0 rot=0", res.Placement)
	}
	if !strings.Contains(res.Reasoning, "lines=1") {
		t.Errorf("reasoning = %q", res.Reasoning)
	}
}

func TestBestMoveAvoidsHole(t *testing.T) {
	// Everything except a 1-wide well (col 0) is height 2. Dropping a vertical I into the well is best (zero holes).
	var b tetris.Board
	for r := tetris.Height - 2; r < tetris.Height; r++ {
		for c := 1; c < tetris.Width; c++ {
			b[r][c] = true
		}
	}
	res, err := BestMove(b, tetris.I, nil, DefaultWeights)
	if err != nil {
		t.Fatal(err)
	}
	if res.Placement.X != 0 || res.Placement.Rotation%2 != 1 {
		t.Errorf("placement = %+v, want vertical I at x=0", res.Placement)
	}
}

func TestBestMoveLookaheadIncludesNextPiece(t *testing.T) {
	var b tetris.Board
	next := tetris.O
	without, err := BestMove(b, tetris.T, nil, DefaultWeights)
	if err != nil {
		t.Fatal(err)
	}
	with, err := BestMove(b, tetris.T, &next, DefaultWeights)
	if err != nil {
		t.Fatal(err)
	}
	if with.Score == without.Score {
		t.Error("lookahead should change the score")
	}
	if !strings.Contains(with.Reasoning, "lookahead=true") || !strings.Contains(without.Reasoning, "lookahead=false") {
		t.Errorf("reasoning: %q / %q", with.Reasoning, without.Reasoning)
	}
}

func TestBestMoveLookaheadPenalizesDeadEnd(t *testing.T) {
	// The only empty cells are a 2x2 at the top left (col 0-1, row 0-1) and a single column on the right edge (col 9).
	// An O can only go at the top left, and after placing it the next O cannot spawn (dead end).
	var b tetris.Board
	for r := 0; r < tetris.Height; r++ {
		for c := 0; c < tetris.Width-1; c++ {
			b[r][c] = r >= 2 || c >= 2
		}
	}
	next := tetris.O

	withLookahead, err := BestMove(b, tetris.O, &next, DefaultWeights)
	if err != nil {
		t.Fatal(err)
	}
	without, err := BestMove(b, tetris.O, nil, DefaultWeights)
	if err != nil {
		t.Fatal(err)
	}
	if withLookahead.Score > without.Score+PenaltyNoPlacement/2 {
		t.Errorf("dead end should be penalized: with=%v without=%v", withLookahead.Score, without.Score)
	}
}

func TestBestFollowUpNoPlacement(t *testing.T) {
	var b tetris.Board
	for c := range b[0] {
		b[0][c] = true
	}
	if got := bestFollowUp(b, tetris.T, DefaultWeights); got != PenaltyNoPlacement {
		t.Errorf("got %v, want %v", got, PenaltyNoPlacement)
	}
}

func TestBestMoveNoPlacement(t *testing.T) {
	var b tetris.Board
	for c := range b[0] {
		b[0][c] = true
	}
	_, err := BestMove(b, tetris.T, nil, DefaultWeights)
	if !errors.Is(err, ErrNoPlacement) {
		t.Errorf("err = %v, want ErrNoPlacement", err)
	}
}

func TestBestMoveDeterministic(t *testing.T) {
	var b tetris.Board
	a, _ := BestMove(b, tetris.S, nil, DefaultWeights)
	for i := 0; i < 5; i++ {
		got, _ := BestMove(b, tetris.S, nil, DefaultWeights)
		if got != a {
			t.Fatalf("non-deterministic: %+v vs %+v", got, a)
		}
	}
}

// Self-play with a 7-bag randomizer and check that the CPU survives for a long time and keeps clearing lines.
func TestSelfPlaySurvives(t *testing.T) {
	const pieces = 1000
	rng := rand.New(rand.NewSource(42))
	var bag []tetris.Piece
	draw := func() tetris.Piece {
		if len(bag) == 0 {
			bag = tetris.AllPieces()
			rng.Shuffle(len(bag), func(i, j int) { bag[i], bag[j] = bag[j], bag[i] })
		}
		p := bag[0]
		bag = bag[1:]
		return p
	}

	var b tetris.Board
	cur, nxt := draw(), draw()
	totalLines := 0
	for i := 0; i < pieces; i++ {
		res, err := BestMove(b, cur, &nxt, DefaultWeights)
		if err != nil {
			t.Fatalf("topped out after %d pieces (%d lines)", i, totalLines)
		}
		var lines int
		b, lines, _ = b.Drop(cur, res.Placement.Rotation, res.Placement.X)
		totalLines += lines
		cur, nxt = nxt, draw()
	}
	// 1000 pieces = 4000 cells. If nearly all of them are cleared, that is around 350 lines.
	if totalLines < 300 {
		t.Errorf("only %d lines cleared in %d pieces", totalLines, pieces)
	}
	t.Logf("survived %d pieces, %d lines", pieces, totalLines)
}
