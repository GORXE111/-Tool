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

    [Header("模式设置")]
    public PatternType horizontalPatternType = PatternType.Procedural;
    public PatternType verticalPatternType = PatternType.Procedural;

    [Header("后坐力参数")]
    public float recoilDuration = 0.1f;
    public float maxRecoilX = 5f;
    public float maxRecoilY = 8f;
    public float recoverySpeed = 5f;

    [Header("基础后坐力设置")]
    public float baseHorizontalRecoil = 0.2f; // 基础水平后坐力
    public float baseVerticalRecoil = 0.5f;   // 基础垂直后坐力

    [Header("固定模式设置")]
    public float baseHorizontalStrength = 0.5f;
    public float baseVerticalStrength = 1.0f;

    [Header("程序模式设置")]
    public float horizontalRandomness = 0.5f;
    public float verticalRandomness = 0.3f;
    public AnimationCurve horizontalRecoilCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    public AnimationCurve verticalRecoilCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("呼吸抖动设置")]
    public List<Vector2> breathingPoints = new List<Vector2>(); // 呼吸抖动轨迹点
    public float breathingIntensityX = 0.1f;
    public float breathingIntensityY = 0.1f;
    public float breathingFrequency = 1f;
    public bool useCustomBreathingPath = false;

    public RecoilPattern()
    {
        // 初始化默认的水平后坐力曲线
        horizontalRecoilCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.1f, 1f),
            new Keyframe(0.2f, 0.8f),
            new Keyframe(0.3f, -0.5f),  // 负值使枪口向左偏移
            new Keyframe(0.5f, 0.2f),
            new Keyframe(1f, 0f)
        );

        // 初始化默认的垂直后坐力曲线
        verticalRecoilCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.1f, 1f),
            new Keyframe(0.3f, 0.8f),
            new Keyframe(0.6f, 0.4f),
            new Keyframe(1f, 0f)
        );

        // 初始化默认的呼吸抖动轨迹
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