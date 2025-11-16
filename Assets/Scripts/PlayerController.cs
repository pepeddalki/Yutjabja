using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 20f;                // 이동 속도 (4배 증가: 5f -> 20f)
    public float rotationSpeed = 10f;            // 회전 속도 (더 크게 하면 더 빠르게 회전, 예: 20f)
    
    private NavMeshAgent agent;
    private Animator animator;
    private bool isInitialized = false;
    
    public bool IsInitialized()
    {
        return isInitialized;
    }
    
    void Start()
    {
        InitializeAgent();
    }
    
    public void InitializeAgent()
    {
        // NavMeshAgent 컴포넌트 가져오기 또는 추가
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
        }
        
        // NavMeshAgent 설정
        agent.speed = moveSpeed;
        agent.angularSpeed = rotationSpeed;
        agent.acceleration = 80f;                 // 가속도 (4배 증가: 20f -> 80f)
        agent.stoppingDistance = 0.3f;
        agent.radius = 0.4f; // NavMesh Bake 설정과 일치 (radius: 0.4)
        agent.height = 5.0f; // 널널하게 설정 (기사/마법사 모두 대응, Bake height=4.3보다 크게)
        agent.baseOffset = 0f; // Base Offset 초기화 (발판에 맞춤)
        // 참고: Step Height는 Unity 에디터의 Navigation Bake 설정에서만 설정 가능합니다 (Window > AI > Navigation > Agents 탭)
        // 현재 Bake 설정: radius=0.4, height=4.3, step height=4, max slope=40
        // NavMeshAgent height를 5.0으로 널널하게 설정하여 기사/마법사 모두 대응
        agent.autoBraking = true;
        agent.autoRepath = true; // 자동 경로 재계산
        agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance; // 장애물 회피 비활성화
        
        // NavMeshAgent가 NavMesh 위에 있는지 확인
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"[{gameObject.name}] NavMeshAgent가 NavMesh 위에 있지 않습니다. Warp 시도...");
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                Debug.Log($"[{gameObject.name}] NavMesh 위로 이동: {hit.position}");
            }
        }
        
        // NavMesh 위로 강제 이동
        ForceToNavMesh();
        
        // Animator 가져오기 또는 추가
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = gameObject.AddComponent<Animator>();
        }
        
        isInitialized = true;
    }
    
    void ForceToNavMesh()
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            agent.Warp(hit.position);
        }
        else
        {
            // NavMesh를 찾을 수 없으면 기본 위치로 이동
            Vector3 defaultPos = new Vector3(0, 0, 0);
            if (NavMesh.SamplePosition(defaultPos, out hit, 50f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                agent.Warp(hit.position);
            }
        }
    }
    
    public void MoveToPosition(Vector3 targetPosition)
    {
        // GameObject가 비활성화되어 있으면 활성화
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }
        
        if (!isInitialized)
        {
            InitializeAgent();
        }
        
        // NavMesh 위에 있는지 다시 확인
        if (agent != null && !agent.isOnNavMesh)
        {
            ForceToNavMesh();
            // ForceToNavMesh 후 NavMesh 위에 있는지 다시 확인
            if (!agent.isOnNavMesh)
            {
                Debug.LogWarning($"[{gameObject.name}] MoveToPosition - NavMesh 위에 있지 않습니다. 이동을 취소합니다.");
                return;
            }
        }
        
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            // 이전 경로 정리
            agent.ResetPath();
            
            // E5, D1, D2 발판 디버깅
            bool isDebugPlatform = (targetPosition.y > 0.5f && targetPosition.y < 3f);
            if (isDebugPlatform)
            {
                Debug.Log($"[{gameObject.name}] 이동 시작 - 목표 위치: {targetPosition}, 현재 위치: {transform.position}");
            }
            
            // 목표 위치가 NavMesh 위에 있는지 확인
            // 발판 위로 올라가야 하므로 발판 위의 NavMesh를 우선적으로 찾음
            NavMeshHit hit;
            Vector3 validTarget = targetPosition;
            bool foundNavMesh = false;
            
            // 1순위: 발판 위의 NavMesh 찾기 (Y 좌표를 올려서 검색)
            // 발판 position.y = 2, scale.y = 2이므로 발판 상단은 약 3~4 정도
            Vector3 platformTop = targetPosition;
            platformTop.y += 3f; // 발판 높이 고려하여 올림 (2.5f -> 3f로 증가)
            
            // 여러 높이에서 발판 위 NavMesh 검색 (조건 완화)
            bool foundPlatformNavMesh = false;
            for (float yOffset = 4f; yOffset >= 0.5f; yOffset -= 0.5f)
            {
                Vector3 searchPos = targetPosition;
                searchPos.y += yOffset;
                if (NavMesh.SamplePosition(searchPos, out hit, 10f, NavMesh.AllAreas)) // 검색 반경 확대: 5f -> 10f
                {
                    // 발판 위의 NavMesh인지 확인 (조건 완화: 0.5 -> 0.2)
                    if (hit.position.y > targetPosition.y + 0.2f)
                    {
                        validTarget = hit.position;
                        foundNavMesh = true;
                        foundPlatformNavMesh = true;
                        break; // 발판 위 NavMesh를 찾으면 중단
                    }
                }
            }
            
            if (!foundPlatformNavMesh)
            {
                Debug.LogWarning($"[{gameObject.name}] 발판 위 NavMesh를 찾지 못함. 목표Y:{targetPosition.y:F2}, Agent Climb:{agent.height:F2}");
            }
            
            // 2순위: 목표 위치에서 검색 (발판 위를 찾지 못한 경우)
            if (!foundNavMesh)
            {
                if (NavMesh.SamplePosition(targetPosition, out hit, 5f, NavMesh.AllAreas))
                {
                    validTarget = hit.position;
                    foundNavMesh = true;
                }
            }
            
            // 3순위: 더 넓은 범위로 발판 위 재시도 (조건 완화)
            if (!foundNavMesh)
            {
                for (float yOffset = 5f; yOffset >= 0.5f; yOffset -= 0.5f)
                {
                    Vector3 searchPos = targetPosition;
                    searchPos.y += yOffset;
                    if (NavMesh.SamplePosition(searchPos, out hit, 15f, NavMesh.AllAreas)) // 검색 반경 확대: 10f -> 15f
                    {
                        // 조건 완화: 0.3 -> 0.1
                        if (hit.position.y > targetPosition.y + 0.1f)
                        {
                            validTarget = hit.position;
                            foundNavMesh = true;
                            break;
                        }
                    }
                }
            }
            
            // 4순위: 매우 넓은 범위로 재시도
            if (!foundNavMesh)
            {
                if (NavMesh.SamplePosition(targetPosition, out hit, 20f, NavMesh.AllAreas))
                {
                    validTarget = hit.position;
                    foundNavMesh = true;
                }
            }
            
            if (foundNavMesh)
            {
                // NavMesh 위에 있는지 다시 확인
                if (!agent.isOnNavMesh)
                {
                    Debug.LogWarning($"[{gameObject.name}] MoveToPosition - SetDestination 호출 전 NavMesh 위에 있지 않습니다. 이동을 취소합니다.");
                    return;
                }
                
                lastDestination = validTarget;
                lastDestinationSetTime = Time.time;
                
                agent.SetDestination(validTarget);
                
                // E5, D1, D2 발판 디버깅
                if (isDebugPlatform)
                {
                    Debug.Log($"[{gameObject.name}] 목표 위치 NavMesh 찾음 - 원본: {targetPosition}, 유효: {validTarget}, 발판 위: {foundPlatformNavMesh}");
                }
                
                // 경로 계산 대기 및 확인 (GameObject가 활성화되어 있을 때만)
                if (gameObject.activeInHierarchy)
                {
                    StartCoroutine(CheckPathCalculation(validTarget));
                }
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] 목표 위치 주변에 NavMesh를 찾을 수 없습니다. 원본 위치로 이동 시도: {targetPosition}");
                // NavMesh를 찾을 수 없어도 원본 위치로 이동 시도
                lastDestination = targetPosition;
                lastDestinationSetTime = Time.time;
                agent.SetDestination(targetPosition);
            }
        }
        else
        {
            Debug.LogError($"{gameObject.name}: NavMeshAgent가 null이거나 비활성화됨");
        }
    }
    
    // 경로 계산 확인 및 재시도
    private IEnumerator CheckPathCalculation(Vector3 targetPosition)
    {
        // E5, D1, D2 발판 디버깅
        bool isDebugPlatform = (targetPosition.y > 0.5f && targetPosition.y < 3f);
        
        float timeout = 0.5f; // 0.5초 타임아웃
        float elapsed = 0f;
        
        // 경로 계산 대기
        while (agent.pathPending && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // 경로 상태 확인
        if (agent.hasPath)
        {
            // PathPartial인 경우 재시도
            if (agent.pathStatus == NavMeshPathStatus.PathPartial)
            {
                
                // 발판 위 NavMesh 존재 여부 확인
                NavMeshHit debugHit;
                bool foundAnyNavMesh = false;
                Vector3 foundNavMeshPos = Vector3.zero;
                float terrainY = 0f; // 지형 높이 추정
                
                // 현재 위치의 지형 높이 확인
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit terrainHit, 10f, NavMesh.AllAreas))
                {
                    terrainY = terrainHit.position.y;
                }
                
                for (float yOffset = 5f; yOffset >= 0f; yOffset -= 0.5f)
                {
                    Vector3 debugPos = targetPosition;
                    debugPos.y += yOffset;
                    if (NavMesh.SamplePosition(debugPos, out debugHit, 20f, NavMesh.AllAreas))
                    {
                        // 발판 위 NavMesh 판단: 조건 완화 (1.0 -> 0.5)
                        if (debugHit.position.y > terrainY + 0.5f)
                        {
                            foundAnyNavMesh = true;
                            foundNavMeshPos = debugHit.position;
                            break;
                        }
                    }
                }
                
                // 발판 위 NavMesh를 찾았으면 바로 사용
                bool foundRetry = false;
                if (foundAnyNavMesh)
                {
                    // 찾은 발판 위 NavMesh에서 경로 계산 시도
                    UnityEngine.AI.NavMeshPath platformPath = new UnityEngine.AI.NavMeshPath();
                    if (agent.CalculatePath(foundNavMeshPos, platformPath))
                    {
                        if (platformPath.status == UnityEngine.AI.NavMeshPathStatus.PathComplete || 
                            platformPath.status == UnityEngine.AI.NavMeshPathStatus.PathPartial)
                        {
                            agent.SetDestination(foundNavMeshPos);
                            lastDestination = foundNavMeshPos;
                            lastDestinationSetTime = Time.time;
                            foundRetry = true;
                        }
                    }
                }
                
                if (!foundAnyNavMesh)
                {
                    Debug.LogError($"[{gameObject.name}] 발판 위 NavMesh가 존재하지 않습니다! 하지만 다른 말들은 성공했으므로 경로 계산 문제일 수 있습니다.");
                    Debug.LogError($"[{gameObject.name}] 현재 위치에서 목표 위치로의 경로를 직접 계산 시도...");
                    
                    // 현재 위치에서 목표 위치로 직접 경로 계산 시도
                    UnityEngine.AI.NavMeshPath testPath = new UnityEngine.AI.NavMeshPath();
                    if (agent.CalculatePath(targetPosition, testPath))
                    {
                        // PathPartial이어도 경로를 사용 (부분 경로라도 이동)
                        if (testPath.status == UnityEngine.AI.NavMeshPathStatus.PathComplete || 
                            testPath.status == UnityEngine.AI.NavMeshPathStatus.PathPartial)
                        {
                            agent.SetPath(testPath);
                            lastDestination = targetPosition;
                            lastDestinationSetTime = Time.time;
                            yield break; // 재시도 로직 건너뛰기
                        }
                    }
                }
                
                // 발판 위 NavMesh를 더 넓은 범위로 재시도
                NavMeshHit hit;
                
                // 1단계: 더 넓은 범위와 높이로 재시도
                if (!foundRetry)
                {
                    for (float searchRadius = 10f; searchRadius <= 30f && !foundRetry; searchRadius += 5f)
                    {
                        for (float yOffset = 6f; yOffset >= 0.5f && !foundRetry; yOffset -= 0.5f)
                        {
                            Vector3 searchPos = targetPosition;
                            searchPos.y += yOffset;
                            if (NavMesh.SamplePosition(searchPos, out hit, searchRadius, NavMesh.AllAreas))
                            {
                                // 발판 위 NavMesh: 조건 완화 (0.8 -> 0.5, 0.3 -> 0.1)
                                bool isPlatformNavMesh = (hit.position.y > terrainY + 0.5f) || (hit.position.y > targetPosition.y + 0.1f);
                                if (isPlatformNavMesh)
                                {
                                    // 발판 위 NavMesh에서 경로 계산 시도
                                    UnityEngine.AI.NavMeshPath platformPath = new UnityEngine.AI.NavMeshPath();
                                    if (agent.CalculatePath(hit.position, platformPath))
                                    {
                                    agent.SetDestination(hit.position);
                                    lastDestination = hit.position;
                                    lastDestinationSetTime = Time.time;
                                    foundRetry = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                
                // 2단계: 발판 주변 여러 위치에서 검색
                if (!foundRetry)
                {
                    Vector3[] searchOffsets = new Vector3[]
                    {
                        new Vector3(1f, 3f, 0f),
                        new Vector3(-1f, 3f, 0f),
                        new Vector3(0f, 3f, 1f),
                        new Vector3(0f, 3f, -1f),
                        new Vector3(1f, 3f, 1f),
                        new Vector3(-1f, 3f, -1f),
                        new Vector3(2f, 3f, 0f),
                        new Vector3(-2f, 3f, 0f),
                        new Vector3(0f, 3f, 2f),
                        new Vector3(0f, 3f, -2f)
                    };
                    
                    foreach (Vector3 offset in searchOffsets)
                    {
                        Vector3 searchPos = targetPosition + offset;
                        if (NavMesh.SamplePosition(searchPos, out hit, 20f, NavMesh.AllAreas))
                        {
                            // 발판 위 NavMesh: 조건 완화 (0.8 -> 0.5, 0.3 -> 0.1)
                            bool isPlatformNavMesh = (hit.position.y > terrainY + 0.5f) || (hit.position.y > targetPosition.y + 0.1f);
                            if (isPlatformNavMesh)
                            {
                                // 발판 위 NavMesh에서 경로 계산 시도
                                UnityEngine.AI.NavMeshPath platformPath = new UnityEngine.AI.NavMeshPath();
                                if (agent.CalculatePath(hit.position, platformPath))
                                {
                                    agent.SetDestination(hit.position);
                                    lastDestination = hit.position;
                                    lastDestinationSetTime = Time.time;
                                    foundRetry = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                
                if (!foundRetry)
                {
                    Debug.LogError($"[{gameObject.name}] 발판 위 NavMesh 재시도 실패! 발판 위 NavMesh가 bake되지 않았을 수 있습니다.");
                    
                    // 최후의 수단: 지면의 NavMesh라도 사용해서 이동 시도
                    NavMeshHit groundHit;
                    if (NavMesh.SamplePosition(targetPosition, out groundHit, 20f, NavMesh.AllAreas))
                    {
                        // 지면의 NavMesh에서 경로 계산 시도
                        UnityEngine.AI.NavMeshPath groundPath = new UnityEngine.AI.NavMeshPath();
                        if (agent.CalculatePath(groundHit.position, groundPath))
                        {
                            agent.SetDestination(groundHit.position);
                            lastDestination = groundHit.position;
                            lastDestinationSetTime = Time.time;
                            Debug.LogWarning($"[{gameObject.name}] 발판 위 NavMesh를 찾지 못했지만 지면 NavMesh로 이동 시도: {groundHit.position}");
                        }
                        else
                        {
                            // 지면 경로도 실패하면 원래 목표 위치로 직접 이동 시도
                            Debug.LogWarning($"[{gameObject.name}] 지면 경로 계산도 실패. 원래 목표 위치로 직접 이동 시도: {targetPosition}");
                            agent.SetDestination(targetPosition);
                            lastDestination = targetPosition;
                            lastDestinationSetTime = Time.time;
                        }
                    }
                    else
                    {
                        // NavMesh를 전혀 찾지 못한 경우 원래 목표로 직접 이동 시도
                        Debug.LogWarning($"[{gameObject.name}] NavMesh를 전혀 찾지 못함. 원래 목표 위치로 직접 이동 시도: {targetPosition}");
                        agent.SetDestination(targetPosition);
                        lastDestination = targetPosition;
                        lastDestinationSetTime = Time.time;
                    }
                }
            }
        }
        else
        {
            // 경로 계산 실패 시 발판 위 NavMesh 재시도
            NavMeshHit hit;
            bool foundRetry = false;
            
            // 현재 위치와 목표 위치의 NavMesh 상태 확인
            NavMeshHit currentHit, targetHit;
            bool currentOnNavMesh = NavMesh.SamplePosition(transform.position, out currentHit, 10f, NavMesh.AllAreas);
            bool targetOnNavMesh = NavMesh.SamplePosition(targetPosition, out targetHit, 10f, NavMesh.AllAreas);
            
            // 직접 경로 계산 시도 (현재 위치에서 목표 위치로)
            if (currentOnNavMesh && targetOnNavMesh)
            {
                UnityEngine.AI.NavMeshPath directPath = new UnityEngine.AI.NavMeshPath();
                if (agent.CalculatePath(targetPosition, directPath))
                {
                    if (directPath.status == UnityEngine.AI.NavMeshPathStatus.PathComplete || 
                        directPath.status == UnityEngine.AI.NavMeshPathStatus.PathPartial)
                    {
                        agent.SetPath(directPath);
                        lastDestination = targetPosition;
                        lastDestinationSetTime = Time.time;
                        foundRetry = true;
                    }
                }
            }
            
            // 1단계: 더 넓은 범위와 높이로 재시도
            if (!foundRetry)
            {
                for (float searchRadius = 10f; searchRadius <= 30f && !foundRetry; searchRadius += 5f)
                {
                    for (float yOffset = 6f; yOffset >= 0.5f && !foundRetry; yOffset -= 0.5f)
                    {
                        Vector3 searchPos = targetPosition;
                        searchPos.y += yOffset;
                        if (NavMesh.SamplePosition(searchPos, out hit, searchRadius, NavMesh.AllAreas))
                        {
                            // 발판 위 NavMesh: 조건 완화 (0.3 -> 0.1)
                            if (hit.position.y > targetPosition.y + 0.1f)
                            {
                                // 발판 위 NavMesh에서 경로 계산 시도
                                UnityEngine.AI.NavMeshPath platformPath = new UnityEngine.AI.NavMeshPath();
                                if (agent.CalculatePath(hit.position, platformPath))
                                {
                                    agent.SetDestination(hit.position);
                                    lastDestination = hit.position;
                                    lastDestinationSetTime = Time.time;
                                    foundRetry = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            
            // 2단계: 발판 주변 여러 위치에서 검색
            if (!foundRetry)
            {
                Vector3[] searchOffsets = new Vector3[]
                {
                    new Vector3(0f, 3f, 0f),      // 발판 위 정중앙
                    new Vector3(1f, 3f, 0f),
                    new Vector3(-1f, 3f, 0f),
                    new Vector3(0f, 3f, 1f),
                    new Vector3(0f, 3f, -1f),
                    new Vector3(1f, 3f, 1f),
                    new Vector3(-1f, 3f, -1f),
                    new Vector3(2f, 3f, 0f),
                    new Vector3(-2f, 3f, 0f),
                    new Vector3(0f, 3f, 2f),
                    new Vector3(0f, 3f, -2f)
                };
                
                foreach (Vector3 offset in searchOffsets)
                {
                    Vector3 searchPos = targetPosition + offset;
                    if (NavMesh.SamplePosition(searchPos, out hit, 20f, NavMesh.AllAreas))
                    {
                        // 발판 위 NavMesh: 조건 완화 (0.3 -> 0.1)
                        if (hit.position.y > targetPosition.y + 0.1f)
                        {
                            // 발판 위 NavMesh에서 경로 계산 시도
                            UnityEngine.AI.NavMeshPath platformPath = new UnityEngine.AI.NavMeshPath();
                            if (agent.CalculatePath(hit.position, platformPath))
                            {
                                agent.SetDestination(hit.position);
                                lastDestination = hit.position;
                                lastDestinationSetTime = Time.time;
                                foundRetry = true;
                                break;
                            }
                        }
                    }
                }
            }
            
            if (!foundRetry)
            {
                Debug.LogError($"[{gameObject.name}] 경로 계산 실패 및 재시도도 실패! Agent 설정 - Height:{agent.height:F2}, Radius:{agent.radius:F2}");
                Debug.LogError($"[{gameObject.name}] NavMesh Bake 설정 확인 필요: Step Height 3.0, Max Slope 40도로 설정했는지 확인하세요.");
                Debug.LogError($"[{gameObject.name}] 발판이 NavMesh Static으로 표시되어 있고, NavMesh가 제대로 Bake되었는지 확인하세요.");
                
                // 최후의 수단: 지면의 NavMesh라도 사용해서 이동 시도
                NavMeshHit groundHit;
                if (NavMesh.SamplePosition(targetPosition, out groundHit, 20f, NavMesh.AllAreas))
                {
                    // 지면의 NavMesh에서 경로 계산 시도
                    UnityEngine.AI.NavMeshPath groundPath = new UnityEngine.AI.NavMeshPath();
                    if (agent.CalculatePath(groundHit.position, groundPath))
                    {
                        agent.SetDestination(groundHit.position);
                        lastDestination = groundHit.position;
                        lastDestinationSetTime = Time.time;
                        Debug.LogWarning($"[{gameObject.name}] 발판 위 NavMesh를 찾지 못했지만 지면 NavMesh로 이동 시도: {groundHit.position}");
                    }
                    else
                    {
                        // 지면 경로도 실패하면 원래 목표 위치로 직접 이동 시도
                        Debug.LogWarning($"[{gameObject.name}] 지면 경로 계산도 실패. 원래 목표 위치로 직접 이동 시도: {targetPosition}");
                        agent.SetDestination(targetPosition);
                        lastDestination = targetPosition;
                        lastDestinationSetTime = Time.time;
                    }
                }
                else
                {
                    // NavMesh를 전혀 찾지 못한 경우 원래 목표로 직접 이동 시도
                    Debug.LogWarning($"[{gameObject.name}] NavMesh를 전혀 찾지 못함. 원래 목표 위치로 직접 이동 시도: {targetPosition}");
                    agent.SetDestination(targetPosition);
                    lastDestination = targetPosition;
                    lastDestinationSetTime = Time.time;
                }
            }
        }
    }
    
    public bool IsMoving()
    {
        if (agent == null || !agent.enabled)
            return false;
            
        return agent.velocity.magnitude > 0.1f;
    }
    
    private Vector3 lastDestination = Vector3.zero;
    private float lastDestinationSetTime = 0f;
    
    public bool HasReachedDestination()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return true;
            
        // 경로가 없거나 완료되었고, 목적지에 충분히 가까우면 도착한 것으로 간주
        bool hasPath = agent.hasPath;
        bool pathPending = agent.pathPending;
        float remainingDistance = agent.remainingDistance;
        float velocity = agent.velocity.magnitude;
        
        // 실제 위치와 목표 위치의 거리 확인 (더 정확한 판정)
        float distanceToTarget = Vector3.Distance(transform.position, lastDestination);
        
        // SetDestination 직후 경로 계산 중이면 도착하지 않은 것으로 간주
        // SetDestination 후 최소 0.1초(약 6프레임)는 경로 계산 시간을 줌
        if (Time.time - lastDestinationSetTime < 0.1f)
        {
            return false; // 경로 계산 중이므로 아직 도착하지 않음
        }
        
        // pathPending이 true면 아직 경로 계산 중이므로 도착하지 않음
        if (pathPending)
        {
            return false;
        }
        
        // hasPath가 false이고 pathPending도 false면 경로를 찾지 못했거나 도착한 것
        if (!hasPath)
        {
            // 목표 위치와 현재 위치의 거리를 확인
            if (distanceToTarget > 0.5f)
            {
                // 아직 멀리 떨어져 있으면 경로를 찾지 못한 것
                return false;
            }
            // 거리가 가까우면 도착한 것으로 간주
            return true;
        }
        
        // 속도가 거의 0이고 목적지에 가까우면 도착한 것으로 간주
        // remainingDistance가 0.4f 미만이거나, 실제 거리가 0.5f 미만이면 도착
        bool reached = (remainingDistance < 0.4f && velocity < 0.05f) || (distanceToTarget < 0.5f && velocity < 0.1f);
        
        return reached;
    }
    
    
    void Update()
    {
        // 애니메이션 업데이트
        if (animator != null)
        {
            float speed = agent != null ? agent.velocity.magnitude : 0f;
            bool isMoving = speed > 0.01f;
            
            // Animator 파라미터 설정
            animator.SetFloat("Speed", speed);
            animator.SetBool("IsMoving", isMoving);
        }
        
    }
    
    // 공격 애니메이션 실행
    public void PlayAttackAnimation()
    {
        if (animator == null) return;
        
        // 파라미터가 존재하는지 확인
        bool hasAttackParam = false;
        AnimatorControllerParameterType attackParamType = AnimatorControllerParameterType.Bool;
        
        if (animator.parameters != null)
        {
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == "Attack")
                {
                    hasAttackParam = true;
                    attackParamType = param.type;
                    break;
                }
            }
        }
        
        if (!hasAttackParam)
        {
            Debug.LogWarning("Animator에 'Attack' 파라미터가 없습니다. Animator Controller에 'Attack' 파라미터를 추가해주세요.");
            return;
        }
        
        // 파라미터 타입에 따라 실행
        if (attackParamType == AnimatorControllerParameterType.Trigger)
        {
            animator.SetTrigger("Attack");
        }
        else if (attackParamType == AnimatorControllerParameterType.Bool)
        {
            animator.SetBool("Attack", true);
            // 0.1초 후 리셋 (애니메이션이 끝나면 자동으로 false로 돌아가도록)
            Invoke(nameof(ResetAttackAnimation), 0.1f);
        }
    }
    
    private void ResetAttackAnimation()
    {
        if (animator == null) return;
        
        // 파라미터가 존재하는지 확인
        bool hasAttackParam = false;
        if (animator.parameters != null)
        {
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == "Attack" && param.type == AnimatorControllerParameterType.Bool)
                {
                    hasAttackParam = true;
                    break;
                }
            }
        }
        
        if (hasAttackParam)
        {
            animator.SetBool("Attack", false);
        }
    }
    
    // 죽음 애니메이션 실행
    public void PlayDeathAnimation()
    {
        if (animator == null)
        {
            return;
        }
        
        // 파라미터가 존재하는지 확인
        bool hasDeathParam = false;
        AnimatorControllerParameterType deathParamType = AnimatorControllerParameterType.Bool;
        
        if (animator.parameters != null)
        {
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == "Death")
                {
                    hasDeathParam = true;
                    deathParamType = param.type;
                    break;
                }
            }
        }
        
        if (!hasDeathParam)
        {
            return;
        }
        
        // 파라미터 타입에 따라 실행
        if (deathParamType == AnimatorControllerParameterType.Trigger)
        {
            animator.SetTrigger("Death");
        }
        else if (deathParamType == AnimatorControllerParameterType.Bool)
        {
            animator.SetBool("Death", true);
        }
    }
    
    // Idle 상태로 전환
    public void SetIdleState()
    {
        if (animator == null) return;
        
        // IsMoving을 false로 설정하여 Idle 상태로 전환
        animator.SetBool("IsMoving", false);
        animator.SetFloat("Speed", 0f);
        
        // Death 애니메이션이 재생 중이면 리셋
        if (animator.parameters != null)
        {
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == "Death")
                {
                    if (param.type == AnimatorControllerParameterType.Bool)
                    {
                        animator.SetBool("Death", false);
                    }
                    else if (param.type == AnimatorControllerParameterType.Trigger)
                    {
                        animator.ResetTrigger("Death");
                    }
                    break;
                }
            }
        }
    }
}