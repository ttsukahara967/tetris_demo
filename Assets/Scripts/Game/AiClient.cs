using System;
using System.Collections;
using System.Text;
using Tetris.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Tetris.Game
{
    /// <summary>Calls POST /api/move on the Go AI server.</summary>
    public static class AiClient
    {
        const int TimeoutSeconds = 3;

        public static IEnumerator PostMove(string json, Action<AiProtocol.MoveResponse> onOk, Action<string> onError)
        {
            using (var req = new UnityWebRequest(ServerConfig.AiBaseUrl + "/api/move", UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = TimeoutSeconds;

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError($"{req.error} ({req.responseCode}) {req.downloadHandler.text}");
                    yield break;
                }

                AiProtocol.MoveResponse resp = null;
                string parseError = null;
                try
                {
                    resp = JsonUtility.FromJson<AiProtocol.MoveResponse>(req.downloadHandler.text);
                }
                catch (Exception e)
                {
                    parseError = e.Message;
                }

                if (resp == null) onError("invalid response: " + (parseError ?? req.downloadHandler.text));
                else onOk(resp);
            }
        }
    }
}
