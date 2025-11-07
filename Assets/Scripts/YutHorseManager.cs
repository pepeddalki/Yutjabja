using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class YutHorseManager : MonoBehaviour
{
    [Header("Horse Components")]
    public PlayerController[] players;
    public Transform[] boardPositions;
    public Material selectableHorseMaterial;
    
    [System.NonSerialized]
    private int[] playerPositions;
    [System.NonSerialized]
    private Vector3[] horseInitialPositions;
    [System.NonSerialized]
    private Dictionary<int, GameObject> horseCountUI = new Dictionary<int, GameObject>();
    [System.NonSerialized]
    private List<int> selectableHorseIndices = new List<int>();
    
    public void Initialize(int[] positions, Vector3[] initialPositions)
    {
        playerPositions = positions;
        horseInitialPositions = initialPositions;
        
        if (horseCountUI == null)
        {
            horseCountUI = new Dictionary<int, GameObject>();
        }
        
        if (selectableHorseIndices == null)
        {
            selectableHorseIndices = new List<int>();
        }
    }
    
    public Vector3 CalculateHorsePosition(int horseIndex, Vector3 basePosition)
    {
        if (horseIndex < 0 || horseIndex >= playerPositions.Length) return basePosition;
        
        // 모든 말을 발판 중앙에 동일하게 배치 (바바리안과 기사 구분 없이)
        // 적의 말을 잡기 위해 같은 발판에 있으면 같은 좌표에 배치
        return basePosition;
    }
    
    public void UpdateHorseCountUI(int horseIndex, int count)
    {
        if (count <= 1)
        {
            // 개수가 1 이하면 UI 제거
            if (horseCountUI.ContainsKey(horseIndex))
            {
                if (horseCountUI[horseIndex] != null)
                {
                    Destroy(horseCountUI[horseIndex]);
                }
                horseCountUI.Remove(horseIndex);
            }
            return;
        }
        
        // 개수가 2 이상이면 UI 표시
        if (players == null || horseIndex < 0 || horseIndex >= players.Length || players[horseIndex] == null)
        {
            return;
        }
        
        GameObject horse = players[horseIndex].gameObject;
        
        // UI가 없으면 생성
        if (!horseCountUI.ContainsKey(horseIndex) || horseCountUI[horseIndex] == null)
        {
            // TextMeshPro UI 생성 (캐릭터 위에 표시)
            GameObject uiObj = new GameObject($"HorseCountUI_{horseIndex}");
            uiObj.transform.SetParent(horse.transform);
            
            // 캐릭터의 실제 높이 계산 (머리 위에 배치)
            float characterHeight = 0f;
            Renderer renderer = horse.GetComponent<Renderer>();
            if (renderer != null)
            {
                characterHeight = renderer.bounds.size.y;
            }
            else
            {
                // Renderer가 없으면 자식에서 찾기
                renderer = horse.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    characterHeight = renderer.bounds.size.y;
                }
                else
                {
                    // 기본 높이 (약 2유닛)
                    characterHeight = 2f;
                }
            }
            
            // 머리 위에 배치 (Pos Y = 6)
            float offsetY = 6f;
            uiObj.transform.localPosition = new Vector3(0f, offsetY, 0f);
            
            TextMeshPro textMesh = uiObj.AddComponent<TextMeshPro>();
            textMesh.text = $"x{count}";
            textMesh.fontSize = 16f;
            textMesh.color = Color.white;
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.sortingOrder = 100;
            
            // 폰트 할당 (중요! 폰트가 없으면 텍스트가 보이지 않음)
            if (textMesh.font == null)
            {
                // 기본 TMP 폰트 찾기 (여러 경로 시도)
                TMPro.TMP_FontAsset font = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (font == null)
                {
                    font = Resources.FindObjectsOfTypeAll<TMPro.TMP_FontAsset>().FirstOrDefault();
                }
                if (font != null)
                {
                    textMesh.font = font;
                }
                else
                {
                    Debug.LogWarning($"TextMeshPro 폰트를 찾을 수 없습니다. Unity에서 Window > TextMeshPro > Import TMP Essential Resources를 실행해주세요.");
                }
            }
            
            // 항상 카메라를 향하도록
            uiObj.AddComponent<Billboard>().targetCamera = Camera.main;
            
            horseCountUI[horseIndex] = uiObj;
        }
        else
        {
            // UI 텍스트 업데이트
            TextMeshPro textMesh = horseCountUI[horseIndex].GetComponent<TextMeshPro>();
            if (textMesh != null)
            {
                textMesh.text = $"x{count}";
            }
        }
    }
    
    public void RefreshHorsesAtPosition(int positionIndex, string[] positionNames)
    {
        // positionIndex 범위 검증
        if (positionIndex < -1 || (positionIndex >= 0 && (boardPositions == null || positionIndex >= boardPositions.Length)))
        {
            Debug.LogWarning($"RefreshHorsesAtPosition: 잘못된 positionIndex {positionIndex} (boardPositions.Length: {boardPositions?.Length ?? 0})");
            return;
        }
        
        List<int> horsesAtPos = new List<int>();
        for (int i = 0; i < playerPositions.Length; i++)
        {
            if (playerPositions[i] == positionIndex && players[i] != null)
            {
                horsesAtPos.Add(i);
            }
        }
        
        if (horsesAtPos.Count == 0)
        {
            return;
        }
        
        // 위치 인덱스가 -1이면 대기공간 (각 말의 초기 좌표 사용)
        if (positionIndex == -1 && horseInitialPositions != null)
        {
            // 각 말을 자신의 대기공간 좌표로 배치
            foreach (int hIndex in horsesAtPos)
            {
                if (players[hIndex] != null && players[hIndex].transform != null && hIndex < horseInitialPositions.Length)
                {
                    Vector3 initialPos = horseInitialPositions[hIndex];
                    
                    // NavMeshAgent 비활성화 후 위치 설정 (y좌표 증가 방지 및 위치 고정)
                    UnityEngine.AI.NavMeshAgent agent = players[hIndex].GetComponent<UnityEngine.AI.NavMeshAgent>();
                    bool wasEnabled = agent != null && agent.enabled;
                    if (agent != null) agent.enabled = false;
                    
                    // 실제 transform.position을 대기공간 좌표로 설정
                    players[hIndex].transform.position = initialPos;
                    
                    // 위치 설정 후 다시 활성화하고 Warp로 위치 고정
                    if (agent != null && wasEnabled)
                    {
                        agent.enabled = true;
                        agent.Warp(initialPos); // NavMesh 위로 강제 이동
                    }
                }
            }
        }
        else if (positionIndex >= 0 && positionIndex < boardPositions.Length && boardPositions[positionIndex] != null)
        {
            // 모든 말을 발판 중앙의 동일한 위치에 배치 (바바리안과 기사 구분 없이)
            // 적의 말을 잡기 위해 같은 발판에 있으면 같은 좌표에 배치
            Vector3 basePos = boardPositions[positionIndex].position;
            Vector3 finalPos = YutGameUtils.GetValidNavMeshPosition(basePos);
            
            // 모든 말을 같은 위치에 배치
            foreach (int hIndex in horsesAtPos)
            {
                if (players[hIndex] != null && players[hIndex].transform != null)
                {
                    players[hIndex].transform.position = finalPos;
                }
            }
        }
        else
        {
            Debug.LogWarning($"RefreshHorsesAtPosition: 잘못된 positionIndex {positionIndex} (범위: -1 ~ {boardPositions.Length - 1})");
        }
        
        // 바바리안과 기사로 그룹화하여 UI 처리
        List<int> barbariansAtPos = new List<int>();
        List<int> knightsAtPos = new List<int>();
        
        foreach (int hIndex in horsesAtPos)
        {
            if (hIndex < 4)
            {
                barbariansAtPos.Add(hIndex);
            }
            else
            {
                knightsAtPos.Add(hIndex);
            }
        }
        
        // 대기공간(-1)에서는 업힘 처리를 하지 않음 (각 말이 자신의 좌표에 있음)
        if (positionIndex == -1)
        {
            // 대기공간에서는 모든 말을 보이게 하고, 각각 자신의 좌표에 배치 (이미 RefreshHorsesAtPosition에서 처리됨)
            // UI는 각 말마다 개별적으로 표시 (업힘 없음)
            foreach (int hIndex in horsesAtPos)
            {
                if (players[hIndex] != null && players[hIndex].transform != null)
                {
                    players[hIndex].gameObject.SetActive(true);
                    UpdateHorseCountUI(hIndex, 1); // 각 말마다 개별 UI (x1)
                }
            }
        }
        else
        {
            // 바바리안 처리: 첫 번째 말만 보이게, 나머지는 숨기기
            ProcessHorseGroup(barbariansAtPos, positionIndex);
            
            // 기사 처리: 첫 번째 말만 보이게, 나머지는 숨기기
            ProcessHorseGroup(knightsAtPos, positionIndex);
        }
    }
    
    private void ProcessHorseGroup(List<int> horseIndices, int positionIndex)
    {
        if (horseIndices.Count == 0) return;
        
        // 첫 번째 말을 기준으로 정렬 (인덱스 순서)
        horseIndices.Sort();
        
        // 모든 말을 같은 위치(발판 중앙)에 배치 (RefreshHorsesAtPosition에서 이미 배치됨)
        // 첫 번째 말만 보이게 하고 나머지는 숨기기
        for (int i = 0; i < horseIndices.Count; i++)
        {
            int hIndex = horseIndices[i];
            if (players[hIndex] == null || players[hIndex].transform == null) continue;
            
            // 첫 번째 말만 보이게, 나머지는 숨기기
            if (i == 0)
            {
                // 첫 번째 말 보이기
                players[hIndex].gameObject.SetActive(true);
                
                // 같은 팀 말 개수 UI 표시 (2개 이상일 때만)
                if (horseIndices.Count >= 2)
                {
                    UpdateHorseCountUI(hIndex, horseIndices.Count);
                }
                else
                {
                    // 말이 하나만 있으면 UI 제거
                    UpdateHorseCountUI(hIndex, 1);
                }
            }
            else
            {
                // 나머지 말 숨기기
                players[hIndex].gameObject.SetActive(false);
                
                // 숨겨진 말의 UI 제거
                UpdateHorseCountUI(hIndex, 1);
            }
        }
    }
    
    // 개별 말의 UI만 업데이트 (대기공간에서 잡힌 말 처리용)
    public void UpdateHorseUI(int horseIndex, int positionIndex, string[] positionNames)
    {
        if (horseIndex < 0 || horseIndex >= players.Length || players[horseIndex] == null)
        {
            return;
        }
        
        // 대기공간이면 각 말의 좌표로 배치
        if (positionIndex == -1 && horseInitialPositions != null && horseIndex < horseInitialPositions.Length)
        {
            Vector3 initialPos = horseInitialPositions[horseIndex];
            
            // NavMeshAgent 비활성화 후 위치 설정
            UnityEngine.AI.NavMeshAgent agent = players[horseIndex].GetComponent<UnityEngine.AI.NavMeshAgent>();
            bool wasEnabled = agent != null && agent.enabled;
            if (agent != null) agent.enabled = false;
            
            players[horseIndex].transform.position = initialPos;
            players[horseIndex].gameObject.SetActive(true);
            
            if (agent != null && wasEnabled)
            {
                agent.enabled = true;
                agent.Warp(initialPos);
            }
            
            // UI는 개별 표시 (업힘 없음)
            UpdateHorseCountUI(horseIndex, 1);
        }
    }
    
    public void ShowSelectableHorses(List<int> horseIndices)
    {
        HideSelectableHorses();
        
        selectableHorseIndices = new List<int>(horseIndices);
        
        if (selectableHorseMaterial == null)
        {
            return;
        }
        
        // 말에 하이라이트 머티리얼 적용
        foreach (int horseIndex in horseIndices)
        {
            // TODO: 말에 하이라이트 효과 추가 (예: 머티리얼 변경, 빛 효과 등)
        }
    }
    
    public void HideSelectableHorses()
    {
        selectableHorseIndices.Clear();
        // TODO: 말 하이라이트 효과 제거
    }
    
    public bool IsHorseSelectable(int index)
    {
        return selectableHorseIndices.Contains(index);
    }
    
    public List<int> GetSelectableHorseIndices()
    {
        return selectableHorseIndices;
    }
    
    // 말이 업혀있는지 확인 (horseCountUI 체크)
    public bool HasHorseCountUI(int horseIndex)
    {
        return horseCountUI != null && horseCountUI.ContainsKey(horseIndex) && horseCountUI[horseIndex] != null;
    }
    
    // Billboard 컴포넌트 (카메라를 향함)
    [System.Serializable]
    public class Billboard : MonoBehaviour
    {
        [System.NonSerialized]
        public Camera targetCamera;
        
        void LateUpdate()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
            
            if (targetCamera != null)
            {
                transform.LookAt(transform.position + targetCamera.transform.rotation * Vector3.forward,
                                targetCamera.transform.rotation * Vector3.up);
            }
        }
    }
}

