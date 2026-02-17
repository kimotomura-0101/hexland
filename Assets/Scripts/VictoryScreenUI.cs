using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class VictoryScreenUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject victoryPanel;
    public TextMeshProUGUI winnerText;
    public Transform scoreContainer;
    public GameObject scoreRowPrefab;
    public Button backToTitleButton;

    [Header("Colors")]
    public Color winnerRowColor = new Color(1f, 0.9f, 0.3f, 0.3f);
    public Color normalRowColor = new Color(1f, 1f, 1f, 0.1f);

    private List<GameObject> spawnedRows = new List<GameObject>();

    void Start()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);

        if (backToTitleButton != null)
        {
            backToTitleButton.onClick.AddListener(OnBackToTitle);
        }
    }

    public void Show(string winnerName)
    {
        if (victoryPanel == null) return;
        victoryPanel.SetActive(true);

        // 勝者テキスト
        if (winnerText != null)
        {
            winnerText.text = $"{winnerName} の勝利！";
            var winnerData = GameManager.Instance.players.Find(p => p.name == winnerName);
            if (winnerData != null) winnerText.color = winnerData.color;
        }

        // 既存の行を削除
        foreach (var row in spawnedRows)
            if (row != null) Destroy(row);
        spawnedRows.Clear();

        // ヘッダー行
        if (scoreRowPrefab != null && scoreContainer != null)
        {
            SpawnRow("プレイヤー", "開拓地", "都市", "最長路", "騎士力", "VPカード", "合計", Color.white, normalRowColor);

            // 各プレイヤー行
            var gm = GameManager.Instance;
            foreach (var p in gm.players)
            {
                int settlements = gm.GetSettlementCount(p.name);
                int cities = gm.GetCityCount(p.name);
                string longestRoad = (gm.LongestRoadPlayer == p.name) ? $"✓ ({gm.LongestRoadLength})" : "-";
                string largestArmy = (gm.LargestArmyPlayer == p.name) ? $"✓ ({gm.LargestArmyCount})" : $"({p.usedKnights})";
                int vpCards = gm.GetVPCardCount(p.name);
                int totalVP = gm.GetVictoryPoints(p.name);

                Color rowBg = (p.name == winnerName) ? winnerRowColor : normalRowColor;
                SpawnRow(p.name, settlements.ToString(), cities.ToString(), longestRoad, largestArmy, vpCards.ToString(), totalVP.ToString(), p.color, rowBg);
            }
        }
    }

    void SpawnRow(string col1, string col2, string col3, string col4, string col5, string col6, string col7, Color textColor, Color bgColor)
    {
        GameObject row = Instantiate(scoreRowPrefab, scoreContainer);
        spawnedRows.Add(row);

        // 背景色設定
        var bg = row.GetComponent<Image>();
        if (bg != null) bg.color = bgColor;

        // テキスト設定（子オブジェクトのTextMeshProUGUIを順番に取得）
        var texts = row.GetComponentsInChildren<TextMeshProUGUI>();
        string[] values = { col1, col2, col3, col4, col5, col6, col7 };

        for (int i = 0; i < texts.Length && i < values.Length; i++)
        {
            texts[i].text = values[i];
            texts[i].color = textColor;
        }
    }

    void OnBackToTitle()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);

        var menuController = FindObjectOfType<MenuController>();
        if (menuController != null)
        {
            menuController.OnBackToTitle();
        }
    }
}
