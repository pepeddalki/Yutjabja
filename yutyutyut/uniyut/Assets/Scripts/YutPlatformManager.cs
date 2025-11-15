using UnityEngine;
using System.Collections.Generic;

public class YutPlatformManager : MonoBehaviour
{
    [Header("Platform Components")]
    public Transform[] boardPositions;
    public Material goldenPlatformMaterial;
    public Material selectablePlatformMaterial;
    
    [System.NonSerialized]
    private YutGameManager gameManager;
    [System.NonSerialized]
    private Renderer[] platformRenderers;
    [System.NonSerialized]
    private Material[] originalMaterials;
    [System.NonSerialized]
    private int goldenPlatformIndex = -1;
    [System.NonSerialized]
    private List<int> selectablePlatformIndices = new List<int>();
    [System.NonSerialized]
    private GameObject goldenPlatformHighlight; // 황금발판 위 빨간색 표시 오브젝트
    
    public int GoldenPlatformIndex => goldenPlatformIndex;
    
    public void InitializePlatformRenderers()
    {
        if (gameManager == null)
        {
            gameManager = GetComponent<YutGameManager>();
        }

        if (boardPositions == null || boardPositions.Length == 0) return;
        
        platformRenderers = new Renderer[boardPositions.Length];
        originalMaterials = new Material[boardPositions.Length];
        
        for (int i = 0; i < boardPositions.Length; i++)
        {
            if (boardPositions[i] != null)
            {
                // 발판의 Renderer 찾기 (자식 객체 포함)
                platformRenderers[i] = boardPositions[i].GetComponent<Renderer>();
                if (platformRenderers[i] == null)
                {
                    platformRenderers[i] = boardPositions[i].GetComponentInChildren<Renderer>();
                }
                
                // 원본 머티리얼 저장
                if (platformRenderers[i] != null && platformRenderers[i].materials != null && platformRenderers[i].materials.Length > 0)
                {
                    originalMaterials[i] = platformRenderers[i].materials[0];
                }
                else if (platformRenderers[i] != null && platformRenderers[i].material != null)
                {
                    originalMaterials[i] = platformRenderers[i].material;
                }
            }
        }
        
        // 황금색 머티리얼 할당 확인
        if (goldenPlatformMaterial == null)
        {
            Debug.LogWarning("Golden Platform Material이 할당되지 않았습니다! Unity Inspector에서 StumpGolden 머티리얼을 할당해주세요.");
        }
    }
    
    public void SelectRandomGoldenPlatform()
    {
        if (boardPositions == null || boardPositions.Length == 0)
        {
            Debug.LogWarning("황금 발판 선택 실패: boardPositions가 null이거나 비어있습니다.");
            return;
        }
        
        // 이전 황금 발판 원래 색으로 복구
        if (goldenPlatformIndex >= 0 && goldenPlatformIndex < boardPositions.Length)
        {
            RestorePlatformColor(goldenPlatformIndex);
        }
        
        // 말이 있는 위치 목록 가져오기
        List<int> occupiedPositions = new List<int>();
        if (gameManager != null && gameManager.playerPositions != null)
        {
            for (int i = 0; i < gameManager.playerPositions.Length; i++)
            {
                int pos = gameManager.playerPositions[i];
                // 대기공간(-1)이나 완주(-2)가 아닌 위치만 추가
                if (pos >= 0 && !occupiedPositions.Contains(pos))
                {
                    occupiedPositions.Add(pos);
                }
            }
        }
        
        // 선택 가능한 발판 목록 생성 (0번 제외, 말이 있는 위치 제외)
        List<int> availablePositions = new List<int>();
        for (int i = 1; i < boardPositions.Length; i++)
        {
            if (!occupiedPositions.Contains(i))
            {
                availablePositions.Add(i);
            }
        }
        
        // 선택 가능한 발판이 없으면 0번 제외하고 아무 곳이나
        if (availablePositions.Count == 0)
        {
            Debug.LogWarning("선택 가능한 발판이 없습니다. 모든 발판에 말이 있습니다.");
            goldenPlatformIndex = Random.Range(1, boardPositions.Length);
        }
        else
        {
            // 랜덤으로 선택
            int randomIndex = Random.Range(0, availablePositions.Count);
            goldenPlatformIndex = availablePositions[randomIndex];
        }
        
        Debug.Log($"[황금발판] 새 위치 선택: {goldenPlatformIndex} (말이 있는 위치: {string.Join(", ", occupiedPositions)})");
        
        // 황금 발판으로 만들기
        MakePlatformGolden(goldenPlatformIndex);
    }

    
    public void MakePlatformGolden(int index)
    {
        if (index < 0 || index >= boardPositions.Length) return;
        if (platformRenderers == null || index >= platformRenderers.Length) return;
        if (platformRenderers[index] == null)
        {
            Debug.LogWarning($"발판 {index}에 Renderer가 없습니다!");
            return;
        }
        
        // Unity Inspector에서 할당한 황금색 머티리얼 적용
        if (goldenPlatformMaterial != null)
        {
            // materials 배열에 황금색 머티리얼 추가 또는 교체
            Material[] currentMaterials = platformRenderers[index].materials;
            
            if (currentMaterials.Length > 0)
            {
                // 첫 번째 머티리얼을 황금색으로 교체
                currentMaterials[0] = goldenPlatformMaterial;
            }
            else
            {
                // materials 배열이 비어있으면 황금색 머티리얼만 추가
                currentMaterials = new Material[] { goldenPlatformMaterial };
            }
            
            // materials 배열 적용
            platformRenderers[index].materials = currentMaterials;
        }
        else
        {
            Debug.LogError($"발판 {index}: 황금색 머티리얼이 null입니다! Unity Inspector에서 Golden Platform Material을 할당해주세요.");
        }
    }
    
