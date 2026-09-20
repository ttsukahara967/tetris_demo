// Package tetris provides a pure simulation of the board and pieces.
//
// Coordinate system: the board is Board[row][col], row 0 is the top row and col 0 is the left column.
// A piece is a set of cells in a size x size box, and the box origin (ox, oy) is placed on the board.
// Rotation follows SRS; rot 0..3 is the number of clockwise rotations.
package tetris

import "fmt"

// Piece is the type of a piece.
type Piece int

const (
	I Piece = iota
	O
	T
	S
	Z
	J
	L
	numPieces
)

// Point is a cell coordinate within the box.
type Point struct{ X, Y int }

var pieceNames = [numPieces]string{"I", "O", "T", "S", "Z", "J", "L"}

func (p Piece) String() string {
	if p < 0 || p >= numPieces {
		return fmt.Sprintf("Piece(%d)", int(p))
	}
	return pieceNames[p]
}

// ParsePiece returns the Piece for a string such as "I", "O", ...
func ParsePiece(s string) (Piece, error) {
	for i, n := range pieceNames {
		if n == s {
			return Piece(i), nil
		}
	}
	return 0, fmt.Errorf("unknown piece %q", s)
}

// AllPieces returns all 7 pieces.
func AllPieces() []Piece { return []Piece{I, O, T, S, Z, J, L} }

// Cell layout at spawn (rot 0). Box size is I=4, O=2, others=3.
var baseShapes = [numPieces]struct {
	size  int
	cells [4]Point
}{
	I: {4, [4]Point{{0, 1}, {1, 1}, {2, 1}, {3, 1}}},
	O: {2, [4]Point{{0, 0}, {1, 0}, {0, 1}, {1, 1}}},
	T: {3, [4]Point{{1, 0}, {0, 1}, {1, 1}, {2, 1}}},
	S: {3, [4]Point{{1, 0}, {2, 0}, {0, 1}, {1, 1}}},
	Z: {3, [4]Point{{0, 0}, {1, 0}, {1, 1}, {2, 1}}},
	J: {3, [4]Point{{0, 0}, {0, 1}, {1, 1}, {2, 1}}},
	L: {3, [4]Point{{2, 0}, {0, 1}, {1, 1}, {2, 1}}},
}

// shapes[piece][rot] holds the rotated cells. Built at init by rotating the base shape clockwise.
var shapes = buildShapes()

func buildShapes() (out [numPieces][4][4]Point) {
	for p := Piece(0); p < numPieces; p++ {
		n := baseShapes[p].size
		cur := baseShapes[p].cells
		for r := 0; r < 4; r++ {
			out[p][r] = cur
			for i, c := range cur {
				// Rotate 90° clockwise: (x, y) -> (n-1-y, x)
				cur[i] = Point{n - 1 - c.Y, c.X}
			}
		}
	}
	return out
}

// Cells returns the cells (box coordinates) for rotation rot. rot is normalized to 0..3.
func (p Piece) Cells(rot int) [4]Point {
	return shapes[p][((rot%4)+4)%4]
}

// Bounds returns the bounding range (box coordinates, inclusive) of the cells for rotation rot.
func (p Piece) Bounds(rot int) (minX, maxX, minY, maxY int) {
	cells := p.Cells(rot)
	minX, maxX, minY, maxY = cells[0].X, cells[0].X, cells[0].Y, cells[0].Y
	for _, c := range cells[1:] {
		minX, maxX = min(minX, c.X), max(maxX, c.X)
		minY, maxY = min(minY, c.Y), max(maxY, c.Y)
	}
	return
}

// Placement is a placement returned by the API. X is the column of the piece's leftmost cell after rotation.
type Placement struct {
	X        int
	Rotation int
}

// Placements returns every way to place p (rotation x horizontal position).
// Rotations that look identical (such as the 4 rotations of O) keep only the smallest rot.
func Placements(p Piece) []Placement {
	var out []Placement
	seen := map[[4]Point]bool{}
	for rot := 0; rot < 4; rot++ {
		minX, maxX, minY, _ := p.Bounds(rot)
		key := normalized(p.Cells(rot), minX, minY)
		if seen[key] {
			continue
		}
		seen[key] = true
		for x := 0; x+(maxX-minX) < Width; x++ {
			out = append(out, Placement{X: x, Rotation: rot})
		}
	}
	return out
}

// normalized shifts the cells so the top-left is the origin and sorts them (used to detect duplicate rotations).
func normalized(cells [4]Point, minX, minY int) [4]Point {
	var out [4]Point
	for i, c := range cells {
		out[i] = Point{c.X - minX, c.Y - minY}
	}
	// insertion sort of 4 elements
	for i := 1; i < 4; i++ {
		for j := i; j > 0 && less(out[j], out[j-1]); j-- {
			out[j], out[j-1] = out[j-1], out[j]
		}
	}
	return out
}

func less(a, b Point) bool {
	if a.Y != b.Y {
		return a.Y < b.Y
	}
	return a.X < b.X
}
