using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WeaponRecoilController : MonoBehaviour
{
    [Header("������ģʽ")]
    public RecoilPattern recoilPattern;

    [Header("��������")]
    public Transform firePoint;
    public GameObject bulletPrefab;
    public float bulletSpeed = 100f;
    public int magazineSize = 30;
    public float defaultRPM = 600f;
    public AudioClip fireSound;
    public ParticleSystem muzzleFlash;

    [Header("״̬")]
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

    // ���ʱ����ٱ�����������������
    private float timeSinceFirstShot = 0f;
    private bool isInRecoilSequence = false;

    private void Start()
    {
        weaponTransform = transform;
        originalPosition = weaponTransform.localPosition;
        originalRotation = weaponTransform.localRotation;

        // ��ʼ���ӵ�����
        remainingBullets = magazineSize;

        // ���û������㣬ʹ�õ�ǰ�任
        if (firePoint == null)
        {
            firePoint = transform;
        }

        // ���û���ӵ�Ԥ���壬����һ��Ĭ�ϵ�
        if (bulletPrefab == null)
        {
            bulletPrefab = CreateDefaultBulletPrefab();
        }

        // ȷ�����ӵ�������
        if (BulletManager.Instance == null)
        {
            new GameObject("BulletManager").AddComponent<BulletManager>();
        }

        // ��ʼ��������ģʽ
        if (recoilPattern == null)
        {
            recoilPattern = new RecoilPattern();
        }

        // ��ʼӦ�ú���Ч��
        isBreathingActive = true;

        // ��ӡ������Ϣ����������
        DebugRecoilCurves();
    }

    private void DebugRecoilCurves()
    {
        if (recoilPattern == null) return;

        Debug.Log("===== ���������ߵ�����Ϣ =====");
        Debug.Log($"����������ʱ��: {recoilPattern.recoilDuration}����");

        // ��ӡˮƽ���ߵļ����ؼ���
        Debug.Log("ˮƽ����������������:");
        for (float t = 0; t <= 1.0f; t += 0.1f)
        {
            float time = t * recoilPattern.recoilDuration;
            float value = recoilPattern.horizontalRecoilCurve.Evaluate(t);
            Debug.Log($"  ʱ��: {time:F0}���� ({t:F1}) -> ֵ: {value:F3}");
        }

        // ��ӡ��ֱ���ߵļ����ؼ���
        Debug.Log("��ֱ����������������:");
        for (float t = 0; t <= 1.0f; t += 0.1f)
        {
            float time = t * recoilPattern.recoilDuration;
            float value = recoilPattern.verticalRecoilCurve.Evaluate(t);
            Debug.Log($"  ʱ��: {time:F0}���� ({t:F1}) -> ֵ: {value:F3}");
        }

        Debug.Log("================================");
    }

    private GameObject CreateDefaultBulletPrefab()
    {
        GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        prefab.name = "BulletPrefab";
        prefab.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

        // ��Ӹ���
        Rigidbody rb = prefab.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // ������ײ��
        SphereCollider collider = prefab.GetComponent<SphereCollider>();
        if (collider != null)
        {
            collider.radius = 0.5f;
            collider.isTrigger = false;
        }

        // ����ӵ��ű�
        RaycastBullet bulletScript = prefab.AddComponent<RaycastBullet>();

        // ����Ԥ����
        prefab.SetActive(false);

        return prefab;
    }

    private void Update()
    {
        // ���º�����ʱ��
        if (isInRecoilSequence)
        {
            timeSinceFirstShot += Time.deltaTime;
        }

        // ���º���Ч��
        UpdateBreathing();

        // ����������ָ�
        if (!isFiring)
        {
            // �������״̬�£��𽥻ָ���������0
            currentRecoil = Vector3.SmoothDamp(currentRecoil, Vector3.zero,
                ref currentRecoilVelocity, 1f / recoilPattern.recoverySpeed);

            ApplyRecoilToWeapon();

            if (currentRecoil.magnitude < 0.01f)
            {
                currentRecoil = Vector3.zero;
                isRecovering = false;

                // �����������ȫ�ָ������ú���������
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
            // ʹ���Զ�������켣
            breathingOffset = EvaluateBreathingPath(normalizedTime);
        }
        else
        {
            // ʹ�ò�������������: x(t) = -cos(t), y(t) = -sin(2*t)
            breathingOffset.x = -Mathf.Cos(breathingTime * recoilPattern.breathingFrequency * 2 * Mathf.PI) * recoilPattern.breathingIntensityX;
            breathingOffset.y = -Mathf.Sin(2 * breathingTime * recoilPattern.breathingFrequency * 2 * Mathf.PI) * recoilPattern.breathingIntensityY;
        }

        // ֻ�ڲ����ʱӦ�ú���Ч��
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

        // �ҵ����ڵ��߶�����
        int pointCount = recoilPattern.breathingPoints.Count;
        float segmentLength = 1.0f / pointCount;
        int index = Mathf.Min(Mathf.FloorToInt(normalizedTime / segmentLength), pointCount - 1);
        float localT = (normalizedTime - index * segmentLength) / segmentLength;

        // ��ȡ�����յ�
        Vector2 start = recoilPattern.breathingPoints[index];
        Vector2 end = recoilPattern.breathingPoints[(index + 1) % pointCount];

        // ���Բ�ֵ
        Vector2 result = Vector2.Lerp(start, end, localT);
        return new Vector2(
            result.x * recoilPattern.breathingIntensityX,
            result.y * recoilPattern.breathingIntensityY
        );
    }

    private void ApplyBreathingToWeapon()
    {
        // Ӧ�ú���Ч����ת
        Quaternion breathingRotation = Quaternion.Euler(breathingOffset.y, breathingOffset.x, 0);

        // ��ϵ�ǰ�������ͺ���Ч��
        Quaternion recoilRotation = Quaternion.Euler(-currentRecoil.y, currentRecoil.x, 0);
        weaponTransform.localRotation = originalRotation * recoilRotation * breathingRotation;
    }

    public void Fire()
    {
        if (remainingBullets <= 0)
        {
            Debug.Log("��ҩ�ѿգ������ò��ԡ�");
            StopFiring();
            return;
        }

        // �����ӵ�
        FireBullet();

        // ���������������еĵ�һǹ����ʼ��¼ʱ��
        if (!isInRecoilSequence)
        {
            isInRecoilSequence = true;
            timeSinceFirstShot = 0f;
        }

        // ���㵱ǰ����ĺ�����
        Vector2 recoilAmount = CalculateRecoilForShot(shotIndex);

        // Ӧ�ú�����
        ApplyRecoil(recoilAmount);

        // ��ӡ��������Ϣ
        Debug.Log($"��� #{shotIndex + 1}: ������ X={recoilAmount.x:F2}, Y={recoilAmount.y:F2}, �ۼƺ����� X={currentRecoil.x:F2}, Y={currentRecoil.y:F2}, ʱ��={timeSinceFirstShot:F2}s");

        // ���º�����������ʱ��
        shotIndex++;
        lastShotTime = Time.time;
        isRecovering = false;

        // �����ӵ�����
        remainingBullets--;
    }

    private void FireBullet()
    {
        // �Ƚ������߼��
        RaycastHit hit;
        if (Physics.Raycast(firePoint.position, firePoint.forward, out hit, 1000f))
        {
            Debug.Log($"���߻���: {hit.collider.gameObject.name} �ھ���: {hit.distance}");

            // ʵ�����ӵ���ֱ�ӷ����ڻ��е�
            GameObject bullet = Instantiate(bulletPrefab, hit.point - firePoint.forward * 0.1f, Quaternion.LookRotation(firePoint.forward));
            bullet.SetActive(true);

            RaycastBullet bulletComponent = bullet.GetComponent<RaycastBullet>();
            if (bulletComponent != null)
            {
                bulletComponent.Initialize(firePoint.position, firePoint.forward, bulletSpeed, hit.point, hit.normal);
            }

            // ע�ᵽ�ӵ�������
            BulletManager.Instance.RegisterBullet(bullet);
        }
        else
        {
            // û�л����κ����壬����Զ�����ӵ�
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
            bullet.SetActive(true);

            RaycastBullet bulletComponent = bullet.GetComponent<RaycastBullet>();
            if (bulletComponent != null)
            {
                bulletComponent.Initialize(firePoint.position, firePoint.forward, bulletSpeed,
                    firePoint.position + firePoint.forward * 1000f, -firePoint.forward);
            }

            // ע�ᵽ�ӵ�������
            BulletManager.Instance.RegisterBullet(bullet);
        }

        // ������Ч
        if (fireSound != null)
        {
            AudioSource.PlayClipAtPoint(fireSound, transform.position, 0.5f);
        }

        // ������Ч
        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
        }
    }

    public void StartFiring(float rpm)
    {
        if (isFiring) return;

        isFiring = true;
        isBreathingActive = false; // ���ʱ��ͣ����Ч��

        // �����������
        isInRecoilSequence = true;
        timeSinceFirstShot = 0f;
        shotIndex = 0;
        currentRecoil = Vector3.zero; // �����ۻ�������

        // ����������
        float interval = 60f / rpm;

        // ��ʼ���Э��
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
        isBreathingActive = true; // �ָ�����Ч��
    }

    public void StopFiring()
    {
        if (firingCoroutine != null)
        {
            StopCoroutine(firingCoroutine);
        }

        isFiring = false;
        isRecovering = true;
        isBreathingActive = true; // �ָ�����Ч��
    }

    public void ResetTest()
    {
        // ֹͣ���
        StopFiring();

        // ���ú�����
        currentRecoil = Vector3.zero;
        currentRecoilVelocity = Vector3.zero;
        ApplyRecoilToWeapon();

        // ��������λ�ú���ת
        weaponTransform.localPosition = originalPosition;
        weaponTransform.localRotation = originalRotation;

        // �����������
        shotIndex = 0;

        // ���ú�����ʱ�����
        isInRecoilSequence = false;
        timeSinceFirstShot = 0f;

        // ���õ�ҩ����
        remainingBullets = magazineSize;

        // ���������ӵ�
        if (BulletManager.Instance != null)
        {
            BulletManager.Instance.ClearAllBullets();
        }

        // ���¿�������Ч��
        isBreathingActive = true;
        breathingTime = 0f;
    }

    private Vector2 CalculateRecoilForShot(int shotIndex)
    {
        Vector2 recoilAmount = Vector2.zero;

        // ʹ�ôӵ�һǹ��ʼ�ĳ���ʱ������������
        // ע�⣺recoilDuration�Ǻ��룬��timeSinceFirstShot���룬��Ҫת��
        float normalizedTime = Mathf.Clamp01(timeSinceFirstShot * 1000f / recoilPattern.recoilDuration);

        // ֱ�Ӵ����߻�ȡ��ǰʱ����ֵ
        float horizontalCurveValue = recoilPattern.horizontalRecoilCurve.Evaluate(timeSinceFirstShot);
        float verticalCurveValue = recoilPattern.verticalRecoilCurve.Evaluate(timeSinceFirstShot);

        // ��ӡԭʼ����ֵ�����ڵ���
        Debug.Log($"�������� - ʱ��: {timeSinceFirstShot * 1000f:F0}����, ��һ��ʱ��: {normalizedTime:F3}, " +
                  $"��ֱ����ԭʼֵ: {verticalCurveValue:F3}, ˮƽ����ԭʼֵ: {horizontalCurveValue:F3}");

        // ȷ������ֵ��Ϊ0���������ȷʵΪ0����ʹ��һ��С��Ĭ��ֵ��
        // ����Ϊ��ȷ����ʹ����ֵΪ0��������Ҳ����������
        if (Mathf.Approximately(verticalCurveValue, 0f))
        {
            Debug.LogWarning("��ֱ����ֵΪ0��ʹ��Ĭ��ֵ0.3");
            verticalCurveValue = 0.3f; // Ĭ��ֵ�����Ը�����Ҫ����
        }

        if (Mathf.Approximately(horizontalCurveValue, 0f))
        {
            Debug.LogWarning("ˮƽ����ֵΪ0��ʹ��Ĭ��ֵ0.3");
            horizontalCurveValue = 0.3f; // Ĭ��ֵ�����Ը�����Ҫ����
        }

        // ˮƽ����������
        float horizontalStrength = 0f;
        if (recoilPattern.horizontalPatternType == RecoilPattern.PatternType.Fixed)
        {
            horizontalStrength = recoilPattern.baseHorizontalStrength;
        }
        else // Procedural
        {
            // ����ˮƽ������
            float randomFactor = 1f + UnityEngine.Random.Range(-recoilPattern.horizontalRandomness, recoilPattern.horizontalRandomness);
            horizontalStrength = recoilPattern.baseHorizontalStrength *
                                Mathf.Sin(shotIndex * 0.7f) *
                                randomFactor;
        }

        // ��ֱ����������
        float verticalStrength = 0f;
        if (recoilPattern.verticalPatternType == RecoilPattern.PatternType.Fixed)
        {
            verticalStrength = recoilPattern.baseVerticalStrength;
        }
        else // Procedural
        {
            // ���򻯴�ֱ������
            float randomFactor = 1f + UnityEngine.Random.Range(-recoilPattern.verticalRandomness, recoilPattern.verticalRandomness);
            verticalStrength = recoilPattern.baseVerticalStrength *
                              (1f + Mathf.Log(shotIndex + 1) * 0.2f) *
                              randomFactor;
        }

        // ���պ���������
        // ˮƽ������ = ����ˮƽ������ + (ˮƽǿ�� * ˮƽ����ֵ)
        recoilAmount.x = recoilPattern.baseHorizontalRecoil + (horizontalStrength * horizontalCurveValue);

        // ��ֱ������ = ������ֱ������ + (��ֱǿ�� * ��ֱ����ֵ)
        recoilAmount.y = recoilPattern.baseVerticalRecoil + (verticalStrength * verticalCurveValue);

        // ��ϸ�������
        Debug.Log($"��� #{shotIndex + 1} ��������������:" +
                  $"\n  ʱ��: {timeSinceFirstShot:F3}s ({timeSinceFirstShot * 1000f:F0}����)" +
                  $"\n  ��һ��ʱ��: {normalizedTime:F3} (�ܳ���ʱ��: {recoilPattern.recoilDuration}����)" +
                  $"\n  ��ֱ����ֵ: {verticalCurveValue:F3}" +
                  $"\n  ��ֱǿ��: {verticalStrength:F3}" +
                  $"\n  ������ֱ������: {recoilPattern.baseVerticalRecoil:F3}" +
                  $"\n  ��ֱ����������: {recoilPattern.baseVerticalRecoil:F3} + ({verticalStrength:F3} * {verticalCurveValue:F3}) = {recoilAmount.y:F3}");

        return recoilAmount;
    }

    private void ApplyRecoil(Vector2 recoilAmount)
    {
        // �ۼӵ���ǰ������
        currentRecoil.x += recoilAmount.x;
        currentRecoil.y += recoilAmount.y;

        // �����ۻ�������
        currentRecoil.x = Mathf.Clamp(currentRecoil.x, -recoilPattern.maxRecoilX, recoilPattern.maxRecoilX);
        currentRecoil.y = Mathf.Clamp(currentRecoil.y, -recoilPattern.maxRecoilY, recoilPattern.maxRecoilY);

        ApplyRecoilToWeapon();
    }

    private void ApplyRecoilToWeapon()
    {
        // Ӧ����ת������ (Ӱ����׼)
        Quaternion recoilRotation = Quaternion.Euler(-currentRecoil.y, currentRecoil.x, 0);

        // ��Ϻ���Ч��
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

        // Ӧ��λ�ú����� (�Ӿ�Ч��)
        Vector3 positionOffset = new Vector3(
            -currentRecoil.x * 0.03f,  // ˮƽλ��
            -currentRecoil.y * 0.02f,  // ��ֱλ��
            -Mathf.Abs(currentRecoil.y) * 0.01f   // ����λ��
        );

        weaponTransform.localPosition = originalPosition + positionOffset;
    }
}