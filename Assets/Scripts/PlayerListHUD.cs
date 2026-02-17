using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class PlayerListHUD : MonoBehaviour
{
    [Header("UI References")]
    public GameObject playerInfoPrefab; // テキストを含むプレハブ
    public Transform listContainer;     // VerticalLayoutGroupなどを持つ親オブジェクト

    private List<GameObject> spawnedObjects = new List<GameObject>();
    private Dictionary<string, RectTransform> playerRects = new Dictionary<string, RectTransform>();

    void Update()
    {
        if (GameManager.Instance == null) return;
        
        var players = GameManager.Instance.players;
        if (players == null || players.Count == 0) return;

        // Player1を除外するため、必要なUI数は (プレイヤー数 - 1)
        int targetCount = Mathf.Max(0, players.Count - 1);
        if (spawnedObjects.Count != targetCount)
        {
            RebuildList(players);
        }

        // 毎フレーム情報を更新
        UpdateList(players);
    }

    void RebuildList(List<GameManager.PlayerData> players)
    {
        // 既存の削除
        foreach (var obj in spawnedObjects)
        {
            Destroy(obj);
        }
        spawnedObjects.Clear();

        if (playerInfoPrefab == null || listContainer == null) return;

        playerRects.Clear();

        // 新規生成
        // Player1 (index 0) はスキップして index 1 から開始
        for (int i = 1; i < players.Count; i++)
        {
            GameObject newObj = Instantiate(playerInfoPrefab, listContainer);
            spawnedObjects.Add(newObj);
            playerRects[players[i].name] = newObj.GetComponent<RectTransform>();
        }
    }

    void UpdateList(List<GameManager.PlayerData> players)
    {
        for (int i = 1; i < players.Count; i++)
        {
            int uiIndex = i - 1;
            if (uiIndex >= spawnedObjects.Count) break;

            var p = players[i];
            var obj = spawnedObjects[uiIndex];
            
            // 資材合計
            int total = p.wood + p.brick + p.ore + p.wheat + p.sheep;
            int cardCount = p.heldCards.Count;

            // テキスト更新
            TextMeshProUGUI text = obj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                // ターンプレイヤーなら矢印を表示
                string marker = (p.name == GameManager.Instance.CurrentPlayer) ? "▶ " : "   ";
                int vp = GameManager.Instance.GetVictoryPoints(p.name);
                text.text = $"{marker}{p.name}: VP{vp} / 資材{total} / カード{cardCount}";
                text.color = p.color;
            }
        }
    }

    public RectTransform GetTargetRect(string playerID)
    {
        playerRects.TryGetValue(playerID, out RectTransform rect);
        return rect;
    }
}
