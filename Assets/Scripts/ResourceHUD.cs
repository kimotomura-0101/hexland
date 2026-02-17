using UnityEngine;
using TMPro;

public class ResourceHUD : MonoBehaviour
{
    [Header("Resource Texts")]
    public TextMeshProUGUI woodText;
    public TextMeshProUGUI brickText;
    public TextMeshProUGUI oreText;
    public TextMeshProUGUI wheatText;
    public TextMeshProUGUI sheepText;

    void Update()
    {
        // ゲームが開始されていない（プレイヤーがいない）場合は何もしない
        if (GameManager.Instance == null || GameManager.Instance.players == null || GameManager.Instance.players.Count == 0) return;

        // 常にPlayer1（リストの先頭）の情報を表示する
        string targetPlayer = GameManager.Instance.players[0].name;

        // 各資源の所持数を表示更新
        UpdateResourceText(woodText, targetPlayer, CatanMapGenerator.HexType.Wood);
        UpdateResourceText(brickText, targetPlayer, CatanMapGenerator.HexType.Brick);
        UpdateResourceText(oreText, targetPlayer, CatanMapGenerator.HexType.Ore);
        UpdateResourceText(wheatText, targetPlayer, CatanMapGenerator.HexType.Wheat);
        UpdateResourceText(sheepText, targetPlayer, CatanMapGenerator.HexType.Sheep);
    }

    public RectTransform GetTargetRect(CatanMapGenerator.HexType type)
    {
        switch (type)
        {
            case CatanMapGenerator.HexType.Wood: return woodText?.rectTransform;
            case CatanMapGenerator.HexType.Brick: return brickText?.rectTransform;
            case CatanMapGenerator.HexType.Ore: return oreText?.rectTransform;
            case CatanMapGenerator.HexType.Wheat: return wheatText?.rectTransform;
            case CatanMapGenerator.HexType.Sheep: return sheepText?.rectTransform;
            default: return null;
        }
    }

    void UpdateResourceText(TextMeshProUGUI textUI, string playerID, CatanMapGenerator.HexType type)
    {
        if (textUI != null)
        {
            int count = GameManager.Instance.GetResourceCount(playerID, type);
            textUI.text = count.ToString();
        }
    }
}
