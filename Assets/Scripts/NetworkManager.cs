using UnityEngine;
using NativeWebSocket;
using System;
using System.Collections.Generic;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    public enum ConnectionState { Disconnected, Connecting, Connected, InRoom, InGame }
    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public string RoomCode { get; private set; }
    public int LocalPlayerIndex { get; private set; } = -1;
    public bool IsHost { get; private set; }
    public bool IsMultiplayer => State == ConnectionState.InRoom || State == ConnectionState.InGame;

    // Events
    public event Action<string> OnRoomCreated;       // room code
    public event Action<int> OnRoomJoined;           // playerIndex
    public event Action<LobbyPlayer[]> OnPlayerListUpdated;
    public event Action<GameStartData> OnGameStarted;
    public event Action<GameActionData> OnGameAction;
    public event Action<ExploreSyncData> OnExploreSync;
    public event Action<string> OnError;
    public event Action OnDisconnected;
    public event Action OnHostChanged;
    public event Action<int> OnPlayerDisconnected;

    [Header("Server")]
    public string serverUrl = "ws://localhost:8080";

    private WebSocket ws;

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        ws?.DispatchMessageQueue();
#endif
    }

    void OnDestroy()
    {
        if (ws != null && ws.State == WebSocketState.Open)
            ws.Close();
    }

    // ===== Connection =====

    public async void Connect(Action onConnected = null)
    {
        if (ws != null && ws.State == WebSocketState.Open) { onConnected?.Invoke(); return; }

        State = ConnectionState.Connecting;
        ws = new WebSocket(serverUrl);

        ws.OnOpen += () =>
        {
            Debug.Log("[Net] Connected to server");
            State = ConnectionState.Connected;
            onConnected?.Invoke();
        };

        ws.OnMessage += (bytes) =>
        {
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            HandleMessage(json);
        };

        ws.OnClose += (code) =>
        {
            Debug.Log($"[Net] Disconnected: {code}");
            State = ConnectionState.Disconnected;
            RoomCode = null;
            LocalPlayerIndex = -1;
            OnDisconnected?.Invoke();
        };

        ws.OnError += (err) =>
        {
            Debug.LogError($"[Net] Error: {err}");
        };

        await ws.Connect();
    }

    public void Disconnect()
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            SendRaw("{\"type\":\"leave_room\"}");
            ws.Close();
        }
        State = ConnectionState.Disconnected;
        RoomCode = null;
        LocalPlayerIndex = -1;
    }

    // ===== Room Operations =====

    public void CreateRoom(string playerName, int maxPlayers, int mapRadius)
    {
        Connect(() =>
        {
            IsHost = true;
            Send(new { type = "create_room", name = playerName, maxPlayers, mapRadius });
        });
    }

    public void JoinRoom(string code, string playerName)
    {
        Connect(() =>
        {
            IsHost = false;
            Send(new { type = "join_room", code, name = playerName });
        });
    }

    public void SetReady(bool ready) => Send(new { type = "player_ready", ready });
    public void StartGame() => Send(new { type = "start_game" });

    // ===== Game Actions =====

    public void SendGameAction(string action, Dictionary<string, object> extra = null)
    {
        var msg = new Dictionary<string, object> { { "type", "game_action" }, { "action", action } };
        if (extra != null) foreach (var kv in extra) msg[kv.Key] = kv.Value;
        Send(msg);
    }

    public void SendExploreSync(Vector3 pos, float rotY, float animSpeed)
    {
        // Minimal JSON for bandwidth
        Send(new { type = "explore_sync", pos = new float[] { pos.x, pos.y, pos.z }, rot = rotY, anim = animSpeed });
    }

    // ===== Message Handling =====

    private void HandleMessage(string json)
    {
        var msg = JsonUtility.FromJson<NetworkMessage>(json);

        switch (msg.type)
        {
            case "ping":
                SendRaw("{\"type\":\"pong\"}");
                break;
            case "room_created":
                var rc = JsonUtility.FromJson<RoomCreatedMsg>(json);
                RoomCode = rc.code;
                LocalPlayerIndex = rc.playerIndex;
                State = ConnectionState.InRoom;
                OnRoomCreated?.Invoke(rc.code);
                break;
            case "room_joined":
                var rj = JsonUtility.FromJson<RoomJoinedMsg>(json);
                RoomCode = rj.code;
                LocalPlayerIndex = rj.playerIndex;
                State = ConnectionState.InRoom;
                OnRoomJoined?.Invoke(rj.playerIndex);
                break;
            case "player_list":
                var pl = JsonUtility.FromJson<PlayerListMsg>(json);
                OnPlayerListUpdated?.Invoke(pl.players);
                break;
            case "game_start":
                var gs = JsonUtility.FromJson<GameStartData>(json);
                State = ConnectionState.InGame;
                OnGameStarted?.Invoke(gs);
                break;
            case "game_action":
                var ga = ParseGameAction(json);
                OnGameAction?.Invoke(ga);
                break;
            case "explore_sync":
                var es = JsonUtility.FromJson<ExploreSyncData>(json);
                OnExploreSync?.Invoke(es);
                break;
            case "error":
                var err = JsonUtility.FromJson<ErrorMsg>(json);
                Debug.LogWarning($"[Net] Server error: {err.message}");
                OnError?.Invoke(err.message);
                break;
            case "host_changed":
                IsHost = true;
                OnHostChanged?.Invoke();
                break;
            case "player_disconnected":
                var pd = JsonUtility.FromJson<PlayerDisconnectedMsg>(json);
                OnPlayerDisconnected?.Invoke(pd.playerIndex);
                break;
            case "action_rejected":
                var ar = JsonUtility.FromJson<ActionRejectedMsg>(json);
                Debug.LogWarning($"[Net] Action rejected: {ar.action} - {ar.reason}");
                break;
        }
    }

    private GameActionData ParseGameAction(string json)
    {
        // JsonUtility can't handle dynamic fields well, use manual parsing
        var data = new GameActionData();
        data.rawJson = json;

        // Extract common fields
        data.action = ExtractString(json, "action");
        data.playerIndex = ExtractInt(json, "playerIndex");
        data.vertexKey = ExtractString(json, "vertexKey");
        data.edgeKey = ExtractString(json, "edgeKey");
        data.tileQ = ExtractInt(json, "tileQ");
        data.tileR = ExtractInt(json, "tileR");
        data.d1 = ExtractInt(json, "d1");
        data.d2 = ExtractInt(json, "d2");
        data.giveType = ExtractInt(json, "giveType");
        data.getType = ExtractInt(json, "getType");
        data.cardIndex = ExtractInt(json, "cardIndex");
        data.resourceType = ExtractInt(json, "resourceType");

        return data;
    }

    // Simple JSON field extraction (avoids dependency on external JSON lib)
    private string ExtractString(string json, string key)
    {
        string search = $"\"{key}\":\"";
        int idx = json.IndexOf(search);
        if (idx == -1) return null;
        idx += search.Length;
        int end = json.IndexOf("\"", idx);
        return end == -1 ? null : json.Substring(idx, end - idx);
    }

    private int ExtractInt(string json, string key)
    {
        string search = $"\"{key}\":";
        int idx = json.IndexOf(search);
        if (idx == -1) return 0;
        idx += search.Length;
        string num = "";
        while (idx < json.Length && (char.IsDigit(json[idx]) || json[idx] == '-'))
            num += json[idx++];
        return num.Length > 0 ? int.Parse(num) : 0;
    }

    // ===== Send Helpers =====

    private void Send(object obj)
    {
        if (ws == null || ws.State != WebSocketState.Open) return;
        string json = JsonUtility.ToJson(obj);
        // JsonUtility doesn't handle anonymous/Dictionary well, use manual for those
        if (obj is Dictionary<string, object> dict)
            json = DictToJson(dict);
        else if (obj.GetType().IsAnonymousType())
            json = AnonymousToJson(obj);
        ws.SendText(json);
    }

    private void SendRaw(string json)
    {
        if (ws != null && ws.State == WebSocketState.Open)
            ws.SendText(json);
    }

    private string DictToJson(Dictionary<string, object> dict)
    {
        var parts = new List<string>();
        foreach (var kv in dict)
        {
            if (kv.Value is string s) parts.Add($"\"{kv.Key}\":\"{s}\"");
            else if (kv.Value is int i) parts.Add($"\"{kv.Key}\":{i}");
            else if (kv.Value is float f) parts.Add($"\"{kv.Key}\":{f}");
            else if (kv.Value is bool b) parts.Add($"\"{kv.Key}\":{(b ? "true" : "false")}");
            else parts.Add($"\"{kv.Key}\":{kv.Value}");
        }
        return "{" + string.Join(",", parts) + "}";
    }

    private string AnonymousToJson(object obj)
    {
        var parts = new List<string>();
        foreach (var prop in obj.GetType().GetProperties())
        {
            var val = prop.GetValue(obj);
            if (val is string s) parts.Add($"\"{prop.Name}\":\"{s}\"");
            else if (val is int i) parts.Add($"\"{prop.Name}\":{i}");
            else if (val is float f) parts.Add($"\"{prop.Name}\":{f:F2}");
            else if (val is bool b) parts.Add($"\"{prop.Name}\":{(b ? "true" : "false")}");
            else if (val is float[] fa)
            {
                parts.Add($"\"{prop.Name}\":[{fa[0]:F2},{fa[1]:F2},{fa[2]:F2}]");
            }
            else parts.Add($"\"{prop.Name}\":{val}");
        }
        return "{" + string.Join(",", parts) + "}";
    }
}

