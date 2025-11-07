using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class YutSelectionHandler : MonoBehaviour
{
    private YutGameManager gameManager;
    
    public void Initialize(YutGameManager manager)
    {
        this.gameManager = manager;
    }
    
    public void OnHorseSelected(int horseIndex)
    {
        if (!gameManager.waitingForHorseSelection)
        {
            return;
        }
        
        if (!gameManager.IsHorseSelectable(horseIndex))
        {
            Debug.LogWarning($"말 {horseIndex}는 선택 가능한 말이 아닙니다.");
            return;
        }
        
        // 말 선택 표시 제거
        gameManager.HideSelectableHorses();
        
        // 선택 대기 상태 해제
        gameManager.waitingForHorseSelection = false;
        
        // 빽도 턴인 경우: 말 선택 후 바로 뒤로 이동
        if (gameManager.isBackDoTurn)
        {
            gameManager.currentHorseIndexForMove = horseIndex;
            return; // 빽도 처리는 ThrowAndMoveSequence에서 계속 진행
        }
        
        // 선택한 말의 현재 위치
        int currentPosition = gameManager.playerPositions[horseIndex];

        // 대기 중인 이동이 있으면 (윷/모 + 도/개/걸/빽도), 해당 말에서 사용 가능한 모든 이동의 발판 표시
        List<int> availablePositions = new List<int>();
        
        List<YutOutcome> pendingMovements = gameManager.turnManager != null ? gameManager.turnManager.GetPendingMovements() : new List<YutOutcome>();
        if (pendingMovements.Count > 0)
        {
            // 대기 중인 모든 이동에 대해 해당 말에서 이동 가능한 발판 계산
            bool hasBackDoAvailable = false;
            foreach (YutOutcome movement in pendingMovements)
            {
                // 빽도인 경우 특별 처리
                if (gameManager.IsBackDo(movement))
                {
                    // 빽도는 뒤로 갈 수 있는 말만 가능 (위치가 0보다 큰 말)
                    // 대기공간(-1)의 말은 빽도 불가능
                    if (currentPosition > 0)
                    {
                        hasBackDoAvailable = true;
                    }
                }
                else
                {
                    // 일반 이동 (도/개/걸/윷/모)
                    // 대기공간(-1)에서 시작하는 경우, A1(0)에서 시작하는 것으로 계산
                    int calcPosition = currentPosition == -1 ? 0 : currentPosition;
                    int moveSteps = YutGameUtils.GetMoveSteps((int)movement);
                    List<int> positions = gameManager.CalculateAvailablePositions(calcPosition, moveSteps);
                    foreach (int pos in positions)
                    {
                        // C1이 최종 목적지인 경우 F1도 추가하는 로직은 CalculateAvailablePositions에서 처리
                        // 단, C1에서 시작해서 C1에 도착하는 경우에만 F1도 추가
                        // 따라서 C1이 최종 목적지가 아닌 경우(다른 위치에서 C1으로 이동)는 C1만 추가
                        if (!availablePositions.Contains(pos))
                        {
                            availablePositions.Add(pos);
                        }
                    }
                }
            }
            
            // 빽도가 있고 다른 발판이 없으면 빽도만 처리
            if (hasBackDoAvailable && availablePositions.Count == 0)
            {
                // 빽도만 가능한 경우: 바로 빽도 처리
                gameManager.isBackDoTurn = true;
                gameManager.currentHorseIndexForMove = horseIndex;
                return; // 빽도 처리는 ThrowAndMoveSequence의 while 루프에서 계속 진행
            }
        }
        else
        {
            // 일반 경우: 현재 moveSteps로만 이동
            availablePositions = gameManager.CalculateAvailablePositions(currentPosition, gameManager.currentMoveSteps);
        }
        
        // 빽도가 pendingMovements에 있는지 확인 (발판 선택 전에)
        if (pendingMovements.Count > 0 && currentPosition > 0)
        {
            foreach (YutOutcome movement in pendingMovements)
            {
                if (gameManager.IsBackDo(movement))
                {
                    // 빽도는 바로 전 칸 추가
                    int backPosition = currentPosition - 1;
                    if (backPosition >= 0 && !availablePositions.Contains(backPosition))
                    {
                        availablePositions.Add(backPosition);
                    }
                    break;
                }
            }
        }
        
        if (availablePositions.Count == 0)
        {
            Debug.LogWarning($"말 {horseIndex}의 이동 가능한 발판이 없습니다.");
            if (gameManager.resultText != null)
            {
                gameManager.resultText.text = "이동 가능한 발판이 없습니다.";
            }
            return;
        }
        
        // 발판 선택 모드로 전환 (빽도가 있으면 바로 전 칸도 표시됨)
        gameManager.ShowSelectablePlatforms(availablePositions);
        gameManager.currentHorseIndexForMove = horseIndex;
        gameManager.waitingForPlatformSelection = true;
        
        string horseName = YutGameUtils.GetHorseName(horseIndex);
        if (gameManager.resultText != null)
        {
            if (pendingMovements.Count > 0)
            {
                string movementsText = string.Join(", ", pendingMovements.Select(m => YutGameUtils.OutcomeToKorean(m)));
                gameManager.resultText.text = $"{horseName} 선택됨 - 이동할 발판을 선택하세요 ({movementsText} 중 선택)";
            }
            else
            {
                gameManager.resultText.text = $"{horseName} 선택됨 - 이동할 발판을 선택하세요";
            }
        }
    }
    
    public void OnPlatformSelected(int platformIndex)
    {
        if (!gameManager.waitingForPlatformSelection || gameManager.currentHorseIndexForMove < 0)
        {
            Debug.LogWarning($"발판 선택 조건 불만족: waitingForPlatformSelection={gameManager.waitingForPlatformSelection}, currentHorseIndexForMove={gameManager.currentHorseIndexForMove}");
            return;
        }
        
        if (!gameManager.IsPlatformSelectable(platformIndex))
        {
            Debug.LogWarning($"발판 {platformIndex}는 선택 가능한 발판이 아닙니다. 선택 가능한 발판: {string.Join(", ", gameManager.GetSelectablePlatformIndices())}");
            return;
        }
        
        // 발판 선택 표시 제거
        gameManager.HideSelectablePlatforms();
        
        // 선택 대기 상태 해제
        gameManager.waitingForPlatformSelection = false;
        
        // 이동할 말 인덱스 저장
        int horseIndexToMove = gameManager.currentHorseIndexForMove;
        int currentPosition = gameManager.playerPositions[horseIndexToMove];
        
        // 대기 중인 이동이 있으면, 선택한 발판에 도달하는 이동을 찾아서 저장 (이동 완료 후 제거)
        YutOutcome usedMovement = YutOutcome.Nak;
        List<YutOutcome> pendingMovementsList = gameManager.turnManager != null ? gameManager.turnManager.GetPendingMovements() : new List<YutOutcome>();
        if (pendingMovementsList.Count > 0)
        {
            // 선택한 발판에 도달하는 이동 찾기
            foreach (YutOutcome movement in pendingMovementsList)
            {
                // 빽도인 경우: 바로 전 칸인지 확인
                if (gameManager.IsBackDo(movement))
                {
                    int backPosition = currentPosition - 1;
                    if (platformIndex == backPosition)
                    {
                        usedMovement = movement;
                        break;
                    }
                }
                else
                {
                    // 일반 이동: moveSteps만큼 앞으로 이동
                    // 대기공간(-1)에서 시작하는 경우, A1(0)에서 시작하는 것으로 계산
                    int calcPosition = currentPosition == -1 ? 0 : currentPosition;
                    int moveSteps = YutGameUtils.GetMoveSteps((int)movement);
                    List<int> positions = gameManager.CalculateAvailablePositions(calcPosition, moveSteps);
                    if (positions.Contains(platformIndex))
                    {
                        usedMovement = movement;
                        break;
                    }
                }
            }
            
            // 사용된 이동을 찾지 못한 경우, 첫 번째 이동을 사용
            if (usedMovement == YutOutcome.Nak)
            {
                usedMovement = pendingMovementsList[0];
                Debug.LogWarning($"선택한 발판에 정확히 매칭되는 이동을 찾지 못했습니다. 첫 번째 이동을 사용: {YutGameUtils.OutcomeToKorean(usedMovement)}");
            }
            
            // 빽도인 경우: 빽도 이동 처리 (즉시 제거)
            if (usedMovement != YutOutcome.Nak && gameManager.IsBackDo(usedMovement))
            {
                if (gameManager.turnManager != null)
                {
                    gameManager.turnManager.RemovePendingMovement(usedMovement);
                }
                gameManager.isBackDoTurn = true;
                // 빽도 이동은 MoveToSelectedPlatform 대신 MoveHorseBackward 사용
                if (gameManager.movementManager != null)
                {
                    gameManager.StartCoroutine(gameManager.movementManager.MoveHorseBackward(horseIndexToMove, 1));
                }
                else
                {
                    gameManager.StartCoroutine(gameManager.MoveHorseBackwardInternal(horseIndexToMove, 1));
                }
                return;
            }
        }
        
        gameManager.currentHorseIndexForMove = -1;
        
        // 말 이동 시작 (사용된 이동 정보를 전달하여 이동 완료 후 제거)
        if (gameManager.movementManager != null)
        {
            gameManager.StartCoroutine(gameManager.movementManager.MoveToSelectedPlatform(horseIndexToMove, platformIndex, usedMovement));
        }
        else
        {
            gameManager.StartCoroutine(gameManager.MoveToSelectedPlatformInternal(horseIndexToMove, platformIndex, usedMovement));
        }
    }
    
    public bool CheckBackDoSelection(GameObject hitObject)
    {
        List<YutOutcome> pendingMovementsList = gameManager.turnManager != null ? gameManager.turnManager.GetPendingMovements() : new List<YutOutcome>();
        if (!gameManager.waitingForPlatformSelection || gameManager.currentHorseIndexForMove < 0 || pendingMovementsList.Count == 0)
        {
            return false;
        }
        
        // Terrain은 무시
        if (hitObject.name.Contains("Terrain") || hitObject.GetComponent<Terrain>() != null)
        {
            return false;
        }
        
        for (int i = 0; i < gameManager.players.Length; i++)
        {
            if (gameManager.players[i] == null) continue;
            GameObject playerObj = gameManager.players[i].gameObject;
            
            // 클릭한 오브젝트가 현재 선택된 말인지 확인
            Transform currentCheck = hitObject.transform;
            int maxDepth = 5;
            int depth = 0;
            bool isCurrentHorse = false;
            
            while (currentCheck != null && depth < maxDepth)
            {
                if (currentCheck == playerObj.transform)
                {
                    isCurrentHorse = true;
                    break;
                }
                currentCheck = currentCheck.parent;
                depth++;
            }
            
            if (isCurrentHorse && gameManager.currentHorseIndexForMove == i)
            {
                // 빽도가 pendingMovements에 있는지 확인
                bool hasBackDo = false;
                foreach (YutOutcome movement in pendingMovementsList)
                {
                    if (gameManager.IsBackDo(movement))
                    {
                        hasBackDo = true;
                        break;
                    }
                }
                
                if (hasBackDo && gameManager.playerPositions[i] > 0)
                {
                    // 빽도 선택
                    gameManager.isBackDoTurn = true;
                    gameManager.waitingForPlatformSelection = false;
                    gameManager.HideSelectablePlatforms();
                    return true;
                }
            }
        }
        
        return false;
    }
}

