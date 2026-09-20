namespace Tetris.Game
{
    /// <summary>Connection settings. Fixed values for local development.</summary>
    public static class ServerConfig
    {
        // Nakama (nakama/docker-compose.yml)
        public const string NakamaScheme = "http";
        public const string NakamaHost = "127.0.0.1";
        public const int NakamaPort = 7350;
        public const string NakamaServerKey = "defaultkey";

        // Fixed account for the initial stage (create=true, so it is created automatically on first use)
        public const string NakamaEmail = "unity-player@example.com";
        public const string NakamaPassword = "password1234";
        public const string NakamaUsername = "player1";

        // Leaderboard created by nakama/data/modules/init.lua
        public const string LeaderboardId = "tetris_score";

        // Go AI server (ai-server/)
        public const string AiBaseUrl = "http://127.0.0.1:8090";
    }
}
