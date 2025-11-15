using UnityEngine;
using UnityEngine.AI;

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
        agent.radius = 0.2f; // 반지름 감소 (더 정확한 경로)
        agent.height = 2f;
        agent.autoBraking = true;
        agent.autoRepath = true; // 자동 경로 재계산
        agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance; // 장애물 회피 비활성화
        
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
        if (!isInitialized)
        {
            InitializeAgent();
        }
        
        // NavMesh 위에 있는지 다시 확인
        if (!agent.isOnNavMesh)
        {
            ForceToNavMesh();
        }
        
        if (agent != null && agent.enabled)
        {
            // 이전 경로 정리
            agent.ResetPath();
            
            // 목표 위치가 NavMesh 위에 있는지 확인 (검색 범위를 넓힘)
            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPosition, out hit, 5f, NavMesh.AllAreas))
            {
                Vector3 validTarget = hit.position;
                lastDestination = validTarget;
                lastDestinationSetTime = Time.time;
                agent.SetDestination(validTarget);
            }
            else
            {
                Debug.LogWarning($"{gameObject.name}: 목표 위치 주변에 NavMesh를 찾을 수 없습니다. 원본 위치로 이동 시도: {targetPosition}");
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
        // 하지만 실제 위치와 목표 위치의 거리를 확인해야 함
        if (!hasPath)
        {
            // 목표 위치와 현재 위치의 거리를 확인
            float distanceToTarget = Vector3.Distance(transform.position, lastDestination);
            if (distanceToTarget > 0.5f)
            {
                // 아직 멀리 떨어져 있으면 경로를 찾지 못한 것
                return false;
            }
        }
        
        // 속도가 거의 0이고 목적지에 가까우면 도착한 것으로 간주
        bool reached = !hasPath || (remainingDistance < 0.4f && velocity < 0.05f);
        
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
        if (animator == null) return;
        
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
            Debug.LogWarning("Animator에 'Death' 파라미터가 없습니다. Animator Controller에 'Death' 파라미터를 추가해주세요.");
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
}