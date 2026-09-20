# tetris_demo

Tetris with a single-player mode and a VS-CPU mode. The client is built with Unity, authentication and scores go through Nakama, and the CPU's decisions come from a hand-written Go server.

## Screenshots

| Menu | Single play |
|---|---|
| ![Menu](docs/images/menu.png) | ![Single play](docs/images/single.png) |

| VS CPU | Result |
|---|---|
| ![VS CPU](docs/images/vs-cpu.png) | ![Result](docs/images/result.png) |

| Ranking |
|---|
| ![Ranking](docs/images/ranking.png) |

## Layout

| Directory | Role | Runs on |
|---|---|---|
| `Assets/` `Packages/` `ProjectSettings/` | Unity client (Unity 6000.6.2f1, URP) | Unity Editor |
| `nakama/` | Nakama + PostgreSQL (authentication, leaderboard) | Docker |
| `ai-server/` | CPU opponent AI in Go (`POST /api/move`) | Docker (or `go run` on the host during development) |

## Getting started

```bash
cd nakama && docker compose up -d              # Nakama + PostgreSQL
cd ai-server && docker compose up -d --build   # AI server
```

Then press Play in Unity, in any scene, and the menu appears. No scene editing is needed:
`GameBootstrap` creates the game at runtime.

| Key | Action |
|---|---|
| Left / Right (A / D) | Move (hold to repeat) |
| Down (S) | Soft drop |
| Up / X / W, Z | Rotate clockwise, counter-clockwise |
| Space | Hard drop |
| 1 / 2 / 3 | Menu: single play / VS CPU / ranking |
| R / Esc | Result screen: retry / back to the menu |
| F12 | Editor only: save the current screen to `docs/images/` |

## Ports

| Port | Purpose |
|---|---|
| 7350 | Nakama HTTP API (what the Unity SDK connects to) |
| 7351 | Nakama Console (`admin` / `password`) |
| 7349 | Nakama gRPC |
| 8090 | Go AI server (8080 is avoided because other apps often use it) |

Connection settings and the fixed account are in [ServerConfig.cs](Assets/Scripts/Game/ServerConfig.cs).

## Tests

```bash
cd ai-server && go test ./...    # board simulation, evaluation, search, API, and a self-play run
```

On the Unity side, use Window > General > Test Runner > EditMode
(`Assets/Tests/EditMode`: rules, board, and AI protocol tests).

## Coordinate contract between Unity and the AI server

Unity and Go must agree on all of the following.

- The board is `board[row][col]`, and row 0 is the top row. 0 = empty, 1 = block.
- Piece shapes are SRS-style `size x size` boxes (I = 4, O = 2, others = 3). `rotation` is the number of clockwise rotations, 0..3.
- `x` is **the column of the piece's leftmost cell after rotation**. The AI evaluates as if the piece is dropped straight down from the top row.
- On the Unity side, `TetrisGame.TryAlignRotation` puts the piece back at its spawn position, then the piece is moved until `LeftmostX` equals `x` and dropped.
- The shape definitions live in two places: `PieceShapes.cs` in Unity and `internal/tetris/piece.go` in Go.
  Both test suites use the same expected values, so changing only one side makes a test fail.

## Notes

- **Local development only.** The Nakama server key (`defaultkey`), the Nakama Console login (`admin` / `password`), the database password, and the fixed player account in `ServerConfig.cs` are all well-known defaults or placeholders. Change every one of them before exposing any of this beyond your own machine.
- The leaderboard `tetris_score` is created at startup by `nakama/data/modules/init.lua`
  (Nakama does not allow clients to create leaderboards).
- To call `http://localhost` from Unity, "Allow downloads over HTTP" must be enabled.
  `Assets/Scripts/Editor/ProjectSetup.cs` sets it to `Always allowed` automatically.
- The AI weights can be tuned through the constants in `ai-server/internal/ai/weights.go` alone.
- In versus mode, clearing 2 lines sends 1 garbage line to the opponent, 3 lines send 2, and 4 lines send 4 (incoming garbage is cancelled first).
