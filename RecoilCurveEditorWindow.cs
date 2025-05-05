using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class RecoilCurveEditorWindow : EditorWindow
{
    public enum CurveType { Horizontal, Vertical, Breathing }  // 改为 public

    private CurveType currentCurveType;
    private AnimationCurve currentCurve;
    private RecoilPattern targetPattern;
    private List<Vector2> breathingPoints;
    private bool showTimeInMilliseconds = true;

    private Rect curveRect = new Rect(0, 0, 400, 200);
    private Rect breathingEditorRect = new Rect(0, 0, 400, 400);
    private int selectedPointIndex = -1;

    // 添加缩放功能变量
    private float zoomLevel = 1.0f;
    private Vector2 zoomPivot = Vector2.zero;
    private Vector2 scrollPosition = Vector2.zero;

    // 添加曲线编辑范围控制
    private float minValue = -2.0f;  // 最小Y值
    private float maxValue = 2.0f;   // 最大Y值

    // 添加时间范围控制
    private float maxTimeMs = 5000f; // 最大时间（毫秒）
    private float viewTimeMs = 1000f; // 当前视图中显示的时间范围（毫秒）

    public static void ShowWindow(RecoilPattern pattern, CurveType type)
    {
        RecoilCurveEditorWindow window = GetWindow<RecoilCurveEditorWindow>("曲线编辑器");
        window.targetPattern = pattern;
        window.currentCurveType = type;

        switch (type)
        {
            case CurveType.Horizontal:
                window.currentCurve = pattern.horizontalRecoilCurve;
                window.titleContent = new GUIContent("水平后坐力曲线");
                break;
            case CurveType.Vertical:
                window.currentCurve = pattern.verticalRecoilCurve;
                window.titleContent = new GUIContent("垂直后坐力曲线");
                break;
            case CurveType.Breathing:
                window.breathingPoints = pattern.breathingPoints;
                window.titleContent = new GUIContent("呼吸曲线编辑");
                break;
        }
    }

    private void OnGUI()
    {
        if (targetPattern == null) return;

        EditorGUILayout.BeginVertical();

        // 显示缩放控制
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("缩放级别:", GUILayout.Width(60));
        zoomLevel = EditorGUILayout.Slider(zoomLevel, 0.5f, 3.0f);
        if (GUILayout.Button("重置缩放", GUILayout.Width(80)))
        {
            zoomLevel = 1.0f;
            scrollPosition = Vector2.zero;
        }
        EditorGUILayout.EndHorizontal();

        // 显示值范围控制（仅对曲线编辑器有效）
        if (currentCurveType != CurveType.Breathing)
        {
            // 值范围控制
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("值范围:", GUILayout.Width(60));
            EditorGUILayout.LabelField("最小值", GUILayout.Width(40));
            minValue = EditorGUILayout.FloatField(minValue, GUILayout.Width(40));
            EditorGUILayout.LabelField("最大值", GUILayout.Width(40));
            maxValue = EditorGUILayout.FloatField(maxValue, GUILayout.Width(40));
            if (GUILayout.Button("重置范围", GUILayout.Width(80)))
            {
                minValue = -2.0f;
                maxValue = 2.0f;
            }
            EditorGUILayout.EndHorizontal();

            // 时间范围控制
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("时间范围(ms):", GUILayout.Width(90));
            EditorGUILayout.LabelField("最大值", GUILayout.Width(40));
            maxTimeMs = EditorGUILayout.FloatField(maxTimeMs, GUILayout.Width(60));
            EditorGUILayout.LabelField("显示范围", GUILayout.Width(60));
            viewTimeMs = EditorGUILayout.FloatField(viewTimeMs, GUILayout.Width(60));
            if (GUILayout.Button("重置时间", GUILayout.Width(80)))
            {
                maxTimeMs = 5000f;
                viewTimeMs = 1000f;
            }
            EditorGUILayout.EndHorizontal();

            // 确保合法值
            maxTimeMs = Mathf.Max(100f, maxTimeMs);
            viewTimeMs = Mathf.Clamp(viewTimeMs, 100f, maxTimeMs);
        }

        showTimeInMilliseconds = EditorGUILayout.Toggle("显示毫秒单位", showTimeInMilliseconds);

        // 使用滚动视图包装内容
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (currentCurveType == CurveType.Breathing)
        {
            DrawBreathingPathEditor();
        }
        else
        {
            DrawCurveEditor();
        }

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("应用更改"))
        {
            ApplyChanges();
        }

        EditorGUILayout.EndVertical();

        // 处理缩放事件
        Event e = Event.current;
        if (e.type == EventType.ScrollWheel && e.control)
        {
            float delta = -e.delta.y * 0.05f;
            zoomLevel = Mathf.Clamp(zoomLevel + delta, 0.5f, 3.0f);
            e.Use();
            Repaint();
        }
    }

    private void DrawCurveEditor()
    {
        EditorGUILayout.LabelField($"曲线编辑器 (X轴: 时间({(showTimeInMilliseconds ? "毫秒" : "秒")}), Y轴: 强度)");

        // 计算缩放后的矩形大小
        float scaledWidth = 400 * zoomLevel;
        float scaledHeight = 300 * zoomLevel;
        Rect rect = GUILayoutUtility.GetRect(scaledWidth, scaledHeight);

        // 绘制背景和网格
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 1));
        DrawGrid(rect, 20 * zoomLevel, 0.2f, Color.gray);
        DrawGrid(rect, 100 * zoomLevel, 0.4f, Color.gray);

        // 绘制坐标轴
        Handles.color = Color.white;
        // 水平轴（Y=0）的位置
        float zeroY = rect.y + rect.height * (maxValue / (maxValue - minValue));
        Handles.DrawLine(new Vector3(rect.x, zeroY), new Vector3(rect.x + rect.width, zeroY));
        // 垂直轴（X=0）
        Handles.DrawLine(new Vector3(rect.x, rect.y), new Vector3(rect.x, rect.y + rect.height));

        // 绘制坐标轴标签
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = Color.white;

        // X轴标签 - 在扩展的时间范围内显示标签
        float timeStep = viewTimeMs / 5; // 5个等分
        for (float t = 0; t <= viewTimeMs; t += timeStep)
        {
            float normalizedTime = t / viewTimeMs;
            float x = rect.x + normalizedTime * rect.width;

            string timeLabel;
            if (showTimeInMilliseconds)
            {
                timeLabel = t.ToString("F0") + "ms";
            }
            else
            {
                timeLabel = (t / 1000f).ToString("F2") + "s";
            }

            Handles.Label(new Vector3(x, rect.y + rect.height + 5), timeLabel, labelStyle);

            // 绘制垂直辅助线
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.y + rect.height));
            Handles.color = Color.white;
        }

        // Y轴标签 - 在扩展的范围内显示标签
        float valueStep = (maxValue - minValue) / 8; // 8个等分
        for (float v = minValue; v <= maxValue; v += valueStep)
        {
            // 计算标签在Y轴上的位置
            float normalizedValue = (v - minValue) / (maxValue - minValue);
            float y = rect.y + rect.height * (1 - normalizedValue);

            Handles.Label(new Vector3(rect.x - 30, y), v.ToString("F1"), labelStyle);

            // 绘制水平辅助线
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.x + rect.width, y));
            Handles.color = Color.white;
        }

        // 绘制曲线
        Handles.color = currentCurveType == CurveType.Horizontal ? Color.blue : Color.red;
        Vector3 prevPoint = Vector3.zero;
        bool firstPoint = true;

        // 绘制更多点以获得更平滑的曲线
        int numSteps = 200;
        for (int i = 0; i <= numSteps; i++)
        {
            float normalizedTime = (float)i / numSteps;
            float timeInMs = normalizedTime * viewTimeMs;
            float timeNormalized = timeInMs / maxTimeMs; // 归一化到0-1范围用于曲线评估

            float value = currentCurve.Evaluate(timeNormalized);
            float x = rect.x + normalizedTime * rect.width;

            // 将值映射到扩展的范围内
            float normalizedValue = Mathf.InverseLerp(minValue, maxValue, value);
            float y = rect.y + rect.height * (1 - normalizedValue);

            Vector3 point = new Vector3(x, y, 0);
            if (!firstPoint)
            {
                Handles.DrawLine(prevPoint, point);
            }
            prevPoint = point;
            firstPoint = false;
        }

        // 绘制关键帧点
        for (int i = 0; i < currentCurve.keys.Length; i++)
        {
            Keyframe key = currentCurve.keys[i];

            // 将时间从0-1范围转换为毫秒
            float timeInMs = key.time * maxTimeMs;

            // 检查点是否在当前视图范围内
            if (timeInMs <= viewTimeMs)
            {
                float normalizedViewTime = timeInMs / viewTimeMs;
                float x = rect.x + normalizedViewTime * rect.width;

                // 将值映射到扩展的范围内
                float normalizedValue = Mathf.InverseLerp(minValue, maxValue, key.value);
                float y = rect.y + rect.height * (1 - normalizedValue);

                Color pointColor = (selectedPointIndex == i) ? Color.yellow : (currentCurveType == CurveType.Horizontal ? Color.blue : Color.red);
                Handles.color = pointColor;
                Handles.DrawSolidDisc(new Vector3(x, y, 0), Vector3.forward, 5f);

                // 显示点的值
                string timeValue = showTimeInMilliseconds ? timeInMs.ToString("F0") : (timeInMs / 1000f).ToString("F2");
                Handles.Label(new Vector3(x + 10, y), $"({timeValue}, {key.value:F2})");
            }
        }

        // 处理交互事件
        Event e = Event.current;
        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            // 查找最近的点
            float minDist = float.MaxValue;
            int closestIndex = -1;

            for (int i = 0; i < currentCurve.keys.Length; i++)
            {
                Keyframe key = currentCurve.keys[i];

                // 将时间从0-1范围转换为毫秒
                float timeInMs = key.time * maxTimeMs;

                // 检查点是否在当前视图范围内
                if (timeInMs <= viewTimeMs)
                {
                    float normalizedViewTime = timeInMs / viewTimeMs;
                    float x = rect.x + normalizedViewTime * rect.width;

                    // 将值映射到扩展的范围内
                    float normalizedValue = Mathf.InverseLerp(minValue, maxValue, key.value);
                    float y = rect.y + rect.height * (1 - normalizedValue);

                    float dist = Vector2.Distance(e.mousePosition, new Vector2(x, y));
                    if (dist < minDist && dist < 10f)
                    {
                        minDist = dist;
                        closestIndex = i;
                    }
                }
            }

            // 如果点击一个点，选中它
            if (closestIndex >= 0)
            {
                selectedPointIndex = closestIndex;
                e.Use();
            }
            // 否则添加一个新点
            else if (e.button == 0)
            {
                // 从视图位置计算时间（毫秒）
                float normalizedViewPos = (e.mousePosition.x - rect.x) / rect.width;
                float timeInMs = normalizedViewPos * viewTimeMs;
                float normalizedTime = timeInMs / maxTimeMs; // 归一化到0-1范围

                // 从扩展范围内的位置计算实际值
                float normalizedValue = 1 - (e.mousePosition.y - rect.y) / rect.height;
                float value = Mathf.Lerp(minValue, maxValue, normalizedValue);

                // 添加新的关键帧
                currentCurve.AddKey(normalizedTime, value);

                // 排序所有关键帧
                SortCurveKeys(currentCurve);

                // 找到新添加的点的索引
                for (int i = 0; i < currentCurve.keys.Length; i++)
                {
                    if (Mathf.Approximately(currentCurve.keys[i].time, normalizedTime) &&
                        Mathf.Approximately(currentCurve.keys[i].value, value))
                    {
                        selectedPointIndex = i;
                        break;
                    }
                }

                e.Use();
            }
        }
        else if (e.type == EventType.MouseDrag && rect.Contains(e.mousePosition) &&
                 selectedPointIndex >= 0 && selectedPointIndex < currentCurve.keys.Length)
        {
            // 移动选中的点

            // 从视图位置计算时间（毫秒）
            float normalizedViewPos = (e.mousePosition.x - rect.x) / rect.width;
            float timeInMs = normalizedViewPos * viewTimeMs;
            float normalizedTime = timeInMs / maxTimeMs; // 归一化到0-1范围

            // 限制在0-1范围内
            normalizedTime = Mathf.Clamp01(normalizedTime);

            // 从扩展范围内的位置计算实际值
            float normalizedValue = 1 - (e.mousePosition.y - rect.y) / rect.height;
            float value = Mathf.Lerp(minValue, maxValue, normalizedValue);

            // 更新关键帧
            Keyframe key = currentCurve.keys[selectedPointIndex];
            key.time = normalizedTime;
            key.value = value;
            currentCurve.MoveKey(selectedPointIndex, key);

            // 排序所有关键帧
            SortCurveKeys(currentCurve);

            // 找到移动后的点的索引
            for (int i = 0; i < currentCurve.keys.Length; i++)
            {
                if (Mathf.Approximately(currentCurve.keys[i].time, normalizedTime) &&
                    Mathf.Approximately(currentCurve.keys[i].value, value))
                {
                    selectedPointIndex = i;
                    break;
                }
            }

            e.Use();
        }
        else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete &&
                 selectedPointIndex >= 0 && selectedPointIndex < currentCurve.keys.Length)
        {
            // 删除选中的点
            currentCurve.RemoveKey(selectedPointIndex);
            selectedPointIndex = -1;
            e.Use();
        }

        // 添加预设按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("预设曲线"))
        {
            if (currentCurveType == CurveType.Horizontal)
            {
                currentCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.1f, 1f),
                    new Keyframe(0.2f, 0.8f),
                    new Keyframe(0.3f, -0.5f),
                    new Keyframe(0.5f, 0.2f),
                    new Keyframe(1f, 0f)
                );
            }
            else
            {
                currentCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.1f, 1f),
                    new Keyframe(0.3f, 0.8f),
                    new Keyframe(0.6f, 0.4f),
                    new Keyframe(1f, 0f)
                );
            }
        }

        if (GUILayout.Button("长时间预设"))
        {
            // 创建一个长时间范围的预设曲线
            currentCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.05f, 1f),  // 250ms
                new Keyframe(0.1f, 0.8f), // 500ms
                new Keyframe(0.2f, 0.6f), // 1000ms
                new Keyframe(0.4f, 0.4f), // 2000ms
                new Keyframe(0.6f, 0.2f), // 3000ms
                new Keyframe(0.8f, 0.1f), // 4000ms
                new Keyframe(1f, 0f)      // 5000ms
            );
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawBreathingPathEditor()
    {
        EditorGUILayout.LabelField("呼吸曲线轨迹编辑器 (X轴: 水平位置, Y轴: 垂直位置)");

        // 计算缩放后的矩形大小
        float scaledWidth = 400 * zoomLevel;
        float scaledHeight = 400 * zoomLevel;
        Rect rect = GUILayoutUtility.GetRect(scaledWidth, scaledHeight);

        // 绘制背景和网格
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 1));
        DrawGrid(rect, 20 * zoomLevel, 0.2f, Color.gray);
        DrawGrid(rect, 100 * zoomLevel, 0.4f, Color.gray);

        // 绘制坐标轴
        Handles.color = Color.white;
        Handles.DrawLine(new Vector3(rect.x, rect.y + rect.height / 2), new Vector3(rect.x + rect.width, rect.y + rect.height / 2));
        Handles.DrawLine(new Vector3(rect.x + rect.width / 2, rect.y), new Vector3(rect.x + rect.width / 2, rect.y + rect.height));

        // 绘制坐标轴标签
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = Color.white;

        // X轴标签
        for (float x = -1; x <= 1; x += 0.5f)
        {
            float xPos = rect.x + rect.width / 2 + x * rect.width / 2;
            Handles.Label(new Vector3(xPos, rect.y + rect.height + 5), x.ToString("F1"), labelStyle);
        }

        // Y轴标签
        for (float y = -1; y <= 1; y += 0.5f)
        {
            float yPos = rect.y + rect.height / 2 - y * rect.height / 2;
            Handles.Label(new Vector3(rect.x - 30, yPos), y.ToString("F1"), labelStyle);
        }

        // 绘制呼吸轨迹的点
        float scale = Mathf.Min(rect.width, rect.height) * 0.4f;
        Vector2 center = new Vector2(rect.x + rect.width / 2, rect.y + rect.height / 2);

        // 绘制呼吸轨迹
        if (breathingPoints.Count > 1)
        {
            Handles.color = Color.cyan;
            for (int i = 0; i < breathingPoints.Count - 1; i++)
            {
                Vector2 start = breathingPoints[i];
                Vector2 end = breathingPoints[i + 1];

                Vector2 startPos = center + new Vector2(start.x * scale, -start.y * scale);
                Vector2 endPos = center + new Vector2(end.x * scale, -end.y * scale);

                Handles.DrawLine(startPos, endPos);
            }

            // 闭合曲线，连接最后一点和第一点
            if (breathingPoints.Count > 2)
            {
                Vector2 start = breathingPoints[breathingPoints.Count - 1];
                Vector2 end = breathingPoints[0];

                Vector2 startPos = center + new Vector2(start.x * scale, -start.y * scale);
                Vector2 endPos = center + new Vector2(end.x * scale, -end.y * scale);

                Handles.DrawLine(startPos, endPos);
            }
        }

        // 绘制轨迹点
        for (int i = 0; i < breathingPoints.Count; i++)
        {
            Vector2 point = breathingPoints[i];
            Vector2 pos = center + new Vector2(point.x * scale, -point.y * scale);

            Color pointColor = (selectedPointIndex == i) ? Color.yellow : Color.cyan;
            Handles.color = pointColor;
            Handles.DrawSolidDisc(pos, Vector3.forward, 5f);

            // 显示点的索引和坐标
            Handles.Label(pos + Vector2.right * 10, $"{i}: ({point.x:F2}, {point.y:F2})");
        }

        // 处理交互事件
        Event e = Event.current;
        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            // 查找最近的点
            float minDist = float.MaxValue;
            int closestIndex = -1;

            for (int i = 0; i < breathingPoints.Count; i++)
            {
                Vector2 point = breathingPoints[i];
                Vector2 pos = center + new Vector2(point.x * scale, -point.y * scale);

                float dist = Vector2.Distance(e.mousePosition, pos);
                if (dist < minDist && dist < 10f)
                {
                    minDist = dist;
                    closestIndex = i;
                }
            }

            // 如果点击一个点，选中它
            if (closestIndex >= 0)
            {
                selectedPointIndex = closestIndex;
                e.Use();
            }
            // 否则添加一个新点
            else if (e.button == 0 && e.shift)
            {
                Vector2 newPoint = new Vector2(
                    (e.mousePosition.x - center.x) / scale,
                    -(e.mousePosition.y - center.y) / scale
                );

                breathingPoints.Add(newPoint);
                selectedPointIndex = breathingPoints.Count - 1;

                e.Use();
            }
        }
        else if (e.type == EventType.MouseDrag && rect.Contains(e.mousePosition) &&
                 selectedPointIndex >= 0 && selectedPointIndex < breathingPoints.Count)
        {
            // 移动选中的点
            Vector2 newPoint = new Vector2(
                (e.mousePosition.x - center.x) / scale,
                -(e.mousePosition.y - center.y) / scale
            );

            breathingPoints[selectedPointIndex] = newPoint;

            e.Use();
        }
        else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete &&
                 selectedPointIndex >= 0 && selectedPointIndex < breathingPoints.Count)
        {
            // 删除选中的点
            breathingPoints.RemoveAt(selectedPointIndex);
            selectedPointIndex = -1;
            e.Use();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("添加点"))
        {
            if (breathingPoints == null)
                breathingPoints = new List<Vector2>();

            breathingPoints.Add(Vector2.zero);
        }

        if (GUILayout.Button("删除选中点") && selectedPointIndex >= 0 && selectedPointIndex < breathingPoints.Count)
        {
            breathingPoints.RemoveAt(selectedPointIndex);
            selectedPointIndex = -1;
        }

        if (GUILayout.Button("重置为默认轨迹"))
        {
            breathingPoints = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(0.25f, 1f),
                new Vector2(0.5f, 0f),
                new Vector2(0.75f, -1f),
                new Vector2(1f, 0f)
            };
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawGrid(Rect rect, float gridSpacing, float gridOpacity, Color gridColor)
    {
        int widthDivs = Mathf.CeilToInt(rect.width / gridSpacing);
        int heightDivs = Mathf.CeilToInt(rect.height / gridSpacing);

        Handles.BeginGUI();
        Handles.color = new Color(gridColor.r, gridColor.g, gridColor.b, gridOpacity);

        for (int i = 0; i < widthDivs; i++)
        {
            Handles.DrawLine(
                new Vector3(rect.x + gridSpacing * i, rect.y),
                new Vector3(rect.x + gridSpacing * i, rect.y + rect.height)
            );
        }

        for (int j = 0; j < heightDivs; j++)
        {
            Handles.DrawLine(
                new Vector3(rect.x, rect.y + gridSpacing * j),
                new Vector3(rect.x + rect.width, rect.y + gridSpacing * j)
            );
        }

        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void SortCurveKeys(AnimationCurve curve)
    {
        // 获取所有关键帧
        Keyframe[] keys = curve.keys;

        // 按时间排序
        System.Array.Sort(keys, (a, b) => a.time.CompareTo(b.time));

        // 重建曲线并设置排序后的关键帧
        curve.keys = keys;
    }

    private void ApplyChanges()
    {
        if (targetPattern == null) return;

        switch (currentCurveType)
        {
            case CurveType.Horizontal:
                targetPattern.horizontalRecoilCurve = new AnimationCurve(currentCurve.keys);
                break;
            case CurveType.Vertical:
                targetPattern.verticalRecoilCurve = new AnimationCurve(currentCurve.keys);
                break;
            case CurveType.Breathing:
                targetPattern.breathingPoints = new List<Vector2>(breathingPoints);
                break;
        }

        // 由于 RecoilPattern 不是 UnityEngine.Object，所以不能直接对其调用 SetDirty
        // 需要在调用此方法的地方确保更改被保存

        Close();
    }
}