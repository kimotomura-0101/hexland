using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public CatanMapGenerator mapGenerator;
    public CameraController cameraController;
    public DiceController diceController; // Inspectorで割り当てる

    public enum GamePhase { Setup1, Setup2, Playing }
    public enum TurnStep { PlaceSettlement, PlaceRoad, Waiting, MoveRobber, RoadBuildingCard, Monopoly }
    public enum DevCardType { Knight, VictoryPoint, RoadBuilding, Monopoly }

    [Header("Victory")]
    public int victoryPointsToWin = 10;

    [Header("Game State")]
    public GamePhase currentPhase = GamePhase.Setup1; // StartNewGameでリセットされます
    public TurnStep currentStep = TurnStep.PlaceSettlement;
    
    [System.Serializable]
    public class PlayerData
    {
        public string name;
        public Color color;
        public int wood;
        public int brick;
        public int ore;
        public int wheat;
        public int sheep;
        public List<DevCardType> heldCards = new List<DevCardType>();
        public bool isAI = false;
        public int usedKnights = 0;
    }

    public List<PlayerData> players = new List<PlayerData>();

    public int currentPlayerIndex = 0;

    public Color GetPlayerColor(string playerID)
    {
        var p = players.Find(x => x.name == playerID);
        if (p != null) return p.color;
        return Color.white;
    }

    // 初期配置で、家を建てた直後にその家に道を繋げるための参照
    public VertexPoint lastBuiltSettlement;

    public string CurrentPlayer => players[currentPlayerIndex].name;
    public bool IsSetupPhase => currentPhase == GamePhase.Setup1 || currentPhase == GamePhase.Setup2;

    public bool IsCurrentPlayerAI()
    {
        if (currentPlayerIndex < 0 || currentPlayerIndex >= players.Count) return false;
        return players[currentPlayerIndex].isAI;
    }

    /// <summary>
    /// ローカルプレイヤーのターンかどうか。マルチプレイ時はリモートプレイヤーのターンをブロックする。
    /// </summary>
    public bool IsLocalPlayerTurn()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsMultiplayer)
            return currentPlayerIndex == NetworkManager.Instance.LocalPlayerIndex;
        return !IsCurrentPlayerAI();
    }

    // 建設モードフラグ
    public bool isConstructionMode = false;

    [Header("Player Prefabs (Order: Player1, Player2, Player3, Player4)")]
    public List<GameObject> settlementPrefabs;
    public List<GameObject> cityPrefabs;
    public List<GameObject> roadPrefabs;

    public GameObject GetSettlementPrefab(string playerID)
    {
        int index = players.FindIndex(x => x.name == playerID);
        if (index >= 0 && settlementPrefabs != null && settlementPrefabs.Count > 0)
            return settlementPrefabs[index % settlementPrefabs.Count];
        return null;
    }

    public GameObject GetCityPrefab(string playerID)
    {
        int index = players.FindIndex(x => x.name == playerID);
        if (index >= 0 && cityPrefabs != null && cityPrefabs.Count > 0)
            return cityPrefabs[index % cityPrefabs.Count];
        return null;
    }

    public GameObject GetRoadPrefab(string playerID)
    {
        int index = players.FindIndex(x => x.name == playerID);
        if (index >= 0 && roadPrefabs != null && roadPrefabs.Count > 0)
            return roadPrefabs[index % roadPrefabs.Count];
        return null;
    }

    public GameObject GetCharacterPrefab(string playerID)
    {
        int index = players.FindIndex(x => x.name == playerID);
        if (index >= 0 && characterPrefabs != null && characterPrefabs.Count > 0)
            return characterPrefabs[index % characterPrefabs.Count];
        return null;
    }

    [Header("HUD References")]
    public ResourceHUD resourceHUD;
    public PlayerListHUD playerListHUD;

    [Header("UI")]
    public Button constructionButton;
    public TextMeshProUGUI constructionButtonText;
    public Button rollDiceButton;
    public Button buyCardButton;
    public Button endTurnButton;
    public TextMeshProUGUI gameInfoText;
    
    [Header("Explore Mode")]
    public Button exploreButton; // 探索開始ボタン
    public Button exitExploreButton; // 探索終了ボタン
    public List<GameObject> characterPrefabs; // キャラクターのプレハブ

    public bool hasRolledDice = false;
    public bool isDiceRolling = false;

    [Header("Victory")]
    public VictoryScreenUI victoryScreen;
    public bool isGameOver = false;
    private string longestRoadPlayer = null;
    private int longestRoadLength = 0;
    private string largestArmyPlayer = null;
    private int largestArmyCount = 0;

    [Header("Robber")]
    public GameObject robberObject;
    public HexTileData currentRobberTile;
    public int roadBuildingCardCount = 0;

    // 探索モード用変数
    public bool isExploreMode = false;
    private GameObject currentCharacter;
    private Vector3 lastCameraPosition;
    private Quaternion lastCameraRotation;

    private List<DevCardType> devCardDeck = new List<DevCardType>();

    public void ToggleConstructionMode()
    {
        isConstructionMode = !isConstructionMode;
        if (constructionButtonText != null)
        {
            constructionButtonText.text = isConstructionMode ? "建設モード終了" : "建設モード";
        }
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // メニューから呼ばれるゲーム開始処理
    public void StartNewGame(int playerCount, int mapRadius, List<string> playerNames = null, int seed = -1)
    {
        // マルチプレイ: シード指定でRNG初期化 → 全員同一マップ生成
        if (seed >= 0) UnityEngine.Random.InitState(seed);

        // 1. プレイヤーデータの初期化
        players.Clear();
        Color[] presetColors = { Color.red, Color.blue, Color.green, Color.yellow };

        for (int i = 0; i < playerCount; i++)
        {
            Color pColor = (i < presetColors.Length) ? presetColors[i] : Random.ColorHSV();
            string pName = (playerNames != null && i < playerNames.Count && !string.IsNullOrWhiteSpace(playerNames[i]))
                ? playerNames[i]
                : (i == 0 ? "Player1" : $"CPU {i}");
            players.Add(new PlayerData
            {
                name = pName,
                color = pColor,
                isAI = (i >= 1) // Player1以外はAI
            });
        }

        // 2. ゲーム状態のリセット
        currentPlayerIndex = 0;
        currentPhase = GamePhase.Setup1;
        currentStep = TurnStep.PlaceSettlement;
        lastBuiltSettlement = null;
        hasRolledDice = false;
        isDiceRolling = false;
        isGameOver = false;
        longestRoadPlayer = null;
        longestRoadLength = 0;
        largestArmyPlayer = null;
        largestArmyCount = 0;
        isConstructionMode = false;
        robberObject = null;
        currentRobberTile = null;
        InitializeDeck();
        
        // 探索モードリセット
        isExploreMode = false;
        if (exitExploreButton != null) exitExploreButton.gameObject.SetActive(false);

        // 3. マップ生成設定と実行
        if (mapGenerator != null)
        {
            mapGenerator.mapRadius = mapRadius;
            mapGenerator.GenerateMap();

            // カメラ位置の自動調整
            if (cameraController != null)
            {
                // マップの幅を概算: (直径のヘックス数) * ヘックスの幅
                // ヘックスの幅(対辺距離) = sqrt(3) * 半径(hexSize)
                float hexWidth = Mathf.Sqrt(3) * mapGenerator.CurrentHexSize;
                float mapWidth = (2 * mapRadius + 1) * hexWidth;

                // 画面に収まる距離を計算 (係数1.5は経験則。45度で見下ろすため少し余裕を持たせる)
                float targetDistance = mapWidth * 1.5f;

                // 必要なら最大距離を拡張
                if (targetDistance > cameraController.maxDistance) cameraController.maxDistance = targetDistance * 1.2f;
                cameraController.distance = targetDistance;
            }
        }

        Debug.Log($"ゲーム開始: {playerCount}人, 半径{mapRadius}");
        Debug.Log($"最初は {CurrentPlayer} の番です（家を配置）");
        UpdateGameInfoText();
    }

    void Update()
    {
        // スペースキーでダイスを振るテスト (Playingフェーズのみ)
        if (currentPhase == GamePhase.Playing && !hasRolledDice && !isExploreMode && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            OnClickRollDice();
        }

        // 特殊アクション中（盗賊移動、街道建設カード）かどうか
        bool isBusy = currentStep == TurnStep.MoveRobber || currentStep == TurnStep.RoadBuildingCard || currentStep == TurnStep.Monopoly;
        bool canInteract = !isExploreMode && !isBusy && !IsCurrentPlayerAI();

        // ボタンの表示制御（プレイ中のみ表示）
        if (constructionButton != null)
        {
            constructionButton.gameObject.SetActive(currentPhase == GamePhase.Playing);
            constructionButton.interactable = hasRolledDice && !isDiceRolling && canInteract; // ダイスを振った後のみ建設可能
        }
        
        if (rollDiceButton != null)
        {
            rollDiceButton.gameObject.SetActive(currentPhase == GamePhase.Playing);
            rollDiceButton.interactable = !hasRolledDice && !isDiceRolling && canInteract; // ダイスを振る前のみ有効
        }

        if (buyCardButton != null)
        {
            buyCardButton.gameObject.SetActive(currentPhase == GamePhase.Playing);
            // 鉄1, 麦1, 羊1が必要
            bool canAfford = GetResourceCount(CurrentPlayer, CatanMapGenerator.HexType.Ore) >= 1 &&
                             GetResourceCount(CurrentPlayer, CatanMapGenerator.HexType.Wheat) >= 1 &&
                             GetResourceCount(CurrentPlayer, CatanMapGenerator.HexType.Sheep) >= 1;
            buyCardButton.interactable = hasRolledDice && !isDiceRolling && canAfford && devCardDeck.Count > 0 && canInteract;
        }

        if (endTurnButton != null)
        {
            endTurnButton.gameObject.SetActive(currentPhase == GamePhase.Playing);
            endTurnButton.interactable = hasRolledDice && !isDiceRolling && canInteract; // ダイスを振った後のみ有効
        }

        if (exploreButton != null)
        {
            exploreButton.gameObject.SetActive(currentPhase == GamePhase.Playing && !isExploreMode);
            exploreButton.interactable = !isBusy;
        }

        // 盗賊移動モード時のクリック判定（ハイライトをクリック）
        if (currentStep == TurnStep.MoveRobber && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // クリックしたオブジェクトがハイライト（またはその親）で、HexTileDataを持っているか確認
                HexTileData tile = hit.collider.GetComponentInParent<HexTileData>();
                
                // ハイライトオブジェクト自体をクリックしたかどうかの確認
                // (highlightObjectがアクティブな時だけ反応させたい場合など)
                if (tile != null && tile.highlightObject != null && tile.highlightObject.activeSelf)
                {
                    OnHexClicked(tile);
                }
            }
        }
    }

    // 探索モード開始
    public void StartExploreMode()
    {
        if (isExploreMode) return;
        isExploreMode = true;

        // 現在のカメラ位置保存
        if (Camera.main != null)
        {
            lastCameraPosition = Camera.main.transform.position;
            lastCameraRotation = Camera.main.transform.rotation;
            
            // CameraControllerを一時停止
            if (cameraController != null) cameraController.enabled = false;
        }

        // 既存のキャラクターがいれば再利用（位置を維持するため）
        if (currentCharacter != null)
        {
            currentCharacter.SetActive(true);
        }
        else
        {
        // キャラクター生成（真ん中の砂漠タイルの上）
        Vector3 spawnPos = Vector3.up * 5;
        if (mapGenerator != null)
        {
            var tiles = mapGenerator.GetAllTiles();
            // 砂漠タイルを探す
            var desertTile = tiles.Find(t => t.resourceType == CatanMapGenerator.HexType.Desert);
            
            if (desertTile != null)
            {
                spawnPos = desertTile.transform.position + Vector3.up * 10;
            }
            else
            {
                // 砂漠が見つからない場合のフォールバック（ランダムな土地）
                var landTiles = tiles.FindAll(t => t.resourceType != CatanMapGenerator.HexType.Beach);
                if (landTiles.Count > 0)
                {
                    var tile = landTiles[Random.Range(0, landTiles.Count)];
                    spawnPos = tile.transform.position + Vector3.up * 10;
                }
            }
        }

        GameObject prefab = GetCharacterPrefab(CurrentPlayer);
        if (prefab != null)
        {
            currentCharacter = Instantiate(prefab, spawnPos, Quaternion.identity);
            // コントローラーがついていなければ追加
            var controller = currentCharacter.GetComponent<ExploreController>();
            if (controller == null) controller = currentCharacter.AddComponent<ExploreController>();
        }
        }

        // UI切り替え
        if (exploreButton != null) exploreButton.gameObject.SetActive(false);
        if (exitExploreButton != null) exitExploreButton.gameObject.SetActive(true);
    }

    // 探索モード終了
    public void EndExploreMode()
    {
        if (!isExploreMode) return;
        isExploreMode = false;

        // キャラクターを削除せず非表示にする（位置を保持するため）
        if (currentCharacter != null) currentCharacter.SetActive(false);

        // カメラ復帰
        if (Camera.main != null)
        {
            Camera.main.transform.position = lastCameraPosition;
            Camera.main.transform.rotation = lastCameraRotation;
            if (cameraController != null) cameraController.enabled = true;
        }

        // UI切り替え
        if (exploreButton != null) exploreButton.gameObject.SetActive(true);
        if (exitExploreButton != null) exitExploreButton.gameObject.SetActive(false);
    }

    public void OnSettlementBuilt(VertexPoint vertex)
    {
        if (IsSetupPhase)
        {
            // 初期配置2巡目の場合、隣接するタイルから資源を獲得
            if (currentPhase == GamePhase.Setup2)
            {
                foreach (var tile in vertex.adjacentTiles)
                {
                    if (tile.resourceType != CatanMapGenerator.HexType.Desert)
                    {
                        Debug.Log($"<color=green>初期資源獲得: {CurrentPlayer} gets {tile.resourceType}</color>");
                        AddResource(CurrentPlayer, tile.resourceType, 1);
                    }
                }
            }

            lastBuiltSettlement = vertex;
            currentStep = TurnStep.PlaceRoad;
            Debug.Log($"{CurrentPlayer} は道を配置してください。");
            UpdateGameInfoText();
        }
        else
        {
            // 通常フェーズ: 建設してもターンは終わらない
            CheckVictory();
        }
    }

    public void OnRoadBuilt(EdgePoint edge)
    {
        // 最長交易路の更新チェック
        UpdateLongestRoad();

        // 街道建設カードモード中の処理
        if (currentPhase == GamePhase.Playing && currentStep == TurnStep.RoadBuildingCard)
        {
            roadBuildingCardCount--;
            if (roadBuildingCardCount <= 0)
            {
                currentStep = TurnStep.Waiting;
                isConstructionMode = false;
                if (constructionButtonText != null) constructionButtonText.text = "建設モード";
                Debug.Log("街道建設カードの効果終了");
            }
            UpdateGameInfoText();
            return;
        }

        if (currentPhase == GamePhase.Setup1)
        {
            // 次のプレイヤーへ
            currentPlayerIndex++;
            if (currentPlayerIndex >= players.Count)
            {
                // 1巡目終了、2巡目へ（最後のプレイヤーがそのまま2巡目の最初を行う）
                currentPhase = GamePhase.Setup2;
                currentPlayerIndex = players.Count - 1;
                Debug.Log("初期配置2巡目開始（逆順）");
            }
            currentStep = TurnStep.PlaceSettlement;
            lastBuiltSettlement = null;
            Debug.Log($"次は {CurrentPlayer} の番です（家を配置）");
        }
        else if (currentPhase == GamePhase.Setup2)
        {
            // 前のプレイヤーへ
            currentPlayerIndex--;
            if (currentPlayerIndex < 0)
            {
                // 初期配置終了、通常プレイへ
                currentPhase = GamePhase.Playing;
                currentPlayerIndex = 0;
                currentStep = TurnStep.Waiting; // ダイスロール待ちなど
                Debug.Log("初期配置終了。ゲーム開始！ Player1のターン");
            }
            else
            {
                currentStep = TurnStep.PlaceSettlement;
                Debug.Log($"次は {CurrentPlayer} の番です（家を配置）");
            }
            lastBuiltSettlement = null;
        }
        UpdateGameInfoText();
        NotifyAI();
    }

    public void AddResource(string playerID, CatanMapGenerator.HexType type, int amount)
    {
        var p = players.Find(x => x.name == playerID);
        if (p != null)
        {
            switch (type)
            {
                case CatanMapGenerator.HexType.Wood: p.wood += amount; break;
                case CatanMapGenerator.HexType.Brick: p.brick += amount; break;
                case CatanMapGenerator.HexType.Ore: p.ore += amount; break;
                case CatanMapGenerator.HexType.Wheat: p.wheat += amount; break;
                case CatanMapGenerator.HexType.Sheep: p.sheep += amount; break;
            }
        }
    }

    // 指定した種類の資源をいくつ持っているか返す
    public int GetResourceCount(string playerID, CatanMapGenerator.HexType type)
    {
        var p = players.Find(x => x.name == playerID);
        if (p == null) return 0;
        switch (type)
        {
            case CatanMapGenerator.HexType.Wood: return p.wood;
            case CatanMapGenerator.HexType.Brick: return p.brick;
            case CatanMapGenerator.HexType.Ore: return p.ore;
            case CatanMapGenerator.HexType.Wheat: return p.wheat;
            case CatanMapGenerator.HexType.Sheep: return p.sheep;
            default: return 0;
        }
    }

    // 指定した種類の資源を消費する
    public void ConsumeResource(string playerID, CatanMapGenerator.HexType type, int amount)
    {
        var p = players.Find(x => x.name == playerID);
        if (p == null) return;
        switch (type)
        {
            case CatanMapGenerator.HexType.Wood: p.wood -= amount; break;
            case CatanMapGenerator.HexType.Brick: p.brick -= amount; break;
            case CatanMapGenerator.HexType.Ore: p.ore -= amount; break;
            case CatanMapGenerator.HexType.Wheat: p.wheat -= amount; break;
            case CatanMapGenerator.HexType.Sheep: p.sheep -= amount; break;
        }
    }

    // プレイヤーの港の所持状況から、その資源を渡すときのコスト（4, 3, 2）を計算
    public int GetTradeCost(string playerID, CatanMapGenerator.HexType giveType)
    {
        int cost = 4; // 基本は4:1

        foreach (var v in VertexPoint.AllVertices)
        {
            // 自分の建物があり、かつ港がある場合
            if (v.hasBuilding && v.ownerPlayer == playerID && v.hasPort)
            {
                if (v.portType == CatanMapGenerator.HexType.Any)
                {
                    cost = Mathf.Min(cost, 3); // 3:1港
                }
                else if (v.portType == giveType)
                {
                    return 2; // 2:1港（その資源専用）があれば即座に2で確定
                }
            }
        }
        return cost;
    }

    // 交易実行
    public void ExecuteTrade(string playerID, CatanMapGenerator.HexType give, CatanMapGenerator.HexType get, int cost)
    {
        ConsumeResource(playerID, give, cost);
        AddResource(playerID, get, 1);
        Debug.Log($"{playerID} Traded {cost} {give} for 1 {get}");
    }

    public bool TryConsumeResources(string playerID, int wood, int brick, int ore, int wheat, int sheep)
    {
        var p = players.Find(x => x.name == playerID);
        if (p == null) return false;

        if (p.wood >= wood && p.brick >= brick && p.ore >= ore && p.wheat >= wheat && p.sheep >= sheep)
        {
            p.wood -= wood;
            p.brick -= brick;
            p.ore -= ore;
            p.wheat -= wheat;
            p.sheep -= sheep;
            return true;
        }
        return false;
    }

    public int GetTotalResourceCount(string playerID)
    {
        var p = players.Find(x => x.name == playerID);
        if (p == null) return 0;
        return p.wood + p.brick + p.ore + p.wheat + p.sheep;
    }

    void InitializeDeck()
    {
        devCardDeck.Clear();
        // 騎士14, ポイント5, 道2, 独占2
        for (int i = 0; i < 14; i++) devCardDeck.Add(DevCardType.Knight);
        for (int i = 0; i < 5; i++) devCardDeck.Add(DevCardType.VictoryPoint);
        for (int i = 0; i < 2; i++) devCardDeck.Add(DevCardType.RoadBuilding);
        for (int i = 0; i < 2; i++) devCardDeck.Add(DevCardType.Monopoly);
        
        // シャッフル
        for (int i = 0; i < devCardDeck.Count; i++)
        {
            DevCardType temp = devCardDeck[i];
            int randomIndex = Random.Range(i, devCardDeck.Count);
            devCardDeck[i] = devCardDeck[randomIndex];
            devCardDeck[randomIndex] = temp;
        }
    }

    public void OnClickBuyCard()
    {
        if (currentPhase != GamePhase.Playing) return;
        if (devCardDeck.Count == 0) return;

        // コスト消費: 鉄1, 麦1, 羊1
        if (TryConsumeResources(CurrentPlayer, 0, 0, 1, 1, 1))
        {
            DevCardType card = devCardDeck[0];
            devCardDeck.RemoveAt(0);

            var p = players.Find(x => x.name == CurrentPlayer);
            if (p != null)
            {
                p.heldCards.Add(card);
                if (card == DevCardType.VictoryPoint)
                {
                    Debug.Log($"{CurrentPlayer} が勝利点カードを獲得しました（非公開）");
                    if (gameInfoText != null)
                        gameInfoText.text = $"{CurrentPlayer} がカードを引きました";
                    CheckVictory();
                }
                else
                {
                    Debug.Log($"{CurrentPlayer} が発展カードを購入しました: {card}");
                    if (gameInfoText != null)
                        gameInfoText.text = $"{CurrentPlayer} がカードを引きました: {card}";
                }
            }
        }
    }

    public void UseDevCard(int playerIndex, int cardIndex)
    {
        if (currentPhase != GamePhase.Playing) return;
        
        // 自分のターンでないと使えない
        if (currentPlayerIndex != playerIndex) 
        {
            Debug.Log("自分のターンではありません");
            return;
        }

        var p = players[playerIndex];
        if (cardIndex < 0 || cardIndex >= p.heldCards.Count) return;

        DevCardType card = p.heldCards[cardIndex];
        bool used = false;

        switch (card)
        {
            case DevCardType.Knight:
                Debug.Log($"{p.name} が騎士カードを使用しました！");
                p.usedKnights++;
                UpdateLargestArmy(p);
                // 盗賊移動モードへ
                currentStep = TurnStep.MoveRobber;
                ShowRobberHighlights();
                UpdateGameInfoText();
                used = true;
                break;

            case DevCardType.RoadBuilding:
                Debug.Log($"{p.name} が街道建設カードを使用しました！");
                // 道2本分の資材を付与 (木2, 土2)
                currentStep = TurnStep.RoadBuildingCard;
                roadBuildingCardCount = 2;
                isConstructionMode = true; // 建設モードON（ハイライト表示のため）
                if (constructionButtonText != null) constructionButtonText.text = "建設モード終了";
                UpdateGameInfoText();
                used = true;
                break;
            
            case DevCardType.Monopoly:
                Debug.Log($"{p.name} が独占カードを使用しました！");
                currentStep = TurnStep.Monopoly;
                UpdateGameInfoText();
                used = true;
                break;

            case DevCardType.VictoryPoint:
                // 勝利点カードは使用せず手札に保持し続ける（自動でVPに加算される）
                Debug.Log($"{p.name} の勝利点カードは使用できません（自動で加算されます）");
                used = false;
                break;
        }

        if (used)
        {
            p.heldCards.RemoveAt(cardIndex);
        }
    }

    public void ExecuteMonopoly(CatanMapGenerator.HexType resourceType)
    {
        if (currentStep != TurnStep.Monopoly) return;

        int totalStolen = 0;
        foreach (var p in players)
        {
            if (p.name == CurrentPlayer) continue;

            int count = GetResourceCount(p.name, resourceType);
            if (count > 0)
            {
                ConsumeResource(p.name, resourceType, count);
                totalStolen += count;
                Debug.Log($"{CurrentPlayer} が {p.name} から {resourceType} を {count}枚 奪いました");
            }
        }

        if (totalStolen > 0)
        {
            AddResource(CurrentPlayer, resourceType, totalStolen);
        }
        
        Debug.Log($"独占完了: 合計 {totalStolen}枚 の {resourceType} を獲得しました");
        currentStep = TurnStep.Waiting;
        UpdateGameInfoText();
    }

    public void OnClickRollDice()
    {
        if (currentPhase != GamePhase.Playing || hasRolledDice || isDiceRolling || isGameOver) return;

        // マルチプレイ: サーバーにリクエスト
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsMultiplayer)
        {
            if (!IsLocalPlayerTurn()) return;
            NetworkManager.Instance.SendGameAction("roll_dice");
            isDiceRolling = true;
            UpdateGameInfoText();
            return;
        }

        isDiceRolling = true;
        RollDice();
        UpdateGameInfoText();
    }

    public void OnClickEndTurn()
    {
        if (currentPhase != GamePhase.Playing || currentStep == TurnStep.MoveRobber || currentStep == TurnStep.RoadBuildingCard || currentStep == TurnStep.Monopoly || isExploreMode || isGameOver) return;

        // マルチプレイ: サーバーに送信（NetworkBridge経由で実行される）
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsMultiplayer
            && !_isFromNetwork)
        {
            if (!IsLocalPlayerTurn()) return;
            NetworkManager.Instance.SendGameAction("end_turn");
            return;
        }
        _isFromNetwork = false;

        // 建設モードがONならOFFにする
        if (isConstructionMode) ToggleConstructionMode();

        // 次のプレイヤーへ
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
        hasRolledDice = false;
        Debug.Log($"ターン終了。次は {CurrentPlayer} の番です。");
        UpdateGameInfoText();
        NotifyAI();
    }

    // NetworkBridgeからの呼び出しを識別するフラグ
    [HideInInspector] public bool _isFromNetwork = false;

    public void RollDice()
    {
        if (diceController != null)
        {
            // 物理ダイス演出がある場合
            diceController.RollDice(OnDiceRolled);
        }
        else
        {
            // 演出がない場合は即時計算
            int d1 = Random.Range(1, 7);
            int d2 = Random.Range(1, 7);
            OnDiceRolled(d1, d2);
        }
    }

    /// <summary>
    /// ネットワーク経由でダイス結果を受け取る（サーバーが出目を決定）
    /// </summary>
    public void OnDiceRolledNetwork(int d1, int d2)
    {
        isDiceRolling = false;
        hasRolledDice = true;
        OnDiceRolled(d1, d2);
    }

    // ダイスの結果が出た後に呼ばれる処理
    void OnDiceRolled(int d1, int d2)
    {
        isDiceRolling = false;
        hasRolledDice = true;
        int total = d1 + d2;

        Debug.Log($"<color=yellow>ダイスロール: {d1} + {d2} = {total}</color>");

        if (total == 7)
        {
            // 盗賊イベント発生
            Debug.Log("盗賊出現！ (7が出ました)");
            HandleBurst();
            currentStep = TurnStep.MoveRobber;
            ShowRobberHighlights(); // ハイライト表示
            UpdateGameInfoText();
            return; // 資源配布は行わない
        }

        if (gameInfoText != null)
        {
            gameInfoText.text = $"{CurrentPlayer} のターン";
        }

        // 2. マップ上の全タイルをチェック
        List<HexTileData> tiles = mapGenerator.GetAllTiles();
        
        foreach (var tile in tiles)
        {
            // 数字が一致したら資材配布
            if (tile.diceNumber == total)
            {
                tile.DistributeResources();
            }
        }
    }

    // バースト処理: 資源が8枚以上のプレイヤーは半分捨てる
    void HandleBurst()
    {
        foreach (var p in players)
        {
            int total = p.wood + p.brick + p.ore + p.wheat + p.sheep;
            if (total > 7)
            {
                int discardCount = total / 2;
                Debug.Log($"<color=red>{p.name} はバーストしました！ (所持: {total} -> 破棄: {discardCount})</color>");
                
                // 簡易実装: ランダムに捨てる
                for (int i = 0; i < discardCount; i++)
                {
                    // 所持している資源タイプをリストアップ
                    List<CatanMapGenerator.HexType> types = new List<CatanMapGenerator.HexType>();
                    if (p.wood > 0) types.Add(CatanMapGenerator.HexType.Wood);
                    if (p.brick > 0) types.Add(CatanMapGenerator.HexType.Brick);
                    if (p.ore > 0) types.Add(CatanMapGenerator.HexType.Ore);
                    if (p.wheat > 0) types.Add(CatanMapGenerator.HexType.Wheat);
                    if (p.sheep > 0) types.Add(CatanMapGenerator.HexType.Sheep);

                    if (types.Count > 0)
                    {
                        var typeToDiscard = types[Random.Range(0, types.Count)];
                        ConsumeResource(p.name, typeToDiscard, 1);
                    }
                }
            }
        }
    }

    // 盗賊の初期設定（MapGeneratorから呼ばれる）
    public void SetRobber(GameObject robber, HexTileData tile)
    {
        robberObject = robber;
        currentRobberTile = tile;
        if (tile != null) tile.hasRobber = true;
    }

    // タイルがクリックされた時の処理
    public void OnHexClicked(HexTileData tile)
    {
        if (currentStep == TurnStep.MoveRobber)
        {
            if (currentRobberTile == tile)
            {
                Debug.Log("盗賊は移動しなければなりません（同じ場所には置けません）");
                return;
            }
            MoveRobberTo(tile);
        }
    }

    public void MoveRobberTo(HexTileData tile)
    {
        // 古い場所から削除
        if (currentRobberTile != null) currentRobberTile.hasRobber = false;
        
        // 新しい場所に配置
        currentRobberTile = tile;
        currentRobberTile.hasRobber = true;
        
        // 視覚的な移動
        if (robberObject != null)
        {
            float dropHeight = (mapGenerator != null) ? mapGenerator.robberDropHeight : 10.0f;
            robberObject.transform.position = tile.transform.position + Vector3.up * dropHeight;

            // 物理演算のリセット（落下させるため）
            Rigidbody rb = robberObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.WakeUp();
            }
        }
        
        Debug.Log($"盗賊が {tile.name} に移動しました");
        
        // 強奪処理
        StealResource(tile);
        
        // モード終了
        HideRobberHighlights(); // ハイライト非表示
        currentStep = TurnStep.Waiting;
        UpdateGameInfoText();
    }

    void StealResource(HexTileData tile)
    {
        List<string> victims = new List<string>();
        foreach (var v in tile.adjacentVertices)
        {
            // 建物があり、かつ自分以外で、資源を持っているプレイヤー
            if (v.hasBuilding && v.ownerPlayer != CurrentPlayer && !victims.Contains(v.ownerPlayer))
            {
                if (GetTotalResourceCount(v.ownerPlayer) > 0)
                {
                    victims.Add(v.ownerPlayer);
                }
            }
        }

        if (victims.Count > 0)
        {
            // ランダムに1人選ぶ
            string victimName = victims[Random.Range(0, victims.Count)];
            
            // そのプレイヤーからランダムに資源を1つ奪う
            var p = players.Find(x => x.name == victimName);
            List<CatanMapGenerator.HexType> types = new List<CatanMapGenerator.HexType>();
            if (p.wood > 0) types.Add(CatanMapGenerator.HexType.Wood);
            if (p.brick > 0) types.Add(CatanMapGenerator.HexType.Brick);
            if (p.ore > 0) types.Add(CatanMapGenerator.HexType.Ore);
            if (p.wheat > 0) types.Add(CatanMapGenerator.HexType.Wheat);
            if (p.sheep > 0) types.Add(CatanMapGenerator.HexType.Sheep);

            if (types.Count > 0)
            {
                var stolenType = types[Random.Range(0, types.Count)];
                ConsumeResource(victimName, stolenType, 1);
                AddResource(CurrentPlayer, stolenType, 1);
                Debug.Log($"<color=red>{CurrentPlayer} が {victimName} から {stolenType} を奪いました！</color>");
            }
        }
    }

    // 全タイルのハイライトを表示
    void ShowRobberHighlights()
    {
        if (mapGenerator == null) return;
        foreach (var tile in mapGenerator.GetAllTiles())
        {
            // 盗賊がいる場所以外、かつ海以外を表示
            if (tile != currentRobberTile && tile.resourceType != CatanMapGenerator.HexType.Beach && tile.highlightObject != null)
            {
                tile.highlightObject.SetActive(true);
            }
        }
    }

    // 全タイルのハイライトを非表示
    void HideRobberHighlights()
    {
        if (mapGenerator == null) return;
        foreach (var tile in mapGenerator.GetAllTiles())
        {
            if (tile.highlightObject != null) tile.highlightObject.SetActive(false);
        }
    }

    public void UpdateGameInfoText()
    {
        if (gameInfoText == null) return;

        if (currentStep == TurnStep.MoveRobber)
        {
            gameInfoText.text = $"{CurrentPlayer}: 盗賊を移動させてください";
        }
        else if (currentStep == TurnStep.RoadBuildingCard)
        {
            gameInfoText.text = $"{CurrentPlayer}: 道を建設してください (残り{roadBuildingCardCount}本)";
        }
        else if (currentStep == TurnStep.Monopoly)
        {
            gameInfoText.text = $"{CurrentPlayer}: 上の素材一覧から独占する素材を選択してください";
        }
        else
        {
            gameInfoText.text = $"{CurrentPlayer} のターン";
        }
    }

    /// <summary>
    /// ターン遷移後にAIコントローラーへ通知
    /// </summary>
    public void NotifyAI()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsMultiplayer)
        {
            // マルチプレイ: ホストのみAI操作
            if (NetworkManager.Instance.IsHost && IsCurrentPlayerAI())
            {
                if (AIController.Instance != null)
                    AIController.Instance.OnTurnChanged();
            }
            return;
        }

        // シングルプレイ
        if (AIController.Instance != null)
            AIController.Instance.OnTurnChanged();
    }

    // =========== 勝利点 ===========

    public int GetVictoryPoints(string playerID)
    {
        int vp = 0;

        // 開拓地(1VP) と 都市(2VP)
        foreach (var v in VertexPoint.AllVertices)
        {
            if (v.hasBuilding && v.ownerPlayer == playerID)
                vp += v.isCity ? 2 : 1;
        }

        // 手持ちの勝利点カード
        var p = players.Find(x => x.name == playerID);
        if (p != null)
        {
            foreach (var card in p.heldCards)
            {
                if (card == DevCardType.VictoryPoint) vp++;
            }
        }

        // 最長交易路ボーナス
        if (longestRoadPlayer == playerID) vp += 2;

        // 最大騎士力ボーナス
        if (largestArmyPlayer == playerID) vp += 2;

        return vp;
    }

    public int GetSettlementCount(string playerID)
    {
        int count = 0;
        foreach (var v in VertexPoint.AllVertices)
            if (v.hasBuilding && v.ownerPlayer == playerID && !v.isCity) count++;
        return count;
    }

    public int GetCityCount(string playerID)
    {
        int count = 0;
        foreach (var v in VertexPoint.AllVertices)
            if (v.hasBuilding && v.ownerPlayer == playerID && v.isCity) count++;
        return count;
    }

    public int GetVPCardCount(string playerID)
    {
        var p = players.Find(x => x.name == playerID);
        if (p == null) return 0;
        int count = 0;
        foreach (var card in p.heldCards)
            if (card == DevCardType.VictoryPoint) count++;
        return count;
    }

    public string LongestRoadPlayer => longestRoadPlayer;
    public int LongestRoadLength => longestRoadLength;
    public string LargestArmyPlayer => largestArmyPlayer;
    public int LargestArmyCount => largestArmyCount;

    // =========== 最大騎士力 ===========

    void UpdateLargestArmy(PlayerData p)
    {
        if (p.usedKnights >= 3 && p.usedKnights > largestArmyCount)
        {
            largestArmyPlayer = p.name;
            largestArmyCount = p.usedKnights;
            Debug.Log($"<color=yellow>最大騎士力: {p.name} ({p.usedKnights}騎士)</color>");
        }
    }

    // =========== 最長交易路 ===========

    void UpdateLongestRoad()
    {
        foreach (var p in players)
        {
            int roadLen = CalculateLongestRoad(p.name);
            if (roadLen >= 5 && roadLen > longestRoadLength)
            {
                longestRoadPlayer = p.name;
                longestRoadLength = roadLen;
                Debug.Log($"<color=yellow>最長交易路: {p.name} ({roadLen}道)</color>");
            }
        }
        CheckVictory();
    }

    public int CalculateLongestRoad(string playerID)
    {
        int maxLen = 0;
        // そのプレイヤーの全EdgeからDFS
        foreach (var v in VertexPoint.AllVertices)
        {
            foreach (var edge in v.edges)
            {
                if (edge.hasRoad && edge.ownerPlayer == playerID)
                {
                    HashSet<EdgePoint> visited = new HashSet<EdgePoint> { edge };
                    DFSRoad(edge.vertex1, playerID, visited, 1, ref maxLen);
                    DFSRoad(edge.vertex2, playerID, visited, 1, ref maxLen);
                }
            }
        }
        return maxLen;
    }

    void DFSRoad(VertexPoint vertex, string playerID, HashSet<EdgePoint> visited, int currentLen, ref int maxLen)
    {
        if (currentLen > maxLen) maxLen = currentLen;

        // 敵の建物がある頂点で道が切断
        if (vertex.hasBuilding && vertex.ownerPlayer != playerID) return;

        foreach (var edge in vertex.edges)
        {
            if (edge.hasRoad && edge.ownerPlayer == playerID && !visited.Contains(edge))
            {
                visited.Add(edge);
                VertexPoint nextVertex = (edge.vertex1 == vertex) ? edge.vertex2 : edge.vertex1;
                DFSRoad(nextVertex, playerID, visited, currentLen + 1, ref maxLen);
                visited.Remove(edge);
            }
        }
    }

    // =========== 勝利チェック ===========

    public void CheckVictory()
    {
        if (isGameOver) return;
        if (currentPhase != GamePhase.Playing) return;

        string player = CurrentPlayer;
        int vp = GetVictoryPoints(player);

        if (vp >= victoryPointsToWin)
        {
            OnGameWon(player);
        }
    }

    void OnGameWon(string winnerName)
    {
        isGameOver = true;
        Debug.Log($"<color=green>★★★ {winnerName} の勝利！ ({GetVictoryPoints(winnerName)} VP) ★★★</color>");

        if (gameInfoText != null)
            gameInfoText.text = $"{winnerName} の勝利！";

        if (victoryScreen != null)
            victoryScreen.Show(winnerName);
    }
}