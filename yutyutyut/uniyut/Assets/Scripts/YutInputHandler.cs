using UnityEngine;
using UnityEngine.InputSystem;

public class YutInputHandler : MonoBehaviour
{
    private YutGameManager gameManager;
    private PlayerController[] players;
    private Transform[] boardPositions;
    private string[] positionNames;
    
    public void Initialize(YutGameManager manager, PlayerController[] players, Transform[] boardPositions, string[] positionNames)
    {
        this.gameManager = manager;
        this.players = players;
        this.boardPositions = boardPositions;
        this.positionNames = positionNames;
    }
    
    void Update()
    {
        if (gameManager == null) return;
        
        // 말 선택 대기 중일 때 터치/클릭 감지
        if (gameManager.IsWaitingForHorseSelection())
        {
            HandleHorseSelection();
        }
        
        // 발판 선택 대기 중일 때 터치/클릭 감지
        if (gameManager.IsWaitingForPlatformSelection())
        {
            HandlePlatformSelection();
        }
    }
    
    private void HandleHorseSelection()
    {
        // 마우스 클릭 또는 터치 (새 Input System 사용)
        bool mouseClicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool touchPressed = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        
        if (!mouseClicked && !touchPressed) return;
        
        Vector2 screenPosition = mouseClicked 
            ? Mouse.current.position.ReadValue() 
            : Touchscreen.current.primaryTouch.position.ReadValue();
        
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        
        foreach (RaycastHit hit in hits)
        {
            GameObject hitObject = hit.collider.gameObject;
            
            // Terrain은 무시
            if (hitObject.name.Contains("Terrain") || hitObject.GetComponent<Terrain>() != null)
            {
                continue;
            }
            
            // 클릭한 오브젝트가 말인지 확인
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                
                GameObject playerObj = players[i].gameObject;
                
                // 직접 비교
                if (hitObject == playerObj)
                {
                    if (gameManager.IsHorseSelectable(i))
                    {
                        Debug.Log($"말 선택: 인덱스 {i}");
                        gameManager.OnHorseSelected(i);
                        return;
                    }
                }
                
                // 자식 오브젝트 확인
                Transform currentCheck = hitObject.transform;
                int maxDepth = 5;
                int depth = 0;
                
                while (currentCheck != null && depth < maxDepth)
                {
                    if (currentCheck == playerObj.transform)
                    {
                        if (gameManager.IsHorseSelectable(i))
                        {
                            Debug.Log($"말 선택: 인덱스 {i}");
                            gameManager.OnHorseSelected(i);
                            return;
                        }
                        break;
                    }
                    currentCheck = currentCheck.parent;
                    depth++;
                }
                
                // 역방향: playerObj의 자식 확인
                if (playerObj.transform.IsChildOf(hitObject.transform) || hitObject.transform.IsChildOf(playerObj.transform))
                {
                    if (gameManager.IsHorseSelectable(i))
                    {
                        Debug.Log($"말 선택: 인덱스 {i}");
                        gameManager.OnHorseSelected(i);
                        return;
                    }
                }
            }
        }
        
