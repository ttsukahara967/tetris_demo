package tetris

import (
	"slices"
	"testing"
)

func TestCellsRotation(t *testing.T) {
	sorted := func(c [4]Point) []Point {
		s := c[:]
		slices.SortFunc(s, func(a, b Point) int {
			if a.Y != b.Y {
				return a.Y - b.Y
			}
			return a.X - b.X
		})
		return s
	}
	tests := []struct {
		p    Piece
		rot  int
		want []Point
	}{
		{T, 0, []Point{{1, 0}, {0, 1}, {1, 1}, {2, 1}}},
		{T, 1, []Point{{1, 0}, {1, 1}, {2, 1}, {1, 2}}},
		{T, 2, []Point{{0, 1}, {1, 1}, {2, 1}, {1, 2}}},
		{T, 3, []Point{{1, 0}, {0, 1}, {1, 1}, {1, 2}}},
		{I, 0, []Point{{0, 1}, {1, 1}, {2, 1}, {3, 1}}},
		{I, 1, []Point{{2, 0}, {2, 1}, {2, 2}, {2, 3}}},
		{L, 1, []Point{{1, 0}, {1, 1}, {1, 2}, {2, 2}}},
		{J, 1, []Point{{1, 0}, {2, 0}, {1, 1}, {1, 2}}},
		{S, 1, []Point{{1, 0}, {1, 1}, {2, 1}, {2, 2}}},
		{Z, 1, []Point{{2, 0}, {1, 1}, {2, 1}, {1, 2}}},
	}
	for _, tt := range tests {
		got := sorted(tt.p.Cells(tt.rot))
		if !slices.Equal(got, sorted([4]Point(tt.want))) {
			t.Errorf("%s rot%d = %v, want %v", tt.p, tt.rot, got, tt.want)
		}
	}
}

func TestCellsRotationCycle(t *testing.T) {
	for _, p := range AllPieces() {
		if p.Cells(0) != p.Cells(4) || p.Cells(3) != p.Cells(-1) {
			t.Errorf("%s: rotation is not cyclic", p)
		}
	}
}

func TestParsePiece(t *testing.T) {
	for _, p := range AllPieces() {
		got, err := ParsePiece(p.String())
		if err != nil || got != p {
			t.Errorf("ParsePiece(%q) = %v, %v", p.String(), got, err)
		}
	}
	if _, err := ParsePiece("X"); err == nil {
		t.Error("ParsePiece(X) should fail")
	}
}

func TestPlacementsCount(t *testing.T) {
	want := map[Piece]int{
		O: 9,             // only 2 wide; all rotations are the same shape
		I: 7 + 10,        // 4 wide horizontally + 1 wide vertically
		S: 8 + 9,         // 3 wide horizontally + 2 wide vertically (rot2/3 are duplicates)
		Z: 8 + 9,         //
		T: 8 + 9 + 8 + 9, // all 4 rotations are distinct shapes
		J: 8 + 9 + 8 + 9,
		L: 8 + 9 + 8 + 9,
	}
	for p, n := range want {
		if got := len(Placements(p)); got != n {
			t.Errorf("Placements(%s) = %d, want %d", p, got, n)
		}
	}
}

func TestDropOnEmptyBoard(t *testing.T) {
	var b Board
	got, lines, ok := b.Drop(I, 0, 0)
	if !ok || lines != 0 {
		t.Fatalf("ok=%v lines=%d", ok, lines)
	}
	for c := 0; c < 4; c++ {
		if !got[Height-1][c] {
			t.Errorf("expected block at bottom row col %d", c)
		}
	}
	if got[Height-1][4] {
		t.Error("unexpected block at col 4")
	}
	// the original board is unchanged (passed by value)
	if b != (Board{}) {
		t.Error("original board was mutated")
	}
}

func TestDropVerticalI(t *testing.T) {
	var b Board
	got, _, ok := b.Drop(I, 1, 5)
	if !ok {
		t.Fatal("not ok")
	}
	for r := Height - 4; r < Height; r++ {
		if !got[r][5] {
			t.Errorf("expected block at row %d col 5", r)
		}
	}
}

func TestDropClearsLine(t *testing.T) {
	var b Board
	for c := 0; c < 6; c++ {
		b[Height-1][c] = true
	}
	got, lines, ok := b.Drop(I, 0, 6)
	if !ok || lines != 1 {
		t.Fatalf("ok=%v lines=%d, want 1 line", ok, lines)
	}
	if got != (Board{}) {
		t.Errorf("board should be empty after clear:\n%v", got)
	}
}

func TestDropClearsAndCompactsRowsAbove(t *testing.T) {
	var b Board
	for c := 0; c < 6; c++ {
		b[Height-1][c] = true
	}
	b[Height-2][0] = true // a block that remains above the cleared row
	got, lines, _ := b.Drop(I, 0, 6)
	if lines != 1 {
		t.Fatalf("lines=%d", lines)
	}
	if !got[Height-1][0] || got[Height-2][0] {
		t.Error("block above the cleared row should fall by one")
	}
}

func TestDropTetris(t *testing.T) {
	var b Board
	for r := Height - 4; r < Height; r++ {
		for c := 1; c < Width; c++ {
			b[r][c] = true
		}
	}
	got, lines, ok := b.Drop(I, 1, 0)
	if !ok || lines != 4 || got != (Board{}) {
		t.Fatalf("ok=%v lines=%d", ok, lines)
	}
}

func TestDropOutOfRange(t *testing.T) {
	var b Board
	for _, x := range []int{-1, 7, 10} { // a horizontal I only fits x=0..6
		if _, _, ok := b.Drop(I, 0, x); ok {
			t.Errorf("Drop(I, 0, %d) should fail", x)
		}
	}
}

func TestDropBlockedAtSpawn(t *testing.T) {
	var b Board
	for c := range b[0] {
		b[0][c] = true
	}
	if _, _, ok := b.Drop(O, 0, 4); ok {
		t.Error("should not be placeable when spawn area is occupied")
	}
}

func TestBoardFromGrid(t *testing.T) {
	grid := make([][]int, Height)
	for i := range grid {
		grid[i] = make([]int, Width)
	}
	grid[Height-1][3] = 1
	b, err := BoardFromGrid(grid)
	if err != nil || !b[Height-1][3] {
		t.Fatalf("err=%v", err)
	}

	if _, err := BoardFromGrid(grid[:19]); err == nil {
		t.Error("19 rows should fail")
	}
	bad := append([][]int(nil), grid...)
	bad[5] = make([]int, 9)
	if _, err := BoardFromGrid(bad); err == nil {
		t.Error("9 columns should fail")
	}
	grid[0][0] = 2
	if _, err := BoardFromGrid(grid); err == nil {
		t.Error("value 2 should fail")
	}
}
