package main

import (
	"log"
	"net/http"

	"tetris-ai-server/internal/api"
)

const addr = ":8090"

func main() {
	mux := http.NewServeMux()
	mux.HandleFunc("GET /healthz", api.Health)
	mux.HandleFunc("POST /api/move", api.Move)

	log.Printf("ai-server listening on %s", addr)
	log.Fatal(http.ListenAndServe(addr, mux))
}
