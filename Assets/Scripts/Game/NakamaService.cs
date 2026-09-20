using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

namespace Tetris.Game
{
    public readonly struct RankEntry
    {
        public readonly string Rank;
        public readonly string Username;
        public readonly string Score;

        public RankEntry(string rank, string username, string score)
        {
            Rank = rank;
            Username = username;
            Score = score;
        }
    }

    /// <summary>Authentication (fixed account) and the leaderboard (score submission, ranking fetch) on Nakama.</summary>
    public sealed class NakamaService
    {
        readonly IClient client;
        ISession session;

        public NakamaService()
        {
            client = new Client(
                ServerConfig.NakamaScheme,
                ServerConfig.NakamaHost,
                ServerConfig.NakamaPort,
                ServerConfig.NakamaServerKey,
                UnityWebRequestAdapter.Instance);
        }

        public string Username => session?.Username;

        /// <summary>Authenticates if not already authenticated. Returns null on success, or an error message on failure.</summary>
        public async Task<string> EnsureSessionAsync()
        {
            if (session != null && !session.IsExpired) return null;
            try
            {
                session = await client.AuthenticateEmailAsync(
                    ServerConfig.NakamaEmail, ServerConfig.NakamaPassword, ServerConfig.NakamaUsername, true);
                return null;
            }
            catch (Exception e)
            {
                session = null;
                return e.Message;
            }
        }

        /// <summary>Submits a score. Returns null on success, or an error message on failure.</summary>
        public async Task<string> SubmitScoreAsync(long score, string metadataJson)
        {
            string authError = await EnsureSessionAsync();
            if (authError != null) return authError;
            try
            {
                await client.WriteLeaderboardRecordAsync(session, ServerConfig.LeaderboardId, score, 0, metadataJson);
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        /// <summary>Fetches the top limit ranking entries. On failure, error holds the message.</summary>
        public async Task<(List<RankEntry> entries, string error)> GetTopAsync(int limit)
        {
            var entries = new List<RankEntry>();
            string authError = await EnsureSessionAsync();
            if (authError != null) return (entries, authError);
            try
            {
                var list = await client.ListLeaderboardRecordsAsync(session, ServerConfig.LeaderboardId, null, null, limit);
                foreach (var r in list.Records) entries.Add(new RankEntry(r.Rank, r.Username, r.Score));
                return (entries, null);
            }
            catch (Exception e)
            {
                return (entries, e.Message);
            }
        }
    }
}
