using UnityEngine;

// 황금발판 효과 종류
public enum GoldenEffectType
{
    ExtraThrow,      // 한 번 더 던지기
    Shield,          // 보호막 효과
    Reroll,          // 선택권 효과 (다시 던지기)
    NakProtection    // 낙 방지 아이템
}

// 황금발판 효과 관리 클래스
public class GoldenPlatformEffect : MonoBehaviour
{
    [Header("Effect State")]
    [System.NonSerialized]
    public bool[] hasShield = new bool[8];           // 보호막 보유 여부
    [System.NonSerialized]
    public bool[] hasRerollChance = new bool[8];     // 다시 던지기 기회 보유 여부
    [System.NonSerialized]
    public bool[] hasNakProtection = new bool[8];    // 낙 방지 아이템 보유 여부
    [System.NonSerialized]
    private int[] shieldGrantedTurn = new int[8];    // 보호막을 받은 턴 (0: 메이지, 1: 기사)
    
    [System.NonSerialized]
    private GameObject[] shieldVisuals = new GameObject[8]; // 보호막 시각 효과

    private YutGameManager gameManager;
    
    public void Initialize(YutGameManager manager)
    {
        gameManager = manager;
        
        // 배열 초기화
        hasShield = new bool[8];
        hasRerollChance = new bool[8];
        hasNakProtection = new bool[8];
        shieldGrantedTurn = new int[8];
        shieldVisuals = new GameObject[8];
        
        // 초기값 설정
        for (int i = 0; i < 8; i++)
        {
            shieldGrantedTurn[i] = -1; // -1은 보호막 없음
        }
    }

    
    // 랜덤 효과 부여
    public GoldenEffectType GrantRandomEffect(int horseIndex)
    {
        // 랜덤으로 효과 선택 (0~3)
        int randomValue = Random.Range(0, 4);
        GoldenEffectType effect = (GoldenEffectType)randomValue;
        
        Debug.Log($"[황금발판 랜덤] 랜덤 값: {randomValue}, 효과: {effect}");
        
        ApplyEffect(horseIndex, effect);
        
        return effect;
    }

    
    // 효과 적용
    public void ApplyEffect(int horseIndex, GoldenEffectType effect)
    {
        switch (effect)
        {
            case GoldenEffectType.ExtraThrow:
                ApplyExtraThrow();
                break;
                
            case GoldenEffectType.Shield:
                ApplyShield(horseIndex);
                break;
                
            case GoldenEffectType.Reroll:
                ApplyReroll(horseIndex);
                break;
                
            case GoldenEffectType.NakProtection:
                ApplyNakProtection(horseIndex);
                break;
        }
    }
    
    // 1. 한 번 더 던지기
    private void ApplyExtraThrow()
    {
        if (gameManager != null)
        {
            gameManager.hasExtraThrow = true;
            gameManager.canThrowAgain = true;
            gameManager.extraThrowCount++; // 추가 던지기 횟수 증가
            
            // 즉시 버튼 활성화
            if (gameManager.throwButton != null)
            {
                gameManager.throwButton.interactable = true;
            }
            gameManager.EnableTestButtons(true);
            
            Debug.Log($"[황금발판 효과] 한 번 더 던지기 부여! extraThrowCount={gameManager.extraThrowCount}");
        }
    }


    
    // 2. 보호막 효과
    private void ApplyShield(int horseIndex)
    {
        hasShield[horseIndex] = true;
        
        // 보호막을 받은 턴 저장
        if (gameManager != null)
        {
            shieldGrantedTurn[horseIndex] = gameManager.currentTurnIndex;
            Debug.Log($"[황금발판 효과] 말 {horseIndex}에게 보호막 부여! (턴: {shieldGrantedTurn[horseIndex]})");
        }
        
        CreateShieldVisual(horseIndex);
    }

    
    // 3. 선택권 효과 (다시 던지기)
    private void ApplyReroll(int horseIndex)
    {
        hasRerollChance[horseIndex] = true;
        Debug.Log($"[황금발판 효과] 말 {horseIndex}에게 다시 던지기 기회 부여!");
    }
    
    // 4. 낙 방지 아이템
    private void ApplyNakProtection(int horseIndex)
    {
        hasNakProtection[horseIndex] = true;
        Debug.Log($"[황금발판 효과] 말 {horseIndex}에게 낙 방지 아이템 부여!");
    }
    
