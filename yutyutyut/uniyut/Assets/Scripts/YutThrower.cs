using UnityEngine;
using System.Collections;

public class YutThrower : MonoBehaviour
{
    [Header("Yut Pieces")]
    public GameObject[] yutPieces; // 윷 조각들
    public Transform throwPosition;
    public float throwForce = 10f;
    public float throwTorque = 5f;
    
    [Header("Animation")]
    public float animationDuration = 2f;
    
    public IEnumerator ThrowAnimation()
    {
        // 윷 조각들을 던지는 애니메이션
        foreach (GameObject yut in yutPieces)
        {
            if (yut != null)
            {
                Rigidbody rb = yut.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // 랜덤한 방향으로 던지기
                    Vector3 randomDirection = Random.insideUnitSphere;
                    randomDirection.y = Mathf.Abs(randomDirection.y); // 위쪽으로 던지기
                    
                    rb.AddForce(randomDirection * throwForce, ForceMode.Impulse);
                    rb.AddTorque(Random.insideUnitSphere * throwTorque, ForceMode.Impulse);
                }
            }
        }
        
        yield return new WaitForSeconds(animationDuration);
    }
    
    // 윷 결과 계산 (실제로는 윷 조각들의 상태를 보고 판단해야 함)
    public int CalculateYutResult()
    {
        // 임시로 랜덤 결과 반환
        return Random.Range(1, 6);
    }
}
