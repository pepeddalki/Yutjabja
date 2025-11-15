using System.Collections;
using UnityEngine;

public enum YutOutcome
{
    Nak = 0, // 낙
    Do = 1,
    Gae = 2,
    Geol = 3,
    Yut = 4,
    Mo = 5
}

public class YutThrowController : MonoBehaviour
{
    [Header("Yut Throw Setup")]
    public Rigidbody[] yutSticks; // 길이 4
    public GameObject boardObject;  // BoardRange GameObject (낙 판정용 원형 MeshCollider를 가진 객체)
    [HideInInspector]
    public MeshCollider boardCollider;  // BoardRange의 MeshCollider (낙 판정만 사용, 물리 충돌은 보드의 MeshCollider 사용)

    [Header("Stick Physics Tuning")]
    public float stickMass = 0.3f;              // 무게 증가 (튕김 감소)
    public float stickDrag = 0.5f;              // 저항력 크게 증가 (튕김 감소)
    public float stickAngularDrag = 1.0f;       // 회전 저항력 크게 증가 (튕김 감소)
    [Tooltip("중력 배수 (기본 중력의 배수, 1.5 = 1.5배 강한 중력)")]
    public float gravityMultiplier = 2.0f;        // 중력 2배 강하게 (튕김 감소)
    [Tooltip("PhysicsMaterial의 Bounciness (0 = 튕김 없음, 1 = 완전 탄성)")]
    public float bounciness = 0f;               // 튕김 방지 (0으로 설정)
    [Tooltip("PhysicsMaterial의 Dynamic Friction (마찰력, 0~1)")]
    public float dynamicFriction = 1.0f;       // 마찰력 최대값 (튕김 최소화)
    [Tooltip("PhysicsMaterial의 Static Friction (정지 마찰력, 0~1)")]
    public float staticFriction = 1.0f;         // 정지 마찰력 최대값 (튕김 최소화)

    [Header("Throw Forces")]
    public float upwardForce = 100000f;          // 윷을 위로 던지는 힘 (가벼운 무게에 맞춰 조정)
    public float lateralJitter = 0.5f;      // 좌우 약간의 무작위 힘
    public float torqueForce = 3f;          // 회전 토크
    public float fireInterval = 0.02f;      // 스틱 간 발사 간격(초)

    [Header("Settle Detection")]
    public float settleVelocityThreshold = 0.05f;
    public float settleAngularVelocityThreshold = 0.1f;
    public float settleStableTime = 0.5f; // 이 시간 동안 임계치 이하 유지
    public float maxWaitTime = 5f; // 최대 대기 시간

    [Header("Stick Side Detect")]
    public Vector3 localUpAxis = Vector3.up; // 스틱 로컬 업 축
    public float upDotThreshold = 0.5f;      // 월드 업과의 내적 임계치
    

    // 보드 위 기본 휴식 위치/회전(초기 배치 기준)
    private Vector3[] initialPositions;
    private Quaternion[] initialRotations;

    public YutOutcome LastOutcome { get; private set; } = YutOutcome.Nak;
    
    void FixedUpdate()
    {
        // 윷에만 추가 중력 적용 (3배 강하게)
        if (yutSticks == null) return;
        Vector3 extraGravity = Physics.gravity * (gravityMultiplier - 1f); // 기본 중력 제외한 추가 중력
        
        foreach (var rb in yutSticks)
        {
            if (rb == null || rb.isKinematic) continue;
            rb.AddForce(extraGravity, ForceMode.Acceleration);
        }
    }

    void Awake()
    {
        // yutSticks가 할당되지 않았으면 자동으로 찾기
        if (yutSticks == null || yutSticks.Length == 0 || yutSticks[0] == null)
        {
            AutoFindYutSticks();
        }
        
        // boardCollider 자동 할당
        AutoFindBoardCollider();
        
        ApplyRigidbodyTuning();
    }
    
    void Start()
    {
        // Start에서 초기 위치 저장 (씬이 완전히 로드된 후)
        CacheInitialRestTransforms();
        
        // 플레이 시작 시 윷을 초기 위치로 리셋 (코루틴으로 실행)
        StartCoroutine(StartResetCoroutine());
    }
    
