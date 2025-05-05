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

        // 设置朝向
        transform.LookAt(targetPosition);

        // 设置子弹颜色
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

        // 计算当前时间下应该移动的距离
        float distCovered = (Time.time - startTime) * bulletSpeed;
        float fractionOfJourney = distCovered / journeyLength;

        // 移动子弹
        transform.position = Vector3.Lerp(startPosition, targetPosition, fractionOfJourney);

        // 检查是否到达目标
        if (fractionOfJourney >= 1.0f)
        {
            hasReachedTarget = true;

            // 创建击中标记
            CreateImpactMarker(targetPosition, impactNormal);
        }
    }

    private void CreateImpactMarker(Vector3 position, Vector3 normal)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        marker.transform.position = position + normal * 0.01f;

        // 移除碰撞器
        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            Destroy(markerCollider);
        }

        // 设置标记颜色
        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = bulletColor;
        }

        // 注册到子弹管理器以便清理
        if (BulletManager.Instance != null)
        {
            BulletManager.Instance.RegisterImpactMarker(marker);
        }
    }
}