package tetris

import "fmt"

const (
	Width  = 10
	Height = 20
)

// Board is a 10x20 board. Board[row][col] is true if there is a block. Row 0 is the top row.
type Board [Height][Width]bool

// BoardFromGrid builds a Board from the API's 0/1 grid, validating its dimensions and values.
func BoardFromGrid(grid [][]int) (Board, error) {
	var b Board
	if len(grid) != Height {
		return b, fmt.Errorf("board must have %d rows, got %d", Height, len(grid))
	}
	for r, row := range grid {
		if len(row) != Width {
			return b, fmt.Errorf("board row %d must have %d columns, got %d", r, Width, len(row))
		}
		for c, v := range row {
			switch v {
			case 0:
			case 1:
				b[r][c] = true
			default:
				return b, fmt.Errorf("board[%d][%d] must be 0 or 1, got %d", r, c, v)
			}
		}
	}
	return b, nil
}

// Collides reports whether placing p rotated by rot with its box origin at (ox, oy) hits a wall, the floor, or an existing block.
// Cells above the board (row < 0) count as empty.
func (b *Board) Collides(p Piece, rot, ox, oy int) bool {
	for _, c := range p.Cells(rot) {
		x, y := ox+c.X, oy+c.Y
		if x < 0 || x >= Width || y >= Height {
			return true
		}
		if y >= 0 && b[y][x] {
			return true
		}
	}
	return false
}

// Drop drops p, rotated by rot with its leftmost cell in column x, straight down from the top row and returns the result.
// The return values are (the board after placing and clearing lines, the number of lines cleared, whether it could be placed).
// It cannot be placed (ok=false) if it is out of range, or if it already collides at the spawn position.
func (b Board) Drop(p Piece, rot, x int) (result Board, lines int, ok bool) {
	minX, maxX, minY, _ := p.Bounds(rot)
	ox := x - minX
	if x < 0 || x+(maxX-minX) >= Width {
		return b, 0, false
	}
	oy := -minY // start from the position where the topmost cell is on row 0
	if b.Collides(p, rot, ox, oy) {
		return b, 0, false
	}
	for !b.Collides(p, rot, ox, oy+1) {
		oy++
	}
	for _, c := range p.Cells(rot) {
		b[oy+c.Y][ox+c.X] = true
	}
	lines = b.clearLines()
	return b, lines, true
}

// clearLines removes full rows, compacts the rows above, and returns the number of rows cleared.
func (b *Board) clearLines() int {
	cleared := 0
	dst := Height - 1
	for src := Height - 1; src >= 0; src-- {
		if b.rowFull(src) {
			cleared++
			continue
		}
		b[dst] = b[src]
		dst--
	}
	for ; dst >= 0; dst-- {
		b[dst] = [Width]bool{}
	}
	return cleared
}

func (b *Board) rowFull(r int) bool {
	for _, v := range b[r] {
		if !v {
			return false
		}
	}
	return true
}
