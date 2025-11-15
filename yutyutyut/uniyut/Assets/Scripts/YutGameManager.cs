using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

public class YutGameManager : MonoBehaviour
{
    [Header("Game Components")]
    public PlayerController[] players; // 0~3: 메이지, 4~7: 기사
    public Transform[] boardPositions; // 나무 발판 위치들 (A1~F5)
    public Button throwButton;
    public TextMeshProUGUI resultText;
    public TextMeshProUGUI positionText;
    public YutThrowController yutThrowController; // 물리 윷 던지기 컨트롤러
    
    [Header("Managers")]
    public YutPlatformManager platformManager;
    public YutHorseManager horseManager;
    public YutInputHandler inputHandler;
    public YutMovementManager movementManager;
    public YutTurnManager turnManager;
    public YutSelectionHandler selectionHandler;
    public GoldenPlatformEffect goldenEffectManager;
    
    [Header("Test Buttons (테스트용 - 나중에 삭제 가능)")]
    public Button testDoButton;      // 도 테스트 버튼
    public Button testGaeButton;    // 개 테스트 버튼
    public Button testGeolButton;   // 걸 테스트 버튼
    public Button testYutButton;    // 윷 테스트 버튼
    public Button testMoButton;     // 모 테스트 버튼
    public Button testBackDoButton; // 빽도 테스트 버튼
    
    [Header("Goal In Button")]
    public Button goalInButton;      // 골인 버튼
    
    [Header("Dodo Image")]
    public Sprite dodoSprite;        // Inspector에서 Assets/Yut/dodo.png를 드래그 앤 드롭하여 할당
    public Vector2 dodoPosition = new Vector2(0, -50);  // Inspector에서 이미지 위치 조정 (X, Y)
    public Vector2 dodoSize = new Vector2(200, 200);      // Inspector에서 이미지 크기 조정 (Width, Height)
    
    [Header("Game State")]
    // 턴 인덱스(0~1): 0=메이지 팀, 1=기사 팀 (기사 → 메이지 → 기사 → 메이지 순서)
    public int currentTurnIndex = 0; // 0: 메이지, 1: 기사
    public int[] playerPositions; // 0~3: 메이지, 4~7: 기사
    public bool isPlayerMoving = false;
    public bool canThrowAgain = false; // 윷/모로 인한 추가 던지기 가능 여부
    public int extraThrowCount = 0; // 추가 던지기 횟수 (황금발판 효과 누적)

    // A2에서 빽도로 A1에 도착한 말 추적 (골인 가능 상태)
    private bool[] canGoalInFromA1 = new bool[8]; // 0~3: 메이지, 4~7: 기사
    
    // 들어온 말 수 추적 (-2: 완주 완료/사라짐)
    private int mageFinishedCount = 0; // 메이지 완주한 말 수
    private int knightFinishedCount = 0; // 기사 완주한 말 수
    
    [Header("Golden Platform")]
    public Material goldenPlatformMaterial; // Unity Inspector에서 할당할 황금색 머티리얼 (StumpGolden)
    public bool hasExtraThrow = false; // 추가 던질 기회 여부
    
    [Header("Horse Position Offset")]
    public float positionOffsetDistance = 0.5f; // 같은 위치의 말들 사이 간격
    
    [Header("Platform Selection")]
    public Material selectablePlatformMaterial; // 선택 가능한 발판 하이라이트 머티리얼
    public Material selectableHorseMaterial; // 선택 가능한 말 하이라이트 머티리얼
    [System.NonSerialized]
    private List<int> selectablePlatformIndices; // 현재 선택 가능한 발판 인덱스 목록
    [System.NonSerialized]
    private List<int> selectableHorseIndices; // 현재 선택 가능한 말 인덱스 목록
    [System.NonSerialized]
    public bool waitingForPlatformSelection = false; // 발판 선택 대기 중인지
    [System.NonSerialized]
    public bool waitingForHorseSelection = false; // 말 선택 대기 중인지
    [System.NonSerialized]
    public int currentHorseIndexForMove = -1; // 현재 이동할 말 인덱스
    [System.NonSerialized]
    public int currentMoveSteps = 0; // 현재 이동할 칸 수
    [System.NonSerialized]
    public bool isBackDoTurn = false; // 빽도 턴인지 여부
    [System.NonSerialized]
    public YutOutcome currentGoalInMovement = YutOutcome.Nak; // 골인에 사용할 이동
    // pendingMovements, savedYutOutcome, hasSavedYutOutcome, turnChangedInMoveToPlatform는 YutTurnManager로 이동됨
    [System.NonSerialized]
    private Vector3[] horseInitialPositions; // 각 말의 초기 시작 위치 (0~3: 메이지, 4~7: 기사) - 실제 transform.position 좌표
    
    // 윷놀이 보드 경로 (29개)
    private string[] positionNames = {
        "A1", "A2", "A3", "A4", "A5",
        "B1", "B2", "B3", "B4", "B5", 
        "C1", "C2", "C3", "C4", "C5",
        "D1", "D2", "D3", "D4", "D5",
        "E1", "E2", "EF3", "E4", "E5",
        "F1", "F2", "F4", "F5"
    };
    
    private string[] playerNames = {"메이지", "기사"};
    
    void Start()
    {
        // 매니저 자동 초기화 (Inspector에서 할당하지 않았을 경우)
        if (platformManager == null)
        {
            platformManager = GetComponent<YutPlatformManager>();
            if (platformManager == null)
            {
                platformManager = gameObject.AddComponent<YutPlatformManager>();
            }
            // platformManager의 필수 데이터 할당
            platformManager.boardPositions = boardPositions;
            platformManager.goldenPlatformMaterial = goldenPlatformMaterial;
            platformManager.selectablePlatformMaterial = selectablePlatformMaterial;
        }
        
        if (horseManager == null)
        {
            horseManager = GetComponent<YutHorseManager>();
            if (horseManager == null)
            {
                horseManager = gameObject.AddComponent<YutHorseManager>();
            }
            // horseManager의 필수 데이터 할당
            horseManager.players = players;
            horseManager.boardPositions = boardPositions;
            horseManager.selectableHorseMaterial = selectableHorseMaterial;
        }
        
        if (inputHandler == null)
        {
            inputHandler = GetComponent<YutInputHandler>();
            if (inputHandler == null)
            {
                inputHandler = gameObject.AddComponent<YutInputHandler>();
            }
            // inputHandler 초기화
            inputHandler.Initialize(this, players, boardPositions, positionNames);
        }
        
        if (movementManager == null)
        {
            movementManager = GetComponent<YutMovementManager>();
            if (movementManager == null)
            {
                movementManager = gameObject.AddComponent<YutMovementManager>();
            }
            // movementManager 초기화
            movementManager.Initialize(this);
        }
        
        if (turnManager == null)
        {
            turnManager = GetComponent<YutTurnManager>();
            if (turnManager == null)
            {
                turnManager = gameObject.AddComponent<YutTurnManager>();
            }
            // turnManager 초기화
            turnManager.Initialize(this);
        }
        
        if (selectionHandler == null)
        {
            selectionHandler = GetComponent<YutSelectionHandler>();
            if (selectionHandler == null)
            {
                selectionHandler = gameObject.AddComponent<YutSelectionHandler>();
            }
            // selectionHandler 초기화
            selectionHandler.Initialize(this);
        }

        if (goldenEffectManager == null)
        {
            goldenEffectManager = GetComponent<GoldenPlatformEffect>();
            if (goldenEffectManager == null)
            {
                goldenEffectManager = gameObject.AddComponent<GoldenPlatformEffect>();
            }
            // goldenEffectManager 초기화
            goldenEffectManager.Initialize(this);
        }
        
        // playerPositions 배열 강제 초기화
        // -1: 대기공간, 0 이상: 발판 위치 (0=A1, 1=A2, ...), -2: 완주 완료/사라짐
        if (playerPositions == null || playerPositions.Length != 8)
        {
            playerPositions = new int[8] {-1, -1, -1, -1, -1, -1, -1, -1}; // 모든 말이 대기공간에 있음
        }
        
        // canGoalInFromA1 배열 초기화 (모든 말은 초기값 false)
        if (canGoalInFromA1 == null || canGoalInFromA1.Length != 8)
        {
            canGoalInFromA1 = new bool[8] {false, false, false, false, false, false, false, false};
        }
        
        // 완주한 말 수 초기화
        mageFinishedCount = 0;
        knightFinishedCount = 0;
        
        // 각 말의 초기 시작 위치 저장 (실제 transform.position 좌표)
        if (horseInitialPositions == null || horseInitialPositions.Length != 8)
        {
            horseInitialPositions = new Vector3[8];
        }
        
        // 각 말의 초기 위치 좌표 설정 (메이지 1~4, 기사 1~4 순서)
        horseInitialPositions[0] = new Vector3(97.5f, 1.5f, 32f);  // 메이지1
        horseInitialPositions[1] = new Vector3(105f, 1.5f, 32f);  // 메이지2
        horseInitialPositions[2] = new Vector3(97.5f, 1.5f, 28f); // 메이지3
        horseInitialPositions[3] = new Vector3(105f, 1.5f, 28f);  // 메이지4
        horseInitialPositions[4] = new Vector3(90f, 1.5f, 32f);   // 기사1
        horseInitialPositions[5] = new Vector3(82.5f, 1.5f, 32f); // 기사2
        horseInitialPositions[6] = new Vector3(90f, 1.5f, 28f);   // 기사3
        horseInitialPositions[7] = new Vector3(82.5f, 1.5f, 28f); // 기사4
        
        
        // 각 말의 transform.position을 초기 위치로 설정
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
            {
                players[i].transform.position = horseInitialPositions[i];
            }
        }
        
        // pendingMovements는 YutTurnManager에서 관리됨
        
        // selectablePlatformIndices 초기화
        if (selectablePlatformIndices == null)
        {
            selectablePlatformIndices = new List<int>();
        }
        
