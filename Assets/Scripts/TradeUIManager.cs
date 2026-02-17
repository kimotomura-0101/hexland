using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class TradeUIManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject tradePanel;
    public Button openTradeButton; // 画面上の「交易」ボタン
    public Button closeButton;
    public Button confirmTradeButton;
    public TextMeshProUGUI rateText; // "4 : 1" などを表示
    public TextMeshProUGUI messageText; // エラーメッセージなど

    [System.Serializable]
    public class ResourceButton
    {
        public CatanMapGenerator.HexType type;
        public Button button;
        public Image selectionOutline; // 選択中であることを示す枠など
    }

    [Header("Resource Buttons")]
    public List<ResourceButton> giveButtons; // 渡す側のボタンリスト
    public List<ResourceButton> getButtons;  // もらう側のボタンリスト

    // 内部状態
    private CatanMapGenerator.HexType selectedGive = CatanMapGenerator.HexType.Desert; // Desertを未選択扱いとする
    private CatanMapGenerator.HexType selectedGet = CatanMapGenerator.HexType.Desert;

    void Start()
    {
        // パネル初期化
        if (tradePanel != null) tradePanel.SetActive(false);

        // UIが3Dオブジェクトの手前に来るようにCanvas設定をOverlayにする
        if (tradePanel != null)
        {
            Canvas canvas = tradePanel.GetComponentInParent<Canvas>();
            if (canvas != null) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        // ボタンイベント登録
        if (openTradeButton != null)
        {
            openTradeButton.onClick = new Button.ButtonClickedEvent(); // 既存の設定をクリア
            openTradeButton.onClick.AddListener(TogglePanel);
        }
        if (closeButton != null)
        {
            closeButton.onClick = new Button.ButtonClickedEvent(); // 既存の設定をクリア
            closeButton.onClick.AddListener(ClosePanel);
        }
        if (confirmTradeButton != null)
        {
            confirmTradeButton.onClick = new Button.ButtonClickedEvent(); // 既存の設定をクリア
            confirmTradeButton.onClick.AddListener(OnConfirmTrade);
        }

        // 資源ボタンのイベント登録
        foreach (var btn in giveButtons)
        {
            btn.button.onClick.AddListener(() => OnGiveSelected(btn.type));
            if (btn.selectionOutline != null)
            {
                btn.selectionOutline.enabled = false;
                btn.selectionOutline.transform.SetAsFirstSibling(); // 文字などの下に表示されるように順序を変更
            }
        }
        foreach (var btn in getButtons)
        {
            btn.button.onClick.AddListener(() => OnGetSelected(btn.type));
            if (btn.selectionOutline != null)
            {
                btn.selectionOutline.enabled = false;
                btn.selectionOutline.transform.SetAsFirstSibling(); // 文字などの下に表示されるように順序を変更
            }
        }
    }

    void Update()
    {
        if (GameManager.Instance == null) return;

        // 自分のターン中のみ交易ボタンを表示
        if (openTradeButton != null)
        {
            bool isPlaying = GameManager.Instance.currentPhase == GameManager.GamePhase.Playing;
            openTradeButton.gameObject.SetActive(isPlaying);

            // 取引タブの開閉はいつでも可能にする
            openTradeButton.interactable = isPlaying && !GameManager.Instance.isExploreMode;
        }

        if (tradePanel.activeSelf)
        {
            UpdateUIState();
        }
    }

    public void TogglePanel()
    {
        if (tradePanel != null && tradePanel.activeSelf)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }

    public void OpenPanel()
    {
        tradePanel.SetActive(true);
        ResetSelection();
    }

    public void ClosePanel()
    {
        tradePanel.SetActive(false);
    }

    void ResetSelection()
    {
        selectedGive = CatanMapGenerator.HexType.Desert;
        selectedGet = CatanMapGenerator.HexType.Desert;
        UpdateUIState();
    }

    void OnGiveSelected(CatanMapGenerator.HexType type)
    {
        // 既に選択されている場合は解除（トグル）
        if (selectedGive == type)
        {
            selectedGive = CatanMapGenerator.HexType.Desert;
        }
        else
        {
            selectedGive = type;
        }
        UpdateUIState();
    }

    void OnGetSelected(CatanMapGenerator.HexType type)
    {
        if (selectedGet == type)
        {
            selectedGet = CatanMapGenerator.HexType.Desert;
        }
        else
        {
            selectedGet = type;
        }
        UpdateUIState();
    }

    void UpdateUIState()
    {
        string currentPlayer = GameManager.Instance.CurrentPlayer;
        
        // 取引実行の条件: ダイスロール後かつ、特殊アクション中でない
        bool hasRolled = GameManager.Instance.hasRolledDice;
        bool isBusy = GameManager.Instance.currentStep == GameManager.TurnStep.MoveRobber || 
                      GameManager.Instance.currentStep == GameManager.TurnStep.RoadBuildingCard ||
                      GameManager.Instance.currentStep == GameManager.TurnStep.Monopoly;
        bool canTradePhase = hasRolled && !isBusy;

        // 1. ボタンのハイライト更新
        foreach (var btn in giveButtons)
        {
            if (btn.selectionOutline != null) 
                btn.selectionOutline.enabled = (btn.type == selectedGive);
        }
        foreach (var btn in getButtons)
        {
            if (btn.selectionOutline != null) 
                btn.selectionOutline.enabled = (btn.type == selectedGet);
        }

        // 2. レート計算と表示
        if (selectedGive != CatanMapGenerator.HexType.Desert)
        {
            int cost = GameManager.Instance.GetTradeCost(currentPlayer, selectedGive);
            int currentAmount = GameManager.Instance.GetResourceCount(currentPlayer, selectedGive);
            
            rateText.text = $"{cost} : 1";

            // 3. 取引可能か判定
            bool canAfford = currentAmount >= cost;
            bool isGetSelected = selectedGet != CatanMapGenerator.HexType.Desert;
            bool isDifferent = selectedGive != selectedGet;

            confirmTradeButton.interactable = canAfford && isGetSelected && isDifferent && canTradePhase;

            if (!canTradePhase)
            {
                messageText.text = "現在は取引できません";
            }
            else if (!isGetSelected)
            {
                messageText.text = "欲しい資源を選んでください";
            }
            else if (!isDifferent)
            {
                messageText.text = "同じ資源は交換できません";
            }
            else if (!canAfford)
            {
                messageText.text = $"資源が足りません (所持: {currentAmount})";
            }
            else
            {
                messageText.text = "取引可能";
            }
        }
        else
        {
            rateText.text = "- : -";
            confirmTradeButton.interactable = false;
            messageText.text = "渡す資源を選んでください";
        }
    }

    void OnConfirmTrade()
    {
        if (selectedGive == CatanMapGenerator.HexType.Desert || selectedGet == CatanMapGenerator.HexType.Desert) return;

        string currentPlayer = GameManager.Instance.CurrentPlayer;
        int cost = GameManager.Instance.GetTradeCost(currentPlayer, selectedGive);

        if (GameManager.Instance.GetResourceCount(currentPlayer, selectedGive) >= cost)
        {
            GameManager.Instance.ExecuteTrade(currentPlayer, selectedGive, selectedGet, cost);
            ResetSelection();
            // ClosePanel(); // 連続で取引したい場合もあるので閉じない方が便利かも
        }
    }
}
