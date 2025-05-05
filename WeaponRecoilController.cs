using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WeaponRecoilController : MonoBehaviour
{
    [Header("后坐力模式")]
    public RecoilPattern recoilPattern;

    [Header("基本设置")]
    public Transform firePoint;
    public GameObject bulletPrefab;
    public float bulletSpeed = 100f;
    public int magazineSize = 30;
    public float defaultRPM = 600f;
    public AudioClip fireSound;
    public ParticleSystem muzzleFlash;

    [Header("状态")]
    public int remainingBullets;
    public bool isFiring = false;

    private Transform weaponTransform;
    private Vector3 originalPosition;
    private Quaternion originalRotation;

    private Vector3 currentRecoil;
    private Vector3 currentRecoilVelocity;
    private Vector2 breathingOffset;

    private int shotIndex = 0;
    private float lastShotTime = 0f;
    private bool isRecovering = false;
    private Coroutine firingCoroutine;
    private float breathingTime = 0f;
    private bool isBreathingActive = true;

    // 添加时间跟踪变量，用于曲线评估
    private float timeSinceFirstShot = 0f;
    private bool isInRecoilSequence = false;

    private void Start()
    {
        weaponTransform = transform;
        originalPosition = weaponTransform.localPosition;
        originalRotation = weaponTransform.localRotation;

        // 初始化子弹数量
        remainingBullets = magazineSize;

        // 如果没有射击点，使用当前变换
        if (firePoint == null)
        {
            firePoint = transform;
        }

        // 如果没有子弹预制体，创建一个默认的
        if (bulletPrefab == null)
        {
            bulletPrefab = CreateDefaultBulletPrefab();
        }

        // 确保有子弹管理器
        if (BulletManager.Instance == null)
        {
            new GameObject("BulletManager").AddComponent<BulletManager>();
        }

        // 初始化后坐力模式
        if (recoilPattern == null)
        {
            recoilPattern = new RecoilPattern();
        }

        // 开始应用呼吸效果
        isBreathingActive = true;

        // 打印曲线信息，帮助调试
        DebugRecoilCurves();
    }

    private void DebugRecoilCurves()
    {
        if (recoilPattern == null) return;

        Debug.Log("===== 后坐力曲线调试信息 =====");
        Debug.Log($"后坐力持续时间: {recoilPattern.recoilDuration}毫秒");

        // 打印水平曲线的几个关键点
        Debug.Log("水平后坐力曲线样本点:");
        for (float t = 0; t <= 1.0f; t += 0.1f)
        {
            float time = t * recoilPattern.recoilDuration;
            float value = recoilPattern.horizontalRecoilCurve.Evaluate(t);
            Debug.Log($"  时间: {time:F0}毫秒 ({t:F1}) -> 值: {value:F3}");
        }

        // 打印垂直曲线的几个关键点
        Debug.Log("垂直后坐力曲线样本点:");
        for (float t = 0; t <= 1.0f; t += 0.1f)
        {
            float time = t * recoilPattern.recoilDuration;
            float value = recoilPattern.verticalRecoilCurve.Evaluate(t);
            Debug.Log($"  时间: {time:F0}毫秒 ({t:F1}) -> 值: {value:F3}");
        }

        Debug.Log("================================");
    }

    private GameObject CreateDefaultBulletPrefab()
    {
        GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        prefab.name = "BulletPrefab";
        prefab.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

        // 添加刚体
        Rigidbody rb = prefab.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // 设置碰撞器
        SphereCollider collider = prefab.GetComponent<SphereCollider>();
        if (collider != null)
        {
            collider.radius = 0.5f;
            collider.isTrigger = false;
        }

        // 添加子弹脚本
        RaycastBullet bulletScript = prefab.AddComponent<RaycastBullet>();

        // 隐藏预制体
        prefab.SetActive(false);

        return prefab;
    }

    private void Update()
    {
        // 更新后坐力时间
        if (isInRecoilSequence)
        {
            timeSinceFirstShot += Time.deltaTime;
        }

        // 更新呼吸效果
        UpdateBreathing();

        // 处理后坐力恢复
        if (!isFiring)
        {
            // 不在射击状态下，逐渐恢复后坐力到0
            currentRecoil = Vector3.SmoothDamp(currentRecoil, Vector3.zero,
                ref currentRecoilVelocity, 1f / recoilPattern.recoverySpeed);

            ApplyRecoilToWeapon();

            if (currentRecoil.magnitude < 0.01f)
            {
                currentRecoil = Vector3.zero;
                isRecovering = false;

                // 如果后坐力完全恢复，重置后坐力序列
                if (isInRecoilSequence)
                {
                    isInRecoilSequence = false;
                    timeSinceFirstShot = 0f;
                }
            }
        }
    }

    private void UpdateBreathing()
    {
        if (!isBreathingActive) return;

        breathingTime += Time.deltaTime;
        float normalizedTime = (breathingTime * recoilPattern.breathingFrequency) % 1f;

        if (recoilPattern.useCustomBreathingPath && recoilPattern.breathingPoints.Count >= 2)
        {
            // 使用自定义呼吸轨迹
            breathingOffset = EvaluateBreathingPath(normalizedTime);
        }
        else
        {
            // 使用参数化呼吸曲线: x(t) = -cos(t), y(t) = -sin(2*t)
            breathingOffset.x = -Mathf.Cos(breathingTime * recoilPattern.breathingFrequency * 2 * Mathf.PI) * recoilPattern.breathingIntensityX;
            breathingOffset.y = -Mathf.Sin(2 * breathingTime * recoilPattern.breathingFrequency * 2 * Mathf.PI) * recoilPattern.breathingIntensityY;
        }

        // 只在不射击时应用呼吸效果
        if (!isFiring)
        {
            ApplyBreathingToWeapon();
        }
    }

    private Vector2 EvaluateBreathingPath(float normalizedTime)
    {
        if (recoilPattern.breathingPoints.Count == 0)
            return Vector2.zero;

        if (recoilPattern.breathingPoints.Count == 1)
            return recoilPattern.breathingPoints[0];

        // 找到所在的线段索引
        int pointCount = recoilPattern.breathingPoints.Count;
        float segmentLength = 1.0f / pointCount;
        int index = Mathf.Min(Mathf.FloorToInt(normalizedTime / segmentLength), pointCount - 1);
        float localT = (normalizedTime - index * segmentLength) / segmentLength;

        // 获取起点和终点
        Vector2 start = recoilPattern.breathingPoints[index];
        Vector2 end = recoilPattern.breathingPoints[(index + 1) % pointCount];

        // 线性插值
        Vector2 result = Vector2.Lerp(start, end, localT);
        return new Vector2(
            result.x * recoilPattern.breathingIntensityX,
            result.y * recoilPattern.breathingIntensityY
        );
    }

    private void ApplyBreathingToWeapon()
    {
        // 应用呼吸效果旋转
        Quaternion breathingRotation = Quaternion.Euler(breathingOffset.y, breathingOffset.x, 0);

        // 结合当前后坐力和呼吸效果
        Quaternion recoilRotation = Quaternion.Euler(-currentRecoil.y, currentRecoil.x, 0);
        weaponTransform.localRotation = originalRotation * recoilRotation * breathingRotation;
    }

    public void Fire()
    {
        if (remainingBullets <= 0)
        {
            Debug.Log("弹药已空，请重置测试。");
            StopFiring();
            return;
        }

        // 发射子弹
        FireBullet();

        // 如果这是连续射击中的第一枪，开始记录时间
        if (!isInRecoilSequence)
        {
            isInRecoilSequence = true;
            timeSinceFirstShot = 0f;
        }

        // 计算当前射击的后坐力
        Vector2 recoilAmount = CalculateRecoilForShot(shotIndex);

        // 应用后坐力
        ApplyRecoil(recoilAmount);

        // 打印后坐力信息
        Debug.Log($"射击 #{shotIndex + 1}: 后坐力 X={recoilAmount.x:F2}, Y={recoilAmount.y:F2}, 累计后坐力 X={currentRecoil.x:F2}, Y={currentRecoil.y:F2}, 时间={timeSinceFirstShot:F2}s");

        // 更新后坐力索引和时间
        shotIndex++;
        lastShotTime = Time.time;
        isRecovering = false;

        // 减少子弹数量
        remainingBullets--;
    }

    private void FireBullet()
    {
        // 先进行射线检测
        RaycastHit hit;
        if (Physics.Raycast(firePoint.position, firePoint.forward, out hit, 1000f))
        {
            Debug.Log($"射线击中: {hit.collider.gameObject.name} 在距离: {hit.distance}");

            // 实例化子弹并直接放置在击中点
            GameObject bullet = Instantiate(bulletPrefab, hit.point - firePoint.forward * 0.1f, Quaternion.LookRotation(firePoint.forward));
            bullet.SetActive(true);

            RaycastBullet bulletComponent = bullet.GetComponent<RaycastBullet>();
            if (bulletComponent != null)
            {
                bulletComponent.Initialize(firePoint.position, firePoint.forward, bulletSpeed, hit.point, hit.normal);
            }

            // 注册到子弹管理器
            BulletManager.Instance.RegisterBullet(bullet);
        }
        else
        {
            // 没有击中任何物体，创建远距离子弹
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
            bullet.SetActive(true);

            RaycastBullet bulletComponent = bullet.GetComponent<RaycastBullet>();
            if (bulletComponent != null)
            {
                bulletComponent.Initialize(firePoint.position, firePoint.forward, bulletSpeed,
                    firePoint.position + firePoint.forward * 1000f, -firePoint.forward);
            }

            // 注册到子弹管理器
            BulletManager.Instance.RegisterBullet(bullet);
        }

        // 播放音效
        if (fireSound != null)
        {
            AudioSource.PlayClipAtPoint(fireSound, transform.position, 0.5f);
        }

        // 播放特效
        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
        }
    }

    public void StartFiring(float rpm)
    {
        if (isFiring) return;

        isFiring = true;
        isBreathingActive = false; // 射击时暂停呼吸效果

        // 重置射击序列
        isInRecoilSequence = true;
        timeSinceFirstShot = 0f;
        shotIndex = 0;
        currentRecoil = Vector3.zero; // 重置累积后坐力

        // 计算射击间隔
        float interval = 60f / rpm;

        // 开始射击协程
        firingCoroutine = StartCoroutine(AutoFireCoroutine(interval));
    }

    private IEnumerator AutoFireCoroutine(float interval)
    {
        while (isFiring && remainingBullets > 0)
        {
            Fire();

            yield return new WaitForSeconds(interval);
        }

        isFiring = false;
        isRecovering = true;
        isBreathingActive = true; // 恢复呼吸效果
    }

    public void StopFiring()
    {
        if (firingCoroutine != null)
        {
            StopCoroutine(firingCoroutine);
        }

        isFiring = false;
        isRecovering = true;
        isBreathingActive = true; // 恢复呼吸效果
    }

    public void ResetTest()
    {
        // 停止射击
        StopFiring();

        // 重置后坐力
        currentRecoil = Vector3.zero;
        currentRecoilVelocity = Vector3.zero;
        ApplyRecoilToWeapon();

        // 重置武器位置和旋转
        weaponTransform.localPosition = originalPosition;
        weaponTransform.localRotation = originalRotation;

        // 重置射击索引
        shotIndex = 0;

        // 重置后坐力时间跟踪
        isInRecoilSequence = false;
        timeSinceFirstShot = 0f;

        // 重置弹药数量
        remainingBullets = magazineSize;

        // 清理所有子弹
        if (BulletManager.Instance != null)
        {
            BulletManager.Instance.ClearAllBullets();
        }

        // 重新开启呼吸效果
        isBreathingActive = true;
        breathingTime = 0f;
    }

    private Vector2 CalculateRecoilForShot(int shotIndex)
    {
        Vector2 recoilAmount = Vector2.zero;

        // 使用从第一枪开始的持续时间来评估曲线
        // 注意：recoilDuration是毫秒，而timeSinceFirstShot是秒，需要转换
        float normalizedTime = Mathf.Clamp01(timeSinceFirstShot * 1000f / recoilPattern.recoilDuration);

        // 直接从曲线获取当前时间点的值
        float horizontalCurveValue = recoilPattern.horizontalRecoilCurve.Evaluate(timeSinceFirstShot);
        float verticalCurveValue = recoilPattern.verticalRecoilCurve.Evaluate(timeSinceFirstShot);

        // 打印原始曲线值，用于调试
        Debug.Log($"曲线评估 - 时间: {timeSinceFirstShot * 1000f:F0}毫秒, 归一化时间: {normalizedTime:F3}, " +
                  $"垂直曲线原始值: {verticalCurveValue:F3}, 水平曲线原始值: {horizontalCurveValue:F3}");

        // 确保曲线值不为0（如果曲线确实为0，则使用一个小的默认值）
        // 这是为了确保即使曲线值为0，后坐力也能正常工作
        if (Mathf.Approximately(verticalCurveValue, 0f))
        {
            Debug.LogWarning("垂直曲线值为0，使用默认值0.3");
            verticalCurveValue = 0.3f; // 默认值，可以根据需要调整
        }

        if (Mathf.Approximately(horizontalCurveValue, 0f))
        {
            Debug.LogWarning("水平曲线值为0，使用默认值0.3");
            horizontalCurveValue = 0.3f; // 默认值，可以根据需要调整
        }

        // 水平后坐力计算
        float horizontalStrength = 0f;
        if (recoilPattern.horizontalPatternType == RecoilPattern.PatternType.Fixed)
        {
            horizontalStrength = recoilPattern.baseHorizontalStrength;
        }
        else // Procedural
        {
            // 程序化水平后坐力
            float randomFactor = 1f + UnityEngine.Random.Range(-recoilPattern.horizontalRandomness, recoilPattern.horizontalRandomness);
            horizontalStrength = recoilPattern.baseHorizontalStrength *
                                Mathf.Sin(shotIndex * 0.7f) *
                                randomFactor;
        }

        // 垂直后坐力计算
        float verticalStrength = 0f;
        if (recoilPattern.verticalPatternType == RecoilPattern.PatternType.Fixed)
        {
            verticalStrength = recoilPattern.baseVerticalStrength;
        }
        else // Procedural
        {
            // 程序化垂直后坐力
            float randomFactor = 1f + UnityEngine.Random.Range(-recoilPattern.verticalRandomness, recoilPattern.verticalRandomness);
            verticalStrength = recoilPattern.baseVerticalStrength *
                              (1f + Mathf.Log(shotIndex + 1) * 0.2f) *
                              randomFactor;
        }

        // 最终后坐力计算
        // 水平后坐力 = 基础水平后坐力 + (水平强度 * 水平曲线值)
        recoilAmount.x = recoilPattern.baseHorizontalRecoil + (horizontalStrength * horizontalCurveValue);

        // 垂直后坐力 = 基础垂直后坐力 + (垂直强度 * 垂直曲线值)
        recoilAmount.y = recoilPattern.baseVerticalRecoil + (verticalStrength * verticalCurveValue);

        // 详细调试输出
        Debug.Log($"射击 #{shotIndex + 1} 后坐力计算详情:" +
                  $"\n  时间: {timeSinceFirstShot:F3}s ({timeSinceFirstShot * 1000f:F0}毫秒)" +
                  $"\n  归一化时间: {normalizedTime:F3} (总持续时间: {recoilPattern.recoilDuration}毫秒)" +
                  $"\n  垂直曲线值: {verticalCurveValue:F3}" +
                  $"\n  垂直强度: {verticalStrength:F3}" +
                  $"\n  基础垂直后坐力: {recoilPattern.baseVerticalRecoil:F3}" +
                  $"\n  垂直后坐力计算: {recoilPattern.baseVerticalRecoil:F3} + ({verticalStrength:F3} * {verticalCurveValue:F3}) = {recoilAmount.y:F3}");

        return recoilAmount;
    }

    private void ApplyRecoil(Vector2 recoilAmount)
    {
        // 累加到当前后坐力
        currentRecoil.x += recoilAmount.x;
        currentRecoil.y += recoilAmount.y;

        // 限制累积后坐力
        currentRecoil.x = Mathf.Clamp(currentRecoil.x, -recoilPattern.maxRecoilX, recoilPattern.maxRecoilX);
        currentRecoil.y = Mathf.Clamp(currentRecoil.y, -recoilPattern.maxRecoilY, recoilPattern.maxRecoilY);

        ApplyRecoilToWeapon();
    }

    private void ApplyRecoilToWeapon()
    {
        // 应用旋转后坐力 (影响瞄准)
        Quaternion recoilRotation = Quaternion.Euler(-currentRecoil.y, currentRecoil.x, 0);

        // 结合呼吸效果
        Quaternion finalRotation;
        if (isBreathingActive)
        {
            Quaternion breathingRotation = Quaternion.Euler(breathingOffset.y, breathingOffset.x, 0);
            finalRotation = originalRotation * recoilRotation * breathingRotation;
        }
        else
        {
            finalRotation = originalRotation * recoilRotation;
        }

        weaponTransform.localRotation = finalRotation;

        // 应用位置后坐力 (视觉效果)
        Vector3 positionOffset = new Vector3(
            -currentRecoil.x * 0.03f,  // 水平位移
            -currentRecoil.y * 0.02f,  // 垂直位移
            -Mathf.Abs(currentRecoil.y) * 0.01f   // 后退位移
        );

        weaponTransform.localPosition = originalPosition + positionOffset;
    }
}