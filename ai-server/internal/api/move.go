package api

import (
	"encoding/json"
	"errors"
	"net/http"

	"tetris-ai-server/internal/ai"
	"tetris-ai-server/internal/tetris"
)

const maxBodyBytes = 64 << 10

// MoveRequest is the request of POST /api/move.
type MoveRequest struct {
	Board        [][]int `json:"board"`        // 20 rows x 10 columns. 0 = empty, 1 = block. Row 0 is the top row
	CurrentPiece string  `json:"currentPiece"` // I, O, T, S, Z, J, L
	NextPiece    string  `json:"nextPiece"`    // optional. If empty, no lookahead is done
}

// MoveResponse is the response of POST /api/move.
type MoveResponse struct {
	X         int    `json:"x"`        // leftmost column of the piece after rotation
	Rotation  int    `json:"rotation"` // 0..3 (number of clockwise rotations)
	Reasoning string `json:"reasoning,omitempty"`
}

type errorResponse struct {
	Error string `json:"error"`
}

// Move is a handler that computes and returns the CPU's next move.
func Move(w http.ResponseWriter, r *http.Request) {
	r.Body = http.MaxBytesReader(w, r.Body, maxBodyBytes)
	var req MoveRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		writeJSON(w, http.StatusBadRequest, errorResponse{"invalid JSON: " + err.Error()})
		return
	}

	board, err := tetris.BoardFromGrid(req.Board)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, errorResponse{err.Error()})
		return
	}
	cur, err := tetris.ParsePiece(req.CurrentPiece)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, errorResponse{"currentPiece: " + err.Error()})
		return
	}
	var next *tetris.Piece
	if req.NextPiece != "" {
		p, err := tetris.ParsePiece(req.NextPiece)
		if err != nil {
			writeJSON(w, http.StatusBadRequest, errorResponse{"nextPiece: " + err.Error()})
			return
		}
		next = &p
	}

	res, err := ai.BestMove(board, cur, next, ai.DefaultWeights)
	if errors.Is(err, ai.ErrNoPlacement) {
		writeJSON(w, http.StatusUnprocessableEntity, errorResponse{err.Error()})
		return
	}
	if err != nil {
		writeJSON(w, http.StatusInternalServerError, errorResponse{err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, MoveResponse{
		X:         res.Placement.X,
		Rotation:  res.Placement.Rotation,
		Reasoning: res.Reasoning,
	})
}

func writeJSON(w http.ResponseWriter, status int, v any) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(v)
}