// ===== Extension =====
public static class TypeExtensions
{
    public static bool IsAnonymousType(this Type type)
    {
        return type.Name.Contains("AnonymousType");
    }
}

// ===== Data Classes =====

[Serializable]
public class NetworkMessage { public string type; }

[Serializable]
public class RoomCreatedMsg { public string type; public string code; public int playerIndex; }

[Serializable]
public class RoomJoinedMsg { public string type; public string code; public int playerIndex; }

[Serializable]
public class LobbyPlayer { public string name; public bool ready; public bool isAI; }

[Serializable]
public class PlayerListMsg { public string type; public LobbyPlayer[] players; }

[Serializable]
public class GameStartData { public string type; public int seed; public LobbyPlayer[] players; public int mapRadius; }

[Serializable]
public class GameActionData
{
    public string action;
    public int playerIndex;
    public string vertexKey;
    public string edgeKey;
    public int tileQ, tileR;
    public int d1, d2;
    public int giveType, getType;
    public int cardIndex;
    public int resourceType;
    public string rawJson;
}

[Serializable]
public class ExploreSyncData { public string type; public int playerIndex; public float[] pos; public float rot; public float anim; }

[Serializable]
public class ErrorMsg { public string type; public string message; }

[Serializable]
public class PlayerDisconnectedMsg { public string type; public int playerIndex; }

[Serializable]
public class ActionRejectedMsg { public string type; public string action; public string reason; }
