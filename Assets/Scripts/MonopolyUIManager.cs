using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MonopolyUIManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI infoText;

    [Header("Resource Buttons")]
    public Button woodButton;
    public Button brickButton;
    public Button oreButton;
    public Button wheatButton;
    public Button sheepButton;

    private GameManager.TurnStep lastStep;
    private string lastPlayer;

    void Start()
    {
        // ボタンイベント登録
        if (woodButton != null) woodButton.onClick.AddListener(() => OnResourceSelected(CatanMapGenerator.HexType.Wood));
        if (brickButton != null) brickButton.onClick.AddListener(() => OnResourceSelected(CatanMapGenerator.HexType.Brick));
        if (oreButton != null) oreButton.onClick.AddListener(() => OnResourceSelected(CatanMapGenerator.HexType.Ore));
        if (wheatButton != null) wheatButton.onClick.AddListener(() => OnResourceSelected(CatanMapGenerator.HexType.Wheat));
        if (sheepButton != null) sheepButton.onClick.AddListener(() => OnResourceSelected(CatanMapGenerator.HexType.Sheep));
    }

    void Update()
    {
        if (GameManager.Instance == null) return;

        var currentStep = GameManager.Instance.currentStep;
        var currentPlayer = GameManager.Instance.CurrentPlayer;

        if (currentStep == GameManager.TurnStep.Monopoly)
        {
            if (infoText != null)
            {
                if (currentStep != lastStep || currentPlayer != lastPlayer)
                {
                    infoText.text = $"{currentPlayer}: 上の素材一覧から独占する素材を選択してください";
                }
            }
        }

        lastStep = currentStep;
        lastPlayer = currentPlayer;
    }

    void OnResourceSelected(CatanMapGenerator.HexType type)
    {
        GameManager.Instance.ExecuteMonopoly(type);
    }
}
