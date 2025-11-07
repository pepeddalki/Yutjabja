using UnityEngine;
using System.Collections.Generic;

public class YutPlatformManager : MonoBehaviour
{
    [Header("Platform Components")]
    public Transform[] boardPositions;
    public Material goldenPlatformMaterial;
    public Material selectablePlatformMaterial;
    
    [System.NonSerialized]
    private Renderer[] platformRenderers;
    [System.NonSerialized]
    private Material[] originalMaterials;
    [System.NonSerialized]
    private int goldenPlatformIndex = -1;
    [System.NonSerialized]
    private List<int> selectablePlatformIndices = new List<int>();
    
    public int GoldenPlatformIndex => goldenPlatformIndex;
    
    public void InitializePlatformRenderers()
    {
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
        
        // 랜덤으로 새 황금 발판 선택 (0번 제외 - 시작 위치)
        if (boardPositions.Length > 1)
        {
            goldenPlatformIndex = Random.Range(1, boardPositions.Length);
        }
        else
        {
            goldenPlatformIndex = 0;
        }
        
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
                // 발판에 하이라이트 머티리얼 적용
                Material[] materials = platformRenderers[index].materials;
                if (materials.Length > 0)
                {
                    materials[0] = selectablePlatformMaterial;
                    platformRenderers[index].materials = materials;
                }
            }
        }
    }
    
    public void HideSelectablePlatforms()
    {
        foreach (int index in selectablePlatformIndices)
        {
            if (index >= 0 && index < platformRenderers.Length && platformRenderers[index] != null && originalMaterials[index] != null)
            {
                // 원본 머티리얼로 복구
                Material[] materials = platformRenderers[index].materials;
                if (materials.Length > 0)
                {
                    materials[0] = originalMaterials[index];
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

