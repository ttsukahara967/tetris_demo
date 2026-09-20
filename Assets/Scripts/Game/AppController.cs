using System;
using System.Collections.Generic;
using Tetris.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Tetris.Game
{
    public enum AppState { Menu, Playing, Result, Ranking }
    public enum GameMode { Single, Versus }

    /// <summary>
    /// Screen flow and drawing (IMGUI) for the menu, play, result, and ranking screens.
    /// Single mode is one human; versus mode is human vs CPU (Go AI server). Submits the score to Nakama when the game ends.
    /// </summary>
    public sealed class AppController : MonoBehaviour
    {
        const int RankingLimit = 10;
        const int ResultRankingRows = 5;

        static readonly Color[] PieceColors =
        {
            new Color(0.25f, 0.85f, 0.95f), // I
            new Color(0.98f, 0.85f, 0.25f), // O
            new Color(0.70f, 0.40f, 0.90f), // T
            new Color(0.40f, 0.85f, 0.40f), // S
            new Color(0.95f, 0.35f, 0.35f), // Z
            new Color(0.30f, 0.45f, 0.95f), // J
            new Color(0.98f, 0.60f, 0.25f), // L
        };
        static readonly Color GarbageColor = new Color(0.5f, 0.5f, 0.55f);
        static readonly Color BoardColor = new Color(0.08f, 0.09f, 0.12f);
        static readonly Color PanelColor = new Color(0.09f, 0.10f, 0.14f, 0.97f);
        static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);

        AppState state = AppState.Menu;
        GameMode mode;
        TetrisGame human;
        TetrisGame cpuGame;
        HumanInput input;
        CpuPlayer cpu;
        NakamaService nakama;

        string nakamaStatus = "Nakama: connecting...";
        string resultTitle = "";
        string submitStatus = "";
        List<RankEntry> ranking = new List<RankEntry>();
        string rankingStatus = "";

        GUIStyle label, big, button, small, smallCenter, center;
        int styleHeight;
        float cell;

        void Awake()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            }
            nakama = new NakamaService();
        }

        async void Start()
        {
            string err = await nakama.EnsureSessionAsync();
            nakamaStatus = err == null ? $"Nakama: connected as {nakama.Username}" : "Nakama: offline (" + err + ")";
        }

        // ---------------------------------------------------------------- Update

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
#if UNITY_EDITOR
                if (kb.f12Key.wasPressedThisFrame) CaptureScreenshot();
#endif
                if (kb.escapeKey.wasPressedThisFrame && state != AppState.Menu) state = AppState.Menu;
                if (state == AppState.Menu)
                {
                    if (kb.digit1Key.wasPressedThisFrame) StartGame(GameMode.Single);
                    else if (kb.digit2Key.wasPressedThisFrame) StartGame(GameMode.Versus);
                    else if (kb.digit3Key.wasPressedThisFrame) ShowRanking();
                }
                else if (state == AppState.Result && kb.rKey.wasPressedThisFrame)
                {
                    StartGame(mode);
                }
            }

            if (state != AppState.Playing) return;

            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            input.Update(human, dt);
            human.Update(dt);
            if (mode == GameMode.Versus)
            {
                cpu.Update(dt);
                cpuGame.Update(dt);
            }

            if (human.IsGameOver || (mode == GameMode.Versus && cpuGame.IsGameOver)) FinishGame();
        }

#if UNITY_EDITOR
        /// <summary>Editor-only helper: F12 saves the Game view to docs/images/ (used for the README screenshots).</summary>
        void CaptureScreenshot()
        {
            string name = state == AppState.Playing
                ? (mode == GameMode.Versus ? "vs-cpu" : "single")
                : state.ToString().ToLowerInvariant();
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "docs", "images"));
            System.IO.Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png"));
            Debug.Log("Saved screenshot: docs/images/" + name + ".png");
        }
