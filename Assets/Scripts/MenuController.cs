using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class MenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject titlePanel;       // タイトル画面
    public GameObject settingsPanel;    // 設定画面
    public GameObject gameHUDPanel;     // ゲーム中のUI（建設ボタンなど）

    [Header("Background")]
    public MenuBackgroundGenerator backgroundGenerator;

    [Header("Settings Inputs")]
    public TMP_Dropdown playerCountDropdown; // プレイヤー人数選択 (2, 3, 4人...)
    public Slider mapSizeSlider;             // マップサイズ選択
    public TextMeshProUGUI mapSizeValueText; // サイズの数値を表示するテキスト
    public PlayerNameSetup playerNameSetup;  // プレイヤー名設定パネル

    void Start()
    {
        // 初期状態: タイトルのみ表示
        ShowTitle();
        
        // マップサイズスライダーの初期値表示更新
        if (mapSizeSlider != null)
        {
            UpdateMapSizeText(mapSizeSlider.value);
            mapSizeSlider.onValueChanged.AddListener(UpdateMapSizeText);
        }

        // UIが3Dオブジェクトに埋もれないように、CanvasをScreen Space - Overlayに設定
        GameObject[] panels = { titlePanel, settingsPanel, gameHUDPanel };
        foreach (var panel in panels)
        {
            if (panel != null)
            {
                Canvas canvas = panel.GetComponentInParent<Canvas>();
                if (canvas != null) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }
    }

    // タイトル画面を表示
    public void ShowTitle()
    {
        titlePanel.SetActive(true);
        settingsPanel.SetActive(false);
        gameHUDPanel.SetActive(false);

        // 勝利パネルが表示されていれば非表示にする
        if (GameManager.Instance != null && GameManager.Instance.victoryScreen != null
            && GameManager.Instance.victoryScreen.victoryPanel != null)
        {
            GameManager.Instance.victoryScreen.victoryPanel.SetActive(false);
        }

        if (GameManager.Instance != null && GameManager.Instance.cameraController != null)
        {
            GameManager.Instance.cameraController.enabled = false;
        }
    }

    // 設定画面を表示
    public void ShowSettings()
    {
        titlePanel.SetActive(false);
        settingsPanel.SetActive(true);
        gameHUDPanel.SetActive(false);

        if (GameManager.Instance != null && GameManager.Instance.cameraController != null)
        {
            GameManager.Instance.cameraController.enabled = false;
        }
    }

    // スライダーの値が変わった時にテキストを更新
    void UpdateMapSizeText(float value)
    {
        if (mapSizeValueText != null)
        {
            mapSizeValueText.text = $"Map Radius: {value}";
        }
    }

    // Playボタンが押された時の処理
    public void OnPlayButton()
    {
        // 設定値の取得
        // Dropdownのオプションが "2 Players", "3 Players", "4 Players" の順だと仮定
        int playerCount = playerCountDropdown.value + 2; 
        int mapRadius = (int)mapSizeSlider.value;

        // UIをゲームモードに切り替え
        titlePanel.SetActive(false);
        settingsPanel.SetActive(false);
        gameHUDPanel.SetActive(true);

        if (GameManager.Instance != null && GameManager.Instance.cameraController != null)
        {
            GameManager.Instance.cameraController.enabled = true;
        }

        // プレイヤー名を取得してゲーム開始
        var names = playerNameSetup != null ? playerNameSetup.GetPlayerNames() : null;
        GameManager.Instance.StartNewGame(playerCount, mapRadius, names);
    }

    // ゲーム終了（タイトルに戻る場合など）
    public void OnBackToTitle()
    {
        ShowTitle();
    }

    // 背景タイル生成ボタン用
    public void OnGenerateBackground()
    {
        if (backgroundGenerator != null) backgroundGenerator.Generate();
    }

    // 背景タイル削除ボタン用
    public void OnClearBackground()
    {
        if (backgroundGenerator != null) backgroundGenerator.Clear();
    }
}