        // Raycast로 말을 찾지 못했으면 클릭 위치에서 가장 가까운 말 찾기
        FindClosestHorse(ray);
    }
    
    private void FindClosestHorse(Ray ray)
    {
        // 카메라에서 말 높이까지의 평면 계산
        float horseHeight = 0f;
        if (players.Length > 0 && players[0] != null)
        {
            horseHeight = players[0].transform.position.y;
        }
        
        Plane plane = new Plane(Vector3.up, horseHeight);
        float distance;
        
        if (!plane.Raycast(ray, out distance)) return;
        
        Vector3 worldPoint = ray.GetPoint(distance);
        
        // 선택 가능한 말 중 가장 가까운 말 찾기
        float minDistance = float.MaxValue;
        int closestHorseIndex = -1;
        
        var selectableHorses = gameManager.GetSelectableHorseIndices();
        foreach (int horseIndex in selectableHorses)
        {
            if (players[horseIndex] != null)
            {
                Vector3 horsePos = players[horseIndex].transform.position;
                float dist = Vector3.Distance(new Vector3(worldPoint.x, 0, worldPoint.z), new Vector3(horsePos.x, 0, horsePos.z));
                
                if (dist < minDistance && dist < 3f) // 3유닛 이내의 말만 선택
                {
                    minDistance = dist;
                    closestHorseIndex = horseIndex;
                }
            }
        }
        
        if (closestHorseIndex >= 0)
        {
            gameManager.OnHorseSelected(closestHorseIndex);
        }
    }
    
    private void HandlePlatformSelection()
    {
        // 마우스 클릭 또는 터치 (새 Input System 사용)
        bool mouseClicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool touchPressed = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        
        if (!mouseClicked && !touchPressed) return;
        
        Vector2 screenPosition = mouseClicked 
            ? Mouse.current.position.ReadValue() 
            : Touchscreen.current.primaryTouch.position.ReadValue();
        
        // 여러 Raycast를 수행하여 발판을 찾기
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        
        // Terrain을 제외하고 발판만 찾기
        bool foundPlatform = false;
        foreach (RaycastHit hit in hits)
        {
            GameObject hitObject = hit.collider.gameObject;
            
            // Terrain은 무시
            if (hitObject.name.Contains("Terrain") || hitObject.GetComponent<Terrain>() != null)
            {
                continue;
            }
            
            Transform platformCheck = hitObject.transform;
            int platformMaxDepth = 5;
            int platformDepth = 0;
            
            while (platformCheck != null && platformDepth < platformMaxDepth)
            {
                for (int i = 0; i < boardPositions.Length; i++)
                {
                    if (boardPositions[i] != null && boardPositions[i] == platformCheck)
                    {
                        if (gameManager.IsPlatformSelectable(i))
                        {
                            gameManager.OnPlatformSelected(i);
                            return;
                        }
                    }
                }
                platformCheck = platformCheck.parent;
                platformDepth++;
            }
            
            // 발판 선택 대기 중이고, 말이 클릭되었고, 빽도가 pendingMovements에 있으면 빽도 처리
            if (gameManager.CheckBackDoSelection(hitObject))
            {
                return;
            }
        }
        
        // Raycast로 발판을 찾지 못했으면 클릭 위치에서 가장 가까운 발판 찾기
        if (!foundPlatform)
        {
            FindClosestPlatform(ray);
        }
    }
    
    private void FindClosestPlatform(Ray ray)
    {
        // 카메라에서 발판 높이까지의 평면 계산
        float platformHeight = 0f;
        if (boardPositions.Length > 0 && boardPositions[0] != null)
        {
            platformHeight = boardPositions[0].position.y;
        }
        
        Plane plane = new Plane(Vector3.up, platformHeight);
        float distance;
        
        if (!plane.Raycast(ray, out distance)) return;
        
        Vector3 worldPoint = ray.GetPoint(distance);
        
        // 선택 가능한 발판 중 가장 가까운 발판 찾기
        float minDistance = float.MaxValue;
        int closestPlatformIndex = -1;
        
        var selectablePlatforms = gameManager.GetSelectablePlatformIndices();
        foreach (int platformIndex in selectablePlatforms)
        {
            if (boardPositions[platformIndex] != null)
            {
                Vector3 platformPos = boardPositions[platformIndex].position;
                // XZ 평면에서의 거리만 계산 (Y축 무시)
                float dist = Vector2.Distance(new Vector2(worldPoint.x, worldPoint.z), new Vector2(platformPos.x, platformPos.z));
                
                if (dist < minDistance && dist < 3f) // 3유닛 이내의 발판만 선택
                {
                    minDistance = dist;
                    closestPlatformIndex = platformIndex;
                }
            }
        }
        
        if (closestPlatformIndex >= 0)
        {
            gameManager.OnPlatformSelected(closestPlatformIndex);
        }
    }
}

