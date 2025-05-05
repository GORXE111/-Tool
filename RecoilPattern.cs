using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RecoilPattern
{
    public enum PatternType
    {
        Fixed,
        Procedural
    }

    [Header("ģʽ����")]
    public PatternType horizontalPatternType = PatternType.Procedural;
    public PatternType verticalPatternType = PatternType.Procedural;

    [Header("����������")]
    public float recoilDuration = 0.1f;
    public float maxRecoilX = 5f;
    public float maxRecoilY = 8f;
    public float recoverySpeed = 5f;

    [Header("��������������")]
    public float baseHorizontalRecoil = 0.2f; // ����ˮƽ������
    public float baseVerticalRecoil = 0.5f;   // ������ֱ������

    [Header("�̶�ģʽ����")]
    public float baseHorizontalStrength = 0.5f;
    public float baseVerticalStrength = 1.0f;

    [Header("����ģʽ����")]
    public float horizontalRandomness = 0.5f;
    public float verticalRandomness = 0.3f;
    public AnimationCurve horizontalRecoilCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    public AnimationCurve verticalRecoilCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("������������")]
    public List<Vector2> breathingPoints = new List<Vector2>(); // ���������켣��
    public float breathingIntensityX = 0.1f;
    public float breathingIntensityY = 0.1f;
    public float breathingFrequency = 1f;
    public bool useCustomBreathingPath = false;

    public RecoilPattern()
    {
        // ��ʼ��Ĭ�ϵ�ˮƽ����������
        horizontalRecoilCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.1f, 1f),
            new Keyframe(0.2f, 0.8f),
            new Keyframe(0.3f, -0.5f),  // ��ֵʹǹ������ƫ��
            new Keyframe(0.5f, 0.2f),
            new Keyframe(1f, 0f)
        );

        // ��ʼ��Ĭ�ϵĴ�ֱ����������
        verticalRecoilCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.1f, 1f),
            new Keyframe(0.3f, 0.8f),
            new Keyframe(0.6f, 0.4f),
            new Keyframe(1f, 0f)
        );

        // ��ʼ��Ĭ�ϵĺ��������켣
        breathingPoints = new List<Vector2>
        {
            new Vector2(0f, 0f),
            new Vector2(0.25f, 1f),
            new Vector2(0.5f, 0f),
            new Vector2(0.75f, -1f),
            new Vector2(1f, 0f)
        };
    }
}