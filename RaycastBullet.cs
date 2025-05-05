using UnityEngine;

public class RaycastBullet : MonoBehaviour
{
    public float maxDistance = 1000f;
    public Color bulletColor = Color.yellow;
    public float bulletSpeed = 100f;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Vector3 impactNormal;
    private float journeyLength;
    private float startTime;
    private bool hasReachedTarget = false;

    public void Initialize(Vector3 start, Vector3 direction, float speed, Vector3 hitPoint, Vector3 hitNormal)
    {
        startPosition = start;
        transform.position = start;
        targetPosition = hitPoint;
        impactNormal = hitNormal;
        journeyLength = Vector3.Distance(start, targetPosition);
        bulletSpeed = speed;
        startTime = Time.time;

        // ���ó���
        transform.LookAt(targetPosition);

        // �����ӵ���ɫ
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = bulletColor;
        }
    }

    private void Update()
    {
        if (hasReachedTarget)
            return;

        // ���㵱ǰʱ����Ӧ���ƶ��ľ���
        float distCovered = (Time.time - startTime) * bulletSpeed;
        float fractionOfJourney = distCovered / journeyLength;

        // �ƶ��ӵ�
        transform.position = Vector3.Lerp(startPosition, targetPosition, fractionOfJourney);

        // ����Ƿ񵽴�Ŀ��
        if (fractionOfJourney >= 1.0f)
        {
            hasReachedTarget = true;

            // �������б��
            CreateImpactMarker(targetPosition, impactNormal);
        }
    }

    private void CreateImpactMarker(Vector3 position, Vector3 normal)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        marker.transform.position = position + normal * 0.01f;

        // �Ƴ���ײ��
        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            Destroy(markerCollider);
        }

        // ���ñ����ɫ
        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = bulletColor;
        }

        // ע�ᵽ�ӵ��������Ա�����
        if (BulletManager.Instance != null)
        {
            BulletManager.Instance.RegisterImpactMarker(marker);
        }
    }
}