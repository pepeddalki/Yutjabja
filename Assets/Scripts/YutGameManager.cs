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
    public PlayerController[] players; // 0~3: 바바리안, 4~7: 기사
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
    
    [Header("Test Buttons (테스트용 - 나중에 삭제 가능)")]
    public Button testDoButton;      // 도 테스트 버튼
    public Button testGaeButton;    // 개 테스트 버튼
    public Button testGeolButton;   // 걸 테스트 버튼
    public Button testYutButton;    // 윷 테스트 버튼
    public Button testMoButton;     // 모 테스트 버튼
    public Button testBackDoButton; // 빽도 테스트 버튼
    
    [Header("Game State")]
    // 턴 인덱스(0~1): 0=바바리안 팀, 1=기사 팀 (기사 → 바바리안 → 기사 → 바바리안 순서)
    public int currentTurnIndex = 0; // 0: 바바리안, 1: 기사
    public int[] playerPositions; // 0~3: 바바리안, 4~7: 기사
    public bool isPlayerMoving = false;
    public bool canThrowAgain = false; // 윷/모로 인한 추가 던지기 가능 여부
    
    // 들어온 말 수 추적 (-2: 완주 완료/사라짐)
    private int barbarianFinishedCount = 0; // 바바리안 완주한 말 수
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
    // pendingMovements, savedYutOutcome, hasSavedYutOutcome, turnChangedInMoveToPlatform는 YutTurnManager로 이동됨
    [System.NonSerialized]
    private Vector3[] horseInitialPositions; // 각 말의 초기 시작 위치 (0~3: 바바리안, 4~7: 기사) - 실제 transform.position 좌표
    
    // 윷놀이 보드 경로 (29개)
    private string[] positionNames = {
        "A1", "A2", "A3", "A4", "A5",
        "B1", "B2", "B3", "B4", "B5", 
        "C1", "C2", "C3", "C4", "C5",
        "D1", "D2", "D3", "D4", "D5",
        "E1", "E2", "EF3", "E4", "E5",
        "F1", "F2", "F4", "F5"
    };
    
    private string[] playerNames = {"바바리안", "기사"};
    
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
        
        // playerPositions 배열 강제 초기화
        // -1: 대기공간, 0 이상: 발판 위치 (0=A1, 1=A2, ...), -2: 완주 완료/사라짐
        if (playerPositions == null || playerPositions.Length != 8)
        {
            playerPositions = new int[8] {-1, -1, -1, -1, -1, -1, -1, -1}; // 모든 말이 대기공간에 있음
        }
        
        // 완주한 말 수 초기화
        barbarianFinishedCount = 0;
        knightFinishedCount = 0;
        
        // 각 말의 초기 시작 위치 저장 (실제 transform.position 좌표)
        if (horseInitialPositions == null || horseInitialPositions.Length != 8)
        {
            horseInitialPositions = new Vector3[8];
        }
        
        // 각 말의 초기 위치 좌표 설정 (바바리안 1~4, 기사 1~4 순서)
        horseInitialPositions[0] = new Vector3(97.5f, 1.5f, 32f);  // 바바리안1
        horseInitialPositions[1] = new Vector3(105f, 1.5f, 32f);  // 바바리안2
        horseInitialPositions[2] = new Vector3(97.5f, 1.5f, 28f); // 바바리안3
        horseInitialPositions[3] = new Vector3(105f, 1.5f, 28f);  // 바바리안4
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
        
        // selectableHorseIndices 초기화
        if (selectableHorseIndices == null)
        {
            selectableHorseIndices = new List<int>();
        }
        
        if (throwButton != null)
        {
            throwButton.onClick.AddListener(ThrowYut);
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
        
        // horseManager 초기화
        horseManager.Initialize(playerPositions, horseInitialPositions);
        
        // 발판 렌더러 초기화 (platformManager 사용)
        platformManager.InitializePlatformRenderers();
        
        // 랜덤 황금 발판 선택 (platformManager 사용)
        platformManager.SelectRandomGoldenPlatform();
        
        UpdateUI();
    }
    
    public void ThrowYut()
    {
        string currentPlayer = currentTurnIndex == 0 ? "바바리안" : "기사";
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
    
    // 현재 턴의 플레이어(0: 바바리안, 1: 기사)
    public int GetCurrentPlayerIndex()
    {
        return currentTurnIndex; // 0: 바바리안, 1: 기사
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
        currentTurnIndex = (currentTurnIndex + 1) % 2;
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
        bool isBarbarian = horseIndex < 4;
        
        // 대기공간(-1)에서 시작하는 경우, 대기공간의 다른 말들을 업지 않음
        if (startPosition != -1)
        {
            // 시작 위치의 같은 팀 말들 찾기 (대기공간 제외)
        List<int> sameTeamHorsesAtStart = new List<int>();
        for (int j = 0; j < playerPositions.Length; j++)
        {
                if (j != horseIndex && playerPositions[j] == startPosition && playerPositions[j] != -1)
            {
                bool jIsBarbarian = j < 4;
                if (jIsBarbarian == isBarbarian)
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
            }
            else
            {
                // 일반 경로 또는 중간 경로 (EF3를 지나갈 때는 E4로 진행)
                nextPos = GetNextPositionInPath(currentPos);
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
                        barbarianFinishedCount++;
                    }
                    else
                    {
                        knightFinishedCount++;
                    }
                }
                
                // 완주한 말 수 증가
                if (horseIndex < 4)
                {
                    barbarianFinishedCount++;
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
                        bool jIsBarbarian = j < 4;
                        // 적의 말이면 잡기 목록에 추가
                        if (jIsBarbarian != isBarbarian)
                        {
                            enemyHorsesToCapture.Add(j);
                        }
                    }
                }
                
                // 적의 말을 잡았으면 시작 위치로 보내기
                if (enemyHorsesToCapture.Count > 0)
                {
                    foreach (int enemyHorseIndex in enemyHorsesToCapture)
                    {
                        // 해당 적의 말과 같은 위치에 있는 같은 팀 말들도 모두 찾기
                        int enemyPos = playerPositions[enemyHorseIndex];
                        List<int> enemyTeamHorses = new List<int>();
                        bool enemyIsBarbarian = enemyHorseIndex < 4;
                        
                        for (int k = 0; k < playerPositions.Length; k++)
                        {
                            if (playerPositions[k] == enemyPos)
                            {
                                bool kIsBarbarian = k < 4;
                                if (kIsBarbarian == enemyIsBarbarian)
                                {
                                    enemyTeamHorses.Add(k);
                                }
                            }
                        }
                        
                        // 적의 말(들)을 모두 시작 위치로 이동
                        foreach (int enemyTeamHorse in enemyTeamHorses)
                        {
                            Vector3 initialPosVector = horseInitialPositions[enemyTeamHorse];
                            string enemyHorseName = enemyTeamHorse < 4 ? $"바바리안{enemyTeamHorse+1}" : $"기사{enemyTeamHorse-3}";
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
                        bool jIsBarbarian = j < 4;
                        // 같은 팀이면 함께 이동 목록에 추가
                        if (jIsBarbarian == isBarbarian)
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
            string horseName = horseIndex < 4 ? $"바바리안{horseIndex+1}" : $"기사{horseIndex-3}";
            
            // 같은 위치의 말들을 고려한 위치 계산 (기사 왼쪽, 바바리안 오른쪽)
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
                    
                    // 최종 발판에서만 같은 위치의 모든 말들 위치 재계산 및 UI 업데이트
                    if (isLastStep && horseManager != null)
                    {
                        horseManager.RefreshHorsesAtPosition(playerPositions[horseIndex], positionNames);
                    }
                    
                    // 황금 발판인지 확인 (최종 발판에서만)
                    if (isLastStep && platformManager != null && playerPositions[horseIndex] == platformManager.GoldenPlatformIndex)
                    {
                        hasExtraThrow = true;
                        platformManager.RestorePlatformColor(platformManager.GoldenPlatformIndex);
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
            string positionInfo = "들어온 말\n\n";
            positionInfo += $"기사 : {knightFinishedCount}\n";
            positionInfo += $"바바리안 : {barbarianFinishedCount}\n";
            
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
        bool isBarbarian = horseIndex < 4;
        
        // 대기공간(-1)에서 시작하는 경우, 대기공간의 다른 말들을 업지 않음
        if (startPosition != -1)
        {
            // 시작 위치의 같은 팀 말들 찾기 (대기공간 제외)
        List<int> sameTeamHorsesAtStart = new List<int>();
        for (int j = 0; j < playerPositions.Length; j++)
        {
                if (j != horseIndex && playerPositions[j] == startPosition && playerPositions[j] != -1)
            {
                bool jIsBarbarian = j < 4;
                if (jIsBarbarian == isBarbarian)
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
            if (isLastStep)
            {
                // 도착한 발판에 적의 말이 있으면 잡기 (같은 팀 말 처리 전에)
                int finalPos = playerPositions[horseIndex];
                List<int> enemyHorsesToCapture = new List<int>();
                for (int j = 0; j < playerPositions.Length; j++)
                {
                    if (j != horseIndex && !horsesToMoveTogether.Contains(j) && playerPositions[j] == finalPos)
                    {
                        bool jIsBarbarian = j < 4;
                        // 적의 말이면 잡기 목록에 추가
                        if (jIsBarbarian != isBarbarian)
                        {
                            enemyHorsesToCapture.Add(j);
                        }
                    }
                }
                
                // 적의 말을 잡았으면 시작 위치로 보내기
                if (enemyHorsesToCapture.Count > 0)
                {
                    foreach (int enemyHorseIndex in enemyHorsesToCapture)
                    {
                        // 해당 적의 말과 같은 위치에 있는 같은 팀 말들도 모두 찾기
                        int enemyPos = playerPositions[enemyHorseIndex];
                        List<int> enemyTeamHorses = new List<int>();
                        bool enemyIsBarbarian = enemyHorseIndex < 4;
                        
                        for (int k = 0; k < playerPositions.Length; k++)
                        {
                            if (playerPositions[k] == enemyPos)
                            {
                                bool kIsBarbarian = k < 4;
                                if (kIsBarbarian == enemyIsBarbarian)
                                {
                                    enemyTeamHorses.Add(k);
                                }
                            }
                        }
                        
                        // 적의 말(들)을 모두 시작 위치로 이동
                        foreach (int enemyTeamHorse in enemyTeamHorses)
                        {
                            Vector3 initialPosVector = horseInitialPositions[enemyTeamHorse];
                            string enemyHorseName = enemyTeamHorse < 4 ? $"바바리안{enemyTeamHorse+1}" : $"기사{enemyTeamHorse-3}";
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
                        bool jIsBarbarian = j < 4;
                        // 같은 팀이면 함께 이동 목록에 추가
                        if (jIsBarbarian == isBarbarian)
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
            
            // 같은 위치의 말들을 고려한 위치 계산 (기사 왼쪽, 바바리안 오른쪽)
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
    }
    
    
    // 현재 플레이어의 모든 말 가져오기
    public List<int> GetAvailableHorsesForCurrentPlayer()
    {
        List<int> availableHorses = new List<int>();
        int playerIndex = GetCurrentPlayerIndex(); // 0: 바바리안, 1: 기사
        
        // 바바리안이면 0~3, 기사면 4~7
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
        
        // B1(5) 또는 EF3(22)에서 시작하는 경우, 분기 경로로 이동 (분기는 한 칸 소모하지 않음)
        // C1(10)은 F 경로로 일직선 이동 (분기도 한 칸 소모)
        if (currentPos == 5) // B1에서 시작
        {
            currentPos = 20; // E1로 분기
        }
        else if (currentPos == 22) // EF3에서 시작
        {
            currentPos = 27; // F4로 분기
        }
        // C1은 F 경로로 일직선 이동하므로 분기 후 루프에서 처리
        
        // C1 경로에서 시작했는지 추적 (EF3에서 분기 판단용)
        bool isC1Path = (startPos == 10);
        
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
            }
            else
            {
                // 일반 경로 또는 중간 경로
                nextPos = GetNextPositionInPath(currentPos);
            }
            
            currentPos = nextPos;
        }
        
        // 최종 도착 위치 추가
        availablePositions.Add(currentPos);
        
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
    
    // 경로상 다음 위치 계산 (분기 경로 포함)
    int GetNextPositionInPath(int currentPosition)
    {
        // B1 경로: E1(20) → E2(21) → EF3(22) → E4(23) → E5(24) → D1(15) → D2(16)
        if (currentPosition == 20) return 21; // E1 → E2
        if (currentPosition == 21) return 22; // E2 → EF3
        if (currentPosition == 22) return 23; // EF3 → E4
        if (currentPosition == 23) return 24; // E4 → E5
        if (currentPosition == 24) return 15; // E5 → D1
        if (currentPosition == 15) return 16; // D1 → D2
        
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
    public IEnumerator MoveToSelectedPlatformInternal(int horseIndex, int targetPlatformIndex, YutOutcome usedMovement = YutOutcome.Nak)
    {
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
        bool isBarbarian = horseIndex < 4;
        
        // 대기공간(-1)에서 시작하는 경우, 대기공간의 다른 말들을 업지 않음
        if (startPosition != -1)
        {
            // 시작 위치의 같은 팀 말들 찾기 (대기공간 제외)
        List<int> sameTeamHorsesAtStart = new List<int>();
        for (int j = 0; j < playerPositions.Length; j++)
        {
                if (j != horseIndex && playerPositions[j] == startPosition && playerPositions[j] != -1)
            {
                bool jIsBarbarian = j < 4;
                if (jIsBarbarian == isBarbarian)
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
        
        // 발판별로 이동 (목표 발판에 도착할 때까지)
        int maxSteps = 100; // 최대 이동 횟수 제한 (무한 루프 방지)
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
            }
            else
            {
                // 일반 경로 또는 중간 경로 (EF3를 지나갈 때는 E4로 진행)
                nextPos = GetNextPositionInPath(currentPos);
            }
            
            playerPositions[horseIndex] = nextPos;
            
            // 함께 이동할 말들(업힌 말들)도 같은 발판으로 이동
            foreach (int otherHorseIndex in horsesToMoveTogether)
            {
                playerPositions[otherHorseIndex] = nextPos;
            }
            
            // 목표 발판에 도착했으면 이동 종료
            if (nextPos == targetPlatformIndex)
            {
                isLastStep = true;
                
                // A0(인덱스 0)에 도착하면 말이 완주 완료 (사라짐)
                if (nextPos == 0)
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
                            barbarianFinishedCount++;
                        }
                        else
                        {
                            knightFinishedCount++;
                        }
                    }
                    
                    // 완주한 말 수 증가
                    if (horseIndex < 4)
                    {
                        barbarianFinishedCount++;
                    }
                    else
                    {
                        knightFinishedCount++;
                    }
                    
                    // UI 업데이트
                    UpdateUI();
                }
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
                        bool jIsBarbarian = j < 4;
                        // 적의 말이면 잡기 목록에 추가
                        if (jIsBarbarian != isBarbarian)
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
                    
                    foreach (int enemyHorseIndex in enemyHorsesToCapture)
                    {
                        // 해당 적의 말과 같은 위치에 있는 같은 팀 말들도 모두 찾기
                        int enemyPos = playerPositions[enemyHorseIndex];
                        List<int> enemyTeamHorses = new List<int>();
                        bool enemyIsBarbarian = enemyHorseIndex < 4;
                        
                        for (int k = 0; k < playerPositions.Length; k++)
                        {
                            if (playerPositions[k] == enemyPos)
                            {
                                bool kIsBarbarian = k < 4;
                                if (kIsBarbarian == enemyIsBarbarian)
                                {
                                    enemyTeamHorses.Add(k);
                                }
                            }
                        }
                        
                        // 적의 말(들)을 모두 시작 위치로 이동
                        foreach (int enemyTeamHorse in enemyTeamHorses)
                        {
                            Vector3 initialPosVector = horseInitialPositions[enemyTeamHorse];
                            string enemyHorseName = enemyTeamHorse < 4 ? $"바바리안{enemyTeamHorse+1}" : $"기사{enemyTeamHorse-3}";
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
                for (int j = 0; j < playerPositions.Length; j++)
                {
                    if (j != horseIndex && !horsesToMoveTogether.Contains(j) && playerPositions[j] == finalPos)
                    {
                        bool jIsBarbarian = j < 4;
                        // 같은 팀이면 함께 이동 목록에 추가
                        if (jIsBarbarian == isBarbarian)
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
                    
                    // 최종 발판에서만 같은 위치의 모든 말들 위치 재계산 및 UI 업데이트
                    if (isLastStep && horseManager != null && playerPositions[horseIndex] >= -1 && playerPositions[horseIndex] < boardPositions.Length)
                    {
                        horseManager.RefreshHorsesAtPosition(playerPositions[horseIndex], positionNames);
                    }
                    
                    // 황금 발판인지 확인 (최종 발판에서만)
                    if (isLastStep && platformManager != null && playerPositions[horseIndex] == platformManager.GoldenPlatformIndex)
                    {
                        hasExtraThrow = true;
                        platformManager.RestorePlatformColor(platformManager.GoldenPlatformIndex);
                        yield return new WaitForSeconds(0.5f);
                        platformManager.SelectRandomGoldenPlatform();
                    }
                }
            }
            
            yield return new WaitForSeconds(0.1f);
        }
        
        // 이동 완료
        isPlayerMoving = false;
        
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
        
        // 대기 중인 이동이 남아있으면 턴을 종료하지 않음 (ThrowAndMoveSequence의 while 루프가 계속 진행)
        if (turnManager != null && turnManager.GetPendingMovements().Count > 0)
        {
            yield break; // 턴 종료하지 않고 리턴
        }
        
        // 대기 중인 이동이 없으면 턴 종료
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
}