    public void RestorePlatformColor(int index)
    {
        if (index < 0 || index >= boardPositions.Length) return;
        if (platformRenderers == null || index >= platformRenderers.Length) return;
        if (platformRenderers[index] == null) return;
        
        // 원본 머티리얼로 복구 (materials 배열 사용)
        if (originalMaterials[index] != null)
        {
            Material[] currentMaterials = platformRenderers[index].materials;
            
            if (currentMaterials.Length > 0)
            {
                // 첫 번째 머티리얼을 원본으로 교체
                currentMaterials[0] = originalMaterials[index];
            }
            else
            {
                // materials 배열이 비어있으면 원본 머티리얼만 추가
                currentMaterials = new Material[] { originalMaterials[index] };
            }
            
            // materials 배열 적용
            platformRenderers[index].materials = currentMaterials;
        }
        else
        {
            Debug.LogWarning($"발판 {index}의 원본 머티리얼이 null입니다!");
        }
    }
    
    public void ShowSelectablePlatforms(List<int> platformIndices)
    {
        // 기존 선택 표시 제거
        HideSelectablePlatforms();
        
        selectablePlatformIndices = new List<int>(platformIndices);
        
        if (selectablePlatformMaterial == null)
        {
            Debug.LogWarning("Selectable Platform Material이 할당되지 않았습니다!");
            return;
        }
        
        foreach (int index in platformIndices)
        {
            if (index >= 0 && index < platformRenderers.Length && platformRenderers[index] != null)
            {
                // 황금 발판은 황금색 유지하고 위에 빨간색 링 표시
                if (index == goldenPlatformIndex)
                {
                    // 황금 발판 위에 빨간색 링 생성
                    CreateGoldenPlatformHighlight(index);
                }
                else
                {
                    // 일반 발판에 하이라이트 머티리얼 적용
                    Material[] materials = new Material[platformRenderers[index].materials.Length];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = i == 0 ? selectablePlatformMaterial : platformRenderers[index].materials[i];
                    }
                    platformRenderers[index].materials = materials;
                }
            }
        }
    }
    
    // 황금 발판 위에 빨간색 표시 생성
    private void CreateGoldenPlatformHighlight(int index)
    {
        if (boardPositions == null || index < 0 || index >= boardPositions.Length || boardPositions[index] == null)
        {
            Debug.LogWarning($"[황금발판 하이라이트] 생성 실패 - 잘못된 인덱스: {index}");
            return;
        }
        
        // 이미 존재하면 제거
        if (goldenPlatformHighlight != null)
        {
            Destroy(goldenPlatformHighlight);
        }
        
        // 빨간색 원판 생성 (Cylinder를 얇게 만들어서 동그란 원판처럼)
        goldenPlatformHighlight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        goldenPlatformHighlight.name = "GoldenPlatformHighlight";
        goldenPlatformHighlight.layer = 0; // Default 레이어
        
        // 발판 위치보다 위로 배치
        Vector3 platformPos = boardPositions[index].position;
        goldenPlatformHighlight.transform.position = platformPos + new Vector3(0f, 2.0f, 0f); // 발판 위 2.0 유닛
        
        // 크기 조정 (넓고 얇은 원판)
        goldenPlatformHighlight.transform.localScale = new Vector3(2.5f, 0.1f, 2.5f); // 넓고 얇은 원판
        
        // RGB 색상으로 직접 머티리얼 생성 (r:198, g:36, b:70, a:255)
        Renderer highlightRenderer = goldenPlatformHighlight.GetComponent<Renderer>();
        if (highlightRenderer != null)
        {
            // 여러 셰이더 시도 (프로젝트 설정에 따라 다름)
            Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // URP
            if (shader == null)
            {
                shader = Shader.Find("Standard"); // Built-in
            }
            if (shader == null)
            {
                shader = Shader.Find("Diffuse"); // Legacy
            }
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color"); // Unlit
            }
            
            if (shader != null)
            {
                // 새로운 머티리얼 생성
                Material customMaterial = new Material(shader);
                // RGB 값을 0~1 범위로 변환 (255로 나눔)
                customMaterial.color = new Color(198f / 255f, 36f / 255f, 70f / 255f, 255f / 255f);
                
                // Standard 셰이더인 경우 추가 설정
                if (shader.name.Contains("Standard"))
                {
                    customMaterial.SetFloat("_Metallic", 0f);
                    customMaterial.SetFloat("_Glossiness", 0.5f);
                }
                
                highlightRenderer.material = customMaterial;
                highlightRenderer.enabled = true;
                highlightRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                highlightRenderer.receiveShadows = true;
                
                Debug.Log($"[황금발판 하이라이트] 커스텀 색상 적용 완료 - RGB(198, 36, 70), 셰이더: {shader.name}");
            }
            else
            {
                Debug.LogError("[황금발판 하이라이트] 사용 가능한 셰이더를 찾을 수 없습니다!");
            }
        }
        else
        {
            Debug.LogError("[황금발판 하이라이트] Renderer를 찾을 수 없습니다!");
        }
        
        // Collider 제거 (클릭 방해하지 않도록)
        Collider highlightCollider = goldenPlatformHighlight.GetComponent<Collider>();
        if (highlightCollider != null)
        {
            Destroy(highlightCollider);
        }
        
        // 오브젝트 활성화 확인
        goldenPlatformHighlight.SetActive(true);
        
        Debug.Log($"[황금발판 하이라이트] 생성 완료 - 위치: {goldenPlatformHighlight.transform.position}, 크기: {goldenPlatformHighlight.transform.localScale}, 활성화: {goldenPlatformHighlight.activeSelf}");
    }
    
    public void HideSelectablePlatforms()
    {
        foreach (int index in selectablePlatformIndices)
        {
            if (index >= 0 && index < platformRenderers.Length && platformRenderers[index] != null && originalMaterials[index] != null)
            {
                // 황금 발판은 빨간색 표시만 제거 (황금색은 유지)
                if (index == goldenPlatformIndex)
                {
                    // 빨간색 링 제거
                    if (goldenPlatformHighlight != null)
                    {
                        Destroy(goldenPlatformHighlight);
                        goldenPlatformHighlight = null;
                    }
                }
                else
                {
                    // 원본 머티리얼로 복구
                    Material[] materials = new Material[platformRenderers[index].materials.Length];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = i == 0 ? originalMaterials[index] : platformRenderers[index].materials[i];
                    }
                    platformRenderers[index].materials = materials;
                }
            }
        }
        
        selectablePlatformIndices.Clear();
    }
    
    public bool IsPlatformSelectable(int index)
    {
        return selectablePlatformIndices.Contains(index);
    }
}

