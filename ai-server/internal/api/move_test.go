package api

import (
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"tetris-ai-server/internal/tetris"
)

func newMux() *http.ServeMux {
	mux := http.NewServeMux()
	mux.HandleFunc("POST /api/move", Move)
	return mux
}

func emptyGrid() [][]int {
	g := make([][]int, tetris.Height)
	for i := range g {
		g[i] = make([]int, tetris.Width)
	}
	return g
}

func post(t *testing.T, body string) *httptest.ResponseRecorder {
	t.Helper()
	rec := httptest.NewRecorder()
	newMux().ServeHTTP(rec, httptest.NewRequest(http.MethodPost, "/api/move", strings.NewReader(body)))
	return rec
}

func marshal(t *testing.T, v any) string {
	t.Helper()
	b, err := json.Marshal(v)
	if err != nil {
		t.Fatal(err)
	}
	return string(b)
}

func TestMoveOK(t *testing.T) {
	g := emptyGrid()
	for c := 4; c < tetris.Width; c++ {
		g[tetris.Height-1][c] = 1
	}
	rec := post(t, marshal(t, MoveRequest{Board: g, CurrentPiece: "I"}))
	if rec.Code != http.StatusOK {
		t.Fatalf("status = %d, body = %s", rec.Code, rec.Body)
	}
	var resp MoveResponse
	if err := json.Unmarshal(rec.Body.Bytes(), &resp); err != nil {
		t.Fatal(err)
	}
	if resp.X != 0 || resp.Rotation != 0 || resp.Reasoning == "" {
		t.Errorf("resp = %+v", resp)
	}
	if ct := rec.Header().Get("Content-Type"); ct != "application/json" {
		t.Errorf("Content-Type = %q", ct)
	}
}

func TestMoveWithNextPiece(t *testing.T) {
	rec := post(t, marshal(t, MoveRequest{Board: emptyGrid(), CurrentPiece: "T", NextPiece: "L"}))
	if rec.Code != http.StatusOK {
		t.Fatalf("status = %d, body = %s", rec.Code, rec.Body)
	}
	if !strings.Contains(rec.Body.String(), "lookahead=true") {
		t.Errorf("body = %s", rec.Body)
	}
}

func TestMoveBadRequests(t *testing.T) {
	short := emptyGrid()[:19]
	bad := emptyGrid()
	bad[3][3] = 7
	tests := []struct {
		name string
		body string
	}{
		{"not json", `{`},
		{"empty body", ``},
		{"unknown current piece", marshal(t, MoveRequest{Board: emptyGrid(), CurrentPiece: "X"})},
		{"missing current piece", marshal(t, MoveRequest{Board: emptyGrid()})},
		{"unknown next piece", marshal(t, MoveRequest{Board: emptyGrid(), CurrentPiece: "T", NextPiece: "Q"})},
		{"wrong height", marshal(t, MoveRequest{Board: short, CurrentPiece: "T"})},
		{"bad cell value", marshal(t, MoveRequest{Board: bad, CurrentPiece: "T"})},
		{"missing board", `{"currentPiece":"T"}`},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			rec := post(t, tt.body)
			if rec.Code != http.StatusBadRequest {
				t.Errorf("status = %d, want 400 (body %s)", rec.Code, rec.Body)
			}
			var e errorResponse
			if err := json.Unmarshal(rec.Body.Bytes(), &e); err != nil || e.Error == "" {
				t.Errorf("error body = %s", rec.Body)
			}
		})
	}
}

func TestMoveNoPlacement(t *testing.T) {
	g := emptyGrid()
	for c := range g[0] {
		g[0][c] = 1
	}
	rec := post(t, marshal(t, MoveRequest{Board: g, CurrentPiece: "T"}))
	if rec.Code != http.StatusUnprocessableEntity {
		t.Errorf("status = %d, want 422", rec.Code)
	}
}

func TestMoveBodyTooLarge(t *testing.T) {
	rec := post(t, `{"board":[`+strings.Repeat("0,", maxBodyBytes)+`0]}`)
	if rec.Code != http.StatusBadRequest {
		t.Errorf("status = %d, want 400", rec.Code)
	}
}

func TestMoveMethodNotAllowed(t *testing.T) {
	rec := httptest.NewRecorder()
	newMux().ServeHTTP(rec, httptest.NewRequest(http.MethodGet, "/api/move", nil))
	if rec.Code != http.StatusMethodNotAllowed {
		t.Errorf("status = %d, want 405", rec.Code)
	}
}
