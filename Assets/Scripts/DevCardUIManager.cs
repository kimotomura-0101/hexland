using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class DevCardUIManager : MonoBehaviour
{
    [System.Serializable]
    public struct CardDesign
    {
        public GameManager.DevCardType type;
        public Sprite sprite;
    }

    [Header("UI References")]
    public Transform cardPanel; // カードを追加する親パネル（HorizontalLayoutGroupなどを推奨）
    public GameObject cardPrefab; // カードのプレハブ（ButtonとImageを持つ）
    
    [Header("Card Designs")]
    public List<CardDesign> cardDesigns; // 各カードタイプのデザイン設定

    private List<GameObject> spawnedCards = new List<GameObject>();
    private List<GameManager.DevCardType> lastFrameCards = new List<GameManager.DevCardType>();

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.players.Count == 0) return;

        // Player1 (index 0) のカード情報を監視
        var p1 = GameManager.Instance.players[0];
        
        // リストに変更があった場合のみ再生成
        if (IsCardListChanged(p1.heldCards))
        {
            RebuildList(p1);
        }
    }

    bool IsCardListChanged(List<GameManager.DevCardType> currentCards)
    {
        if (currentCards.Count != lastFrameCards.Count) return true;
        for (int i = 0; i < currentCards.Count; i++)
        {
            if (currentCards[i] != lastFrameCards[i]) return true;
        }
        return false;
    }

    void RebuildList(GameManager.PlayerData player)
    {
        // キャッシュ更新
        lastFrameCards = new List<GameManager.DevCardType>(player.heldCards);

        // 既存のカードUIを削除
        foreach (var obj in spawnedCards) Destroy(obj);
        spawnedCards.Clear();

        if (cardPrefab == null || cardPanel == null) return;

        // 新しいリストに基づいて生成
        for (int i = 0; i < player.heldCards.Count; i++)
        {
            var cardType = player.heldCards[i];
            GameObject newCard = Instantiate(cardPrefab, cardPanel);
            spawnedCards.Add(newCard);

            // 画像設定
            Image img = newCard.GetComponent<Image>();
            if (img != null)
            {
                var design = cardDesigns.Find(d => d.type == cardType);
                if (design.sprite != null) img.sprite = design.sprite;
            }

            // ボタン設定
            Button btn = newCard.GetComponent<Button>();
            if (btn != null)
            {
                int index = i; // クロージャ用
                btn.onClick.AddListener(() => OnCardClicked(index));
            }
        }
    }

    void OnCardClicked(int index)
    {
        // Player1 (index 0) としてカードを使用
        GameManager.Instance.UseDevCard(0, index);
    }
}
