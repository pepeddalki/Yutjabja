using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class YutMovementManager : MonoBehaviour
{
    private YutGameManager gameManager;
    
    public void Initialize(YutGameManager manager)
    {
        this.gameManager = manager;
    }
    
    public IEnumerator MoveHorse(int horseIndex, int steps)
    {
        return gameManager.MoveHorseInternal(horseIndex, steps);
    }
    
    public IEnumerator MoveHorseBackward(int horseIndex, int steps)
    {
        return gameManager.MoveHorseBackwardInternal(horseIndex, steps);
    }
    
    public IEnumerator MoveToSelectedPlatform(int horseIndex, int targetPlatformIndex, YutOutcome usedMovement = YutOutcome.Nak, bool forceGoalIn = false)
    {
        return gameManager.MoveToSelectedPlatformInternal(horseIndex, targetPlatformIndex, usedMovement, forceGoalIn);
    }
    
    public IEnumerator WaitForMovement(PlayerController horse)
    {
        return gameManager.WaitForMovementInternal(horse);
    }
}