    IEnumerator StartResetCoroutine()
    {
        yield return new WaitForFixedUpdate();
        ResetToBoardRest();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        
        // 리셋 후 위치 확인 및 재조정
        if (yutSticks != null && initialPositions != null)
        {
            for (int i = 0; i < yutSticks.Length; i++)
            {
                var rb = yutSticks[i];
                if (rb == null || i >= initialPositions.Length) continue;
                
                Vector3 currentPos = rb.transform.position;
                Vector3 targetPos = initialPositions[i];
                float distance = Vector3.Distance(currentPos, targetPos);
                
                // 위치가 많이 벗어났으면 다시 리셋
                if (distance > 0.01f)
                {
                    rb.isKinematic = true;
                    rb.transform.position = targetPos;
                    rb.transform.rotation = initialRotations[i];
                    rb.isKinematic = false;
                    rb.WakeUp();
                }
            }
        }
    }
    
    void AutoFindBoardCollider()
    {
        // boardObject가 할당되어 있으면 그곳에서 MeshCollider 찾기
        if (boardObject != null && boardCollider == null)
        {
            boardCollider = boardObject.GetComponent<MeshCollider>();
            if (boardCollider != null)
            {
                // BoardRange의 MeshCollider를 물리 충돌 활성화
                SetupBoardCollider(boardCollider);
                return;
            }
        }
        
        // boardObject가 없거나 찾지 못했으면 씬 전체에서 찾기
        if (boardCollider == null)
        {
            MeshCollider[] allColliders = FindObjectsOfType<MeshCollider>();
            
            // 1순위: "BoardRange" 이름을 가진 GameObject 찾기
            foreach (MeshCollider mc in allColliders)
            {
                if (mc.gameObject.name == "BoardRange")
                {
                    boardCollider = mc;
                    boardObject = mc.gameObject;
                    // BoardRange의 MeshCollider를 물리 충돌 활성화
                    SetupBoardCollider(mc);
                    return;
                }
            }
            
            // 2순위: "Board" 또는 "보드" 이름을 가진 GameObject 찾기
            foreach (MeshCollider mc in allColliders)
            {
                if (mc.gameObject.name.Contains("Board") || mc.gameObject.name.Contains("보드"))
                {
                    boardCollider = mc;
                    boardObject = mc.gameObject;
                    SetupBoardCollider(mc);
                    return;
                }
            }
            
            // 3순위: "circle" mesh를 가진 MeshCollider 찾기
            foreach (MeshCollider mc in allColliders)
            {
                if (mc.sharedMesh != null && mc.sharedMesh.name.Contains("circle"))
                {
                    boardCollider = mc;
                    boardObject = mc.gameObject;
                    SetupBoardCollider(mc);
                    return;
                }
            }
            
            // 이름으로 찾지 못했으면 첫 번째 MeshCollider 사용
            if (allColliders.Length > 0)
            {
                boardCollider = allColliders[0];
                boardObject = allColliders[0].gameObject;
                SetupBoardCollider(allColliders[0]);
                Debug.LogWarning("보드 이름을 가진 MeshCollider를 찾지 못해 첫 번째 MeshCollider를 사용합니다.");
            }
            else
            {
                Debug.LogWarning("씬에서 MeshCollider를 찾을 수 없습니다. Inspector에서 boardObject를 할당해주세요.");
            }
        }
    }
    