    // 보호막 시각 효과 생성
    private void CreateShieldVisual(int horseIndex)
    {
        if (gameManager == null || gameManager.players == null || horseIndex >= gameManager.players.Length)
            return;
            
        if (gameManager.players[horseIndex] == null)
            return;
        
        // 기존 보호막 제거
        if (shieldVisuals[horseIndex] != null)
        {
            Destroy(shieldVisuals[horseIndex]);
        }
        
        // 반투명 구체 생성
        GameObject shield = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shield.name = $"Shield_{horseIndex}";
        shield.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        shield.transform.SetParent(gameManager.players[horseIndex].transform);
        shield.transform.localPosition = Vector3.zero;
        shield.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
        
        // Collider 제거 (충돌 방지)
        Collider shieldCollider = shield.GetComponent<Collider>();
        if (shieldCollider != null)
        {
            Destroy(shieldCollider);
        }
        
                // 반투명 파란색 머티리얼 생성
        Renderer shieldRenderer = shield.GetComponent<Renderer>();
        if (shieldRenderer != null)
        {
            // Unlit/Transparent 셰이더 사용 (가장 호환성 좋음)
            Shader shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            }
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            
            if (shader != null)
            {
                Material shieldMaterial = new Material(shader);
                shieldMaterial.color = new Color(0.3f, 0.6f, 1f, 0.3f); // 반투명 파란색
                
                // Standard 셰이더인 경우에만 투명도 설정
                if (shader.name.Contains("Standard"))
                {
                    shieldMaterial.SetFloat("_Mode", 3); // Transparent mode
                    shieldMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    shieldMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    shieldMaterial.SetInt("_ZWrite", 0);
                    shieldMaterial.DisableKeyword("_ALPHATEST_ON");
                    shieldMaterial.EnableKeyword("_ALPHABLEND_ON");
                    shieldMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    shieldMaterial.renderQueue = 3000;
                }
                
                shieldRenderer.material = shieldMaterial;
                shieldRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                
                Debug.Log($"[보호막] 셰이더 사용: {shader.name}");
            }
            else
            {
                Debug.LogError("[보호막] 사용 가능한 셰이더를 찾을 수 없습니다!");
            }
        }

        
        shieldVisuals[horseIndex] = shield;
        
        // 회전 애니메이션 추가
        ShieldRotation rotation = shield.AddComponent<ShieldRotation>();
    }
    
    // 보호막 제거
    public void RemoveShield(int horseIndex)
    {
        hasShield[horseIndex] = false;
        
        if (shieldVisuals[horseIndex] != null)
        {
            Destroy(shieldVisuals[horseIndex]);
            shieldVisuals[horseIndex] = null;
        }
        
        Debug.Log($"[황금발판 효과] 말 {horseIndex}의 보호막 제거!");
    }

    // 턴 시작 시 보호막 체크 (내 턴이 다시 오면 보호막 제거)
    public void CheckShieldExpiration()
    {
        if (gameManager == null) return;
        
        int currentTurn = gameManager.currentTurnIndex;
        
        for (int i = 0; i < 8; i++)
        {
            if (hasShield[i])
            {
                // 보호막을 받은 턴과 현재 턴이 같으면 (내 턴이 다시 돌아옴)
                if (shieldGrantedTurn[i] == currentTurn)
                {
                    RemoveShield(i);
                    Debug.Log($"[보호막] 말 {i}의 보호막이 만료되어 제거됨 (턴: {currentTurn})");
                }
            }
        }
    }
    
    // 말이 잡힐 때 보호막 체크
    public bool IsProtectedByShield(int horseIndex)
    {
        if (hasShield[horseIndex])
        {
            RemoveShield(horseIndex); // 보호막 사용 후 제거
            return true;
        }
        return false;
    }
    
    // 다시 던지기 기회 사용
    public void UseRerollChance(int horseIndex)
    {
        hasRerollChance[horseIndex] = false;
        Debug.Log($"[황금발판 효과] 말 {horseIndex}의 다시 던지기 기회 사용!");
    }
    
    // 낙 방지 아이템 사용
    public void UseNakProtection(int horseIndex)
    {
        hasNakProtection[horseIndex] = false;
        Debug.Log($"[황금발판 효과] 말 {horseIndex}의 낙 방지 아이템 사용!");
    }
    
    // 효과 이름 가져오기
    public static string GetEffectName(GoldenEffectType effect)
    {
        switch (effect)
        {
            case GoldenEffectType.ExtraThrow:
                return "한 번 더 던지기";
            case GoldenEffectType.Shield:
                return "보호막";
            case GoldenEffectType.Reroll:
                return "다시 던지기 기회";
            case GoldenEffectType.NakProtection:
                return "낙 방지";
            default:
                return "알 수 없는 효과";
        }
    }
    
    // 현재 플레이어의 말들이 가진 효과 확인
    public bool CurrentPlayerHasReroll()
    {
        if (gameManager == null) return false;
        
        int playerIndex = gameManager.GetCurrentPlayerIndex();
        int startIndex = playerIndex * 4;
        int endIndex = startIndex + 4;
        
        for (int i = startIndex; i < endIndex; i++)
        {
            if (hasRerollChance[i])
                return true;
        }
        
        return false;
    }
    
    // 현재 플레이어의 말들이 가진 낙 방지 확인
    public bool CurrentPlayerHasNakProtection()
    {
        if (gameManager == null) return false;
        
        int playerIndex = gameManager.GetCurrentPlayerIndex();
        int startIndex = playerIndex * 4;
        int endIndex = startIndex + 4;
        
        for (int i = startIndex; i < endIndex; i++)
        {
            if (hasNakProtection[i])
                return true;
        }
        
        return false;
    }
}

// 보호막 회전 애니메이션
public class ShieldRotation : MonoBehaviour
{
    public float rotationSpeed = 30f;
    
    void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }
}