        if (throwButton != null)
        {
            throwButton.onClick.AddListener(ThrowYut);
            
            // 버튼 텍스트 설정
            TextMeshProUGUI buttonText = throwButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = "윷 던지자";
                
                // 폰트 크기를 2배로 설정
                buttonText.fontSize = buttonText.fontSize * 2f;
                
                // 텍스트 색상을 흰색으로 설정
                buttonText.color = Color.white;
                
                // Maplestory Bold 폰트 로드 및 적용
                TMP_FontAsset maplestoryFont = Resources.Load<TMP_FontAsset>("TextMesh Pro/Fonts & Materials/Maplestory Bold SDF");
                if (maplestoryFont == null)
                {
                    // Resources 폴더에 없으면 직접 경로로 로드 시도 (에디터 전용)
                    #if UNITY_EDITOR
                    maplestoryFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Maplestory Bold SDF.asset");
                    #endif
                }
                
                if (maplestoryFont != null)
                {
                    buttonText.font = maplestoryFont;
                }
                else
                {
                    Debug.LogWarning("Maplestory Bold 폰트를 찾을 수 없습니다. 기본 폰트를 사용합니다.");
                }
            }
            else
            {
                // TextMeshProUGUI가 없으면 일반 Text 컴포넌트 확인
                UnityEngine.UI.Text textComponent = throwButton.GetComponentInChildren<UnityEngine.UI.Text>();
                if (textComponent != null)
                {
                    textComponent.text = "윷 던지자";
                    
                    // 폰트 크기를 2배로 설정
                    textComponent.fontSize = (int)(textComponent.fontSize * 2f);
                    
                    // 텍스트 색상을 흰색으로 설정
                    textComponent.color = Color.white;
                    
                    // 일반 Text 컴포넌트의 경우 폰트는 Font 타입이므로 TTF 파일을 로드해야 함
                    Font maplestoryFont = Resources.Load<Font>("TextMesh Pro/Fonts/Maplestory Bold");
                    if (maplestoryFont == null)
                    {
                        #if UNITY_EDITOR
                        maplestoryFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/Maplestory Bold.ttf");
                        #endif
                    }
                    
                    if (maplestoryFont != null)
                    {
                        textComponent.font = maplestoryFont;
                    }
                }
            }
        }
        else
        {
            Debug.LogError("Throw 버튼이 연결되지 않았습니다!");
        }
        
        // 테스트 버튼 리스너 설정
        if (testDoButton != null)
        {
            testDoButton.onClick.AddListener(() => TestThrowYut(YutOutcome.Do));
        }
        if (testGaeButton != null)
        {
            testGaeButton.onClick.AddListener(() => TestThrowYut(YutOutcome.Gae));
        }
        if (testGeolButton != null)
        {
            testGeolButton.onClick.AddListener(() => TestThrowYut(YutOutcome.Geol));
        }
        if (testYutButton != null)
        {
            testYutButton.onClick.AddListener(() => TestThrowYut(YutOutcome.Yut));
        }
        if (testMoButton != null)
        {
            testMoButton.onClick.AddListener(() => TestThrowYut(YutOutcome.Mo));
        }
        if (testBackDoButton != null)
        {
            testBackDoButton.onClick.AddListener(() => TestThrowYut(YutOutcome.Do, true)); // 빽도는 Do로 전달하되 isBackDo=true
        }
        
        // 골인 버튼 리스너 설정
        if (goalInButton != null)
        {
            goalInButton.onClick.AddListener(OnGoalInButtonClicked);
            goalInButton.gameObject.SetActive(false); // 초기에는 숨김
            
            // 골인 버튼 텍스트 설정 (throwButton과 동일한 폰트 사용)
            TextMeshProUGUI buttonText = goalInButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = "골인";
                
                // 폰트 크기를 2배로 설정 (throwButton과 동일)
                buttonText.fontSize = buttonText.fontSize * 2f;
                
                // 텍스트 색상을 흰색으로 설정 (throwButton과 동일)
                buttonText.color = Color.white;
                
                // Maplestory Bold 폰트 로드 및 적용 (throwButton과 동일)
                TMP_FontAsset maplestoryFont = Resources.Load<TMP_FontAsset>("TextMesh Pro/Fonts & Materials/Maplestory Bold SDF");
                if (maplestoryFont == null)
                {
                    // Resources 폴더에 없으면 직접 경로로 로드 시도 (에디터 전용)
                    #if UNITY_EDITOR
                    maplestoryFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Maplestory Bold SDF.asset");
                    #endif
                }
                
                if (maplestoryFont != null)
                {
                    buttonText.font = maplestoryFont;
                }
                else
                {
                    Debug.LogWarning("골인 버튼: Maplestory Bold 폰트를 찾을 수 없습니다. 기본 폰트를 사용합니다.");
                }
            }
            else
            {
                // TextMeshProUGUI가 없으면 일반 Text 컴포넌트 확인
                UnityEngine.UI.Text textComponent = goalInButton.GetComponentInChildren<UnityEngine.UI.Text>();
                if (textComponent != null)
                {
                    textComponent.text = "골인";
                    
                    // 폰트 크기를 2배로 설정 (throwButton과 동일)
                    textComponent.fontSize = (int)(textComponent.fontSize * 2f);
                    
                    // 텍스트 색상을 흰색으로 설정 (throwButton과 동일)
                    textComponent.color = Color.white;
                    
                    // 일반 Text 컴포넌트의 경우 폰트는 Font 타입이므로 TTF 파일을 로드해야 함
                    Font maplestoryFont = Resources.Load<Font>("TextMesh Pro/Fonts/Maplestory Bold");
                    if (maplestoryFont == null)
                    {
                        #if UNITY_EDITOR
                        maplestoryFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/Maplestory Bold.ttf");
                        #endif
                    }
                    
                    if (maplestoryFont != null)
                    {
                        textComponent.font = maplestoryFont;
                    }
                    else
                    {
                        Debug.LogWarning("골인 버튼: Maplestory Bold 폰트를 찾을 수 없습니다. 기본 폰트를 사용합니다.");
                    }
                }
            }
        }
        
        // horseManager 초기화
        horseManager.Initialize(playerPositions, horseInitialPositions);
        
        // 발판 렌더러 초기화 (platformManager 사용)
        platformManager.InitializePlatformRenderers();
        
        // 랜덤 황금 발판 선택 (platformManager 사용)
        platformManager.SelectRandomGoldenPlatform();
        
        // dodo 이미지 표시
        ShowDodoImage();
        
        UpdateUI();
    }
    
    // dodo 이미지를 화면에 표시하는 메서드
    private void ShowDodoImage()
    {
        Debug.Log("[ShowDodoImage] 시작 - dodoSprite 초기값: " + (dodoSprite == null ? "null" : dodoSprite.name));
        
        // Checkmark나 다른 잘못된 이미지가 할당되어 있으면 dodo.png로 교체
        bool needToLoadDodo = false;
        if (dodoSprite != null)
        {
            Debug.Log("[ShowDodoImage] 현재 할당된 Sprite 이름: " + dodoSprite.name);
            #if UNITY_EDITOR
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(dodoSprite);
            Debug.Log("[ShowDodoImage] 현재 할당된 Sprite 경로: " + assetPath);
            if (!assetPath.Contains("dodo"))
            {
                Debug.LogWarning("[ShowDodoImage] ⚠️ 현재 '" + dodoSprite.name + "' 이미지가 할당되어 있습니다. dodo.png로 교체합니다.");
                needToLoadDodo = true;
            }
            #endif
        }
        
        // Inspector에서 할당되지 않았거나 잘못된 이미지가 할당되었으면 에디터에서 자동으로 로드 시도
        if (dodoSprite == null || needToLoadDodo)
        {
            Debug.Log("[ShowDodoImage] Inspector에서 할당되지 않음. 자동 로드 시도 중...");
            
            #if UNITY_EDITOR
            // 에디터에서만 AssetDatabase를 사용하여 자동 로드
            Debug.Log("[ShowDodoImage] AssetDatabase.LoadAssetAtPath 시도: Assets/Yut/dodo.png");
            dodoSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Yut/dodo.png");
            Debug.Log("[ShowDodoImage] AssetDatabase 로드 결과: " + (dodoSprite == null ? "실패 (null)" : "성공 (" + dodoSprite.name + ")"));
            
            // Texture2D로도 시도
            if (dodoSprite == null)
            {
                Debug.Log("[ShowDodoImage] Texture2D로 로드 시도: Assets/Yut/dodo.png");
                Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Yut/dodo.png");
                if (texture != null)
                {
                    Debug.Log("[ShowDodoImage] Texture2D 로드 성공. Sprite로 변환 중...");
                    dodoSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    Debug.Log("[ShowDodoImage] Sprite 변환 결과: " + (dodoSprite == null ? "실패" : "성공"));
                }
                else
                {
                    Debug.LogWarning("[ShowDodoImage] Texture2D도 로드 실패");
                }
            }
            #endif
            
            // Resources 폴더에서도 시도
            if (dodoSprite == null)
            {
                Debug.Log("[ShowDodoImage] Resources.Load 시도: Yut/dodo");
                dodoSprite = Resources.Load<Sprite>("Yut/dodo");
                Debug.Log("[ShowDodoImage] Resources.Load 결과: " + (dodoSprite == null ? "실패 (null)" : "성공 (" + dodoSprite.name + ")"));
            }
            
            if (dodoSprite == null)
            {
                Debug.LogError("[ShowDodoImage] 모든 로드 방법 실패. dodo 이미지를 찾을 수 없습니다.");
                Debug.LogWarning("Unity Editor에서 다음 방법으로 할당해주세요:\n" +
                    "1. Project 창에서 Assets/Yut/dodo.png 선택\n" +
                    "2. Inspector 창에서 YutGameManager 컴포넌트의 'Dodo Sprite' 필드 찾기\n" +
                    "3. Project 창의 dodo.png를 Inspector의 'Dodo Sprite' 필드로 드래그 앤 드롭\n" +
                    "또는 Assets/Yut/dodo.png를 Assets/Resources/Yut/dodo.png로 이동");
                return;
            }
            else
            {
                Debug.Log("[ShowDodoImage] dodo 이미지 로드 성공: " + dodoSprite.name);
            }
        }
        else
        {
            Debug.Log("[ShowDodoImage] Inspector에서 이미 할당된 dodoSprite 사용: " + dodoSprite.name);
        }
        
        // Canvas 찾기
        Debug.Log("[ShowDodoImage] Canvas 찾기 시작...");
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[ShowDodoImage] Canvas를 찾을 수 없습니다. dodo 이미지를 표시할 수 없습니다.");
            return;
        }
        Debug.Log("[ShowDodoImage] Canvas 찾기 성공: " + canvas.name);
        
        // 기존 DodoImage가 있으면 제거
        Transform existingImage = canvas.transform.Find("DodoImage");
        if (existingImage != null)
        {
            Debug.Log("[ShowDodoImage] 기존 DodoImage 발견. 제거 중...");
            Destroy(existingImage.gameObject);
        }
        
        // Image GameObject 생성
        Debug.Log("[ShowDodoImage] Image GameObject 생성 중...");
        GameObject imageObject = new GameObject("DodoImage");
        imageObject.transform.SetParent(canvas.transform, false);
        Debug.Log("[ShowDodoImage] Image GameObject 생성 완료");
        
        // Image 컴포넌트 추가
        Debug.Log("[ShowDodoImage] Image 컴포넌트 추가 중...");
        Image image = imageObject.AddComponent<Image>();
        image.sprite = dodoSprite;
        Debug.Log("[ShowDodoImage] Image 컴포넌트 추가 완료. Sprite 할당: " + (image.sprite == null ? "null" : image.sprite.name));
        
        // RectTransform 설정 (Inspector에서 설정한 값 사용)
        Debug.Log("[ShowDodoImage] RectTransform 설정 중... 위치: " + dodoPosition + ", 크기: " + dodoSize);
        RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = dodoPosition;  // Inspector에서 설정한 위치 사용
        rectTransform.sizeDelta = dodoSize;              // Inspector에서 설정한 크기 사용
        Debug.Log("[ShowDodoImage] RectTransform 설정 완료");
        
        Debug.Log("[ShowDodoImage] dodo 이미지가 화면에 표시되었습니다. 최종 위치: " + rectTransform.anchoredPosition + ", 최종 크기: " + rectTransform.sizeDelta);
    }
    
    public void ThrowYut()
    {
        string currentPlayer = currentTurnIndex == 0 ? "메이지" : "기사";
        Debug.Log($"[턴 시작] {currentPlayer}의 턴 - ThrowYut() 호출됨 (currentTurnIndex: {currentTurnIndex}, isPlayerMoving: {isPlayerMoving})");
        
        if (isPlayerMoving)
        {
            Debug.LogWarning($"[턴 시작 실패] 플레이어가 이동 중입니다. (isPlayerMoving: {isPlayerMoving})");
            return;
        }
        
        if (turnManager != null)
        {
            StartCoroutine(turnManager.ThrowAndMoveSequence());
        }
        else
        {
            Debug.LogError("YutTurnManager가 연결되지 않았습니다!");
           }
    }
    
    // 테스트용 메서드: 특정 윷 결과를 직접 처리
    public void TestThrowYut(YutOutcome outcome, bool isBackDo = false)
    {
        if (isPlayerMoving) return;
        
        // 테스트 모드로 ThrowAndMoveSequence 호출
        if (turnManager != null)
        {
            StartCoroutine(turnManager.ThrowAndMoveSequence(outcome, isBackDo));
            }
            else
            {
            Debug.LogError("YutTurnManager가 연결되지 않았습니다!");
        }
    }
    
    // ThrowAndMoveSequence는 YutTurnManager로 이동됨
    // 이전 코드는 제거됨 (YutTurnManager.ThrowAndMoveSequence 사용)
    
    int GetMoveSteps(int yutResult)
    {
        return YutGameUtils.GetMoveSteps(yutResult);
    }
    
    // 현재 턴의 플레이어(0: 메이지, 1: 기사)
    public int GetCurrentPlayerIndex()
    {
        return currentTurnIndex; // 0: 메이지, 1: 기사
    }
    
    // 테스트 버튼 활성화 헬퍼 함수
    public void EnableTestButtons(bool enable = true)
    {
        if (testDoButton != null) testDoButton.interactable = enable;
        if (testGaeButton != null) testGaeButton.interactable = enable;
        if (testGeolButton != null) testGeolButton.interactable = enable;
        if (testYutButton != null) testYutButton.interactable = enable;
        if (testMoButton != null) testMoButton.interactable = enable;
        if (testBackDoButton != null) testBackDoButton.interactable = enable;
    }
    
    // 턴 변경 헬퍼 메서드
    public void ChangeTurn()
    {
        // canGoalInFromA1 플래그는 턴 변경 시 리셋하지 않음
        // 골인 처리 후에만 리셋됨 (A2에서 빽도로 A1에 도착한 말은 다음 턴에도 골인 가능해야 함)
        currentTurnIndex = (currentTurnIndex + 1) % 2;
        
        // 턴이 변경되면 새로운 턴의 보호막 체크 (내 턴이 다시 오면 보호막 제거)
        if (goldenEffectManager != null)
        {
            goldenEffectManager.CheckShieldExpiration();
        }
    }

    
    // Internal 메서드로 변경 (movementManager에서 호출)
    public IEnumerator MoveHorseInternal(int horseIndex, int steps)
    {
        // 인덱스 범위 확인
        if (horseIndex < 0 || horseIndex >= players.Length)
        {
            Debug.LogError($"잘못된 말 인덱스: {horseIndex}, players.Length: {players.Length}");
            yield break;
        }
        
        if (horseIndex >= playerPositions.Length)
        {
            Debug.LogError($"잘못된 말 인덱스: {horseIndex}, playerPositions.Length: {playerPositions.Length}");
            yield break;
        }
        
        isPlayerMoving = true;
        
        // 이동 전에 말이 숨겨져 있으면 일단 보이게 함
        if (players[horseIndex] != null && !players[horseIndex].gameObject.activeSelf)
        {
            players[horseIndex].gameObject.SetActive(true);
        }
        
        // 이동할 말 목록 (업힌 말들)
        // 시작 위치에 같은 팀 말이 있고, 이미 업힌 상태(x2 UI가 있음)인지 확인
        List<int> horsesToMoveTogether = new List<int>();
        int startPosition = playerPositions[horseIndex];
        bool isMage = horseIndex < 4;
        
        // 대기공간(-1)에서 시작하는 경우, 대기공간의 다른 말들을 업지 않음
        if (startPosition != -1)
        {
            // 시작 위치의 같은 팀 말들 찾기 (대기공간 제외)
        List<int> sameTeamHorsesAtStart = new List<int>();
        for (int j = 0; j < playerPositions.Length; j++)
        {
                if (j != horseIndex && playerPositions[j] == startPosition && playerPositions[j] != -1 && playerPositions[j] != -2)
            {
                bool jIsMage = j < 4;
                if (jIsMage == isMage)
                {
                    sameTeamHorsesAtStart.Add(j);
                }
            }
        }
        
        // 같은 위치에 같은 팀 말이 2개 이상 있으면 업힌 상태
        // 첫 번째 말(인덱스가 작은 말)에 x2 UI가 있을 수 있음
        if (sameTeamHorsesAtStart.Count >= 1)
        {
            // 같은 위치의 같은 팀 말들 중 첫 번째 말 찾기
            List<int> allSameTeamHorses = new List<int>(sameTeamHorsesAtStart);
            allSameTeamHorses.Add(horseIndex);
            allSameTeamHorses.Sort();
            
            int firstHorse = allSameTeamHorses[0];
            
            // 첫 번째 말에 x2 UI가 있으면 업힌 상태 (이미 업힌 말들)
            if (horseManager != null && horseManager.HasHorseCountUI(firstHorse))
            {
                // 업힌 말들과 함께 이동
                horsesToMoveTogether.AddRange(sameTeamHorsesAtStart);
                }
            }
        }
        
        // 대기공간(-1)에서 시작하는 경우, A1(0)으로 시작
        if (playerPositions[horseIndex] == -1)
        {
            playerPositions[horseIndex] = 0; // A1에서 시작
            startPosition = 0; // 시작 위치도 업데이트
        }
        
        // C1 경로에서 시작했는지 추적 (EF3에서 분기 판단용)
        bool isC1Path = (startPosition == 10);
        // F 경로인지 추적 (F1, F2, F4, F5에서 시작하거나 F 경로를 통해 이동)
        bool isFPath = false;
        
        // F 경로로 시작하는 경우 확인
        if (startPosition == 25 || startPosition == 26 || startPosition == 27 || startPosition == 28)
        {
            isFPath = true;
        }
        if (isC1Path)
        {
            isFPath = true; // C1 경로는 F 경로
        }
        
        for (int i = 0; i < steps; i++)
        {
            // 마지막 발판(최종 목적지)에서만 적의 말 잡기 및 새로 업힘 처리
            // 중간 발판에서는 말이 있어도 업히지 않음
            bool isLastStep = (i == steps - 1);
            
            // 다음 발판으로 이동 (분기점 고려)
            int currentPos = playerPositions[horseIndex];
            int nextPos;
            
            // B1(5) 또는 EF3(22)에서 시작하는 경우, 분기 경로로 이동
            // C1(10)은 F 경로로 일직선 이동 (분기도 한 칸 소모)
            if (currentPos == 5 && i == 0) // B1에서 시작
            {
                nextPos = 20; // E1로 분기
            }
            else if (currentPos == 10 && i == 0) // C1에서 시작 (F 경로로 일직선, 1칸 소모)
            {
                nextPos = 25; // F1로 분기
            }
            else if (currentPos == 22 && i == 0) // EF3에서 시작
            {
                nextPos = 27; // F4로 분기
            }
            else if (currentPos == 5 && isLastStep && startPosition == 5) // B1에서 시작해서 B1에 마지막 단계로 도착
            {
                nextPos = 20; // E1로 분기
            }
            else if (currentPos == 10 && isLastStep && startPosition == 10) // C1에서 시작해서 C1에 마지막 단계로 도착 (즉, C1이 최종 목적지)
            {
                nextPos = 25; // F1로 분기
            }
            // C1을 중간 경로로 거쳐가는 경우는 분기하지 않음
            else if (currentPos == 22 && isLastStep && startPosition == 22) // EF3에서 시작해서 EF3에 마지막 단계로 도착
            {
                nextPos = 27; // F4로 분기
            }
            // C1 경로에서 EF3에 도착한 경우 F4로 분기 (일직선)
            else if (currentPos == 22 && isC1Path) // C1 경로에서 EF3에 도착한 경우 F4로 분기
            {
                nextPos = 27; // F4로 분기
                isFPath = true; // F 경로로 설정
            }
            // F 경로에서 EF3에 도착한 경우 F4로 분기
            else if (currentPos == 22 && isFPath) // F 경로에서 EF3에 도착한 경우 F4로 분기
            {
                nextPos = 27; // F4로 분기
            }
            else
            {
                // 일반 경로 또는 중간 경로
                nextPos = GetNextPositionInPath(currentPos);
                
                // F 경로 추적: F1, F2, F4, F5를 지나가면 F 경로로 설정
                if (currentPos == 25 || currentPos == 26 || currentPos == 27 || currentPos == 28 || nextPos == 25 || nextPos == 26 || nextPos == 27 || nextPos == 28)
                {
                    isFPath = true;
                }
            }
            
            playerPositions[horseIndex] = nextPos;
            
            // 함께 이동한 말들(업힌 말들)도 같은 발판으로 이동
            foreach (int otherHorseIndex in horsesToMoveTogether)
            {
                playerPositions[otherHorseIndex] = nextPos;
            }
            
            // A0(인덱스 0)에 도착하면 말이 완주 완료 (사라짐)
            if (nextPos == 0 && isLastStep)
            {
                // 말 비활성화
                if (players[horseIndex] != null)
                {
                    players[horseIndex].gameObject.SetActive(false);
                }
                
                // 완주 처리 (-2: 완주 완료)
                playerPositions[horseIndex] = -2;
                
                // 함께 이동한 말들도 완주 처리
                foreach (int otherHorseIndex in horsesToMoveTogether)
                {
                    if (players[otherHorseIndex] != null)
                    {
                        players[otherHorseIndex].gameObject.SetActive(false);
                    }
                    playerPositions[otherHorseIndex] = -2;
                    
                    // 완주한 말 수 증가
                    if (otherHorseIndex < 4)
                    {
                        mageFinishedCount++;
                    }
                    else
                    {
                        knightFinishedCount++;
                    }
                }
                
                // 완주한 말 수 증가
                if (horseIndex < 4)
                {
                    mageFinishedCount++;
                }
                else
                {
                    knightFinishedCount++;
                }
                
                // UI 업데이트
                UpdateUI();
                
                // 완주했으므로 이후 처리 건너뛰기
                yield break;
            }
            
            if (isLastStep)
            {
                // 도착한 발판에 적의 말이 있으면 잡기 (같은 팀 말 처리 전에)
                int finalPos = playerPositions[horseIndex];
                List<int> enemyHorsesToCapture = new List<int>();
                for (int j = 0; j < playerPositions.Length; j++)
                {
                    if (j != horseIndex && !horsesToMoveTogether.Contains(j) && playerPositions[j] == finalPos)
                    {
                        bool jIsMage = j < 4;
                        // 적의 말이면 잡기 목록에 추가
                        if (jIsMage != isMage)
                        {
                            enemyHorsesToCapture.Add(j);
                        }
                    }
                }
                
                // 적의 말을 잡았으면 시작 위치로 보내기
                if (enemyHorsesToCapture.Count > 0)
                {
                    // 잡는 말의 공격 애니메이션 실행
                    if (players[horseIndex] != null)
                    {
                        PlayerController attackerController = players[horseIndex].GetComponent<PlayerController>();
                        if (attackerController != null)
                        {
                            attackerController.PlayAttackAnimation();
                        }
                    }
                    
                    foreach (int enemyHorseIndex in enemyHorsesToCapture)
                    {
                        // 해당 적의 말과 같은 위치에 있는 같은 팀 말들도 모두 찾기
                        int enemyPos = playerPositions[enemyHorseIndex];
                        List<int> enemyTeamHorses = new List<int>();
                        bool enemyIsMage = enemyHorseIndex < 4;
                        
                        for (int k = 0; k < playerPositions.Length; k++)
                        {
                            if (playerPositions[k] == enemyPos)
                            {
                                bool kIsMage = k < 4;
                                if (kIsMage == enemyIsMage)
                                {
                                    enemyTeamHorses.Add(k);
                                }
                            }
                        }
                        
                        // 적의 말(들)을 모두 시작 위치로 이동
                        foreach (int enemyTeamHorse in enemyTeamHorses)
                        {
                            Vector3 initialPosVector = horseInitialPositions[enemyTeamHorse];
                            string enemyHorseName = enemyTeamHorse < 4 ? $"메이지{enemyTeamHorse+1}" : $"기사{enemyTeamHorse-3}";
                            
                            // 잡히는 말의 죽음 애니메이션 실행
                            if (players[enemyTeamHorse] != null)
                            {
                                PlayerController victimController = players[enemyTeamHorse].GetComponent<PlayerController>();
                                if (victimController != null)
                                {
                                    victimController.PlayDeathAnimation();
                                }
                            }
                            
                            // playerPositions는 대기공간(-1)으로 설정
                            playerPositions[enemyTeamHorse] = -1;
                            if (players[enemyTeamHorse] != null)
                            {
                                // NavMeshAgent 비활성화 후 위치 설정 (y좌표 증가 방지 및 위치 고정)
                                UnityEngine.AI.NavMeshAgent agent = players[enemyTeamHorse].GetComponent<UnityEngine.AI.NavMeshAgent>();
                                bool wasEnabled = agent != null && agent.enabled;
                                if (agent != null) agent.enabled = false;
                                
                                // 실제 transform.position을 대기공간 좌표로 설정
                                players[enemyTeamHorse].transform.position = initialPosVector;
                                players[enemyTeamHorse].gameObject.SetActive(true);
                                
                                // 위치 설정 후 다시 활성화하고 Warp로 위치 고정
                                if (agent != null && wasEnabled)
                                {
                                    agent.enabled = true;
                                    agent.Warp(initialPosVector); // NavMesh 위로 강제 이동
                                }
                            }
                        }
                        
                        // 잡힌 말들만 UI 업데이트 (기존 대기공간 말들은 건드리지 않음)
                        if (horseManager != null)
                        {
                            foreach (int enemyTeamHorse in enemyTeamHorses)
                            {
                                horseManager.UpdateHorseUI(enemyTeamHorse, -1, positionNames);
                            }
                        }
                    }
                }
                
                // 도착한 발판에 같은 팀 말이 있으면 함께 이동 목록에 추가 (새로 업힘)
                // finalPos는 이미 위에서 선언됨
                for (int j = 0; j < playerPositions.Length; j++)
                {
                    if (j != horseIndex && !horsesToMoveTogether.Contains(j) && playerPositions[j] == finalPos)
                    {
                        bool jIsMage = j < 4;
                        // 같은 팀이면 함께 이동 목록에 추가
                        if (jIsMage == isMage)
                        {
                            horsesToMoveTogether.Add(j);
                        }
                    }
                }
            }
            
            // playerPositions가 -1(대기공간)이거나 유효하지 않으면 건너뛰기
            if (playerPositions[horseIndex] < 0 || playerPositions[horseIndex] >= boardPositions.Length)
            {
                Debug.LogWarning($"MoveHorseInternal: 말 {horseIndex}의 위치 인덱스가 유효하지 않습니다: {playerPositions[horseIndex]}");
                yield break;
            }
            
            Vector3 originalPosition = boardPositions[playerPositions[horseIndex]].position;
            string positionName = positionNames[playerPositions[horseIndex]];
            string horseName = horseIndex < 4 ? $"메이지{horseIndex+1}" : $"기사{horseIndex-3}";
            
            // 같은 위치의 말들을 고려한 위치 계산 (기사 왼쪽, 메이지 오른쪽)
            Vector3 adjustedPosition = horseManager != null 
                ? horseManager.CalculateHorsePosition(horseIndex, originalPosition)
                : originalPosition;
            // NavMesh 위에 있는 유효한 위치 찾기
            Vector3 targetPosition = GetValidNavMeshPosition(adjustedPosition);
            
            if (players[horseIndex] != null)
            {
                // NavMeshAgent가 활성화되어 있는지 확인
                if (!players[horseIndex].gameObject.activeInHierarchy)
                {
                    Debug.LogWarning($"말 {horseIndex}이 비활성화되어 있습니다!");
                    yield break;
                }
                
                // PlayerController 초기화 확인
                if (!players[horseIndex].IsInitialized())
                {
                    Debug.LogWarning($"말 {horseIndex}이 초기화되지 않았습니다. 초기화 시도...");
                    players[horseIndex].InitializeAgent();
                    yield return new WaitForSeconds(0.1f);
                }
                
                // 모든 발판에서 NavMeshAgent를 사용하여 이동 (걸어가는 애니메이션)
                players[horseIndex].MoveToPosition(targetPosition);
                
                // 타임아웃 없이 도착할 때까지 계속 이동 (거리 체크와 HasReachedDestination만 사용)
                bool hasReached = false;
                
                while (!hasReached)
                {
                    if (players[horseIndex].HasReachedDestination())
                    {
                        hasReached = true;
                        break;
                    }
                    
                    // 거리 체크: 정말 가까이 도착했을 때만 도착한 것으로 간주
                    float distance = Vector3.Distance(players[horseIndex].transform.position, targetPosition);
                    float arrivalDistance = isLastStep ? 0.3f : 0.5f; // 중간 발판은 0.5f, 최종 발판은 0.3f
                    if (distance < arrivalDistance)
                    {
                        hasReached = true;
                        break;
                    }
                    
                    yield return null;
                }
                
                // 발판에 도착했는지 확인 (정말 멀리 떨어져 있을 때만 강제 이동)
                if (players[horseIndex] != null && players[horseIndex].transform != null)
                {
                    float distance = Vector3.Distance(players[horseIndex].transform.position, targetPosition);
                    // 거리가 2.0f 이상이면 강제 이동 (경로를 찾지 못한 경우)
                    if (distance > 2.0f)
                    {
                        UnityEngine.AI.NavMeshAgent agent = players[horseIndex].GetComponent<UnityEngine.AI.NavMeshAgent>();
                        bool wasEnabled = agent != null && agent.enabled;
                        if (agent != null) agent.enabled = false;
                        players[horseIndex].transform.position = targetPosition;
                        if (agent != null && wasEnabled)
                        {
                            agent.enabled = true;
                            agent.Warp(targetPosition);
                        }
                    }
                    
                    // 발판에 도착한 후 rotation 설정 (모든 발판에서)
                    // B1에 정확히 도착해서 멈출 때는 225도, 지나갈 때는 270도
                    SetHorseRotationByPosition(horseIndex, playerPositions[horseIndex], isLastStep);
                    foreach (int otherHorseIndex in horsesToMoveTogether)
                    {
                        SetHorseRotationByPosition(otherHorseIndex, playerPositions[horseIndex], isLastStep);
                    }
                    
                    // 최종 발판에서만 같은 위치의 모든 말들 위치 재계산 및 UI 업데이트
                    if (isLastStep && horseManager != null)
                    {
                        horseManager.RefreshHorsesAtPosition(playerPositions[horseIndex], positionNames);
                    }
                    
                                        // 황금 발판인지 확인 (최종 발판에서만)
                    if (isLastStep && platformManager != null && playerPositions[horseIndex] == platformManager.GoldenPlatformIndex)
                    {
                        platformManager.RestorePlatformColor(platformManager.GoldenPlatformIndex);
                        
                        // 랜덤 효과 부여
                        if (goldenEffectManager != null)
                        {
                            GoldenEffectType effect = goldenEffectManager.GrantRandomEffect(horseIndex);
                            string effectName = GoldenPlatformEffect.GetEffectName(effect);
                            
                            // 황금 발판 효과 메시지 표시
                            if (resultText != null)
                            {
                                resultText.text = $"황금 발판 효과! [{effectName}] 획득!";
                            }
                            
                            Debug.Log($"[황금발판] 말 {horseIndex}이(가) [{effectName}] 효과 획득! hasExtraThrow={hasExtraThrow}, canThrowAgain={canThrowAgain}");
                        }

                        
                        yield return new WaitForSeconds(0.5f); // 효과 확인 시간
                        platformManager.SelectRandomGoldenPlatform();

                    }

                }
            }
            else
            {
                Debug.LogError($"말 {horseIndex}이 연결되지 않았습니다!");
            }
            
            // 발판 간 이동 간격
            yield return new WaitForSeconds(0.1f);
        }
        
        isPlayerMoving = false;
    }
    
    // Internal 메서드로 변경 (movementManager에서 호출)
    public IEnumerator WaitForMovementInternal(PlayerController horse)
    {
        if (horse == null) yield break;
        
        float timeout = 3f;
        float elapsed = 0f;
        
        while (elapsed < timeout)
        {
            if (horse.HasReachedDestination()) break;
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if (elapsed >= timeout)
        {
            // 타임아웃 발생 (로그 제거)
        }
    }
    
    void InitializePlatformRenderers()
    {
        // platformManager로 위임
        if (platformManager != null)
        {
            platformManager.InitializePlatformRenderers();
        }
    }
    
    void SelectRandomGoldenPlatform()
    {
        // platformManager로 위임
        if (platformManager != null)
        {
            platformManager.SelectRandomGoldenPlatform();
        }
    }
    
    void MakePlatformGolden(int index)
    {
        // platformManager로 위임
        if (platformManager != null)
        {
            platformManager.MakePlatformGolden(index);
        }
    }
    
    void RestorePlatformColor(int index)
    {
        // platformManager로 위임
        if (platformManager != null)
        {
            platformManager.RestorePlatformColor(index);
        }
    }
    
    Vector3 CalculateHorsePosition(int horseIndex, Vector3 basePosition)
    {
        // horseManager로 위임
        if (horseManager != null)
        {
            return horseManager.CalculateHorsePosition(horseIndex, basePosition);
        }
        return basePosition;
    }
    
    void UpdateHorseCountUI(int horseIndex, int count)
    {
        // horseManager로 위임
        if (horseManager != null)
        {
            horseManager.UpdateHorseCountUI(horseIndex, count);
        }
    }
    
    // 같은 위치의 모든 말들 위치 재계산 및 UI 업데이트
    void RefreshHorsesAtPosition(int positionIndex)
    {
        // horseManager로 위임
        if (horseManager != null)
        {
            horseManager.RefreshHorsesAtPosition(positionIndex, positionNames);
        }
    }
    
    // Billboard 컴포넌트 (카메라를 향함)
    [System.Serializable]
    public class Billboard : MonoBehaviour
    {
        [System.NonSerialized]
        public Camera targetCamera;
        
        void LateUpdate()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
            
            if (targetCamera != null)
            {
                transform.LookAt(transform.position + targetCamera.transform.rotation * Vector3.forward,
                                targetCamera.transform.rotation * Vector3.up);
            }
        }
    }
    
    string OutcomeToKorean(YutOutcome outcome)
    {
        return YutGameUtils.OutcomeToKorean(outcome);
    }
    
    // NavMesh 위에 있는 유효한 위치 찾기
    Vector3 GetValidNavMeshPosition(Vector3 originalPosition)
    {
        return YutGameUtils.GetValidNavMeshPosition(originalPosition);
    }
    
    public void UpdateUI()
    {
        if (positionText != null)
        {
            string positionInfo = "score\n\n";
            positionInfo += $"기사 {knightFinishedCount}\n";
            positionInfo += $"마술사 {mageFinishedCount}";
            
            positionText.text = positionInfo;
        }

        
        if (resultText != null)
        {
            int nextPlayerIndex = GetCurrentPlayerIndex();
            string nextPlayerName = playerNames[nextPlayerIndex];
            string turnInfo = $"다음 차례: {nextPlayerName} 팀";
            if (canThrowAgain)
            {
                turnInfo += " (한 번 더 던지세요!)";
            }
            resultText.text = turnInfo;
        }
    }

    // 빽도 판정 메서드
    public bool IsBackDo(YutOutcome outcome)
    {
        if (outcome != YutOutcome.Do) return false;
        
        if (yutThrowController != null)
        {
            return yutThrowController.IsBackDo();
        }
        
        return false;
    }

    // 뒤로 이동하는 메서드
    // Internal 메서드로 변경 (movementManager에서 호출)
    public IEnumerator MoveHorseBackwardInternal(int horseIndex, int steps)
    {
        // Null 체크
        if (players == null || boardPositions == null || positionNames == null || playerPositions == null)
        {
            Debug.LogError("MoveHorseBackward: 필수 배열이 null입니다!");
            yield break;
        }
        
        // 인덱스 범위 확인
        if (horseIndex < 0 || horseIndex >= players.Length)
        {
            Debug.LogError($"잘못된 말 인덱스: {horseIndex}, players.Length: {players.Length}");
            yield break;
        }
        
        if (horseIndex >= playerPositions.Length)
        {
            Debug.LogError($"잘못된 말 인덱스: {horseIndex}, playerPositions.Length: {playerPositions.Length}");
            yield break;
        }
        
        // 시작 위치에 있으면 뒤로 갈 수 없음
        if (playerPositions[horseIndex] <= 0)
        {
            yield break;
        }
        
        isPlayerMoving = true;
        
        // 이동할 말 목록 (업힌 말들)
        // 시작 위치에 같은 팀 말이 있고, 이미 업힌 상태(x2 UI가 있음)인지 확인
        List<int> horsesToMoveTogether = new List<int>();
        int startPosition = playerPositions[horseIndex];
        bool isMage = horseIndex < 4;
        
        // 대기공간(-1)에서 시작하는 경우, 대기공간의 다른 말들을 업지 않음
        if (startPosition != -1)
        {
            // 시작 위치의 같은 팀 말들 찾기 (대기공간 제외)
        List<int> sameTeamHorsesAtStart = new List<int>();
        for (int j = 0; j < playerPositions.Length; j++)
        {
                if (j != horseIndex && playerPositions[j] == startPosition && playerPositions[j] != -1 && playerPositions[j] != -2)
            {
                bool jIsMage = j < 4;
                if (jIsMage == isMage)
                {
                    sameTeamHorsesAtStart.Add(j);
                }
            }
        }
        
        // 같은 위치에 같은 팀 말이 2개 이상 있으면 업힌 상태
        // 첫 번째 말(인덱스가 작은 말)에 x2 UI가 있을 수 있음
        if (sameTeamHorsesAtStart.Count >= 1)
        {
            // 같은 위치의 같은 팀 말들 중 첫 번째 말 찾기
            List<int> allSameTeamHorses = new List<int>(sameTeamHorsesAtStart);
            allSameTeamHorses.Add(horseIndex);
            allSameTeamHorses.Sort();
            
            int firstHorse = allSameTeamHorses[0];
            
            // 첫 번째 말에 x2 UI가 있으면 업힌 상태 (이미 업힌 말들)
            if (horseManager != null && horseManager.HasHorseCountUI(firstHorse))
            {
                // 업힌 말들과 함께 이동
                horsesToMoveTogether.AddRange(sameTeamHorsesAtStart);
                }
            }
        }
        
        for (int i = 0; i < steps; i++)
        {
            // 이전 발판으로 이동 (뒤로)
            playerPositions[horseIndex] = Mathf.Max(0, playerPositions[horseIndex] - 1);
            
            // 함께 이동할 말들(업힌 말들)도 같은 발판으로 이동
            foreach (int otherHorseIndex in horsesToMoveTogether)
            {
                playerPositions[otherHorseIndex] = Mathf.Max(0, playerPositions[otherHorseIndex] - 1);
            }
            
            // 마지막 발판(최종 목적지)에서만 적의 말 잡기 및 새로 업힘 처리
            // 중간 발판에서는 말이 있어도 업히지 않음
            bool isLastStep = (i == steps - 1);
            
            // 각 발판을 지나갈 때마다 rotation 설정
            // B1에 정확히 도착해서 멈출 때는 225도, 지나갈 때는 270도
            SetHorseRotationByPosition(horseIndex, playerPositions[horseIndex], isLastStep);
            foreach (int otherHorseIndex in horsesToMoveTogether)
            {
                SetHorseRotationByPosition(otherHorseIndex, playerPositions[otherHorseIndex], isLastStep);
            }
            if (isLastStep)
            {
                // 도착한 발판에 적의 말이 있으면 잡기 (같은 팀 말 처리 전에)
                int finalPos = playerPositions[horseIndex];
                List<int> enemyHorsesToCapture = new List<int>();
                for (int j = 0; j < playerPositions.Length; j++)
                {
                    if (j != horseIndex && !horsesToMoveTogether.Contains(j) && playerPositions[j] == finalPos)
                    {
                        bool jIsMage = j < 4;
                        // 적의 말이면 잡기 목록에 추가
                        if (jIsMage != isMage)
                        {
                            enemyHorsesToCapture.Add(j);
                        }
                    }
                }
                
                // 적의 말을 잡았으면 시작 위치로 보내기
                if (enemyHorsesToCapture.Count > 0)
                {
                    // 빽도로 말을 잡았을 때는 추가 던질 기회 부여
                    bool isBackDoCapture = isBackDoTurn;
                    Debug.Log($"[MoveHorseBackwardInternal] 적 말을 잡음. isBackDoTurn: {isBackDoTurn}, isBackDoCapture: {isBackDoCapture}");
                    
                    // 잡는 말의 공격 애니메이션 실행
                    if (players[horseIndex] != null)
                    {
                        PlayerController attackerController = players[horseIndex].GetComponent<PlayerController>();
                        if (attackerController != null)
                        {
                            attackerController.PlayAttackAnimation();
                        }
                    }
                    
                    foreach (int enemyHorseIndex in enemyHorsesToCapture)
                    {
                        // 해당 적의 말과 같은 위치에 있는 같은 팀 말들도 모두 찾기
                        int enemyPos = playerPositions[enemyHorseIndex];
                        List<int> enemyTeamHorses = new List<int>();
                        bool enemyIsMage = enemyHorseIndex < 4;
                        
                        for (int k = 0; k < playerPositions.Length; k++)
                        {
                            if (playerPositions[k] == enemyPos)
                            {
                                bool kIsMage = k < 4;
                                if (kIsMage == enemyIsMage)
                                {
                                    enemyTeamHorses.Add(k);
                                }
                            }
                        }
                        
                        // 적의 말(들)을 모두 시작 위치로 이동
                        foreach (int enemyTeamHorse in enemyTeamHorses)
                        {
                            Vector3 initialPosVector = horseInitialPositions[enemyTeamHorse];
                            string enemyHorseName = enemyTeamHorse < 4 ? $"메이지{enemyTeamHorse+1}" : $"기사{enemyTeamHorse-3}";
                            
                            // 잡히는 말의 죽음 애니메이션 실행
                            if (players[enemyTeamHorse] != null)
                            {
                                PlayerController victimController = players[enemyTeamHorse].GetComponent<PlayerController>();
                                if (victimController != null)
                                {
                                    victimController.PlayDeathAnimation();
                                }
                            }
                            
                            // playerPositions는 대기공간(-1)으로 설정
                            playerPositions[enemyTeamHorse] = -1;
                            if (players[enemyTeamHorse] != null)
                            {
                                // NavMeshAgent 비활성화 후 위치 설정 (y좌표 증가 방지 및 위치 고정)
                                UnityEngine.AI.NavMeshAgent agent = players[enemyTeamHorse].GetComponent<UnityEngine.AI.NavMeshAgent>();
                                bool wasEnabled = agent != null && agent.enabled;
                                if (agent != null) agent.enabled = false;
                                
                                // 실제 transform.position을 대기공간 좌표로 설정
                                players[enemyTeamHorse].transform.position = initialPosVector;
                                players[enemyTeamHorse].gameObject.SetActive(true);
                                
                                // 위치 설정 후 다시 활성화하고 Warp로 위치 고정
                                if (agent != null && wasEnabled)
                                {
                                    agent.enabled = true;
                                    agent.Warp(initialPosVector); // NavMesh 위로 강제 이동
                                }
                            }
                        }
                        
                        // 잡힌 말들만 UI 업데이트 (기존 대기공간 말들은 건드리지 않음)
                        if (horseManager != null)
                        {
                            foreach (int enemyTeamHorse in enemyTeamHorses)
                            {
                                horseManager.UpdateHorseUI(enemyTeamHorse, -1, positionNames);
                            }
                        }
                    }
                    
                    // 빽도로 말을 잡았으면 추가 던질 기회 부여
                    if (isBackDoCapture)
                    {
                        Debug.Log("[MoveHorseBackwardInternal] 빽도로 적 말을 잡아서 추가 던질 기회 부여");
                        canThrowAgain = true;
                        if (resultText != null)
                        {
                            resultText.text = "적 말을 잡았습니다! 빽도로 추가 던질 기회 획득!";
                        }
                    }
                    else
                    {
                        Debug.Log($"[MoveHorseBackwardInternal] 빽도가 아님. isBackDoCapture: {isBackDoCapture}");
                    }
                }
                
                // 도착한 발판에 같은 팀 말이 있으면 함께 이동 목록에 추가 (새로 업힘)
                // finalPos는 이미 위에서 선언됨
                for (int j = 0; j < playerPositions.Length; j++)
                {
                    if (j != horseIndex && !horsesToMoveTogether.Contains(j) && playerPositions[j] == finalPos)
                    {
                        bool jIsMage = j < 4;
                        // 같은 팀이면 함께 이동 목록에 추가
                        if (jIsMage == isMage)
                        {
                            horsesToMoveTogether.Add(j);
                        }
                    }
                }
            }
            
            // 배열 범위 확인 (대기공간 -1 포함)
            if (playerPositions[horseIndex] < -1 || playerPositions[horseIndex] >= boardPositions.Length || playerPositions[horseIndex] >= positionNames.Length)
            {
                Debug.LogError($"발판 인덱스 범위 오류: {playerPositions[horseIndex]}");
                yield break;
            }
            
            // 대기공간(-1)이면 건너뛰기
            if (playerPositions[horseIndex] == -1)
            {
                Debug.LogWarning($"MoveHorseBackwardInternal: 말 {horseIndex}이 대기공간에 있어 뒤로 이동할 수 없습니다.");
                yield break;
            }
            
            if (boardPositions[playerPositions[horseIndex]] == null)
            {
                Debug.LogError($"발판 {playerPositions[horseIndex]}가 null입니다!");
                yield break;
            }
            
            Vector3 originalPosition = boardPositions[playerPositions[horseIndex]].position;
            
            // 같은 위치의 말들을 고려한 위치 계산 (기사 왼쪽, 메이지 오른쪽)
            Vector3 adjustedPosition = horseManager != null 
                ? horseManager.CalculateHorsePosition(horseIndex, originalPosition)
                : originalPosition;
            
            // NavMesh 위에 있는 유효한 위치 찾기
            Vector3 targetPosition = GetValidNavMeshPosition(adjustedPosition);
            
            if (players[horseIndex] != null)
            {
                // NavMeshAgent가 활성화되어 있는지 확인
                if (!players[horseIndex].gameObject.activeInHierarchy)
                {
                    Debug.LogWarning($"말 {horseIndex}이 비활성화되어 있습니다!");
                    yield break;
                }
                
                // PlayerController 초기화 확인
                if (!players[horseIndex].IsInitialized())
                {
                    Debug.LogWarning($"말 {horseIndex}이 초기화되지 않았습니다. 초기화 시도...");
                    players[horseIndex].InitializeAgent();
                    yield return new WaitForSeconds(0.1f);
                }
                
                // 모든 발판에서 NavMeshAgent를 사용하여 이동 (걸어가는 애니메이션)
                players[horseIndex].MoveToPosition(targetPosition);
                
                // 타임아웃 없이 도착할 때까지 계속 이동 (거리 체크와 HasReachedDestination만 사용)
                bool hasReached = false;
                
                while (!hasReached)
                {
                    if (players[horseIndex].HasReachedDestination())
                    {
                        hasReached = true;
                        break;
                    }
                    
                    // 거리 체크: 정말 가까이 도착했을 때만 도착한 것으로 간주
                    float distance = Vector3.Distance(players[horseIndex].transform.position, targetPosition);
                    float arrivalDistance = isLastStep ? 0.3f : 0.5f; // 중간 발판은 0.5f, 최종 발판은 0.3f
                    if (distance < arrivalDistance)
                    {
                        hasReached = true;
                        break;
                    }
                    
                    yield return null;
                }
                
                // 발판에 도착했는지 확인 (정말 멀리 떨어져 있을 때만 강제 이동)
                if (players[horseIndex] != null && players[horseIndex].transform != null)
                {
                    float distance = Vector3.Distance(players[horseIndex].transform.position, targetPosition);
                    // 거리가 2.0f 이상이면 강제 이동 (경로를 찾지 못한 경우)
                    if (distance > 2.0f)
                    {
                        UnityEngine.AI.NavMeshAgent agent = players[horseIndex].GetComponent<UnityEngine.AI.NavMeshAgent>();
                        bool wasEnabled = agent != null && agent.enabled;
                        if (agent != null) agent.enabled = false;
                        players[horseIndex].transform.position = targetPosition;
                        if (agent != null && wasEnabled)
                        {
                            agent.enabled = true;
                            agent.Warp(targetPosition);
                        }
                    }
                    
                    // 발판에 도착한 후 rotation 설정 (모든 발판에서)
                    SetHorseRotationByPosition(horseIndex, playerPositions[horseIndex]);
                    foreach (int otherHorseIndex in horsesToMoveTogether)
                    {
                        SetHorseRotationByPosition(otherHorseIndex, playerPositions[horseIndex]);
                    }
                    
                    // 최종 발판에서만 같은 위치의 모든 말들 위치 재계산 및 UI 업데이트
                    if (isLastStep && horseManager != null && playerPositions[horseIndex] >= -1 && playerPositions[horseIndex] < boardPositions.Length)
                    {
                        horseManager.RefreshHorsesAtPosition(playerPositions[horseIndex], positionNames);
                    }
                }
            }
            else
            {
                Debug.LogError($"말 {horseIndex}이 연결되지 않았습니다!");
            }
            
            // 발판 간 이동 간격
            yield return new WaitForSeconds(0.1f);
        }
        
        isPlayerMoving = false;
        
        // 빽도 턴 종료 (이동 완료 후)
        if (isBackDoTurn)
        {
            isBackDoTurn = false;
        }
    }
    
    
    // 현재 플레이어의 모든 말 가져오기
    public List<int> GetAvailableHorsesForCurrentPlayer()
    {
        List<int> availableHorses = new List<int>();
        int playerIndex = GetCurrentPlayerIndex(); // 0: 메이지, 1: 기사
        
        // 메이지이면 0~3, 기사면 4~7
        int startIndex = playerIndex * 4;
        int endIndex = startIndex + 4;
        
        for (int i = startIndex; i < endIndex && i < players.Length; i++)
        {
            if (players[i] != null)
            {
                availableHorses.Add(i);
            }
        }
        
        return availableHorses;
    }
    
    // 선택 가능한 말 표시
    public void ShowSelectableHorses(List<int> horseIndices)
    {
        if (horseManager != null)
        {
            horseManager.ShowSelectableHorses(horseIndices);
            selectableHorseIndices = horseManager.GetSelectableHorseIndices();
        }
        else
        {
            HideSelectableHorses();
            selectableHorseIndices = new List<int>(horseIndices);
        }
    }
    
    // 선택 가능한 말 표시 제거
    public void HideSelectableHorses()
    {
        if (horseManager != null)
        {
            horseManager.HideSelectableHorses();
        }
        selectableHorseIndices.Clear();
    }
    
    
    // 말 이름 가져오기
    string GetHorseName(int horseIndex)
    {
        return YutGameUtils.GetHorseName(horseIndex);
    }
    
    // 이동 가능한 발판 계산 (분기점 고려)
    public List<int> CalculateAvailablePositions(int currentPosition, int moveSteps)
    {
        List<int> availablePositions = new List<int>();
        
        // 현재 위치에서 moveSteps만큼 이동하면서 경로 추적
        int currentPos = currentPosition;
        int startPos = currentPosition; // 시작 위치 저장
        int originalMoveStepsForGoal = moveSteps; // 골인 조건 확인을 위한 원래 moveSteps 저장
        
        // B1(5) 또는 EF3(22)에서 시작하는 경우, 분기 경로로 이동 (분기는 한 칸 소모하지 않음)
        // C1(10)은 F 경로로 일직선 이동 (분기도 한 칸 소모)
        // F1(25) 또는 F2(26)에서 시작하는 경우도 F 경로로 처리
        bool startedFromB1 = false;
        bool startedFromEF3 = false;
        bool isFPath = false; // F 경로인지 추적 (F1, F2, F4, F5에서 시작하거나 F 경로를 통해 이동)
        
        if (currentPos == 5) // B1에서 시작
        {
            currentPos = 20; // E1로 분기
            startedFromB1 = true;
            // B1에서 분기했으므로 루프에서는 moveSteps-1만큼만 이동
            moveSteps = moveSteps - 1;
        }
        else if (currentPos == 22) // EF3에서 시작
        {
            currentPos = 27; // F4로 분기
            startedFromEF3 = true;
            isFPath = true; // EF3에서 시작하면 F 경로
            // EF3에서 분기했으므로 루프에서는 moveSteps-1만큼만 이동
            moveSteps = moveSteps - 1;
        }
        else if (currentPos == 25 || currentPos == 26) // F1 또는 F2에서 시작
        {
            isFPath = true; // F 경로로 시작
        }
        // C1은 F 경로로 일직선 이동하므로 분기 후 루프에서 처리
        
        // C1 경로에서 시작했는지 추적 (EF3에서 분기 판단용)
        bool isC1Path = (startPos == 10);
        if (isC1Path)
        {
            isFPath = true; // C1 경로는 F 경로
        }
        
        // moveSteps가 0 이하가 되면 분기점에 도착한 것이므로 바로 반환
        if (moveSteps <= 0)
        {
            availablePositions.Add(currentPos);
            return availablePositions;
        }
        
        for (int i = 0; i < moveSteps; i++)
        {
            // 마지막 단계인지 확인 (분기점 로직 적용을 위해)
            bool isLastStep = (i == moveSteps - 1);
            
            // 다음 위치 계산
            // 분기점에서 마지막 단계로 도착하는 경우는 시작 위치가 그 분기점인 경우에만 분기
            int nextPos;
            if (currentPos == 5 && isLastStep && startPos == 5) // B1에서 시작해서 B1에 마지막 단계로 도착
            {
                nextPos = 20; // E1로 분기
            }
            // C1에서 시작하는 경우 F 경로로 일직선 이동
            else if (currentPos == 10 && i == 0) // C1에서 시작
            {
                nextPos = 25; // F1로 분기 (1칸 소모)
            }
            // C1이 최종 목적지일 때만 F1로 분기하는 로직은 MoveToSelectedPlatformInternal에서 처리
            else if (currentPos == 22 && isLastStep && startPos == 22) // EF3에서 시작해서 EF3에 마지막 단계로 도착
            {
                nextPos = 27; // F4로 분기
            }
            // C1 경로에서 EF3에 도착한 경우 F4로 분기 (일직선)
            else if (currentPos == 22 && isC1Path) // C1 경로에서 EF3에 도착한 경우 F4로 분기
            {
                nextPos = 27; // F4로 분기
                isFPath = true; // F 경로로 설정
            }
            // F 경로에서 EF3에 도착한 경우 F4로 분기
            else if (currentPos == 22 && isFPath) // F 경로에서 EF3에 도착한 경우 F4로 분기
            {
                nextPos = 27; // F4로 분기
            }
            else
            {
                // 일반 경로 또는 중간 경로
                nextPos = GetNextPositionInPath(currentPos);
                
                // F 경로 추적: F1, F2, F4, F5를 지나가면 F 경로로 설정
                if (currentPos == 25 || currentPos == 26 || currentPos == 27 || currentPos == 28 || nextPos == 25 || nextPos == 26 || nextPos == 27 || nextPos == 28)
                {
                    isFPath = true;
                }
            }
            
            currentPos = nextPos;
        }
        
        // A1(0)에 도착할 수 있는지 확인
        // 단, 시작점이 A1(0)이거나 대기공간(-1)인 경우는 제외 (시작점)
        bool canReachA1 = false;
        bool isGoalIn = false; // 골인 조건을 만족하는지
        
        if (startPos != 0 && startPos != -1)
        {
            // 실제 이동 경로를 시뮬레이션하여 A1을 지나가는지 확인
            int simPos = startPos;
            // 원래 moveSteps를 사용 (분기는 거리를 소모하지 않으므로)
            int originalMoveSteps = originalMoveStepsForGoal;
            bool passedA1 = false;
            int stepsToA1 = 0;
            bool isC1PathSim = (startPos == 10);
            bool isFPathSim = false; // F 경로인지 추적 (시뮬레이션용)
            
            // B1에서 시작한 경우 E1로 분기 (분기는 거리 소모 안 함)
            if (simPos == 5)
            {
                simPos = 20; // E1로 분기
            }
            else if (simPos == 22) // EF3에서 시작
            {
                simPos = 27; // F4로 분기 (C1 경로로 이동)
                isC1PathSim = true; // EF3에서 시작하면 C1 경로로 이동
                isFPathSim = true; // F 경로로 설정
            }
            else if (simPos == 25 || simPos == 26) // F1 또는 F2에서 시작
            {
                isFPathSim = true; // F 경로로 시작
            }
            if (isC1PathSim)
            {
                isFPathSim = true; // C1 경로는 F 경로
            }
            
            // 실제 이동 경로를 따라가면서 A1을 지나가는지 확인
            int moveCount = 0; // 실제 이동한 횟수 (분기는 제외)
            bool simStartedFromEF3 = (startPos == 22); // EF3에서 시작했는지 확인 (시뮬레이션용)
            bool simStartedFromB1 = (startPos == 5); // B1에서 시작했는지 확인 (시뮬레이션용)
            
            // B1에서 시작한 경우 이미 E1로 분기했으므로 moveCount는 0부터 시작
            // EF3에서 시작한 경우 이미 F4로 분기했으므로 moveCount는 0부터 시작
            // C1에서 시작한 경우는 분기가 1칸 소모하므로 루프에서 처리
            
            for (int i = 0; i < originalMoveSteps; i++)
            {
                if (simPos == 0) // A1에 도착
                {
                    passedA1 = true;
                    canReachA1 = true;
                    // B1이나 EF3에서 시작한 경우 분기는 거리 소모 안 하므로 moveCount가 정확
                    // C1에서 시작한 경우는 분기가 1칸 소모하므로 moveCount가 정확
                    // D 경로에서 시작한 경우도 moveCount가 정확
                    stepsToA1 = moveCount; // 실제 이동 횟수
                    break;
                }
                
                int nextSimPos;
                if (simPos == 10 && i == 0) // C1에서 시작
                {
                    nextSimPos = 25; // F1로 분기 (1칸 소모)
                    moveCount++; // C1에서 F1로 분기는 1칸 소모
                }
                else if (simPos == 22 && simStartedFromEF3 && i == 0) // EF3에서 시작 (첫 번째 단계)
                {
                    nextSimPos = 27; // F4로 분기
                    // EF3에서 F4로 분기는 거리 소모 안 함
                }
                else if (simPos == 22 && isC1PathSim && !simStartedFromEF3) // C1 경로에서 EF3에 도착 (EF3에서 시작한 경우 제외)
                {
                    nextSimPos = 27; // F4로 분기
                    isFPathSim = true; // F 경로로 설정
                    // EF3에서 F4로 분기는 거리 소모 안 함 (이미 분기 경로에 있음)
                }
                else if (simPos == 22 && isFPathSim) // F 경로에서 EF3에 도착한 경우 F4로 분기
                {
                    nextSimPos = 27; // F4로 분기
                    // EF3에서 F4로 분기는 거리 소모 안 함
                }
                else
                {
                    nextSimPos = GetNextPositionInPath(simPos);
                    moveCount++; // 일반 이동은 1칸 소모
                    
                    // F 경로 추적: F1, F2, F4, F5를 지나가면 F 경로로 설정
                    if (simPos == 25 || simPos == 26 || simPos == 27 || simPos == 28 || nextSimPos == 25 || nextSimPos == 26 || nextSimPos == 27 || nextSimPos == 28)
                    {
                        isFPathSim = true;
                    }
                }
                simPos = nextSimPos;
            }
            
            // A1을 지나갔고, A1 이후에 한 칸 이상 더 이동 가능한 경우 골인
            if (passedA1)
            {
                // B1이나 EF3에서 시작한 경우: 분기는 거리 소모 안 하므로 stepsToA1이 실제 이동 횟수
                // C1에서 시작한 경우: 분기는 1칸 소모하므로 stepsToA1이 실제 이동 횟수
                // D 경로에서 시작한 경우: stepsToA1이 실제 이동 횟수
                // A1에 도착했을 때 남은 이동 횟수 = originalMoveSteps - stepsToA1
                // 단, B1이나 EF3에서 시작한 경우 분기는 거리 소모 안 하므로 originalMoveSteps를 그대로 사용
                // C1에서 시작한 경우 분기는 1칸 소모하므로 originalMoveSteps를 그대로 사용
                // D 경로에서 시작한 경우도 originalMoveSteps를 그대로 사용
                int remainingSteps = originalMoveSteps - stepsToA1;
                // A1 이후에 한 칸 이상 더 이동 가능하면 골인 (remainingSteps > 1)
                // A1에 정확히 도착한 경우 (remainingSteps == 1)는 골인이 아님
                // D 경로에서 시작한 경우도 동일한 로직 적용
                if (remainingSteps > 1)
                {
                    isGoalIn = true;
                }
            }
        }
        
        // 골인 조건을 만족하는 경우에도 A1과 최종 도착 위치를 모두 표시
        // (골인 버튼으로 골인할 수 있도록, A1 클릭 시에는 일반 이동)
        if (isGoalIn)
        {
            // A1 추가
            if (!availablePositions.Contains(0))
            {
                availablePositions.Add(0);
            }
            // 최종 도착 위치도 추가 (골인 조건을 만족해도 일반 이동 가능)
            if (!availablePositions.Contains(currentPos))
            {
                availablePositions.Add(currentPos);
            }
            Debug.Log($"[골인 가능] A1과 최종 위치 모두 표시: {string.Join(", ", availablePositions)}");
        }
        else
        {
            // A1에 도착할 수 있으면 A1을 availablePositions에 추가 (골인 조건은 아니지만 A1에 도착 가능)
            if (canReachA1)
            {
                if (!availablePositions.Contains(0))
                {
                    availablePositions.Add(0); // A1 추가
                }
            }
            
            // 최종 도착 위치 추가
            if (!availablePositions.Contains(currentPos))
            {
                availablePositions.Add(currentPos);
            }
        }
        
        // C1이 최종 목적지인 경우 F1로 분기한 위치도 추가 (플레이어 선택용)
        // 단, C1에서 시작해서 C1에 도착하는 경우에만 F1도 추가 (예: 윷윷윷개에서 C1이 최종 목적지)
        // 다른 위치에서 C1으로 이동하는 경우는 C1만 표시
        if (currentPos == 10 && startPos == 10) // C1에서 시작해서 C1에 도착한 경우 (즉, C1이 시작 위치이자 최종 목적지)
        {
            // C1이 최종 목적지이므로 F1로 분기한 위치도 선택 가능하게 함
            availablePositions.Add(25); // F1
        }
        
        return availablePositions;
    }
    
    // 시작 위치에서 A1까지의 거리 계산
    int CalculateDistanceToA1(int startPosition)
    {
        if (startPosition == 0) return 0; // 이미 A1에 있음
        if (startPosition == -1) return 0; // 대기공간에서 시작하면 A1에서 시작
        
        int distance = 0;
        int currentPos = startPosition;
        bool isFPath = false; // F 경로인지 추적
        
        // B1에서 시작하는 경우 E1로 분기
        if (currentPos == 5)
        {
            currentPos = 20; // E1로 분기
            distance = 0; // 분기는 거리 소모 안 함
        }
        else if (currentPos == 22) // EF3에서 시작
        {
            currentPos = 27; // F4로 분기
            distance = 0; // 분기는 거리 소모 안 함
            isFPath = true; // EF3에서 시작하면 F 경로
        }
        else if (currentPos == 25 || currentPos == 26) // F1 또는 F2에서 시작
        {
            isFPath = true; // F 경로로 시작
        }
        else if (currentPos == 10) // C1에서 시작
        {
            isFPath = true; // C1 경로는 F 경로
        }
        // D1~D5에서 시작하는 경우는 일반 경로로 처리 (GetNextPositionInPath에서 D5 → A1 경로가 있음)
        
        // A1에 도착할 때까지 이동
        int maxSteps = 100; // 무한 루프 방지
        for (int i = 0; i < maxSteps; i++)
        {
            if (currentPos == 0) // A1에 도착
            {
                break;
            }
            
            int nextPos;
            if (currentPos == 10 && i == 0) // C1에서 시작
            {
                nextPos = 25; // F1로 분기
            }
            else if (currentPos == 22 && (startPosition == 10)) // C1 경로에서 EF3
            {
                nextPos = 27; // F4로 분기
                isFPath = true; // F 경로로 설정
            }
            else if (currentPos == 22 && isFPath) // F 경로에서 EF3에 도착한 경우 F4로 분기
            {
                nextPos = 27; // F4로 분기
            }
            else
            {
                nextPos = GetNextPositionInPath(currentPos);
                
                // F 경로 추적: F1, F2, F4, F5를 지나가면 F 경로로 설정
                if (currentPos == 25 || currentPos == 26 || currentPos == 27 || currentPos == 28 || nextPos == 25 || nextPos == 26 || nextPos == 27 || nextPos == 28)
                {
                    isFPath = true;
                }
            }
            
            currentPos = nextPos;
            distance++;
        }
        
        // D 경로에서 골인 확인을 위한 디버그 로그
        if (startPosition >= 15 && startPosition <= 19)
        {
            Debug.Log($"[CalculateDistanceToA1] D 경로에서 시작: {startPosition} ({positionNames[startPosition]}), A1까지 거리: {distance}");
        }
        
        return distance;
    }
    
    // 경로상 다음 위치 계산 (분기 경로 포함)
    // 발판 인덱스에 따라 말의 rotation 설정
    void SetHorseRotationByPosition(int horseIndex, int positionIndex, bool isFinalDestination = false)
    {
        if (players[horseIndex] == null || players[horseIndex].transform == null) return;
        
        float rotationY = 0f;
        bool shouldSetRotation = false;
        
        // B1(인덱스 5)에 정확히 도착해서 멈출 때는 225도, 지나갈 때는 270도
        if (positionIndex == 5)
        {
            if (isFinalDestination)
            {
                rotationY = 225f; // B1에 정확히 도착해서 멈출 때
            }
            else
            {
                rotationY = 270f; // B1을 지나갈 때
            }
            shouldSetRotation = true;
        }
        // B2~B5 (인덱스 6~9)에 도착하면 Y rotation을 270도로 설정
        else if (positionIndex >= 6 && positionIndex <= 9)
        {
            rotationY = 270f;
            shouldSetRotation = true;
        }
        // C1(인덱스 10)에 정확히 도착해서 멈출 때는 135도, 지나갈 때는 180도
        else if (positionIndex == 10)
        {
            if (isFinalDestination)
            {
                rotationY = 135f; // C1에 정확히 도착해서 멈출 때
            }
            else
            {
                rotationY = 180f; // C1을 지나갈 때
            }
            shouldSetRotation = true;
        }
        // C2~C5 (인덱스 11~14)에 도착하면 Y rotation을 180도로 설정
        else if (positionIndex >= 11 && positionIndex <= 14)
        {
            rotationY = 180f;
            shouldSetRotation = true;
        }
        // D1~D5 (인덱스 15~19)에 도착하면 Y rotation을 90도로 설정
        else if (positionIndex >= 15 && positionIndex <= 19)
        {
            rotationY = 90f;
            shouldSetRotation = true;
        }
        // E1, E2 (인덱스 20, 21)에 도착하면 Y rotation을 225도로 설정
        else if (positionIndex == 20 || positionIndex == 21)
        {
            rotationY = 225f;
            shouldSetRotation = true;
        }
        // EF3 (인덱스 22)에 도착하면 Y rotation을 135도로 설정
        else if (positionIndex == 22)
        {
            rotationY = 135f;
            shouldSetRotation = true;
        }
        // E4, E5 (인덱스 23, 24)에 도착하면 Y rotation을 225도로 설정
        else if (positionIndex == 23 || positionIndex == 24)
        {
            rotationY = 225f;
            shouldSetRotation = true;
        }
        
        if (shouldSetRotation)
        {
            Vector3 currentRotation = players[horseIndex].transform.eulerAngles;
            players[horseIndex].transform.rotation = Quaternion.Euler(currentRotation.x, rotationY, currentRotation.z);
        }
    }
    
    int GetNextPositionInPath(int currentPosition)
    {
        // B1 경로: E1(20) → E2(21) → EF3(22) → E4(23) → E5(24) → D1(15) → D2(16) → D3(17) → D4(18) → D5(19) → A1(0)
        if (currentPosition == 20) return 21; // E1 → E2
        if (currentPosition == 21) return 22; // E2 → EF3
        if (currentPosition == 22) return 23; // EF3 → E4
        if (currentPosition == 23) return 24; // E4 → E5
        if (currentPosition == 24) return 15; // E5 → D1
        if (currentPosition == 15) return 16; // D1 → D2
        if (currentPosition == 16) return 17; // D2 → D3
        if (currentPosition == 17) return 18; // D3 → D4
        if (currentPosition == 18) return 19; // D4 → D5
        if (currentPosition == 19) return 0;  // D5 → A1
        
        // C1 경로: F1(25) → F2(26) → EF3(22) → F4(27) → F5(28) → A1(0)
        // C1 경로에서만 사용되는 경로들
        if (currentPosition == 25) return 26; // F1 → F2
        if (currentPosition == 26) return 22; // F2 → EF3
        if (currentPosition == 27) return 28; // F4 → F5
        if (currentPosition == 28) return 0;  // F5 → A1
        
        // EF3는 B1 경로와 C1 경로 모두에서 사용됨
        // B1 경로: EF3 → E4
        // C1 경로: EF3 → F4 (분기 로직에서 처리)
        // 기본 경로는 B1 경로 기준
        if (currentPosition == 22) return 23; // EF3 → E4 (기본 경로, C1 경로에서는 분기 로직에서 F4로 처리)
        
        // 일반 순환 경로 (C1(10) → C2(11) 포함)
        return (currentPosition + 1) % boardPositions.Length;
    }
    
    // 선택 가능한 발판 표시
    public void ShowSelectablePlatforms(List<int> platformIndices)
    {
        if (platformManager != null)
        {
            platformManager.ShowSelectablePlatforms(platformIndices);
            // selectablePlatformIndices는 platformManager에서 관리하지만, 호환성을 위해 유지
            selectablePlatformIndices = new List<int>(platformIndices);
        }
        else
        {
            // 플랫폼 매니저가 없으면 기존 방식 사용
            HideSelectablePlatforms();
            selectablePlatformIndices = new List<int>(platformIndices);
        }
    }
    
    // 선택 가능한 발판 표시 제거
    public void HideSelectablePlatforms()
    {
        if (platformManager != null)
        {
            platformManager.HideSelectablePlatforms();
        }
        selectablePlatformIndices.Clear();
    }
    
    
    // 선택한 발판으로 이동
    // Internal 메서드로 변경 (movementManager에서 호출)
    public IEnumerator MoveToSelectedPlatformInternal(int horseIndex, int targetPlatformIndex, YutOutcome usedMovement = YutOutcome.Nak, bool forceGoalIn = false)
    {
        // 골인한 말(-2)은 이동하지 않음
        if (playerPositions[horseIndex] == -2)
        {
            Debug.LogWarning($"MoveToSelectedPlatformInternal: 말 {horseIndex}는 이미 골인했습니다. 이동하지 않습니다.");
            yield break;
        }
        
        isPlayerMoving = true;
        
        // 이동 전에 말이 숨겨져 있으면 일단 보이게 함
        if (players[horseIndex] != null && !players[horseIndex].gameObject.activeSelf)
        {
            players[horseIndex].gameObject.SetActive(true);
        }
        
        // 이동할 말 목록 (업힌 말들)
        // 시작 위치에 같은 팀 말이 있고, 이미 업힌 상태(x2 UI가 있음)인지 확인
        List<int> horsesToMoveTogether = new List<int>();
        int startPosition = playerPositions[horseIndex];
        bool isMage = horseIndex < 4;
        
        // 대기공간(-1)에서 시작하는 경우, 대기공간의 다른 말들을 업지 않음
        if (startPosition != -1)
        {
            // 시작 위치의 같은 팀 말들 찾기 (대기공간 제외)
        List<int> sameTeamHorsesAtStart = new List<int>();
        for (int j = 0; j < playerPositions.Length; j++)
        {
                if (j != horseIndex && playerPositions[j] == startPosition && playerPositions[j] != -1 && playerPositions[j] != -2)
            {
                bool jIsMage = j < 4;
                if (jIsMage == isMage)
                {
                    sameTeamHorsesAtStart.Add(j);
                }
            }
        }
        
        // 같은 위치에 같은 팀 말이 2개 이상 있으면 업힌 상태
        // 첫 번째 말(인덱스가 작은 말)에 x2 UI가 있을 수 있음
        if (sameTeamHorsesAtStart.Count >= 1)
        {
            // 같은 위치의 같은 팀 말들 중 첫 번째 말 찾기
            List<int> allSameTeamHorses = new List<int>(sameTeamHorsesAtStart);
            allSameTeamHorses.Add(horseIndex);
            allSameTeamHorses.Sort();
            
            int firstHorse = allSameTeamHorses[0];
            
            // 첫 번째 말에 x2 UI가 있으면 업힌 상태 (이미 업힌 말들)
            if (horseManager != null && horseManager.HasHorseCountUI(firstHorse))
            {
                // 업힌 말들과 함께 이동
                horsesToMoveTogether.AddRange(sameTeamHorsesAtStart);
                }
            }
        }
        
        // 현재 위치에서 목표 위치까지 이동
        int currentPosition = playerPositions[horseIndex];

        // 대기공간(-1)에서 시작하는 경우, A1(0)으로 시작
        // 실제 이동이 시작될 때만 A1로 변경
        if (currentPosition == -1)
        {
            currentPosition = 0; // A1에서 시작
            playerPositions[horseIndex] = 0;
            startPosition = 0; // 시작 위치도 업데이트
        }
        
        // C1 경로에서 시작했는지 추적 (EF3에서 분기 판단용)
        bool isC1Path = (startPosition == 10);
        // F 경로인지 추적 (F1, F2, F4, F5에서 시작하거나 F 경로를 통해 이동)
        bool isFPath = false;
        
        // F 경로로 시작하는 경우 확인
        if (startPosition == 25 || startPosition == 26 || startPosition == 27 || startPosition == 28)
        {
            isFPath = true;
        }
        if (isC1Path)
        {
            isFPath = true; // C1 경로는 F 경로
        }
        
        // 골인 조건 확인을 위한 변수들
        bool passedA1 = false; // A1을 지나쳤는지
        int stepsToA1 = 0; // A1까지의 실제 이동 횟수
        int moveSteps = 0; // 사용된 이동 칸 수
        if (usedMovement != YutOutcome.Nak)
        {
            moveSteps = YutGameUtils.GetMoveSteps((int)usedMovement);
        }
        
        // 빽도인 경우 특수 위치에서 선택한 발판으로 직접 이동
        // isBackDoTurn 플래그를 우선 확인 (YutSelectionHandler에서 설정됨)
        // 또는 usedMovement가 Do이고 특수 위치에서 특정 발판으로 이동하는 경우
        bool isBackDo = isBackDoTurn || IsBackDo(usedMovement);
        bool isSpecialBackDoPosition = (startPosition == 22 && (targetPlatformIndex == 21 || targetPlatformIndex == 26)) ||
                                       (startPosition == 15 && (targetPlatformIndex == 14 || targetPlatformIndex == 24)) ||
                                       (startPosition == 0 && (targetPlatformIndex == 19 || targetPlatformIndex == 28));
        
        // 빽도 턴이거나, Do이고 특수 위치에서 특정 발판으로 이동하는 경우 빽도로 처리
        if (isBackDo || (usedMovement == YutOutcome.Do && isSpecialBackDoPosition))
        {
            // 특수 위치에서 빽도: 선택한 발판으로 직접 이동
            // EF3(22)에서 E2(21) 또는 F2(26)로
            if (startPosition == 22 && (targetPlatformIndex == 21 || targetPlatformIndex == 26))
            {
                playerPositions[horseIndex] = targetPlatformIndex;
                // 함께 이동할 말들도 이동
                foreach (int otherHorseIndex in horsesToMoveTogether)
                {
                    playerPositions[otherHorseIndex] = targetPlatformIndex;
                }
                
                // 목적지 발판의 rotation 방향을 먼저 바라본 후 이동
                // E2(21): 225도, F2(26): 225도 (F 경로이므로 E2와 동일)
                float targetRotation = 225f;
                if (players[horseIndex] != null && players[horseIndex].transform != null)
                {
                    Vector3 currentRotation = players[horseIndex].transform.eulerAngles;
                    players[horseIndex].transform.rotation = Quaternion.Euler(currentRotation.x, targetRotation, currentRotation.z);
                }
                foreach (int otherHorseIndex in horsesToMoveTogether)
                {
                    if (players[otherHorseIndex] != null && players[otherHorseIndex].transform != null)
                    {
                        Vector3 currentRotation = players[otherHorseIndex].transform.eulerAngles;
                        players[otherHorseIndex].transform.rotation = Quaternion.Euler(currentRotation.x, targetRotation, currentRotation.z);
                    }
                }
                
                // rotation 설정 후 약간의 대기 시간 (바라보는 방향을 먼저 설정)
                yield return new WaitForSeconds(0.2f);
            }
            else if (startPosition == 15 && (targetPlatformIndex == 14 || targetPlatformIndex == 24)) // D1에서 C5 또는 E5로
            {
                playerPositions[horseIndex] = targetPlatformIndex;
                // 함께 이동할 말들도 이동
                foreach (int otherHorseIndex in horsesToMoveTogether)
                {
                    playerPositions[otherHorseIndex] = targetPlatformIndex;
                }
                
                // 목적지 발판의 rotation 방향을 먼저 바라본 후 이동
                // C5(14): 180도, E5(24): 225도
                float targetRotation = (targetPlatformIndex == 14) ? 180f : 225f;
                if (players[horseIndex] != null && players[horseIndex].transform != null)
                {
                    Vector3 currentRotation = players[horseIndex].transform.eulerAngles;
                    players[horseIndex].transform.rotation = Quaternion.Euler(currentRotation.x, targetRotation, currentRotation.z);
                }
                foreach (int otherHorseIndex in horsesToMoveTogether)
                {
                    if (players[otherHorseIndex] != null && players[otherHorseIndex].transform != null)
                    {
                        Vector3 currentRotation = players[otherHorseIndex].transform.eulerAngles;
                        players[otherHorseIndex].transform.rotation = Quaternion.Euler(currentRotation.x, targetRotation, currentRotation.z);
                    }
                }
                
                // rotation 설정 후 약간의 대기 시간 (바라보는 방향을 먼저 설정)
                yield return new WaitForSeconds(0.2f);
            }
            else if (startPosition == 0 && (targetPlatformIndex == 19 || targetPlatformIndex == 28)) // A1에서 D5 또는 F5로
            {
                playerPositions[horseIndex] = targetPlatformIndex;
                // 함께 이동할 말들도 이동
                foreach (int otherHorseIndex in horsesToMoveTogether)
                {
                    playerPositions[otherHorseIndex] = targetPlatformIndex;
                }
            }
            // 일반 빽도: 뒤로 한 칸 이동
            else
            {
                // 특수 분기점에서 빽도: 원래 분기 전 위치로 이동
                if (startPosition == 20) // E1에서 빽도
                {
                    playerPositions[horseIndex] = 5; // B1로 이동
                    foreach (int otherHorseIndex in horsesToMoveTogether)
                    {
                        playerPositions[otherHorseIndex] = 5; // B1로 이동
                    }
                }
                else if (startPosition == 25) // F1에서 빽도
                {
                    playerPositions[horseIndex] = 10; // C1로 이동
                    foreach (int otherHorseIndex in horsesToMoveTogether)
                    {
                        playerPositions[otherHorseIndex] = 10; // C1로 이동
                    }
                }
                else
                {
                    // F 경로에서 빽도: 경로를 역으로 추적
                    if (startPosition == 27) // F4에서 빽도
                    {
                        playerPositions[horseIndex] = 22; // EF3로 이동
                        foreach (int otherHorseIndex in horsesToMoveTogether)
                        {
                            playerPositions[otherHorseIndex] = 22; // EF3로 이동
                        }
                    }
                    else if (startPosition == 28) // F5에서 빽도
                    {
                        playerPositions[horseIndex] = 27; // F4로 이동
                        foreach (int otherHorseIndex in horsesToMoveTogether)
                        {
                            playerPositions[otherHorseIndex] = 27; // F4로 이동
                        }
                    }
                    else if (startPosition == 26) // F2에서 빽도
                    {
                        playerPositions[horseIndex] = 25; // F1로 이동
                        foreach (int otherHorseIndex in horsesToMoveTogether)
                        {
                            playerPositions[otherHorseIndex] = 25; // F1로 이동
                        }
                    }
                    else
                    {
                        // 일반 빽도: 뒤로 한 칸 이동
                        int newPosition = Mathf.Max(0, playerPositions[horseIndex] - 1);
                        playerPositions[horseIndex] = newPosition;
                        
                        // A2에서 빽도로 A1에 도착한 경우 골인 가능 상태로 설정
                        if (startPosition == 1 && newPosition == 0)
                        {
                            canGoalInFromA1[horseIndex] = true;
                        }
                        
                        foreach (int otherHorseIndex in horsesToMoveTogether)
                        {
                            int otherNewPosition = Mathf.Max(0, playerPositions[otherHorseIndex] - 1);
                            playerPositions[otherHorseIndex] = otherNewPosition;
                            
                            // A2에서 빽도로 A1에 도착한 경우 골인 가능 상태로 설정
                            if (startPosition == 1 && otherNewPosition == 0)
                            {
                                canGoalInFromA1[otherHorseIndex] = true;
                            }
                        }
                    }
                }
            }
            
            // 빽도 이동 애니메이션 처리
            if (players[horseIndex] != null)
            {
                Vector3 targetPosition = boardPositions[playerPositions[horseIndex]].position;
                PlayerController playerController = players[horseIndex].GetComponent<PlayerController>();
                if (playerController != null)
                {
                    playerController.MoveToPosition(targetPosition);
                    // 이동 완료 대기
                    while (!playerController.HasReachedDestination())
                    {
                        float distance = Vector3.Distance(players[horseIndex].transform.position, targetPosition);
                        if (distance < 0.3f) break;
                        yield return null;
                    }
                }
                else
                {
                    players[horseIndex].transform.position = targetPosition;
                }
            }
            
            // 함께 이동한 말들도 이동 애니메이션
            foreach (int otherHorseIndex in horsesToMoveTogether)
            {
                if (players[otherHorseIndex] != null)
                {
                    Vector3 targetPos = boardPositions[playerPositions[otherHorseIndex]].position;
                    PlayerController otherController = players[otherHorseIndex].GetComponent<PlayerController>();
                    if (otherController != null)
                    {
                        otherController.MoveToPosition(targetPos);
                        // 이동 완료 대기
                        while (!otherController.HasReachedDestination())
                        {
                            float distance = Vector3.Distance(players[otherHorseIndex].transform.position, targetPos);
                            if (distance < 0.3f) break;
                            yield return null;
                        }
                    }
                    else
                    {
                        players[otherHorseIndex].transform.position = targetPos;
                    }
                }
            }
            
            // 빽도 이동 완료 처리
            int finalPos = playerPositions[horseIndex];
            bool isBackDoMage = horseIndex < 4;
            
            // 도착한 발판에 적의 말이 있으면 잡기
            List<int> enemyHorsesToCapture = new List<int>();
            for (int j = 0; j < playerPositions.Length; j++)
            {
                if (j != horseIndex && !horsesToMoveTogether.Contains(j) && playerPositions[j] == finalPos && playerPositions[j] != -1 && playerPositions[j] != -2)
                {
                    bool jIsMage = j < 4;
                    // 적의 말이면 잡기 목록에 추가
                    if (jIsMage != isBackDoMage)
                    {
                        enemyHorsesToCapture.Add(j);
                    }
                }
            }
            
            // 적의 말 잡기 처리
            if (enemyHorsesToCapture.Count > 0)
            {
                // 잡는 말의 공격 애니메이션 실행
                if (players[horseIndex] != null)
                {
                    PlayerController attackerController = players[horseIndex].GetComponent<PlayerController>();
                    if (attackerController != null)
                    {
                        attackerController.PlayAttackAnimation();
                    }
                }
            }
            
            foreach (int enemyHorseIndex in enemyHorsesToCapture)
            {
                // 적의 말을 대기공간으로 이동
                int oldPosition = playerPositions[enemyHorseIndex];
                playerPositions[enemyHorseIndex] = -1;
                
                // 잡히는 말의 죽음 애니메이션 실행
                if (players[enemyHorseIndex] != null)
                {
                    PlayerController victimController = players[enemyHorseIndex].GetComponent<PlayerController>();
                    if (victimController != null)
                    {
                        victimController.PlayDeathAnimation();
                    }
                }
                
                // 적의 말과 같은 위치에 있던 같은 팀 말들도 대기공간으로
                bool enemyIsMage = enemyHorseIndex < 4;
                for (int k = 0; k < playerPositions.Length; k++)
                {
                    if (k != enemyHorseIndex && playerPositions[k] == oldPosition && playerPositions[k] != -1 && playerPositions[k] != -2)
                    {
                        bool kIsMage = k < 4;
                        if (kIsMage == enemyIsMage)
                        {
                            // 같은 팀 말도 죽음 애니메이션 실행
                            if (players[k] != null)
                            {
                                PlayerController victimController = players[k].GetComponent<PlayerController>();
                                if (victimController != null)
                                {
                                    victimController.PlayDeathAnimation();
                                }
                            }
                            
                            playerPositions[k] = -1;
                            if (players[k] != null && horseManager != null)
                            {
                                horseManager.UpdateHorseUI(k, -1, positionNames);
                            }
                        }
                    }
                }
                
                if (players[enemyHorseIndex] != null && horseManager != null)
                {
                    horseManager.UpdateHorseUI(enemyHorseIndex, -1, positionNames);
                }
            }
            
            // 빽도로 말을 잡았을 때는 추가 던질 기회 부여
            bool isBackDoCapture = isBackDoTurn;
            if (enemyHorsesToCapture.Count > 0 && isBackDoCapture)
            {
                canThrowAgain = true;
                if (resultText != null)
                {
                    resultText.text = "적 말을 잡았습니다! 빽도로 추가 던질 기회 획득!";
                }
            }
            
            // 같은 위치의 말들 위치 재계산 및 UI 업데이트
            if (horseManager != null)
            {
                horseManager.RefreshHorsesAtPosition(finalPos, positionNames);
            }
            
            // 사용된 이동을 pendingMovements에서 제거
            if (usedMovement != YutOutcome.Nak && turnManager != null)
            {
                turnManager.RemovePendingMovement(usedMovement);
            }
            
            // 빽도 턴 종료
            isBackDoTurn = false;
            isPlayerMoving = false;
            yield break;
        }
        
        // A1에서 시작하는 경우 골인 처리 (forceGoalIn == true이고 canGoalInFromA1 == true인 경우만)
        // 골인 버튼을 통해서만 골인 처리 (자동 골인 제거)
        if (startPosition == 0 && forceGoalIn && canGoalInFromA1[horseIndex])
        {
            int goalInMoveSteps = YutGameUtils.GetMoveSteps((int)usedMovement);
            
            // A1에서 시작해서 1칸 이상 이동하면 골인 (A1을 지나가면 골인)
            if (goalInMoveSteps >= 1)
            {
                // 골인 처리: 말 비활성화
                if (players[horseIndex] != null)
                {
                    players[horseIndex].gameObject.SetActive(false);
                }
                
                // 골인 처리 (-2: 골인 완료)
                playerPositions[horseIndex] = -2;
                canGoalInFromA1[horseIndex] = false; // 골인한 말은 플래그 리셋
                
                // 함께 이동한 말들도 골인 처리
                foreach (int otherHorseIndex in horsesToMoveTogether)
                {
                    if (players[otherHorseIndex] != null)
                    {
                        players[otherHorseIndex].gameObject.SetActive(false);
                    }
                    playerPositions[otherHorseIndex] = -2;
                    canGoalInFromA1[otherHorseIndex] = false; // 골인한 말은 플래그 리셋
                    
                    // 골인한 말 수 증가
                    if (otherHorseIndex < 4)
                    {
                        mageFinishedCount++;
                    }
                    else
                    {
                        knightFinishedCount++;
                    }
                }
                
                // 골인한 말 수 증가
                if (horseIndex < 4)
                {
                    mageFinishedCount++;
                }
                else
                {
                    knightFinishedCount++;
                }
                
                // 골인 메시지 표시
                if (resultText != null)
                {
                    string teamName = horseIndex < 4 ? "메이지" : "기사";
                    int finishedCount = horseIndex < 4 ? mageFinishedCount : knightFinishedCount;
                    resultText.text = $"{teamName} {finishedCount}마리 골인!";
                }
                
                // UI 업데이트
                UpdateUI();
                
                // 사용된 이동을 pendingMovements에서 제거 (골인 처리 후)
                if (usedMovement != YutOutcome.Nak && turnManager != null)
                {
                    turnManager.RemovePendingMovement(usedMovement);
                }
                
                // 다음 이동을 위해 상태 변수 리셋
                waitingForHorseSelection = false;
                waitingForPlatformSelection = false;
                currentHorseIndexForMove = -1;
                isBackDoTurn = false;
                currentGoalInMovement = YutOutcome.Nak;
                
                // 골인 버튼 숨김
                if (goalInButton != null)
                {
                    goalInButton.gameObject.SetActive(false);
                }
                
                isPlayerMoving = false;
                yield break; // 골인 처리 완료, 더 이상 이동하지 않음
            }
        }
        
        // 발판별로 이동 (목표 발판에 도착할 때까지)
        int maxSteps = 100; // 최대 이동 횟수 제한 (무한 루프 방지)
        int actualMoveCount = 0; // 실제 이동 횟수 추적
        for (int i = 0; i < maxSteps; i++)
        {
            // 현재 위치가 목표 발판이면 이동 종료
            if (playerPositions[horseIndex] == targetPlatformIndex)
            {
                break;
            }
            
            // 마지막 발판(최종 목적지)에서만 적의 말 잡기 및 새로 업힘 처리
            // 중간 발판에서는 말이 있어도 업히지 않음
            bool isLastStep = (playerPositions[horseIndex] == targetPlatformIndex || 
                               GetNextPositionInPath(playerPositions[horseIndex]) == targetPlatformIndex);
            
            // 다음 발판으로 이동 (분기점 고려)
            int currentPos = playerPositions[horseIndex];
            int nextPos;
            
            // B1(5) 또는 EF3(22)에서 시작하는 경우, 분기 경로로 이동
            // C1(10)은 F 경로로 일직선 이동 (분기도 한 칸 소모)
            if (currentPos == 5 && i == 0) // B1에서 시작
            {
                nextPos = 20; // E1로 분기
            }
            else if (currentPos == 10 && i == 0) // C1에서 시작 (F 경로로 일직선, 1칸 소모)
            {
                nextPos = 25; // F1로 분기
            }
            else if (currentPos == 22 && i == 0) // EF3에서 시작
            {
                nextPos = 27; // F4로 분기
            }
            else if (currentPos == 5 && isLastStep && startPosition == 5) // B1에서 시작해서 B1에 마지막 단계로 도착
            {
                nextPos = 20; // E1로 분기
            }
            else if (currentPos == 10 && isLastStep && startPosition == 10) // C1에서 시작해서 C1에 마지막 단계로 도착 (즉, C1이 최종 목적지)
            {
                nextPos = 25; // F1로 분기
            }
            // C1을 중간 경로로 거쳐가는 경우는 분기하지 않음
            else if (currentPos == 22 && isLastStep && startPosition == 22) // EF3에서 시작해서 EF3에 마지막 단계로 도착
            {
                nextPos = 27; // F4로 분기
            }
            // C1 경로에서 EF3에 도착한 경우 F4로 분기 (일직선)
            else if (currentPos == 22 && isC1Path) // C1 경로에서 EF3에 도착한 경우 F4로 분기
            {
                nextPos = 27; // F4로 분기
                isFPath = true; // F 경로로 설정
            }
            // F 경로에서 EF3에 도착한 경우 F4로 분기
            else if (currentPos == 22 && isFPath) // F 경로에서 EF3에 도착한 경우 F4로 분기
            {
                nextPos = 27; // F4로 분기
            }
            else
            {
                // 일반 경로 또는 중간 경로
                nextPos = GetNextPositionInPath(currentPos);
                
                // F 경로 추적: F1, F2, F4, F5를 지나가면 F 경로로 설정
                if (currentPos == 25 || currentPos == 26 || currentPos == 27 || currentPos == 28 || nextPos == 25 || nextPos == 26 || nextPos == 27 || nextPos == 28)
                {
                    isFPath = true;
                }
            }
            
            playerPositions[horseIndex] = nextPos;
            
            // 실제 이동 횟수 추적 (분기는 제외)
            bool isBranchMove = false;
            if (currentPos == 5 && i == 0) // B1에서 시작
            {
                isBranchMove = true; // 분기는 거리 소모 안 함
            }
            else if (currentPos == 22 && i == 0) // EF3에서 시작
            {
                isBranchMove = true; // 분기는 거리 소모 안 함
            }
            else if (currentPos == 22 && isC1Path) // C1 경로에서 EF3에 도착
            {
                isBranchMove = true; // 분기는 거리 소모 안 함
            }
            else if (currentPos == 22 && isFPath) // F 경로에서 EF3에 도착
            {
                isBranchMove = true; // 분기는 거리 소모 안 함
            }
            
            if (!isBranchMove)
            {
                actualMoveCount++; // 일반 이동은 1칸 소모
            }
            
            // A1에 도착했는지 확인 (골인 조건은 A1 이동 애니메이션 완료 후에 확인)
            if (nextPos == 0 && !passedA1)
            {
                passedA1 = true;
                stepsToA1 = actualMoveCount;
                Debug.Log($"[MoveToSelectedPlatformInternal] A1 도착. stepsToA1: {stepsToA1}, moveSteps: {moveSteps}, actualMoveCount: {actualMoveCount}");
            }
            
            // 함께 이동할 말들(업힌 말들)도 같은 발판으로 이동
            // 단, 골인한 말(-2)은 제외
            foreach (int otherHorseIndex in horsesToMoveTogether)
            {
                // 골인한 말(-2)은 이동하지 않음
                if (playerPositions[otherHorseIndex] != -2)
                {
                    playerPositions[otherHorseIndex] = nextPos;
                }
            }
            
            // 목표 발판에 도착했으면 이동 종료
            bool isCurrentStepLast = (nextPos == targetPlatformIndex);
            if (isCurrentStepLast)
            {
                isLastStep = true;
            }
            if (isLastStep)
            {
                // 도착한 발판에 적의 말이 있으면 잡기 (같은 팀 말 처리 전에)
                // 단, 골인한 말(-2)은 제외
                int finalPos = playerPositions[horseIndex];
                List<int> enemyHorsesToCapture = new List<int>();
                for (int j = 0; j < playerPositions.Length; j++)
                {
                    if (j != horseIndex && !horsesToMoveTogether.Contains(j) && playerPositions[j] == finalPos && playerPositions[j] != -2)
                    {
                        bool jIsMage = j < 4;
                        // 적의 말이면 잡기 목록에 추가
                        if (jIsMage != isMage)
                        {
                            enemyHorsesToCapture.Add(j);
                        }
                    }
                }
                
                // 적의 말을 잡았으면 시작 위치로 보내기
                if (enemyHorsesToCapture.Count > 0)
                {
                    // 말을 잡았을 때, 도/개/걸/빽도로 도착했으면 추가 던질 기회 부여 (윷/모 제외)
                    bool isDoGaeGeolBackDo = false;
                    if (usedMovement != YutOutcome.Nak)
                    {
                        int yutResult = (int)usedMovement;
                        // 도(1), 개(2), 걸(3) - 윷(4), 모(5)는 제외
                        if (yutResult >= 1 && yutResult <= 3)
                        {
                            isDoGaeGeolBackDo = true;
                        }
                        else if (IsBackDo(usedMovement))
                        {
                            isDoGaeGeolBackDo = true;
                        }
                    }
                    
                    // 잡는 말의 공격 애니메이션 실행
                    if (players[horseIndex] != null)
                    {
                        PlayerController attackerController = players[horseIndex].GetComponent<PlayerController>();
                        if (attackerController != null)
                        {
                            attackerController.PlayAttackAnimation();
                        }
                    }
                    
                    foreach (int enemyHorseIndex in enemyHorsesToCapture)
                    {
                        // 해당 적의 말과 같은 위치에 있는 같은 팀 말들도 모두 찾기
                        int enemyPos = playerPositions[enemyHorseIndex];
                        List<int> enemyTeamHorses = new List<int>();
                        bool enemyIsMage = enemyHorseIndex < 4;
                        
                        for (int k = 0; k < playerPositions.Length; k++)
                        {
                            if (playerPositions[k] == enemyPos)
                            {
                                bool kIsMage = k < 4;
                                if (kIsMage == enemyIsMage)
                                {
                                    enemyTeamHorses.Add(k);
                                }
                            }
                        }
                        
                        // 적의 말(들)을 모두 시작 위치로 이동
                        foreach (int enemyTeamHorse in enemyTeamHorses)
                        {
                            Vector3 initialPosVector = horseInitialPositions[enemyTeamHorse];
                            string enemyHorseName = enemyTeamHorse < 4 ? $"메이지{enemyTeamHorse+1}" : $"기사{enemyTeamHorse-3}";
                            
                            // 잡히는 말의 죽음 애니메이션 실행
                            if (players[enemyTeamHorse] != null)
                            {
                                PlayerController victimController = players[enemyTeamHorse].GetComponent<PlayerController>();
                                if (victimController != null)
                                {
                                    victimController.PlayDeathAnimation();
                                }
                            }
                            
                            // playerPositions는 대기공간(-1)으로 설정
                            playerPositions[enemyTeamHorse] = -1;
                            if (players[enemyTeamHorse] != null)
                            {
                                // NavMeshAgent 비활성화 후 위치 설정 (y좌표 증가 방지 및 위치 고정)
                                UnityEngine.AI.NavMeshAgent agent = players[enemyTeamHorse].GetComponent<UnityEngine.AI.NavMeshAgent>();
                                bool wasEnabled = agent != null && agent.enabled;
                                if (agent != null) agent.enabled = false;
                                
                                // 실제 transform.position을 대기공간 좌표로 설정
                                players[enemyTeamHorse].transform.position = initialPosVector;
                                players[enemyTeamHorse].gameObject.SetActive(true);
                                
                                // 위치 설정 후 다시 활성화하고 Warp로 위치 고정
                                if (agent != null && wasEnabled)
                                {
                                    agent.enabled = true;
                                    agent.Warp(initialPosVector); // NavMesh 위로 강제 이동
                                }
                            }
                        }
                        
                        // 잡힌 말들만 UI 업데이트 (기존 대기공간 말들은 건드리지 않음)
                        if (horseManager != null)
                        {
                            foreach (int enemyTeamHorse in enemyTeamHorses)
                            {
                                horseManager.UpdateHorseUI(enemyTeamHorse, -1, positionNames);
                            }
                        }
                    }
                    
                    // 말을 잡았고, 도/개/걸/빽도로 도착했으면 추가 던질 기회 부여
                    if (isDoGaeGeolBackDo)
                    {
                        canThrowAgain = true;
                        if (resultText != null)
                        {
                            string outcomeText = YutGameUtils.OutcomeToKorean(usedMovement);
                            if (IsBackDo(usedMovement))
                            {
                                outcomeText = "빽도";
                            }
                            resultText.text = $"적 말을 잡았습니다! {outcomeText}로 추가 던질 기회 획득!";
                        }
                    }
                }
                
                // 도착한 발판에 같은 팀 말이 있으면 함께 이동 목록에 추가 (새로 업힘)
                // finalPos는 이미 위에서 선언됨
                // 단, 골인한 말(-2)은 제외
                for (int j = 0; j < playerPositions.Length; j++)
                {
                    if (j != horseIndex && !horsesToMoveTogether.Contains(j) && playerPositions[j] == finalPos && playerPositions[j] != -2)
                    {
                        bool jIsMage = j < 4;
                        // 같은 팀이면 함께 이동 목록에 추가
                        if (jIsMage == isMage)
                        {
                            horsesToMoveTogether.Add(j);
                        }
                    }
                }
            }
            
            // playerPositions가 -1(대기공간)이거나 유효하지 않으면 건너뛰기
            if (playerPositions[horseIndex] < 0 || playerPositions[horseIndex] >= boardPositions.Length)
            {
                Debug.LogWarning($"MoveToSelectedPlatformInternal: 말 {horseIndex}의 위치 인덱스가 유효하지 않습니다: {playerPositions[horseIndex]}");
                yield break;
            }
            
            Vector3 originalPosition = boardPositions[playerPositions[horseIndex]].position;
            Vector3 adjustedPosition = horseManager != null 
                ? horseManager.CalculateHorsePosition(horseIndex, originalPosition)
                : CalculateHorsePosition(horseIndex, originalPosition);
            Vector3 targetPosition = GetValidNavMeshPosition(adjustedPosition);
            
            if (players[horseIndex] != null)
            {
                if (!players[horseIndex].gameObject.activeInHierarchy)
                {
                    yield break;
                }
                
                // PlayerController 초기화 확인
                if (!players[horseIndex].IsInitialized())
                {
                    players[horseIndex].InitializeAgent();
                    yield return new WaitForSeconds(0.1f);
                }
                
                // 모든 발판에서 NavMeshAgent를 사용하여 이동 (걸어가는 애니메이션)
                players[horseIndex].MoveToPosition(targetPosition);
                
                // 타임아웃 없이 도착할 때까지 계속 이동 (거리 체크와 HasReachedDestination만 사용)
                bool hasReached = false;
                
                while (!hasReached)
                {
                    if (players[horseIndex].HasReachedDestination())
                    {
                        hasReached = true;
                        break;
                    }
                    
                    // 거리 체크: 정말 가까이 도착했을 때만 도착한 것으로 간주
                    float distance = Vector3.Distance(players[horseIndex].transform.position, targetPosition);
                    float arrivalDistance = isLastStep ? 0.3f : 0.5f; // 중간 발판은 0.5f, 최종 발판은 0.3f
                    if (distance < arrivalDistance)
                    {
                        hasReached = true;
                        break;
                    }
                    
                    yield return null;
                }
                
                // 발판에 도착했는지 확인 (정말 멀리 떨어져 있을 때만 강제 이동)
                if (players[horseIndex] != null && players[horseIndex].transform != null)
                {
                    float distance = Vector3.Distance(players[horseIndex].transform.position, targetPosition);
                    // 거리가 2.0f 이상이면 강제 이동 (경로를 찾지 못한 경우)
                    if (distance > 2.0f)
                    {
                        UnityEngine.AI.NavMeshAgent agent = players[horseIndex].GetComponent<UnityEngine.AI.NavMeshAgent>();
                        bool wasEnabled = agent != null && agent.enabled;
                        if (agent != null) agent.enabled = false;
                        players[horseIndex].transform.position = targetPosition;
                        if (agent != null && wasEnabled)
                        {
                            agent.enabled = true;
                            agent.Warp(targetPosition);
                        }
                    }
                    
                    // 발판에 도착한 후 rotation 설정 (모든 발판에서)
                    // B1에 정확히 도착해서 멈출 때는 225도, 지나갈 때는 270도
                    bool isCurrentStepFinal = (playerPositions[horseIndex] == targetPlatformIndex);
                    SetHorseRotationByPosition(horseIndex, playerPositions[horseIndex], isCurrentStepFinal);
                    foreach (int otherHorseIndex in horsesToMoveTogether)
                    {
                        SetHorseRotationByPosition(otherHorseIndex, playerPositions[horseIndex], isCurrentStepFinal);
                    }
                    
                    // 최종 발판에서만 같은 위치의 모든 말들 위치 재계산 및 UI 업데이트
                    if (isLastStep && horseManager != null && playerPositions[horseIndex] >= -1 && playerPositions[horseIndex] < boardPositions.Length)
                    {
                        horseManager.RefreshHorsesAtPosition(playerPositions[horseIndex], positionNames);
                    }
                    
                    // A1에 도착했을 때 골인 조건 확인 (A1 이동 애니메이션 완료 후)
                    // forceGoalIn이 true일 때만 골인 처리 (골인 버튼 클릭 시)
                    if (playerPositions[horseIndex] == 0 && passedA1 && usedMovement != YutOutcome.Nak && startPosition != 0 && startPosition != -1)
                    {
                        // A1 이후 실제 이동 횟수 계산
                        int remainingSteps = moveSteps - stepsToA1;
                        
                        Debug.Log($"[MoveToSelectedPlatformInternal] A1 이동 완료 후 골인 조건 확인. stepsToA1: {stepsToA1}, moveSteps: {moveSteps}, remainingSteps: {remainingSteps}, forceGoalIn: {forceGoalIn}");
                        
                        // forceGoalIn이 true이고, A1 이후에 한 칸 이상 더 이동 가능하면 골인 (remainingSteps > 1)
                        // A1에 정확히 도착한 경우 (remainingSteps == 1)는 골인이 아님
                        // A1을 클릭한 경우 (forceGoalIn == false)는 일반 이동으로 처리
                        if (forceGoalIn && remainingSteps > 1)
                        {
                            // 골인 처리: 말 비활성화
                            if (players[horseIndex] != null)
                            {
                                players[horseIndex].gameObject.SetActive(false);
                            }
                            
                            // 골인 처리 (-2: 골인 완료)
                            playerPositions[horseIndex] = -2;
                            
                            // 함께 이동한 말들도 골인 처리
                            foreach (int otherHorseIndex in horsesToMoveTogether)
                            {
                                if (players[otherHorseIndex] != null)
                                {
                                    players[otherHorseIndex].gameObject.SetActive(false);
                                }
                                playerPositions[otherHorseIndex] = -2;
                                
                                // 골인한 말 수 증가
                                if (otherHorseIndex < 4)
                                {
                                    mageFinishedCount++;
                                }
                                else
                                {
                                    knightFinishedCount++;
                                }
                            }
                            
                            // 골인한 말 수 증가
                            if (horseIndex < 4)
                            {
                                mageFinishedCount++;
                            }
                            else
                            {
                                knightFinishedCount++;
                            }
                            
                            // 골인 메시지 표시
                            if (resultText != null)
                            {
                                string teamName = horseIndex < 4 ? "메이지" : "기사";
                                int finishedCount = horseIndex < 4 ? mageFinishedCount : knightFinishedCount;
                                resultText.text = $"{teamName} {finishedCount}마리 골인!";
                            }
                            
                            // UI 업데이트
                            UpdateUI();
                            
                            // 사용된 이동을 pendingMovements에서 제거 (골인 처리 후)
                            if (usedMovement != YutOutcome.Nak && turnManager != null)
                            {
                                turnManager.RemovePendingMovement(usedMovement);
                            }
                            
                            // 다음 이동을 위해 상태 변수 리셋
                            waitingForHorseSelection = false;
                            waitingForPlatformSelection = false;
                            currentHorseIndexForMove = -1;
                            isBackDoTurn = false;
                            currentGoalInMovement = YutOutcome.Nak;
                            
                            // 골인 버튼 숨김
                            if (goalInButton != null)
                            {
                                goalInButton.gameObject.SetActive(false);
                            }
                            
                            // 골인했으므로 이동 종료
                            isPlayerMoving = false;
                            
                            // 대기 중인 이동이 남아있으면 턴을 종료하지 않음
                            if (turnManager != null && turnManager.GetPendingMovements().Count > 0)
                            {
                                yield break; // 턴 종료하지 않고 리턴
                            }
                            
                            // 대기 중인 이동이 없으면 턴 종료 (YutTurnManager에서 처리)
                            yield break;
                        }
                        else
                        {
                            Debug.Log($"[MoveToSelectedPlatformInternal] A1에 정확히 도착했습니다. 골인이 아닙니다. remainingSteps: {remainingSteps}");
                        }
                    }
                }
            }
            
            yield return new WaitForSeconds(0.1f);
        }
        
        // 이동 완료
        isPlayerMoving = false;
        
        // 이동 완료 후 황금 발판 체크 (최종 도착 위치에서만)
        // 말이 최종 위치에 완전히 도착한 후에 황금발판 처리
        if (platformManager != null && playerPositions[horseIndex] >= 0 && playerPositions[horseIndex] == platformManager.GoldenPlatformIndex)
        {
            platformManager.RestorePlatformColor(platformManager.GoldenPlatformIndex);
            
            // 랜덤 효과 부여
            if (goldenEffectManager != null)
            {
                GoldenEffectType effect = goldenEffectManager.GrantRandomEffect(horseIndex);
                string effectName = GoldenPlatformEffect.GetEffectName(effect);
                            
                // 황금 발판 효과 메시지 표시
                if (resultText != null)
                {
                    resultText.text = $"황금 발판 효과! [{effectName}] 획득!";
                }
                            
                Debug.Log($"[황금발판] 말 {horseIndex}이(가) [{effectName}] 효과 획득!");
                            
                // 한 번 더 던지기 효과인 경우 즉시 버튼 활성화
                if (effect == GoldenEffectType.ExtraThrow)
                {
                    if (throwButton != null)
                    {
                        throwButton.interactable = true;
                    }
                    EnableTestButtons(true);
                }
            }
                        
            yield return new WaitForSeconds(0.5f); // 효과 확인 시간
            platformManager.SelectRandomGoldenPlatform();
        }

        
        // 사용된 이동을 pendingMovements에서 제거 (이동 완료 후 제거)
        if (usedMovement != YutOutcome.Nak && turnManager != null)
        {
            turnManager.RemovePendingMovement(usedMovement);
        }
        
        // 다음 이동을 위해 상태 변수 리셋
        waitingForHorseSelection = false;
        waitingForPlatformSelection = false;
        currentHorseIndexForMove = -1;
        isBackDoTurn = false;
        currentGoalInMovement = YutOutcome.Nak;
        
        // 골인 버튼 숨김
        if (goalInButton != null)
        {
            goalInButton.gameObject.SetActive(false);
        }
        
        // 황금 발판으로 인한 추가 던질 기회가 있으면 즉시 던지기 버튼 활성화
        if (canThrowAgain)
        {
            Debug.Log($"[황금발판] 추가 던지기 활성화! hasExtraThrow={hasExtraThrow}, canThrowAgain={canThrowAgain}");
            
            hasExtraThrow = false; // 플래그 리셋 (이미 canThrowAgain으로 전달됨)
            
            // 던지기 버튼 활성화
            if (throwButton != null)
            {
                throwButton.interactable = true;
            }
            EnableTestButtons(true);
            
            // 대기 중인 이동이 없으면 턴 종료하지 않고 던지기 대기
            if (turnManager != null && turnManager.GetPendingMovements().Count == 0)
            {
                yield break; // 턴 종료하지 않고 던지기 대기
            }
        }

        
        // 대기 중인 이동이 남아있으면 턴을 종료하지 않음 (ThrowAndMoveSequence의 while 루프가 계속 진행)
        if (turnManager != null && turnManager.GetPendingMovements().Count > 0)
        {
            yield break; // 턴 종료하지 않고 리턴
        }
        
        // 대기 중인 이동이 없고 추가 던질 기회도 없으면 턴 종료
        // 중요: MoveToSelectedPlatformInternal에서는 턴 변경을 하지 않음
        // 턴 변경은 ProcessPendingMovements 완료 후 YutTurnManager에서 처리됨
    }
    
    // 두 위치 사이의 이동 칸 수 계산
    int CalculateStepsBetween(int from, int to)
    {
        return YutGameUtils.CalculateStepsBetween(from, to, boardPositions.Length);
    }
    
    // InputHandler를 위한 public 메서드들
    public bool IsWaitingForHorseSelection()
    {
        return waitingForHorseSelection;
    }
    
    public bool IsWaitingForPlatformSelection()
    {
        return waitingForPlatformSelection;
    }
    
    public bool IsHorseSelectable(int index)
    {
        return selectableHorseIndices.Contains(index);
    }
    
    public bool IsPlatformSelectable(int index)
    {
        return selectablePlatformIndices.Contains(index);
    }
    
    public List<int> GetSelectableHorseIndices()
    {
        return selectableHorseIndices;
    }
    
    public List<int> GetSelectablePlatformIndices()
    {
        return selectablePlatformIndices;
    }
    
    // canGoalInFromA1 플래그 확인용 public 메서드
    public bool CanGoalInFromA1(int horseIndex)
    {
        if (horseIndex >= 0 && horseIndex < canGoalInFromA1.Length)
        {
            return canGoalInFromA1[horseIndex];
        }
        return false;
    }
    
    // OnHorseSelected, OnPlatformSelected, CheckBackDoSelection은 YutSelectionHandler로 이동됨
    public void OnHorseSelected(int horseIndex)
    {
        if (selectionHandler != null)
        {
            selectionHandler.OnHorseSelected(horseIndex);
        }
        else
        {
            Debug.LogError("YutSelectionHandler가 연결되지 않았습니다!");
        }
    }
    
    public void OnPlatformSelected(int platformIndex)
    {
        if (selectionHandler != null)
        {
            selectionHandler.OnPlatformSelected(platformIndex);
                }
                else
                {
            Debug.LogError("YutSelectionHandler가 연결되지 않았습니다!");
        }
    }
    
    public bool CheckBackDoSelection(GameObject hitObject)
    {
        if (selectionHandler != null)
        {
            return selectionHandler.CheckBackDoSelection(hitObject);
        }
        return false;
    }
    
    // 골인 버튼 상태 업데이트 (골인 가능한 말이 있는지 확인)
    public void UpdateGoalInButtonState()
    {
        if (goalInButton == null) return;
        
        bool canGoalIn = false;
        YutOutcome goalInMovement = YutOutcome.Nak;
        int goalInHorseIndex = -1;
        
        // pendingMovements가 있고 (말 선택 대기 중이거나 발판 선택 대기 중일 때) 확인
        if ((waitingForHorseSelection || waitingForPlatformSelection) && turnManager != null)
        {
            List<YutOutcome> pendingMovements = turnManager.GetPendingMovements();
            if (pendingMovements.Count > 0)
            {
                // 발판 선택 대기 중이면 선택된 말만 확인
                if (waitingForPlatformSelection && currentHorseIndexForMove >= 0)
                {
                    int horseIndex = currentHorseIndexForMove;
                    int currentPosition = playerPositions[horseIndex];
                    
                    // A1에 있는 canGoalInFromA1 == true인 말은 골인 가능
                    if (currentPosition == 0 && canGoalInFromA1[horseIndex])
                    {
                        // pendingMovements에서 빽도가 아닌 이동 찾기
                        foreach (YutOutcome movement in pendingMovements)
                        {
                            if (!IsBackDo(movement))
                            {
                                int moveSteps = YutGameUtils.GetMoveSteps((int)movement);
                                // A1에서 시작해서 1칸 이상 이동 가능하면 골인 가능 (A1을 지나가면 골인)
                                if (moveSteps >= 1)
                                {
                                    canGoalIn = true;
                                    goalInMovement = movement;
                                    goalInHorseIndex = horseIndex;
                                    break;
                                }
                            }
                        }
                    }
                    else if (currentPosition != 0 && currentPosition != -1)
                    {
                        // 모든 골인 가능한 이동을 수집하고, 이동 칸수가 가장 적은 것을 선택
                        YutOutcome bestMovement = YutOutcome.Nak;
                        int minMoveSteps = int.MaxValue;
                        
                        foreach (YutOutcome movement in pendingMovements)
                        {
                            if (!IsBackDo(movement))
                            {
                                int calcPosition = currentPosition == -1 ? 0 : currentPosition;
                                int moveSteps = YutGameUtils.GetMoveSteps((int)movement);
                                List<int> positions = CalculateAvailablePositions(calcPosition, moveSteps);
                                // 골인 조건: A1을 포함하고, A1 이후에 한 칸 이상 더 이동 가능한 경우
                                if (positions.Contains(0))
                                {
                                    int originalMoveSteps = moveSteps;
                                    int stepsToA1 = CalculateDistanceToA1(calcPosition);
                                    if (stepsToA1 > 0 && stepsToA1 < originalMoveSteps)
                                    {
                                        int remainingSteps = originalMoveSteps - stepsToA1;
                                        if (remainingSteps > 1)
                                        {
                                            // 이동 칸수가 더 적은 것을 선택
                                            if (originalMoveSteps < minMoveSteps)
                                            {
                                                minMoveSteps = originalMoveSteps;
                                                bestMovement = movement;
                                                canGoalIn = true;
                                                goalInMovement = movement;
                                                goalInHorseIndex = horseIndex;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                // 말 선택 대기 중이면 모든 말 확인
                else if (waitingForHorseSelection)
                {
                    List<int> availableHorses = GetAvailableHorsesForCurrentPlayer();
                    // 모든 골인 가능한 이동을 수집하고, 이동 칸수가 가장 적은 것을 선택
                    YutOutcome bestMovement = YutOutcome.Nak;
                    int bestHorseIndex = -1;
                    int minMoveSteps = int.MaxValue;
                    
                    foreach (int horseIndex in availableHorses)
                    {
                        int currentPosition = playerPositions[horseIndex];
                        if (currentPosition == -1) continue;
                        
                        // A1에 있는 canGoalInFromA1 == true인 말은 골인 가능
                        if (currentPosition == 0 && canGoalInFromA1[horseIndex])
                        {
                            // pendingMovements에서 빽도가 아닌 이동 찾기
                            foreach (YutOutcome movement in pendingMovements)
                            {
                                if (!IsBackDo(movement))
                                {
                                    int moveSteps = YutGameUtils.GetMoveSteps((int)movement);
                                    // A1에서 시작해서 1칸 이상 이동 가능하면 골인 가능 (A1을 지나가면 골인)
                                    if (moveSteps >= 1)
                                    {
                                        // 이동 칸수가 더 적은 것을 선택
                                        if (moveSteps < minMoveSteps)
                                        {
                                            minMoveSteps = moveSteps;
                                            bestMovement = movement;
                                            bestHorseIndex = horseIndex;
                                            canGoalIn = true;
                                        }
                                    }
                                }
                            }
                        }
                        else if (currentPosition != 0)
                        {
                            foreach (YutOutcome movement in pendingMovements)
                            {
                                if (!IsBackDo(movement))
                                {
                                    int calcPosition = currentPosition == -1 ? 0 : currentPosition;
                                    int moveSteps = YutGameUtils.GetMoveSteps((int)movement);
                                    List<int> positions = CalculateAvailablePositions(calcPosition, moveSteps);
                                    // 골인 조건: A1을 포함하고, A1 이후에 한 칸 이상 더 이동 가능한 경우
                                    if (positions.Contains(0))
                                    {
                                        int originalMoveSteps = moveSteps;
                                        int stepsToA1 = CalculateDistanceToA1(calcPosition);
                                        if (stepsToA1 > 0 && stepsToA1 < originalMoveSteps)
                                        {
                                            int remainingSteps = originalMoveSteps - stepsToA1;
                                            if (remainingSteps > 1)
                                            {
                                                // 이동 칸수가 더 적은 것을 선택
                                                if (originalMoveSteps < minMoveSteps)
                                                {
                                                    minMoveSteps = originalMoveSteps;
                                                    bestMovement = movement;
                                                    bestHorseIndex = horseIndex;
                                                    canGoalIn = true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    if (canGoalIn)
                    {
                        goalInMovement = bestMovement;
                        goalInHorseIndex = bestHorseIndex;
                    }
                }
            }
        }
        
        // 골인 버튼 표시/숨김
        if (canGoalIn)
        {
            goalInButton.gameObject.SetActive(true);
            goalInButton.interactable = true;
            currentGoalInMovement = goalInMovement;
            // 말 선택 대기 중이면 자동으로 말 선택할 수 있도록 horseIndex 저장하지 않음
            // (버튼 클릭 시 자동으로 말 선택)
        }
        else
        {
            goalInButton.gameObject.SetActive(false);
            currentGoalInMovement = YutOutcome.Nak;
            if (!waitingForPlatformSelection)
            {
                currentHorseIndexForMove = -1;
            }
        }
    }
    
    // 골인 버튼 클릭 핸들러
    public void OnGoalInButtonClicked()
    {
        if (!waitingForHorseSelection && !waitingForPlatformSelection)
        {
            Debug.LogWarning("골인 버튼: 말 선택 대기 중이 아닙니다.");
            return;
        }
        
        if (currentGoalInMovement == YutOutcome.Nak)
        {
            Debug.LogWarning("골인 버튼: 골인에 사용할 이동이 없습니다.");
            return;
        }
        
        int horseIndex = -1;
        
        // 말 선택 대기 중이면 자동으로 말 선택
        if (waitingForHorseSelection)
        {
            // 골인 가능한 말 찾기
            List<int> availableHorses = GetAvailableHorsesForCurrentPlayer();
            
            // A1에 있는 canGoalInFromA1 == true인 말 먼저 확인
            foreach (int hIndex in availableHorses)
            {
                int horsePos = playerPositions[hIndex];
                if (horsePos == 0 && canGoalInFromA1[hIndex])
                {
                    // A1에 있는 canGoalInFromA1 == true인 말은 골인 가능
                    int moveSteps = YutGameUtils.GetMoveSteps((int)currentGoalInMovement);
                    if (moveSteps >= 1 && !IsBackDo(currentGoalInMovement))
                    {
                        horseIndex = hIndex;
                        break;
                    }
                }
            }
            
            // A1에 있는 말을 찾지 못한 경우 다른 위치의 말 확인
            if (horseIndex < 0)
            {
                foreach (int hIndex in availableHorses)
                {
                    int horsePos = playerPositions[hIndex];
                    if (horsePos == 0 || horsePos == -1) continue;
                    
                    int calcPosition = horsePos == -1 ? 0 : horsePos;
                    int moveSteps = YutGameUtils.GetMoveSteps((int)currentGoalInMovement);
                    List<int> positions = CalculateAvailablePositions(calcPosition, moveSteps);
                    // 골인 조건: A1을 포함하고, A1 이후에 한 칸 이상 더 이동 가능한 경우
                    if (positions.Contains(0))
                    {
                        int originalMoveSteps = moveSteps;
                        int stepsToA1 = CalculateDistanceToA1(calcPosition);
                        if (stepsToA1 > 0 && stepsToA1 < originalMoveSteps)
                        {
                            int remainingSteps = originalMoveSteps - stepsToA1;
                            if (remainingSteps > 1)
                            {
                                horseIndex = hIndex;
                                break;
                            }
                        }
                    }
                }
            }
            
            if (horseIndex < 0)
            {
                Debug.LogWarning("골인 버튼: 골인 가능한 말을 찾을 수 없습니다.");
                return;
            }
            
            // 말 선택 처리
            HideSelectableHorses();
            waitingForHorseSelection = false;
            currentHorseIndexForMove = horseIndex;
        }
        else if (waitingForPlatformSelection)
        {
            horseIndex = currentHorseIndexForMove;
            if (horseIndex < 0)
            {
                Debug.LogWarning("골인 버튼: 말이 선택되지 않았습니다.");
                return;
            }
        }
        else
        {
            Debug.LogWarning("골인 버튼: 말 선택 또는 발판 선택 대기 중이 아닙니다.");
            return;
        }
        
        int currentPosition = playerPositions[horseIndex];
        
        // A1에 있는 canGoalInFromA1 == true인 말은 골인 가능 (골인 버튼 클릭 시)
        if (currentPosition == 0 && canGoalInFromA1[horseIndex])
        {
            // A1에서 골인 이동 시작 (forceGoalIn = true로 설정)
            HideSelectablePlatforms();
            waitingForPlatformSelection = false;
            
            // 골인 버튼을 통해 호출되므로 forceGoalIn = true로 설정
            StartCoroutine(MoveToSelectedPlatformInternal(horseIndex, 0, currentGoalInMovement, true));
        }
        else if (currentPosition == 0 || currentPosition == -1)
        {
            Debug.LogWarning("골인 버튼: 시작 위치가 A1이거나 대기공간입니다.");
            return;
        }
        else
        {
            // A1로 골인 이동 시작 (forceGoalIn = true로 설정)
            HideSelectablePlatforms();
            waitingForPlatformSelection = false;
            
            // 골인 버튼을 통해 호출되므로 forceGoalIn = true로 설정
            StartCoroutine(MoveToSelectedPlatformInternal(horseIndex, 0, currentGoalInMovement, true));
        }
        
        // 골인 버튼 숨김
        if (goalInButton != null)
        {
            goalInButton.gameObject.SetActive(false);
        }
        currentGoalInMovement = YutOutcome.Nak;
    }
}