    void SetupBoardCollider(MeshCollider meshCollider)
    {
        if (meshCollider == null) return;
        
        // Mesh 확인
        if (meshCollider.sharedMesh == null)
        {
            Debug.LogError($"SetupBoardCollider: {meshCollider.gameObject.name}의 MeshCollider에 Mesh가 할당되지 않았습니다!");
            return;
        }
        
        // Convex 설정 (MeshCollider는 Convex가 true여야 물리 충돌 가능)
        if (!meshCollider.convex)
        {
            meshCollider.convex = true;
        }
        
        // 물리 충돌 활성화 (Is Trigger 해제)
        meshCollider.isTrigger = false;
        
        // Rigidbody가 없으면 추가 (Kinematic으로 설정하여 정적 충돌체로 사용)
        Rigidbody rb = meshCollider.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = meshCollider.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; // 움직이지 않는 정적 충돌체
            rb.useGravity = false;
        }
        else
        {
            // 이미 Rigidbody가 있으면 Kinematic 설정 확인
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void AutoFindYutSticks()
    {
        // 자식 객체에서 "Yut" 또는 "YutStick" 이름을 가진 객체 찾기
        Rigidbody[] foundSticks = new Rigidbody[4];
        int foundCount = 0;
        
        // 먼저 자식에서 찾기
        foreach (Transform child in transform)
        {
            if (child.name.Contains("Yut") || child.name.Contains("Stick"))
            {
                Rigidbody rb = child.GetComponent<Rigidbody>();
                if (rb != null && foundCount < 4)
                {
                    foundSticks[foundCount] = rb;
                    foundCount++;
                }
            }
        }
        
        // 자식에서 못 찾으면 씬 전체에서 "Yut" 이름을 가진 객체 찾기
        if (foundCount < 4)
        {
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if ((obj.name.Contains("Yut") || obj.name.Contains("Stick")) && 
                    !obj.transform.IsChildOf(transform))
                {
                    Rigidbody rb = obj.GetComponent<Rigidbody>();
                    if (rb != null && foundCount < 4)
                    {
                        // 중복 체크
                        bool isDuplicate = false;
                        for (int i = 0; i < foundCount; i++)
                        {
                            if (foundSticks[i] == rb)
                            {
                                isDuplicate = true;
                                break;
                            }
                        }
                        
                        if (!isDuplicate)
                        {
                            foundSticks[foundCount] = rb;
                            foundCount++;
                        }
                    }
                }
            }
        }
        
        // 찾은 윷이 4개면 배열 설정
        if (foundCount == 4)
        {
            yutSticks = foundSticks;
        }
        else
        {
            Debug.LogWarning($"윷을 {foundCount}개만 찾았습니다. Inspector에서 수동으로 할당해주세요.");
        }
    }

    void ApplyRigidbodyTuning()
    {
        if (yutSticks == null) return;
        foreach (var rb in yutSticks)
        {
            if (rb == null) continue;
            rb.mass = stickMass;
            rb.linearDamping = stickDrag;
            rb.angularDamping = stickAngularDrag;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            
            // PhysicsMaterial 설정 (튕김 방지)
            // Collider 찾기 (자신 또는 자식 객체)
            Collider col = rb.GetComponent<Collider>();
            if (col == null)
            {
                col = rb.GetComponentInChildren<Collider>();
            }
            
            if (col != null)
            {
                // PhysicsMaterial 가져오기 또는 생성
                PhysicsMaterial physicMat = col.material as PhysicsMaterial;
                if (physicMat == null)
                {
                    // PhysicsMaterial이 없으면 생성
                    physicMat = new PhysicsMaterial("YutPhysicsMat");
                    col.material = physicMat;
                }
                
                // PhysicsMaterial 속성 설정
                physicMat.bounciness = bounciness;  // 튕김 방지 (0으로 설정)
                physicMat.dynamicFriction = dynamicFriction;
                physicMat.staticFriction = staticFriction;
                physicMat.bounceCombine = PhysicsMaterialCombine.Minimum;  // 최소값 사용 (둘 중 낮은 값)
                physicMat.frictionCombine = PhysicsMaterialCombine.Average; // 평균값 사용
            }
            else
            {
                Debug.LogWarning($"윷 [{rb.gameObject.name}]에 Collider를 찾을 수 없습니다. PhysicsMaterial을 설정할 수 없습니다.");
            }
        }
    }

    void CacheInitialRestTransforms()
    {
        if (yutSticks == null)
        {
            Debug.LogWarning("CacheInitialRestTransforms: yutSticks가 null입니다.");
            return;
        }
        
        initialPositions = new Vector3[yutSticks.Length];
        initialRotations = new Quaternion[yutSticks.Length];
        
        for (int i = 0; i < yutSticks.Length; i++)
        {
            var rb = yutSticks[i];
            if (rb == null)
            {
                Debug.LogWarning($"윷[{i}]이 null입니다.");
                continue;
            }
            
            initialPositions[i] = rb.transform.position;
            initialRotations[i] = rb.transform.rotation;
        }
    }

