using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class YutTurnManager : MonoBehaviour
{
    private YutGameManager gameManager;
    
    // 턴 관리 변수들
    [System.NonSerialized]
    private bool turnChangedInMoveToPlatform = false;
    [System.NonSerialized]
    private bool hasSavedYutOutcome = false;
    [System.NonSerialized]
    private YutOutcome savedYutOutcome = YutOutcome.Nak;
    [System.NonSerialized]
    private List<YutOutcome> pendingMovements;
    [System.NonSerialized]
    private bool rerollRequested = false;
    [System.NonSerialized]
    private GameObject rerollButton;

    
    public void Initialize(YutGameManager manager)
    {
        this.gameManager = manager;
        pendingMovements = new List<YutOutcome>();
    }
    
    public IEnumerator ThrowAndMoveSequence(YutOutcome? testOutcome = null, bool testBackDo = false)
    {
        // 추가 던질 기회가 있었다면 이번 던지기에서 사용하므로 카운트 감소
        if (gameManager.extraThrowCount > 0)
        {
            gameManager.extraThrowCount--;
            Debug.Log($"[추가 던지기] 사용! 남은 횟수: {gameManager.extraThrowCount}");
            
            // 남은 횟수가 0이면 canThrowAgain을 false로
            if (gameManager.extraThrowCount == 0)
            {
                gameManager.canThrowAgain = false;
            }
        }
        else if (gameManager.canThrowAgain)
        {
            // extraThrowCount가 0인데 canThrowAgain이 true면 리셋
            gameManager.canThrowAgain = false;
        }


        
        // 턴 변경 플래그 초기화 (새로운 턴 시작 시)
        turnChangedInMoveToPlatform = false;
        
        int currentPlayerIndex = gameManager.GetCurrentPlayerIndex();
        string[] playerNames = {"바바리안", "기사"};
        string currentPlayerName = playerNames[currentPlayerIndex];
        
        if (gameManager.throwButton != null) gameManager.throwButton.interactable = false;
        
        // 테스트 버튼 비활성화
        gameManager.EnableTestButtons(false);
        
        YutOutcome outcome;
        
        // 테스트 모드인지 확인
        if (testOutcome.HasValue)
        {
            // 테스트 모드: 물리 던지기 스킵하고 결과 직접 사용
            outcome = testOutcome.Value;
            if (testBackDo)
            {
                // 빽도는 Do로 처리하되 나중에 빽도 판정 시 true로 처리
                outcome = YutOutcome.Do;
            }
        }
        else
        {
            // 일반 모드: 물리 윷 던지기
            if (gameManager.yutThrowController == null)
            {
                Debug.LogError("YutThrowController가 연결되지 않았습니다!");
                if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                gameManager.EnableTestButtons(true);
                yield break;
            }
            
            // 1단계: 윷 던지기 및 멈출 때까지 대기
            yield return StartCoroutine(gameManager.yutThrowController.ThrowAndWait());
            outcome = gameManager.yutThrowController.LastOutcome;
        }
        
        // 윷이 완전히 멈춘 후 추가 대기 (시각적 확인) - 테스트 모드가 아닐 때만
        if (!testOutcome.HasValue)
        {
            yield return new WaitForSeconds(0.5f);
        }
        
        
        // 결과 텍스트
        if (gameManager.resultText != null)
        {
            string outcomeText = YutGameUtils.OutcomeToKorean(outcome);
            if (testBackDo)
            {
                outcomeText = "빽도";
            }
            string prefix = testOutcome.HasValue ? "[테스트] " : "";
            gameManager.resultText.text = outcome == YutOutcome.Nak
                ? $"{prefix}{currentPlayerName} 낙! (이동 없음)"
                : $"{prefix}{currentPlayerName}의 윷 결과: {outcomeText}";
        }
        
        // 낙: 저장된 윷/모가 있으면 그것을 사용, 없으면 턴 종료
        bool usedSavedOutcome = false; // 저장된 결과를 사용했는지 여부
        if (outcome == YutOutcome.Nak)
        {
            // 저장된 윷/모가 있으면 그것을 사용
            if (hasSavedYutOutcome)
            {
                outcome = savedYutOutcome;
                hasSavedYutOutcome = false;
                usedSavedOutcome = true; // 저장된 결과를 사용했음을 표시
                
                if (gameManager.resultText != null)
                {
                    string outcomeText = YutGameUtils.OutcomeToKorean(outcome);
                    gameManager.resultText.text = $"낙이 나왔지만 이전 {outcomeText}를 사용합니다!";
                }
                
                // 저장된 윷/모로 이동 처리하도록 계속 진행
            }
            else
            {
                // 저장된 윷/모가 없으면 턴 종료
                if (gameManager.yutThrowController != null)
                {
                    yield return StartCoroutine(gameManager.yutThrowController.ResetYutToStartPositionCoroutine());
                    yield return new WaitForSeconds(0.3f);
                }
                
                gameManager.canThrowAgain = false;
                gameManager.ChangeTurn();
                gameManager.UpdateUI();
                if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                gameManager.EnableTestButtons(true);
                yield break;
            }
        }
        
        int yutResult = (int)outcome; // 1~5
        int moveSteps = YutGameUtils.GetMoveSteps(yutResult);
        
        // 윷(4)/모(5): 이동 없이 한 번 더 던지기 (턴 유지), 결과 저장
        // 단, 저장된 결과를 사용한 경우에는 다시 저장하지 않고 바로 이동 처리
        if (yutResult == 4 || yutResult == 5)
        {
            // 저장된 결과를 사용한 경우 (낙 후 저장된 윷/모 사용)에는 바로 이동 처리
            if (usedSavedOutcome)
            {
                // 저장된 윷/모를 사용한 경우이므로 바로 이동 처리로 진행
                // (아래 도/개/걸 처리나 빽도 처리로 진행)
            }
            else if (hasSavedYutOutcome)
            {
                // 저장된 윷/모가 있고, 다시 윷/모가 나온 경우: 둘 다 저장하고 한 번 더 던지기
                pendingMovements.Add(savedYutOutcome);
                pendingMovements.Add(outcome);
                hasSavedYutOutcome = false;
                
                // 윷을 초기 위치로 리셋 (다시 던지기 위해)
                if (gameManager.yutThrowController != null)
                {
                    yield return StartCoroutine(gameManager.yutThrowController.ResetYutToStartPositionCoroutine());
                    yield return new WaitForSeconds(0.3f);
                }
                
                gameManager.canThrowAgain = true;
                gameManager.UpdateUI();
                if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                gameManager.EnableTestButtons(true);
                yield break;
            }
            else
            {
                // 새로운 윷/모가 나온 경우: 결과 저장하고 한 번 더 던지기
                savedYutOutcome = outcome;
                hasSavedYutOutcome = true;
                
                // 윷을 초기 위치로 리셋 (다시 던지기 위해)
                if (gameManager.yutThrowController != null)
                {
                    yield return StartCoroutine(gameManager.yutThrowController.ResetYutToStartPositionCoroutine());
                    yield return new WaitForSeconds(0.3f); // 리셋 애니메이션 대기
                }
                
                gameManager.canThrowAgain = true;
                gameManager.UpdateUI();
                if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                gameManager.EnableTestButtons(true);
                yield break;
            }
        }
        
        // 빽도 판정 (여기서 한 번만 선언)
        // 테스트 모드에서 testBackDo가 true면 빽도로 처리
        bool isBackDo = testBackDo || gameManager.IsBackDo(outcome);
        
        // 저장된 윷/모가 있고, 도/개/걸/빽도가 나왔을 때: 둘 다 이동 가능하게 (순서 자유)
        // 또는 pendingMovements에 이미 윷/모가 있고, 도/개/걸/빽도가 나왔을 때도 추가
        // 단, 빽도는 pendingMovements에 추가하지 않고 바로 빽도 처리 로직으로 감
        if (hasSavedYutOutcome)
        {
            if (isBackDo)
            {
                // 저장된 윷/모가 있고 빽도가 나온 경우
                // 발판에 말이 있는지 확인
                List<int> savedYutAvailableHorses = gameManager.GetAvailableHorsesForCurrentPlayer();
                List<int> horsesOnBoard = new List<int>();
                foreach (int horseIndex in savedYutAvailableHorses)
                {
                    int position = gameManager.playerPositions[horseIndex];
                    // 발판 위에 있는 말만 (0 이상, 완주(-2) 제외, 대기공간(-1) 제외)
                    if (position > 0 && position < gameManager.boardPositions.Length)
                    {
                        horsesOnBoard.Add(horseIndex);
                    }
                }
                
                if (horsesOnBoard.Count > 0)
                {
                    // 발판에 말이 있으면: 윷/모 먼저 처리
                    pendingMovements.Add(savedYutOutcome);
                    hasSavedYutOutcome = false;
                    
                    // 윷/모 먼저 처리
                    if (gameManager.resultText != null && pendingMovements.Count >= 1)
                    {
                        string savedText = YutGameUtils.OutcomeToKorean(pendingMovements[0]);
                        gameManager.resultText.text = $"{savedText} 이동할 수 있습니다. 이동할 말을 선택하세요";
                    }
                    
                    // 대기 중인 이동이 모두 처리될 때까지 반복
                    turnChangedInMoveToPlatform = false; // 플래그 초기화
                    yield return StartCoroutine(ProcessPendingMovements());
                    
                    // 모든 이동 완료 후 빽도 처리
                    if (turnChangedInMoveToPlatform)
                    {
                        gameManager.isPlayerMoving = false;
                        if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                        gameManager.EnableTestButtons(true);
                        yield break;
                    }
                    
                    // 추가 던질 기회가 있으면 빽도 처리를 하지 않음
                    if (gameManager.canThrowAgain)
                    {
                        if (!testOutcome.HasValue && gameManager.yutThrowController != null)
                        {
                            yield return StartCoroutine(gameManager.yutThrowController.ResetYutToStartPositionCoroutine());
                            yield return new WaitForSeconds(0.3f);
                        }
                        
                        gameManager.UpdateUI();
                        gameManager.isPlayerMoving = false;
                        if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                        gameManager.EnableTestButtons(true);
                        yield break;
                    }
                    
                    // 빽도 처리 (윷/모 처리 후)
                    // isBackDo가 여전히 true이므로 아래의 빽도 처리 블록으로 계속 진행
                    // hasSavedYutOutcome은 이미 false로 설정되어 있으므로 210줄의 if 블록을 건너뛰고
                    // 530줄의 if (isBackDo) 블록으로 진행
                }
                else
                {
                    // 발판에 말이 없으면: 무조건 윷/모 먼저 처리
                    pendingMovements.Add(savedYutOutcome);
                    hasSavedYutOutcome = false;
                    
                    // 윷/모 먼저 처리
                    if (gameManager.resultText != null && pendingMovements.Count >= 1)
                    {
                        string savedText = YutGameUtils.OutcomeToKorean(pendingMovements[0]);
                        gameManager.resultText.text = $"{savedText} 이동할 수 있습니다. 이동할 말을 선택하세요";
                    }
                    
                    // 대기 중인 이동이 모두 처리될 때까지 반복
                    turnChangedInMoveToPlatform = false; // 플래그 초기화
                    yield return StartCoroutine(ProcessPendingMovements());
                    
                    // 모든 이동 완료 후 빽도 처리
                    if (turnChangedInMoveToPlatform)
                    {
                        gameManager.isPlayerMoving = false;
                        if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                        gameManager.EnableTestButtons(true);
                        yield break;
                    }
                    
                    // 추가 던질 기회가 있으면 빽도 처리를 하지 않음
                    if (gameManager.canThrowAgain)
                    {
                        if (!testOutcome.HasValue && gameManager.yutThrowController != null)
                        {
                            yield return StartCoroutine(gameManager.yutThrowController.ResetYutToStartPositionCoroutine());
                            yield return new WaitForSeconds(0.3f);
                        }
                        
                        gameManager.UpdateUI();
                        gameManager.isPlayerMoving = false;
                        if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                        gameManager.EnableTestButtons(true);
                        yield break;
                    }
                    
                    // 빽도 처리 (윷/모 처리 후)
                    // isBackDo가 여전히 true이므로 아래의 빽도 처리 블록으로 계속 진행
                    // hasSavedYutOutcome은 이미 false로 설정되어 있으므로 210줄의 if 블록을 건너뛰고
                    // 530줄의 if (isBackDo) 블록으로 진행
                }
            }
            else
            {
                // 저장된 윷/모와 새로 나온 결과를 대기 목록에 추가 (빽도 제외)
                pendingMovements.Add(savedYutOutcome);
                pendingMovements.Add(outcome);
                hasSavedYutOutcome = false;
                
                if (gameManager.resultText != null && pendingMovements.Count >= 2)
                {
                    string savedText = YutGameUtils.OutcomeToKorean(pendingMovements[0]);
                    string newText = YutGameUtils.OutcomeToKorean(pendingMovements[1]);
                    gameManager.resultText.text = $"{savedText}와 {newText} 둘 다 이동할 수 있습니다. 이동할 말을 선택하세요";
                }
                else if (gameManager.resultText != null)
                {
                    gameManager.resultText.text = $"{YutGameUtils.OutcomeToKorean(pendingMovements[pendingMovements.Count - 1])} 이동할 수 있습니다. 이동할 말을 선택하세요";
                }
                
                // 대기 중인 이동이 모두 처리될 때까지 반복
                turnChangedInMoveToPlatform = false; // 플래그 초기화
                yield return StartCoroutine(ProcessPendingMovements());
                
                // 모든 이동 완료 후 턴 종료
                if (turnChangedInMoveToPlatform)
                {
                    gameManager.isPlayerMoving = false;
                    if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                    gameManager.EnableTestButtons(true);
                    yield break;
                }
                
                // 추가 던질 기회가 있으면 턴을 변경하지 않음
                if (gameManager.canThrowAgain)
                {
                    if (!testOutcome.HasValue && gameManager.yutThrowController != null)
                    {
                        yield return StartCoroutine(gameManager.yutThrowController.ResetYutToStartPositionCoroutine());
                        yield return new WaitForSeconds(0.3f);
                    }
                    
                    gameManager.UpdateUI();
                    gameManager.isPlayerMoving = false;
                    if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                    gameManager.EnableTestButtons(true);
                    yield break;
                }
                
                gameManager.canThrowAgain = false;
                
                if (!testOutcome.HasValue && gameManager.yutThrowController != null)
                {
                    yield return StartCoroutine(gameManager.yutThrowController.ResetYutToStartPositionCoroutine());
                    yield return new WaitForSeconds(0.3f);
                }
                
                // 턴 변경
                gameManager.ChangeTurn();
                turnChangedInMoveToPlatform = true; // 턴 변경 플래그 설정 (YutTurnManager에서 변경했으므로)
                gameManager.UpdateUI();
                gameManager.isPlayerMoving = false;
                
                if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                gameManager.EnableTestButtons(true);
                yield break;
            }
        }
        else if (pendingMovements.Count > 0 && (yutResult >= 1 && yutResult <= 3) && !isBackDo)
        {
            // pendingMovements에 이미 윷/모가 있고, 도/개/걸이 나온 경우: 추가 (빽도 제외)
            pendingMovements.Add(outcome);
            
            if (gameManager.resultText != null && pendingMovements.Count >= 2)
            {
                string savedText = YutGameUtils.OutcomeToKorean(pendingMovements[0]);
                string newText = YutGameUtils.OutcomeToKorean(pendingMovements[1]);
                gameManager.resultText.text = $"{savedText}와 {newText} 둘 다 이동할 수 있습니다. 이동할 말을 선택하세요";
            }
            else if (gameManager.resultText != null)
            {
                gameManager.resultText.text = $"{YutGameUtils.OutcomeToKorean(pendingMovements[pendingMovements.Count - 1])} 이동할 수 있습니다. 이동할 말을 선택하세요";
            }
            
            // 대기 중인 이동이 모두 처리될 때까지 반복
            turnChangedInMoveToPlatform = false; // 플래그 초기화
            yield return StartCoroutine(ProcessPendingMovements());
            
            // 모든 이동 완료 후 턴 종료
            if (turnChangedInMoveToPlatform)
            {
                gameManager.isPlayerMoving = false;
                if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                gameManager.EnableTestButtons(true);
                yield break;
            }
            
            // 추가 던질 기회가 있으면 턴을 변경하지 않음
            if (gameManager.canThrowAgain)
            {
                if (!testOutcome.HasValue && gameManager.yutThrowController != null)
                {
                    yield return StartCoroutine(gameManager.yutThrowController.ResetYutToStartPositionCoroutine());
                    yield return new WaitForSeconds(0.3f);
                }
                
                gameManager.UpdateUI();
                gameManager.isPlayerMoving = false;
                if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                gameManager.EnableTestButtons(true);
                yield break;
            }
            
            gameManager.canThrowAgain = false;
            
            if (!testOutcome.HasValue && gameManager.yutThrowController != null)
            {
                yield return StartCoroutine(gameManager.yutThrowController.ResetYutToStartPositionCoroutine());
                yield return new WaitForSeconds(0.3f);
            }
            
            // 턴 변경
            gameManager.ChangeTurn();
            turnChangedInMoveToPlatform = true; // 턴 변경 플래그 설정 (YutTurnManager에서 변경했으므로)
            gameManager.UpdateUI();
            gameManager.isPlayerMoving = false;
            
            if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
            gameManager.EnableTestButtons(true);
            yield break;
        }
        else if (pendingMovements.Count == 0 && (yutResult >= 1 && yutResult <= 3) && !isBackDo)
        {
            // pendingMovements가 비어있고 도/개/걸이 나온 경우: pendingMovements에 추가하여 처리 (빽도 제외)
            pendingMovements.Add(outcome);
            
            // 대기 중인 이동이 모두 처리될 때까지 반복
            turnChangedInMoveToPlatform = false; // 플래그 초기화
            yield return StartCoroutine(ProcessPendingMovements());
            
            // 모든 이동 완료 후 턴 종료
            if (turnChangedInMoveToPlatform)
            {
                gameManager.isPlayerMoving = false;
                if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                gameManager.EnableTestButtons(true);
                yield break;
            }
            
            // 추가 던질 기회가 있으면 턴을 변경하지 않음
            if (gameManager.canThrowAgain)
            {
                if (!testOutcome.HasValue && gameManager.yutThrowController != null)
                {
                    yield return StartCoroutine(gameManager.yutThrowController.ResetYutToStartPositionCoroutine());
                    yield return new WaitForSeconds(0.3f);
                }
                
                gameManager.UpdateUI();
                gameManager.isPlayerMoving = false;
                if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                gameManager.EnableTestButtons(true);
                yield break;
            }
            
            // MoveToSelectedPlatform에서 턴을 변경하지 않았으면 여기서 턴 변경
            gameManager.canThrowAgain = false;
            
            if (!testOutcome.HasValue && gameManager.yutThrowController != null)
            {
                yield return StartCoroutine(gameManager.yutThrowController.ResetYutToStartPositionCoroutine());
                yield return new WaitForSeconds(0.3f);
            }
            
            // 턴 변경
            gameManager.ChangeTurn();
            turnChangedInMoveToPlatform = true; // 턴 변경 플래그 설정
            gameManager.UpdateUI();
            gameManager.isPlayerMoving = false;
            if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
            gameManager.EnableTestButtons(true);
            yield break;
        }

        // 2단계: 이동 가능한 발판 표시 및 플레이어 선택 대기
        // 빽도 처리: 빽도면 뒤로 한 칸 이동 (말 선택 → 발판 선택 방식)
        if (isBackDo)
        {
            gameManager.isBackDoTurn = true;
            
            // 현재 턴의 플레이어(바바리안 또는 기사)의 모든 말 중에서 선택 가능한 말 찾기
            List<int> backDoAvailableHorses = gameManager.GetAvailableHorsesForCurrentPlayer();
            
            // 뒤로 갈 수 있는 말만 필터링 (위치가 0보다 큰 말, 발판 위에 있는 말)
            List<int> horsesCanMoveBack = new List<int>();
            foreach (int horseIndex in backDoAvailableHorses)
            {
                int position = gameManager.playerPositions[horseIndex];
                // 발판 위에 있는 말만 (0 이상, 완주(-2) 제외, 대기공간(-1) 제외)
                if (position > 0 && position < gameManager.boardPositions.Length)
                {
                    horsesCanMoveBack.Add(horseIndex);
                }
            }
            
            if (horsesCanMoveBack.Count > 0)
            {
                // 빽도도 도/개/걸처럼 말 선택 → 발판 선택 방식으로 처리
                gameManager.currentMoveSteps = 1; // 빽도는 뒤로 1칸
                
                // 말 선택 모드로 전환
                gameManager.ShowSelectableHorses(horsesCanMoveBack);
                gameManager.waitingForHorseSelection = true;
                
                if (gameManager.resultText != null)
                {
                    string playerName = playerNames[currentPlayerIndex];
                    gameManager.resultText.text = $"{playerName}의 윷 결과: 빽도 - 이동할 말을 선택하세요";
                }
                
                // 말 선택 대기
                while (gameManager.waitingForHorseSelection)
                {
                    yield return null;
                }
                
                // 발판 선택 대기
                while (gameManager.waitingForPlatformSelection)
                {
                    yield return null;
                }
                
                // 이동 완료 대기
                while (gameManager.isPlayerMoving)
                {
                    yield return null;
                }
                
                gameManager.isBackDoTurn = false;
            }
            else
            {
                // 뒤로 갈 수 있는 말이 없으면
                gameManager.isBackDoTurn = false;
                if (gameManager.resultText != null)
                {
                        gameManager.resultText.text = "??諛⑹? ?꾩씠???ъ슜! ?ㅼ떆 ?섏쭛?덈떎!";
                }
            }
            
            // 빽도 처리 완료 후 pendingMovements가 있으면 처리, 없으면 턴 종료
            if (pendingMovements.Count > 0)
            {
                // 저장된 윷/모가 있는 경우: pendingMovements 처리
                // 단, 빽도로 말을 잡아서 추가 던질 기회가 생긴 경우에는 메시지를 덮어쓰지 않음
                if (gameManager.resultText != null && pendingMovements.Count > 0 && !gameManager.canThrowAgain)
                {
                    string movementsText = string.Join(", ", pendingMovements.Select(m => YutGameUtils.OutcomeToKorean(m)));
                    gameManager.resultText.text = $"빽도 처리 완료. 이제 {movementsText} 이동할 수 있습니다.";
                }
                
                // 대기 중인 이동이 모두 처리될 때까지 반복
                turnChangedInMoveToPlatform = false; // 플래그 초기화
                yield return StartCoroutine(ProcessPendingMovements());
                
                // 모든 이동 완료 후 턴 종료
                if (turnChangedInMoveToPlatform)
                {
                    gameManager.isPlayerMoving = false;
                    if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                    gameManager.EnableTestButtons(true);
                    yield break;
                }
                
                // 추가 던질 기회가 있으면 턴을 변경하지 않음
                if (gameManager.canThrowAgain)
                {
                    if (!testOutcome.HasValue && gameManager.yutThrowController != null)
                    {
                        yield return StartCoroutine(gameManager.yutThrowController.ResetYutToStartPositionCoroutine());
                        yield return new WaitForSeconds(0.3f);
                    }
                    
                    gameManager.UpdateUI();
                    gameManager.isPlayerMoving = false;
                    if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                    gameManager.EnableTestButtons(true);
                    yield break;
                }
                
                gameManager.canThrowAgain = false;
                
                if (!testOutcome.HasValue && gameManager.yutThrowController != null)
                {
                    yield return StartCoroutine(gameManager.yutThrowController.ResetYutToStartPositionCoroutine());
                    yield return new WaitForSeconds(0.3f);
                }
                
                // 턴 변경
                gameManager.ChangeTurn();
                turnChangedInMoveToPlatform = true;
                gameManager.UpdateUI();
                gameManager.isPlayerMoving = false;
                if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                gameManager.EnableTestButtons(true);
                yield break;
            }
            else
            {
                // pendingMovements가 없으면 바로 턴 종료
                // 단, 빽도로 말을 잡아서 추가 던질 기회가 생긴 경우에는 턴을 종료하지 않음
                if (gameManager.canThrowAgain)
                {
                    if (!testOutcome.HasValue && gameManager.yutThrowController != null)
                    {
                        yield return StartCoroutine(gameManager.yutThrowController.ResetYutToStartPositionCoroutine());
                        yield return new WaitForSeconds(0.3f);
                    }
                    
                    gameManager.UpdateUI();
                    gameManager.isPlayerMoving = false;
                    if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                    gameManager.EnableTestButtons(true);
                    yield break;
                }
                
                if (gameManager.yutThrowController != null)
                {
                    yield return StartCoroutine(gameManager.yutThrowController.ResetYutToStartPositionCoroutine());
                    yield return new WaitForSeconds(0.3f);
                }
                
                gameManager.canThrowAgain = false;
                gameManager.ChangeTurn();
                gameManager.UpdateUI();
                gameManager.isPlayerMoving = false;
                if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
                gameManager.EnableTestButtons(true);
                yield break;
            }
        }
        
        // 도/개/걸: 먼저 말 선택, 그 다음 발판 선택
        gameManager.currentMoveSteps = moveSteps;
        
        // 현재 턴의 플레이어(바바리안 또는 기사)의 모든 말 중에서 선택 가능한 말 찾기
        List<int> availableHorses = gameManager.GetAvailableHorsesForCurrentPlayer();
        
        if (availableHorses.Count == 0)
        {
            Debug.LogWarning("이동 가능한 말이 없습니다.");
            if (gameManager.resultText != null)
            {
                        gameManager.resultText.text = "?ㅼ떆 ?섏?湲??ъ슜! ?룹쓣 ?ㅼ떆 ?섏쭛?덈떎!";
            }
            
            // 윷을 초기 위치로 리셋
            if (gameManager.yutThrowController != null)
            {
                yield return StartCoroutine(gameManager.yutThrowController.ResetYutToStartPositionCoroutine());
                yield return new WaitForSeconds(0.3f);
            }
            
            gameManager.canThrowAgain = false;
            gameManager.ChangeTurn();
            gameManager.UpdateUI();
            gameManager.isPlayerMoving = false;
            if (gameManager.throwButton != null) gameManager.throwButton.interactable = true;
            gameManager.EnableTestButtons(true);
            yield break;
        }
        
        // 말 선택 모드로 전환
        gameManager.ShowSelectableHorses(availableHorses);
        gameManager.waitingForHorseSelection = true;
        
        if (gameManager.resultText != null)
        {
            string playerName = playerNames[currentPlayerIndex];
            gameManager.resultText.text = $"{playerName}의 윷 결과: {YutGameUtils.OutcomeToKorean(outcome)} - 이동할 말을 선택하세요";
        }
        
        // 말 선택 대기
        while (gameManager.waitingForHorseSelection)
        {
            yield return null;
        }
        
        // 발판 선택 대기
        while (gameManager.waitingForPlatformSelection)
        {
            yield return null;
        }
        
        // 이동 완료 대기
        while (gameManager.isPlayerMoving)
        {
            yield return null;
        }
        
        // MoveToSelectedPlatform에서 턴 변경을 처리하므로 여기서는 처리하지 않음
        // pendingMovements가 비어있으면 MoveToSelectedPlatform에서 턴을 변경함
        // pendingMovements가 남아있으면 while 루프에서 계속 처리됨
        
        // 여기서는 단순히 함수 종료 (턴 변경은 MoveToSelectedPlatform에서 처리)
        yield break;
    }
    
    private IEnumerator ProcessPendingMovements()
    {
        while (pendingMovements.Count > 0)
        {
            // 상태 변수 리셋 (각 이동마다 초기화)
            gameManager.waitingForHorseSelection = false;
            gameManager.waitingForPlatformSelection = false;
            gameManager.currentHorseIndexForMove = -1;
            gameManager.isBackDoTurn = false;
            
            List<int> horsesToSelect = gameManager.GetAvailableHorsesForCurrentPlayer();
            if (horsesToSelect.Count > 0)
            {
                // 골인 가능한 말이 있는지 확인하고 골인 버튼 활성화
                gameManager.UpdateGoalInButtonState();
                
                gameManager.ShowSelectableHorses(horsesToSelect);
                gameManager.waitingForHorseSelection = true;
                
                // 말 선택 대기
                while (gameManager.waitingForHorseSelection)
                {
                    yield return null;
                }
                
                // 발판 선택 대기 (빽도도 발판 선택 방식으로 처리)
                while (gameManager.waitingForPlatformSelection)
                {
                    yield return null;
                }
                
                // 빽도인 경우: 이동 완료 후 pendingMovements에서 제거
                if (gameManager.isBackDoTurn)
                {
                    YutOutcome backDoMovement = YutOutcome.Nak;
                    foreach (YutOutcome movement in pendingMovements)
                    {
                        if (gameManager.IsBackDo(movement))
                        {
                            backDoMovement = movement;
                            break;
                        }
                    }
                    
                    if (backDoMovement != YutOutcome.Nak)
                    {
                        pendingMovements.Remove(backDoMovement);
                    }
                    gameManager.isBackDoTurn = false;
                }
                
                // 이동 완료 대기
                while (gameManager.isPlayerMoving)
                {
                    yield return null;
                }
                
                // 다음 이동을 위해 상태 변수 리셋
                gameManager.waitingForHorseSelection = false;
                gameManager.waitingForPlatformSelection = false;
                gameManager.currentHorseIndexForMove = -1;
                gameManager.isBackDoTurn = false;
            }
            else
            {
                // 이동 가능한 말이 없으면 대기 목록 모두 제거
                pendingMovements.Clear();
            }
        }
    }
    
    public List<YutOutcome> GetPendingMovements()
    {
        return pendingMovements;
    }
    
    public void RemovePendingMovement(YutOutcome movement)
    {
        if (pendingMovements.Contains(movement))
        {
            pendingMovements.Remove(movement);
        }
    }
    
    public bool GetTurnChangedInMoveToPlatform()
    {
        return turnChangedInMoveToPlatform;
    }
    
    public void SetTurnChangedInMoveToPlatform(bool value)
    {
        turnChangedInMoveToPlatform = value;
    }

    
    // ?ㅼ떆 ?섏?湲?踰꾪듉 ?쒖떆
    private IEnumerator ShowRerollButton()
    {
        rerollRequested = false;
        
        // Canvas 李얘린
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[?ㅼ떆 ?섏?湲? Canvas瑜?李얠쓣 ???놁뒿?덈떎!");
            yield break;
        }
        
        // 버튼 생성
        rerollButton = new GameObject("RerollButton");
        rerollButton.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild; 
        rerollButton.transform.SetParent(canvas.transform, false);

        // RectTransform ?ㅼ젙
        RectTransform rectTransform = rerollButton.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(0, -100);
        rectTransform.sizeDelta = new Vector2(200, 60);
        
        // Button 而댄룷?뚰듃 異붽?
        Button button = rerollButton.AddComponent<Button>();
        
        // Image 而댄룷?뚰듃 異붽? (踰꾪듉 諛곌꼍)
        UnityEngine.UI.Image image = rerollButton.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(1f, 0.8f, 0.2f); // ?⑷툑??
        
        // ?띿뒪??異붽?
        GameObject textObj = new GameObject("Text");
        textObj.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        textObj.transform.SetParent(rerollButton.transform, false);
        
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "다시 던지기";
        text.fontSize = 24;
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.Center;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        // "사용 안 함" 버튼 생성
        GameObject skipButton = new GameObject("SkipRerollButton");
        skipButton.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        skipButton.transform.SetParent(canvas.transform, false);

        RectTransform skipRect = skipButton.AddComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(0.5f, 0.5f);
        skipRect.anchorMax = new Vector2(0.5f, 0.5f);
        skipRect.pivot = new Vector2(0.5f, 0.5f);
        skipRect.anchoredPosition = new Vector2(0, -180);
        skipRect.sizeDelta = new Vector2(200, 60);
        
        Button skipBtn = skipButton.AddComponent<Button>();
        UnityEngine.UI.Image skipImage = skipButton.AddComponent<UnityEngine.UI.Image>();
        skipImage.color = new Color(0.7f, 0.7f, 0.7f); // ?뚯깋
        
        GameObject skipTextObj = new GameObject("Text");
        skipTextObj.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        skipTextObj.transform.SetParent(skipButton.transform, false);
        
        TextMeshProUGUI skipText = skipTextObj.AddComponent<TextMeshProUGUI>();
        skipText.text = "사용 안함";
        skipText.fontSize = 24;
        skipText.color = Color.black;
        skipText.alignment = TextAlignmentOptions.Center;
        
        RectTransform skipTextRect = skipTextObj.GetComponent<RectTransform>();
        skipTextRect.anchorMin = Vector2.zero;
        skipTextRect.anchorMax = Vector2.one;
        skipTextRect.sizeDelta = Vector2.zero;
        
        // 踰꾪듉 ?대┃ ?대깽??
        bool buttonClicked = false;
        button.onClick.AddListener(() => {
            rerollRequested = true;
            buttonClicked = true;
        });
        
        skipBtn.onClick.AddListener(() => {
            rerollRequested = false;
            buttonClicked = true;
        });
        
        // 踰꾪듉 ?대┃ ?湲?
        while (!buttonClicked)
        {
            yield return null;
        }
        
        // 踰꾪듉 ?쒓굅
        Destroy(rerollButton);
        Destroy(skipButton);
    }
}
