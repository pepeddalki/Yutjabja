using UnityEngine;
using System.Collections.Generic;

public static class YutGameUtils
{
    // 윷 결과를 한글로 변환
    public static string OutcomeToKorean(YutOutcome outcome)
    {
        switch (outcome)
        {
            case YutOutcome.Do: return "도";
            case YutOutcome.Gae: return "개";
            case YutOutcome.Geol: return "걸";
            case YutOutcome.Yut: return "윷";
            case YutOutcome.Mo: return "모";
            case YutOutcome.Nak: return "낙";
            default: return "알 수 없음";
        }
    }
    
    // 윷 결과에 따른 이동 칸 수 계산
    public static int GetMoveSteps(int yutResult)
    {
        switch (yutResult)
        {
            case 1: return 1; // 도
            case 2: return 2; // 개
            case 3: return 3; // 걸
            case 4: return 4; // 윷
            case 5: return 5; // 모
            default: return 0;
        }
    }
    
    // NavMesh 위에 있는 유효한 위치 찾기
    public static Vector3 GetValidNavMeshPosition(Vector3 originalPosition)
    {
        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(originalPosition, out hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return hit.position;
        }
        else
        {
            Debug.LogWarning($"NavMesh 유효 위치를 찾을 수 없음. 원본 위치 사용: {originalPosition}");
            return originalPosition;
        }
    }
    
    // 말 이름 가져오기
    public static string GetHorseName(int horseIndex)
    {
        bool isBarbarian = horseIndex < 4;
        int horseNumber = (isBarbarian ? horseIndex : horseIndex - 4) + 1;
        string playerName = isBarbarian ? "바바리안" : "기사";
        return $"{playerName}{horseNumber}";
    }
    
    // 두 위치 사이의 이동 칸 수 계산
    public static int CalculateStepsBetween(int from, int to, int boardLength)
    {
        if (from == to) return 0;
        
        // 직접 경로
        if (to > from)
        {
            return to - from;
        }
        else
        {
            // 순환 경로 (보드 끝에서 시작 위치로)
            return (boardLength - from) + to;
        }
    }
    
    // 이동 가능한 발판 계산 (일직선 경로만)
    public static List<int> CalculateAvailablePositions(int currentPosition, int moveSteps, int boardLength)
    {
        List<int> availablePositions = new List<int>();
        
        // 현재 위치에서 moveSteps만큼 이동한 위치
        int targetPosition = (currentPosition + moveSteps) % boardLength;
        availablePositions.Add(targetPosition);
        
        return availablePositions;
    }
}