    public IEnumerator ThrowAndWait()
    {
        // 던지기 전에 윷을 초기 위치로 리셋 (가지런하게 정렬)
        ResetToBoardRest();
        yield return new WaitForFixedUpdate(); // 물리 업데이트 대기

        // 현재 위치에서 속도/각속도만 초기화하고 바로 던지기 시작
        PrepareForThrow();

        // 위로 힘/토크 가해 던지기(순차 적용)
        yield return StartCoroutine(ApplyThrowForcesSequential());

        // 정지 대기
        yield return WaitUntilSticksSettle();

        // 판정
        LastOutcome = JudgeOutcome();
    }

    // 윷을 초기 위치로 리셋하는 공개 메서드
    public void ResetYutToStartPosition()
    {
        ResetToBoardRest();
    }
    
    // 윷을 초기 위치로 리셋하는 코루틴 (물리 업데이트 대기)
    public IEnumerator ResetYutToStartPositionCoroutine()
    {
        ResetToBoardRest();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        
        // 리셋 후 위치 확인 및 재조정
        for (int i = 0; i < yutSticks.Length; i++)
        {
            var rb = yutSticks[i];
            if (rb == null || i >= initialPositions.Length) continue;
            
            Vector3 currentPos = rb.transform.position;
            Vector3 targetPos = initialPositions[i];
            float distance = Vector3.Distance(currentPos, targetPos);
            
            // 위치가 많이 벗어났으면 다시 리셋
            if (distance > 0.01f)
            {
                rb.isKinematic = true;
                rb.transform.position = targetPos;
                rb.transform.rotation = initialRotations[i];
                rb.isKinematic = false;
                rb.WakeUp();
            }
        }
    }
    
    public IEnumerator ResetToBoardRestCoroutine()
    {
        ResetToBoardRest();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
    }

    void ResetToBoardRest()
    {
        if (yutSticks == null || initialPositions == null)
        {
            Debug.LogWarning("ResetToBoardRest: yutSticks 또는 initialPositions가 null입니다.");
            return;
        }
        
        for (int i = 0; i < yutSticks.Length; i++)
        {
            var rb = yutSticks[i];
            if (rb == null || i >= initialPositions.Length) continue;
            
            // 속도를 먼저 초기화 (kinematic 전에)
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            // 물리 시뮬레이션을 일시 중지하여 안전하게 위치 변경
            rb.isKinematic = true;
            // 초기 배치로 복귀 (가지런하게 정렬)
            rb.transform.position = initialPositions[i];
            rb.transform.rotation = initialRotations[i];
            // 물리 시뮬레이션 재개
            rb.isKinematic = false;
            
            // Rigidbody 깨우기
            rb.WakeUp();
            
        }
    }

