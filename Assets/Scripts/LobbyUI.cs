using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// マルチプレイのロビー画面を管理する。
/// ルーム作成/参加、プレイヤーリスト表示、ゲーム開始を制御。
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject lobbyMainPanel;     // 作成/参加選択画面
    public GameObject roomPanel;          // ルーム内画面

    [Header("Lobby Main")]
    public TMP_InputField playerNameInput;
    public TMP_InputField roomCodeInput;
    public Button createRoomButton;
    public Button joinRoomButton;
    public Button backToTitleButton;

    [Header("Room")]
    public TextMeshProUGUI roomCodeText;
    public TextMeshProUGUI[] playerSlotTexts; // 4つのプレイヤー名表示
    public Button readyButton;
    public Button startGameButton;
    public Button leaveRoomButton;
    public TextMeshProUGUI statusText;

    [Header("Settings")]
    public TMP_Dropdown playerCountDropdown;
    public Slider mapSizeSlider;

    private bool isReady = false;

    void Start()
    {
        if (createRoomButton != null) createRoomButton.onClick.AddListener(OnCreateRoom);
        if (joinRoomButton != null) joinRoomButton.onClick.AddListener(OnJoinRoom);
        if (readyButton != null) readyButton.onClick.AddListener(OnToggleReady);
        if (startGameButton != null) startGameButton.onClick.AddListener(OnStartGame);
        if (leaveRoomButton != null) leaveRoomButton.onClick.AddListener(OnLeaveRoom);
        if (backToTitleButton != null) backToTitleButton.onClick.AddListener(OnBackToTitle);

        var net = NetworkManager.Instance;
        if (net != null)
        {
            net.OnRoomCreated += OnRoomCreated;
            net.OnRoomJoined += OnRoomJoined;
            net.OnPlayerListUpdated += OnPlayerListUpdated;
            net.OnGameStarted += OnGameStarted;
            net.OnError += OnError;
            net.OnDisconnected += OnDisconnected;
        }
    }

    void OnDestroy()
    {
        var net = NetworkManager.Instance;
        if (net == null) return;
        net.OnRoomCreated -= OnRoomCreated;
        net.OnRoomJoined -= OnRoomJoined;
        net.OnPlayerListUpdated -= OnPlayerListUpdated;
        net.OnGameStarted -= OnGameStarted;
        net.OnError -= OnError;
        net.OnDisconnected -= OnDisconnected;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        ShowLobbyMain();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void ShowLobbyMain()
    {
        if (lobbyMainPanel != null) lobbyMainPanel.SetActive(true);
        if (roomPanel != null) roomPanel.SetActive(false);
    }

    void ShowRoom()
    {
        if (lobbyMainPanel != null) lobbyMainPanel.SetActive(false);
        if (roomPanel != null) roomPanel.SetActive(true);
        if (startGameButton != null) startGameButton.gameObject.SetActive(NetworkManager.Instance.IsHost);
        if (readyButton != null) readyButton.gameObject.SetActive(!NetworkManager.Instance.IsHost);
        isReady = false;
    }

    // ===== ボタンハンドラ =====

    void OnCreateRoom()
    {
        string name = playerNameInput != null ? playerNameInput.text : "Host";
        if (string.IsNullOrWhiteSpace(name)) name = "Host";
        int maxPlayers = playerCountDropdown != null ? playerCountDropdown.value + 2 : 4;
        int mapRadius = mapSizeSlider != null ? (int)mapSizeSlider.value : 2;

        SetStatus("接続中...");
        NetworkManager.Instance.CreateRoom(name, maxPlayers, mapRadius);
    }

    void OnJoinRoom()
    {
        string code = roomCodeInput != null ? roomCodeInput.text.Trim().ToUpper() : "";
        if (code.Length != 4) { SetStatus("ルームコードは4文字です"); return; }

        string name = playerNameInput != null ? playerNameInput.text : "Player";
        if (string.IsNullOrWhiteSpace(name)) name = "Player";

        SetStatus("接続中...");
        NetworkManager.Instance.JoinRoom(code, name);
    }

    void OnToggleReady()
    {
        isReady = !isReady;
        NetworkManager.Instance.SetReady(isReady);
        if (readyButton != null)
        {
            var text = readyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = isReady ? "準備完了!" : "準備";
        }
    }

    void OnStartGame()
    {
        NetworkManager.Instance.StartGame();
    }

    void OnLeaveRoom()
    {
        NetworkManager.Instance.Disconnect();
        ShowLobbyMain();
        SetStatus("");
    }

    void OnBackToTitle()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsMultiplayer)
            NetworkManager.Instance.Disconnect();

        Hide();
        var menu = FindAnyObjectByType<MenuController>();
        if (menu != null) menu.ShowTitle();
    }

    // ===== ネットワークイベント =====

    void OnRoomCreated(string code)
    {
        if (roomCodeText != null) roomCodeText.text = $"ルームコード: {code}";
        SetStatus($"ルーム作成完了: {code}");
        ShowRoom();
    }

    void OnRoomJoined(int playerIndex)
    {
        if (roomCodeText != null) roomCodeText.text = $"ルームコード: {NetworkManager.Instance.RoomCode}";
        SetStatus("ルームに参加しました");
        ShowRoom();
    }

    void OnPlayerListUpdated(LobbyPlayer[] players)
    {
        for (int i = 0; i < 4; i++)
        {
            if (i < playerSlotTexts.Length && playerSlotTexts[i] != null)
            {
                if (i < players.Length)
                {
                    var p = players[i];
                    string readyMark = p.ready ? " ✓" : "";
                    string aiMark = p.isAI ? " [AI]" : "";
                    playerSlotTexts[i].text = $"P{i + 1}: {p.name}{aiMark}{readyMark}";
                }
                else
                {
                    playerSlotTexts[i].text = $"P{i + 1}: ---";
                }
            }
        }
    }

    void OnGameStarted(GameStartData data)
    {
        Hide();
        // NetworkBridge が OnGameStarted を受けてゲームを開始する
        // MenuController のHUDを表示
        var menu = FindAnyObjectByType<MenuController>();
        if (menu != null)
        {
            menu.titlePanel.SetActive(false);
            menu.settingsPanel.SetActive(false);
            menu.gameHUDPanel.SetActive(true);
            if (GameManager.Instance != null && GameManager.Instance.cameraController != null)
                GameManager.Instance.cameraController.enabled = true;
        }
    }

    void OnError(string message)
    {
        SetStatus($"エラー: {message}");
    }

    void OnDisconnected()
    {
        SetStatus("切断されました");
        ShowLobbyMain();
    }

    void SetStatus(string text)
    {
        if (statusText != null) statusText.text = text;
        Debug.Log($"[Lobby] {text}");
    }
}
