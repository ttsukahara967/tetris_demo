-- Creates the leaderboard at startup.
-- Nakama leaderboards cannot be created from a client, so the server has to provide them.
-- authoritative=false: allows the client (Unity) to write scores directly (initial stage).
-- To add score validation later, set it to true and switch to writes via an RPC.
local nk = require("nakama")

local ok, err = pcall(nk.leaderboard_create, "tetris_score", false, "desc", "best", nil, { game = "tetris" })
if not ok then
  nk.logger_error(("leaderboard_create failed: %s"):format(tostring(err)))
end
