using UnityEngine;

public class Explosion : MonoBehaviour
{
    public Material glassMat;

    public float cubeSize = 0.2f;
    public int cubesInRow = 5;
    private bool _hasExploded;

    //피벗 계산에 사용할 변수
    float cubesPivotDistance;
    Vector3 cubesPivot;

    public float explosionRadius = 50f;
    public float explosionForce = 4f;
    public float explosionUpward = 0.4f;

    void Start()
    {
        //피벗 거리 계산
        cubesPivotDistance = cubeSize * cubesInRow / 2;
        //피벗 벡터 만드는 변수
        cubesPivot = new Vector3(cubesPivotDistance, cubesPivotDistance, cubesPivotDistance);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            explode();
        }
        
    }

    public void explode()
    {
        if (_hasExploded)
        {
            return;
        }

        _hasExploded = true;
        
        //125개 조각 생성
        for (int x = 0; x < cubesInRow; x++)
        {
            for (int y = 0;  y < cubesInRow; y++)
            {
                for (int z = 0; z < cubesInRow; z++)
                {
                    createPiece(x, y, z);
                }
            }
        }

        //폭발 위치 잡기
        Vector3 explosionPos = transform.position;
        //위치와 반경 콜라이더 잡기
        Collider[] colliders = Physics.OverlapSphere(explosionPos, explosionRadius);
        //오버랩된 스피어의 모든 콜라이더에 폭발력 가하기
        foreach (Collider hit in colliders)
        {
            //콜라이더 오브젝트에서 리지드바디 받기
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                //파라미터를 준 몸체에 폭발력 가하기
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius, explosionUpward);
            }
        }

        // Keep the root active so Photon RPCs on this object can still arrive.
        Invoke(nameof(HideOriginalObject), 0f);
    }

    private void HideOriginalObject()
    {
        foreach (Renderer targetRenderer in GetComponentsInChildren<Renderer>())
        {
            targetRenderer.enabled = false;
        }

        foreach (Collider targetCollider in GetComponentsInChildren<Collider>())
        {
            targetCollider.enabled = false;
        }

        if (TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    void createPiece(int x, int y, int z)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);

        piece.transform.position = transform.position + new Vector3(cubeSize * x, cubeSize * y, cubeSize * z) - cubesPivot;
        piece.transform.localScale = new Vector3(cubeSize, cubeSize, cubeSize);

        if (glassMat != null)
        {
            Renderer rd = piece.GetComponent<Renderer>();
            rd.material = glassMat;
        }

        Rigidbody rb = piece.AddComponent<Rigidbody>();
        rb.mass = cubeSize;

        //생성된 조각에 폭발력 가하기
        rb.AddExplosionForce(explosionForce, transform.position, explosionRadius, explosionUpward);

        //3초 뒤 삭제
        Destroy(piece, 3f);
    }
}