#endif

        void StartGame(GameMode newMode)
        {
            mode = newMode;
            int seed = Environment.TickCount;
            human = new TetrisGame(seed);
            input = new HumanInput();
            if (mode == GameMode.Versus)
            {
                // Same seed = same piece order. Garbage lines are sent back and forth.
                cpuGame = new TetrisGame(seed);
                cpu = new CpuPlayer(cpuGame, this);
                human.GarbageSent += n => cpuGame.ReceiveGarbage(n);
                cpuGame.GarbageSent += n => human.ReceiveGarbage(n);
            }
            else
            {
                cpuGame = null;
                cpu = null;
            }
            submitStatus = "";
            state = AppState.Playing;
        }

        async void FinishGame()
        {
            state = AppState.Result;
            string outcome;
            if (mode == GameMode.Single) { resultTitle = "GAME OVER"; outcome = "single"; }
            else if (human.IsGameOver) { resultTitle = "YOU LOSE"; outcome = "lose"; }
            else { resultTitle = "YOU WIN"; outcome = "win"; }

            submitStatus = "Submitting score...";
            ranking = new List<RankEntry>();
            string meta = "{\"mode\":\"" + (mode == GameMode.Single ? "single" : "versus") + "\",\"result\":\"" + outcome + "\"}";
            string err = await nakama.SubmitScoreAsync(human.Score, meta);
            submitStatus = err == null ? "Score submitted" : "Score not submitted: " + err;
            await LoadRanking();
        }

        async void ShowRanking()
        {
            state = AppState.Ranking;
            await LoadRanking();
        }

        async System.Threading.Tasks.Task LoadRanking()
        {
            rankingStatus = "Loading...";
            var (entries, error) = await nakama.GetTopAsync(RankingLimit);
            ranking = entries;
            rankingStatus = error != null ? "Ranking unavailable: " + error : (entries.Count == 0 ? "No records yet" : "");
        }

        // ---------------------------------------------------------------- Drawing

        void OnGUI()
        {
            EnsureStyles();
            switch (state)
            {
                case AppState.Menu: DrawMenu(); break;
                case AppState.Playing: DrawPlaying(); break;
                case AppState.Result: DrawPlaying(); DrawResult(); break;
                case AppState.Ranking: DrawRanking(); break;
            }
            GUI.Label(new Rect(8, Screen.height - small.fontSize * 1.8f, Screen.width - 16, small.fontSize * 1.8f), nakamaStatus, small);
        }

        void EnsureStyles()
        {
            if (label != null && styleHeight == Screen.height) return;
            styleHeight = Screen.height;
            int basis = Mathf.Max(12, Screen.height / 32);
            label = new GUIStyle(GUI.skin.label) { fontSize = basis, normal = { textColor = Color.white } };
            small = new GUIStyle(label) { fontSize = Mathf.Max(10, basis * 2 / 3), normal = { textColor = new Color(1, 1, 1, 0.65f) } };
            smallCenter = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            big = new GUIStyle(label) { fontSize = basis * 2, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button) { fontSize = basis };
        }

        void DrawMenu()
        {
            float w = Mathf.Min(Screen.width * 0.5f, Screen.height * 0.8f);
            float x = (Screen.width - w) / 2f;
            float bh = button.fontSize * 2.4f;
            float y = Screen.height * 0.18f;

            GUI.Label(new Rect(x, y, w, big.fontSize * 1.6f), "TETRIS", big);
            y += big.fontSize * 2.2f;

            if (GUI.Button(new Rect(x, y, w, bh), "1  Single Play", button)) StartGame(GameMode.Single);
            y += bh * 1.2f;
            if (GUI.Button(new Rect(x, y, w, bh), "2  VS CPU", button)) StartGame(GameMode.Versus);
            y += bh * 1.2f;
            if (GUI.Button(new Rect(x, y, w, bh), "3  Ranking", button)) ShowRanking();
            y += bh * 1.6f;

            GUI.Label(new Rect(x, y, w, small.fontSize * 6f),
                "Move: Left/Right (A/D)   Soft drop: Down (S)\nRotate: Up/X (W) / Z   Hard drop: Space\nBack to menu: Esc", small);
        }

        void DrawRanking()
        {
            float w = Mathf.Min(Screen.width * 0.6f, Screen.height * 1.1f);
            float x = (Screen.width - w) / 2f;
            float y = Screen.height * 0.1f;
            GUI.Label(new Rect(x, y, w, big.fontSize * 1.6f), "RANKING", big);
            y += big.fontSize * 2f;
            y = DrawRankingRows(x, y, w);
            if (GUI.Button(new Rect(x, y + label.fontSize, w, button.fontSize * 2.2f), "Back (Esc)", button)) state = AppState.Menu;
        }

        float DrawRankingRows(float x, float y, float w, int maxRows = int.MaxValue)
        {
            float rh = label.fontSize * 1.5f;
            if (!string.IsNullOrEmpty(rankingStatus))
            {
                GUI.Label(new Rect(x, y, w, rh * 3), rankingStatus, center);
                y += rh;
            }
            int shown = 0;
            foreach (var e in ranking)
            {
                if (shown++ >= maxRows) break;
                GUI.Label(new Rect(x, y, w * 0.15f, rh), "#" + e.Rank, label);
                GUI.Label(new Rect(x + w * 0.15f, y, w * 0.55f, rh), e.Username, label);
                GUI.Label(new Rect(x + w * 0.7f, y, w * 0.3f, rh), e.Score, label);
                y += rh;
            }
            return y;
        }

        void DrawResult()
        {
            float w = Mathf.Min(Screen.width * 0.6f, Screen.height * 1.1f);
            float h = Screen.height * 0.8f;
            float x = (Screen.width - w) / 2f;
            float y = (Screen.height - h) / 2f;
            DrawRect(new Rect(0, 0, Screen.width, Screen.height), DimColor);
            DrawRect(new Rect(x, y, w, h), PanelColor);

            float py = y + label.fontSize;
            GUI.Label(new Rect(x, py, w, big.fontSize * 1.6f), resultTitle, big);
            py += big.fontSize * 1.8f;
            GUI.Label(new Rect(x, py, w, label.fontSize * 1.5f), "Score " + human.Score, center);
            py += label.fontSize * 1.5f;
            GUI.Label(new Rect(x, py, w, small.fontSize * 1.8f), submitStatus, smallCenter);
            py += small.fontSize * 2.2f;
            py = DrawRankingRows(x + w * 0.1f, py, w * 0.8f, ResultRankingRows);

            float bh = button.fontSize * 2.2f;
            float bw = w * 0.4f;
            float by = y + h - bh - label.fontSize;
            if (GUI.Button(new Rect(x + w * 0.07f, by, bw, bh), "Retry (R)", button)) StartGame(mode);
            if (GUI.Button(new Rect(x + w - w * 0.07f - bw, by, bw, bh), "Menu (Esc)", button)) state = AppState.Menu;
        }

        void DrawPlaying()
        {
            bool versus = mode == GameMode.Versus;
            cell = Mathf.Floor(Mathf.Min((Screen.height - 70f) / 20f, Screen.width / (versus ? 31f : 17f)));
            float boardW = cell * Board.Width;
            float panelW = cell * 5f;
            float total = versus ? (boardW + panelW) * 2f + cell : boardW + panelW;
            float left = (Screen.width - total) / 2f;
            float top = (Screen.height - cell * 20f) / 2f - small.fontSize * 0.5f;

            DrawPlayer(human, left, top, "YOU", true);
            if (versus)
            {
                DrawPlayer(cpuGame, left + boardW + panelW + cell, top, "CPU", false);
                DrawCpuInfo(left + boardW + panelW + cell, top + cell * 20f + 4f);
            }
        }

        void DrawCpuInfo(float x, float y)
        {
            string text = cpu.Error != null ? "AI server error: " + cpu.Error : "AI: " + cpu.LastReasoning;
            var style = small;
            var prev = style.normal.textColor;
            style.normal.textColor = cpu.Error != null ? new Color(1f, 0.5f, 0.5f) : prev;
            GUI.Label(new Rect(x, y, cell * 15f, small.fontSize * 1.6f), text, style);
            style.normal.textColor = prev;
        }

        void DrawPlayer(TetrisGame g, float left, float top, string name, bool showGhost)
        {
            float boardW = cell * Board.Width;
            DrawRect(new Rect(left - 2, top - 2, boardW + 4, cell * 20 + 4), new Color(1, 1, 1, 0.25f));
            DrawRect(new Rect(left, top, boardW, cell * 20), BoardColor);

            for (int r = 0; r < Board.Height; r++)
            {
                for (int c = 0; c < Board.Width; c++)
                {
                    byte v = g.Board.Get(r, c);
                    if (v != 0) DrawCell(left, top, c, r, ColorFor(v), 1f);
                }
            }

            if (!g.IsGameOver)
            {
                if (showGhost)
                {
                    int gy = g.GhostY();
                    foreach (var pc in PieceShapes.Cells(g.Current, g.Rotation))
                        DrawCell(left, top, g.PosX + pc.X, gy + pc.Y, ColorFor((byte)((int)g.Current + 1)), 0.25f);
                }
                foreach (var pc in PieceShapes.Cells(g.Current, g.Rotation))
                    DrawCell(left, top, g.PosX + pc.X, g.PosY + pc.Y, ColorFor((byte)((int)g.Current + 1)), 1f);
            }

            // Side panel on the right
            float px = left + boardW + cell * 0.5f;
            float py = top;
            float lh = label.fontSize * 1.4f;
            GUI.Label(new Rect(px, py, cell * 5f, lh), name, label);
            py += lh * 1.4f;
            GUI.Label(new Rect(px, py, cell * 5f, lh), "SCORE", small);
            GUI.Label(new Rect(px, py + small.fontSize * 1.2f, cell * 5f, lh), g.Score.ToString(), label);
            py += lh * 2.1f;
            GUI.Label(new Rect(px, py, cell * 5f, lh), "LINES " + g.Lines + "   LV " + g.Level, small);
            py += lh * 1.2f;
            GUI.Label(new Rect(px, py, cell * 5f, lh), "NEXT", small);
            py += lh * 0.9f;
            for (int i = 0; i < 3; i++)
            {
                DrawMini(g.PeekNext(i), px, py);
                py += cell * 2.6f;
            }
            if (g.PendingGarbage > 0)
            {
                float bh = cell * Mathf.Min(g.PendingGarbage, 20);
                DrawRect(new Rect(left - cell * 0.35f, top + cell * 20 - bh, cell * 0.25f, bh), new Color(0.95f, 0.3f, 0.3f));
            }
        }

        void DrawMini(PieceType type, float x, float y)
        {
            float s = cell * 0.6f;
            foreach (var pc in PieceShapes.Cells(type, 0))
            {
                var color = PieceColors[(int)type];
                DrawRect(new Rect(x + pc.X * s, y + pc.Y * s, s - 1, s - 1), color);
            }
        }

        void DrawCell(float left, float top, int col, int row, Color color, float alpha)
        {
            if (row < 0) return;
            color.a = alpha;
            DrawRect(new Rect(left + col * cell + 1, top + row * cell + 1, cell - 2, cell - 2), color);
        }

        static Color ColorFor(byte v) => v >= Board.GarbageValue ? GarbageColor : PieceColors[Mathf.Clamp(v - 1, 0, PieceColors.Length - 1)];

        static void DrawRect(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