    void PrepareForThrow()
    {
        // 현재 위치에서 속도와 각속도만 초기화 (위치는 그대로 유지)
        if (yutSticks == null) return;
        for (int i = 0; i < yutSticks.Length; i++)
        {
            var rb = yutSticks[i];
            if (rb == null) continue;
            // 속도만 초기화 (현재 위치는 그대로)
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    Vector3 GetAverageBoardCenter()
    {
        if (boardCollider != null) return boardCollider.bounds.center + Vector3.up * 0.05f;
        return transform.position + Vector3.up * 0.05f;
    }

    IEnumerator ApplyThrowForcesSequential()
    {
        if (yutSticks == null) yield break;
        for (int i = 0; i < yutSticks.Length; i++)
        {
            var rb = yutSticks[i];
            if (rb == null) continue;
            
            // kinematic 상태가 아닌지 확인
            if (rb.isKinematic)
            {
                rb.isKinematic = false;
            }
            
            // Rigidbody를 깨우기
            rb.WakeUp();
            
            // Rigidbody 제약 확인
            if (rb.constraints != RigidbodyConstraints.None)
            {
                rb.constraints = RigidbodyConstraints.None; // 모든 제약 해제
            }
            
            // 위로 쏘아 올리는 힘 + 약간의 수평 흔들림
            Vector3 lateral = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * lateralJitter;
            Vector3 velocity = Vector3.up * upwardForce + lateral;
            
            // 속도를 직접 설정
            rb.linearVelocity = velocity;
            
            // 랜덤 토크
            Vector3 angularVelocity = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * torqueForce;
            rb.angularVelocity = angularVelocity;
            
            if (fireInterval > 0f)
                yield return new WaitForSeconds(fireInterval);
        }
    }

    IEnumerator WaitUntilSticksSettle()
    {
        float stableTimer = 0f;
        float elapsed = 0f;
        while (elapsed < maxWaitTime)
        {
            bool allSlow = true;
            for (int i = 0; i < yutSticks.Length; i++)
            {
                var rb = yutSticks[i];
                if (rb == null) continue;
                if (rb.linearVelocity.magnitude > settleVelocityThreshold || rb.angularVelocity.magnitude > settleAngularVelocityThreshold)
                {
                    allSlow = false;
                    break;
                }
            }

            if (allSlow)
            {
                stableTimer += Time.deltaTime;
                if (stableTimer >= settleStableTime)
                {
                    yield break;
                }
            }
            else
            {
                stableTimer = 0f;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    YutOutcome JudgeOutcome()
    {
        // 보드 밖(낙) 우선 판정
        if (IsNak()) return YutOutcome.Nak;

        int upCount = 0;
        bool[] isUpSide = new bool[yutSticks.Length]; // 실제로 위로 향하는지 (뒤집히지 않은 형태)
        
        for (int i = 0; i < yutSticks.Length; i++)
        {
            var rb = yutSticks[i];
            if (rb == null) continue;
            // 스틱의 로컬 업을 월드로 변환 후 월드 업과 내적
            Vector3 stickUp = rb.transform.TransformDirection(localUpAxis).normalized;
            float d = Vector3.Dot(stickUp, Vector3.up);
            // 위/아래 판정 반대로: d < -upDotThreshold가 실제로 위로 향함 (뒤집히지 않은 형태)
            isUpSide[i] = d < -upDotThreshold;  // 뒤집혔다면 안뒤집힌 형태이므로 실제로는 위로 향함
            if (isUpSide[i])
            {
                upCount += 1;
            }
        }

        // 빽도 판정: BackYut(첫 번째 윷)이 뒤집혔고, 나머지 3개는 뒤집히지 않은 도 형태
        // 뒤집혔다 = 안뒤집힌 형태 = 실제로 위로 향함 = isUpSide[i] = true
        // 뒤집히지 않았다 = 뒤집힌 형태 = 실제로 아래로 향함 = isUpSide[i] = false
        // 즉, 첫 번째 윷은 뒤집혔다 (안뒤집힌 형태 = 위로 = isUpSide[0] = true)
        // 나머지 3개는 뒤집히지 않았다 (뒤집힌 형태 = 아래로 = isUpSide[1,2,3] = false)
        // 결과적으로 도가 나옴 (upCount == 1)
        bool isBackDo = isUpSide[0] && !isUpSide[1] && !isUpSide[2] && !isUpSide[3] && upCount == 1;
        if (isBackDo)
        {
            // 빽도로 처리하기 위해 특별한 상태로 반환하지 않고, 
            // 나중에 YutGameManager에서 빽도 판정할 수 있도록 도를 반환
            // (YutGameManager에서 별도로 판정함)
            return YutOutcome.Do;
        }

        // 위/아래 판정이 반대이므로 결과도 반대로 매핑
        // 모/윷이 뒤바뀐 문제 수정: upCount 0과 4를 교체
        // 도/걸이 뒤바뀐 문제 수정: upCount 1과 3을 교체
        if (upCount == 0) return YutOutcome.Mo;  // 0개 실제 위 → 모
        if (upCount == 4) return YutOutcome.Yut; // 4개 실제 위 → 윷
        if (upCount == 1) return YutOutcome.Do;   // 1개 실제 위 → 도 (도와 걸 교체)
        if (upCount == 2) return YutOutcome.Gae;  // 2개 실제 위 → 개
        if (upCount == 3) return YutOutcome.Geol; // 3개 실제 위 → 걸 (도와 걸 교체)
        
        return YutOutcome.Nak;
    }
    
    // 빽도인지 판정하는 공개 메서드
    public bool IsBackDo()
    {
        if (yutSticks == null || yutSticks.Length < 4) return false;
        
        bool[] isUpSide = new bool[yutSticks.Length];
        int upCount = 0;
        
        for (int i = 0; i < yutSticks.Length; i++)
        {
            var rb = yutSticks[i];
            if (rb == null) continue;
            Vector3 stickUp = rb.transform.TransformDirection(localUpAxis).normalized;
            float d = Vector3.Dot(stickUp, Vector3.up);
            // 위/아래 판정 반대로: d < -upDotThreshold가 실제로 위로 향함
            isUpSide[i] = d < -upDotThreshold;
            if (isUpSide[i])
            {
                upCount += 1;
            }
        }
        
        // 빽도: BackYut(첫 번째 윷)이 뒤집혔고, 나머지 3개는 뒤집히지 않은 도 형태
        // 뒤집혔다 = 안뒤집힌 형태 = 실제로 위로 향함 = isUpSide[0] = true
        // 뒤집히지 않았다 = 뒤집힌 형태 = 실제로 아래로 향함 = isUpSide[1,2,3] = false
        // 결과적으로 도가 나옴 (upCount == 1, 하지만 실제로는 3개가 위로 향함이므로 도)
        return isUpSide[0] && !isUpSide[1] && !isUpSide[2] && !isUpSide[3] && upCount == 1;
    }

    bool IsNak()
    {
        if (boardCollider == null || yutSticks == null) return false;
        
        // BoardRange의 MeshCollider를 사용해서 낙 판정만 수행 (물리 충돌은 아님)
        // 보드의 MeshCollider는 윷과의 물리 충돌을 위해 별도로 설정 필요
        return IsNakCircularMesh(boardCollider);
    }
    
    bool IsNakCircularMesh(MeshCollider meshCollider)
    {
        if (meshCollider == null)
        {
            Debug.LogError("IsNakCircularMesh: meshCollider가 null입니다!");
            return false;
        }
        
        // MeshCollider의 Mesh가 있는지 확인
        if (meshCollider.sharedMesh == null)
        {
            Debug.LogError($"IsNakCircularMesh: {meshCollider.gameObject.name}의 MeshCollider에 Mesh가 할당되지 않았습니다!");
            return false;
        }
        
        // MeshCollider의 Bounds를 사용해서 원형 영역 계산
        Bounds bounds = meshCollider.bounds;
        
        // Bounds가 제대로 계산되지 않은 경우 (size가 0인 경우) Mesh의 bounds 사용
        Vector3 center;
        if (bounds.size == Vector3.zero || (bounds.size.y == 0 && bounds.min.y == bounds.max.y))
        {
            Bounds meshBounds = meshCollider.sharedMesh.bounds;
            // Transform을 적용
            center = meshCollider.transform.TransformPoint(meshBounds.center);
            Vector3 size = Vector3.Scale(meshBounds.size, meshCollider.transform.lossyScale);
            bounds = new Bounds(center, size);
        }
        else
        {
            center = bounds.center;
        }
        
        // XZ 평면에서의 반지름 계산 (bounds의 X, Z 크기 중 큰 값의 절반)
        float radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;
        
        // XZ 평면에서만 거리 체크 (Y축 무시)
        for (int i = 0; i < yutSticks.Length; i++)
        {
            var rb = yutSticks[i];
            if (rb == null) continue;
            
            Vector3 pos = rb.transform.position;
            // XZ 평면에서의 거리만 계산 (Y축 무시)
            Vector2 centerXZ = new Vector2(center.x, center.z);
            Vector2 posXZ = new Vector2(pos.x, pos.z);
            float distance = Vector2.Distance(centerXZ, posXZ);
            
            // 반지름보다 멀리 있으면 낙
            if (distance > radius)
            {
                return true;
            }
        }
        return false;
    }
}
