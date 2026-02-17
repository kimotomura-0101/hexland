using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class PlayerNameSetup : MonoBehaviour
{
    [Header("References")]
    public TMP_Dropdown playerCountDropdown;
    public Transform tabContainer;        // タブボタンの親（HorizontalLayoutGroup）
    public Transform inputContainer;      // 名前入力欄の親

    [Header("Prefabs")]
    public GameObject tabButtonPrefab;     // タブボタン用プレハブ（Button + Text）
    public GameObject nameInputPrefab;     // TMP_InputField プレハブ

    [Header("Colors")]
    public Color activeTabColor = new Color(1f, 1f, 1f, 1f);
    public Color inactiveTabColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    private List<GameObject> tabButtons = new List<GameObject>();
    private List<TMP_InputField> nameInputs = new List<TMP_InputField>();
    private int selectedTab = 0;

    // プレイヤーカラー（GameManagerと同じ順）
    private readonly Color[] playerColors = { Color.red, Color.blue, Color.green, Color.yellow };
    private readonly string[] defaultNames = { "Player1", "CPU 1", "CPU 2", "CPU 3" };

    void Start()
    {
        if (playerCountDropdown != null)
        {
            playerCountDropdown.onValueChanged.AddListener(OnPlayerCountChanged);
            OnPlayerCountChanged(playerCountDropdown.value);
        }
    }

    void OnPlayerCountChanged(int dropdownValue)
    {
        int playerCount = dropdownValue + 2; // 0→2人, 1→3人, 2→4人
        RebuildTabs(playerCount);
    }

    void RebuildTabs(int count)
    {
        // 既存を削除
        foreach (var go in tabButtons) Destroy(go);
        foreach (var input in nameInputs) { if (input != null) Destroy(input.gameObject); }
        tabButtons.Clear();
        nameInputs.Clear();

        for (int i = 0; i < count; i++)
        {
            int index = i; // クロージャ用

            // タブボタン生成
            if (tabButtonPrefab != null && tabContainer != null)
            {
                GameObject tab = Instantiate(tabButtonPrefab, tabContainer);
                var text = tab.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = defaultNames[i];
                    Color c = (i < playerColors.Length) ? playerColors[i] : Color.white;
                    text.color = c;
                }
                var btn = tab.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(() => SelectTab(index));
                tabButtons.Add(tab);
            }

            // 名前入力フィールド生成
            if (nameInputPrefab != null && inputContainer != null)
            {
                GameObject inputGo = Instantiate(nameInputPrefab, inputContainer);
                var inputField = inputGo.GetComponent<TMP_InputField>();
                if (inputField != null)
                {
                    inputField.text = defaultNames[i];
                    int idx = index;
                    inputField.onValueChanged.AddListener((val) => OnNameChanged(idx, val));
                }
                nameInputs.Add(inputField);
            }
        }

        if (nameInputs.Count > 0 && tabButtons.Count > 0)
            SelectTab(0);
    }

    void SelectTab(int index)
    {
        selectedTab = index;

        // 全入力欄を非表示、選択中だけ表示
        for (int i = 0; i < nameInputs.Count; i++)
        {
            if (nameInputs[i] != null)
                nameInputs[i].gameObject.SetActive(i == index);
        }

        // タブの見た目を更新
        for (int i = 0; i < tabButtons.Count; i++)
        {
            var img = tabButtons[i].GetComponent<Image>();
            if (img != null)
                img.color = (i == index) ? activeTabColor : inactiveTabColor;
        }
    }

    void OnNameChanged(int index, string newName)
    {
        // タブのテキストも同期
        if (index < tabButtons.Count)
        {
            var text = tabButtons[index].GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = string.IsNullOrEmpty(newName) ? defaultNames[index] : newName;
        }
    }

    /// <summary>
    /// 設定された名前リストを返す（MenuControllerから呼ぶ）
    /// </summary>
    public List<string> GetPlayerNames()
    {
        var names = new List<string>();
        for (int i = 0; i < nameInputs.Count; i++)
        {
            string n = nameInputs[i].text;
            names.Add(string.IsNullOrWhiteSpace(n) ? defaultNames[i] : n);
        }
        return names;
    }
}
