using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class RecoilCurveEditorWindow : EditorWindow
{
    public enum CurveType { Horizontal, Vertical, Breathing }  // ��Ϊ public

    private CurveType currentCurveType;
    private AnimationCurve currentCurve;
    private RecoilPattern targetPattern;
    private List<Vector2> breathingPoints;
    private bool showTimeInMilliseconds = true;

    private Rect curveRect = new Rect(0, 0, 400, 200);
    private Rect breathingEditorRect = new Rect(0, 0, 400, 400);
    private int selectedPointIndex = -1;

    // ������Ź��ܱ���
    private float zoomLevel = 1.0f;
    private Vector2 zoomPivot = Vector2.zero;
    private Vector2 scrollPosition = Vector2.zero;

    // ������߱༭��Χ����
    private float minValue = -2.0f;  // ��СYֵ
    private float maxValue = 2.0f;   // ���Yֵ

    // ���ʱ�䷶Χ����
    private float maxTimeMs = 5000f; // ���ʱ�䣨���룩
    private float viewTimeMs = 1000f; // ��ǰ��ͼ����ʾ��ʱ�䷶Χ�����룩

    public static void ShowWindow(RecoilPattern pattern, CurveType type)
    {
        RecoilCurveEditorWindow window = GetWindow<RecoilCurveEditorWindow>("���߱༭��");
        window.targetPattern = pattern;
        window.currentCurveType = type;

        switch (type)
        {
            case CurveType.Horizontal:
                window.currentCurve = pattern.horizontalRecoilCurve;
                window.titleContent = new GUIContent("ˮƽ����������");
                break;
            case CurveType.Vertical:
                window.currentCurve = pattern.verticalRecoilCurve;
                window.titleContent = new GUIContent("��ֱ����������");
                break;
            case CurveType.Breathing:
                window.breathingPoints = pattern.breathingPoints;
                window.titleContent = new GUIContent("�������߱༭");
                break;
        }
    }

    private void OnGUI()
    {
        if (targetPattern == null) return;

        EditorGUILayout.BeginVertical();

        // ��ʾ���ſ���
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("���ż���:", GUILayout.Width(60));
        zoomLevel = EditorGUILayout.Slider(zoomLevel, 0.5f, 3.0f);
        if (GUILayout.Button("��������", GUILayout.Width(80)))
        {
            zoomLevel = 1.0f;
            scrollPosition = Vector2.zero;
        }
        EditorGUILayout.EndHorizontal();

        // ��ʾֵ��Χ���ƣ��������߱༭����Ч��
        if (currentCurveType != CurveType.Breathing)
        {
            // ֵ��Χ����
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ֵ��Χ:", GUILayout.Width(60));
            EditorGUILayout.LabelField("��Сֵ", GUILayout.Width(40));
            minValue = EditorGUILayout.FloatField(minValue, GUILayout.Width(40));
            EditorGUILayout.LabelField("���ֵ", GUILayout.Width(40));
            maxValue = EditorGUILayout.FloatField(maxValue, GUILayout.Width(40));
            if (GUILayout.Button("���÷�Χ", GUILayout.Width(80)))
            {
                minValue = -2.0f;
                maxValue = 2.0f;
            }
            EditorGUILayout.EndHorizontal();

            // ʱ�䷶Χ����
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ʱ�䷶Χ(ms):", GUILayout.Width(90));
            EditorGUILayout.LabelField("���ֵ", GUILayout.Width(40));
            maxTimeMs = EditorGUILayout.FloatField(maxTimeMs, GUILayout.Width(60));
            EditorGUILayout.LabelField("��ʾ��Χ", GUILayout.Width(60));
            viewTimeMs = EditorGUILayout.FloatField(viewTimeMs, GUILayout.Width(60));
            if (GUILayout.Button("����ʱ��", GUILayout.Width(80)))
            {
                maxTimeMs = 5000f;
                viewTimeMs = 1000f;
            }
            EditorGUILayout.EndHorizontal();

            // ȷ���Ϸ�ֵ
            maxTimeMs = Mathf.Max(100f, maxTimeMs);
            viewTimeMs = Mathf.Clamp(viewTimeMs, 100f, maxTimeMs);
        }

        showTimeInMilliseconds = EditorGUILayout.Toggle("��ʾ���뵥λ", showTimeInMilliseconds);

        // ʹ�ù�����ͼ��װ����
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

        if (GUILayout.Button("Ӧ�ø���"))
        {
            ApplyChanges();
        }

        EditorGUILayout.EndVertical();

        // ���������¼�
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
        EditorGUILayout.LabelField($"���߱༭�� (X��: ʱ��({(showTimeInMilliseconds ? "����" : "��")}), Y��: ǿ��)");

        // �������ź�ľ��δ�С
        float scaledWidth = 400 * zoomLevel;
        float scaledHeight = 300 * zoomLevel;
        Rect rect = GUILayoutUtility.GetRect(scaledWidth, scaledHeight);

        // ���Ʊ���������
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 1));
        DrawGrid(rect, 20 * zoomLevel, 0.2f, Color.gray);
        DrawGrid(rect, 100 * zoomLevel, 0.4f, Color.gray);

        // ����������
        Handles.color = Color.white;
        // ˮƽ�ᣨY=0����λ��
        float zeroY = rect.y + rect.height * (maxValue / (maxValue - minValue));
        Handles.DrawLine(new Vector3(rect.x, zeroY), new Vector3(rect.x + rect.width, zeroY));
        // ��ֱ�ᣨX=0��
        Handles.DrawLine(new Vector3(rect.x, rect.y), new Vector3(rect.x, rect.y + rect.height));

        // �����������ǩ
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = Color.white;

        // X���ǩ - ����չ��ʱ�䷶Χ����ʾ��ǩ
        float timeStep = viewTimeMs / 5; // 5���ȷ�
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

            // ���ƴ�ֱ������
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.y + rect.height));
            Handles.color = Color.white;
        }

        // Y���ǩ - ����չ�ķ�Χ����ʾ��ǩ
        float valueStep = (maxValue - minValue) / 8; // 8���ȷ�
        for (float v = minValue; v <= maxValue; v += valueStep)
        {
            // �����ǩ��Y���ϵ�λ��
            float normalizedValue = (v - minValue) / (maxValue - minValue);
            float y = rect.y + rect.height * (1 - normalizedValue);

            Handles.Label(new Vector3(rect.x - 30, y), v.ToString("F1"), labelStyle);

            // ����ˮƽ������
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.x + rect.width, y));
            Handles.color = Color.white;
        }

        // ��������
        Handles.color = currentCurveType == CurveType.Horizontal ? Color.blue : Color.red;
        Vector3 prevPoint = Vector3.zero;
        bool firstPoint = true;

        // ���Ƹ�����Ի�ø�ƽ��������
        int numSteps = 200;
        for (int i = 0; i <= numSteps; i++)
        {
            float normalizedTime = (float)i / numSteps;
            float timeInMs = normalizedTime * viewTimeMs;
            float timeNormalized = timeInMs / maxTimeMs; // ��һ����0-1��Χ������������

            float value = currentCurve.Evaluate(timeNormalized);
            float x = rect.x + normalizedTime * rect.width;

            // ��ֵӳ�䵽��չ�ķ�Χ��
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

        // ���ƹؼ�֡��
        for (int i = 0; i < currentCurve.keys.Length; i++)
        {
            Keyframe key = currentCurve.keys[i];

            // ��ʱ���0-1��Χת��Ϊ����
            float timeInMs = key.time * maxTimeMs;

            // �����Ƿ��ڵ�ǰ��ͼ��Χ��
            if (timeInMs <= viewTimeMs)
            {
                float normalizedViewTime = timeInMs / viewTimeMs;
                float x = rect.x + normalizedViewTime * rect.width;

                // ��ֵӳ�䵽��չ�ķ�Χ��
                float normalizedValue = Mathf.InverseLerp(minValue, maxValue, key.value);
                float y = rect.y + rect.height * (1 - normalizedValue);

                Color pointColor = (selectedPointIndex == i) ? Color.yellow : (currentCurveType == CurveType.Horizontal ? Color.blue : Color.red);
                Handles.color = pointColor;
                Handles.DrawSolidDisc(new Vector3(x, y, 0), Vector3.forward, 5f);

                // ��ʾ���ֵ
                string timeValue = showTimeInMilliseconds ? timeInMs.ToString("F0") : (timeInMs / 1000f).ToString("F2");
                Handles.Label(new Vector3(x + 10, y), $"({timeValue}, {key.value:F2})");
            }
        }

        // �������¼�
        Event e = Event.current;
        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            // ��������ĵ�
            float minDist = float.MaxValue;
            int closestIndex = -1;

            for (int i = 0; i < currentCurve.keys.Length; i++)
            {
                Keyframe key = currentCurve.keys[i];

                // ��ʱ���0-1��Χת��Ϊ����
                float timeInMs = key.time * maxTimeMs;

                // �����Ƿ��ڵ�ǰ��ͼ��Χ��
                if (timeInMs <= viewTimeMs)
                {
                    float normalizedViewTime = timeInMs / viewTimeMs;
                    float x = rect.x + normalizedViewTime * rect.width;

                    // ��ֵӳ�䵽��չ�ķ�Χ��
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

            // ������һ���㣬ѡ����
            if (closestIndex >= 0)
            {
                selectedPointIndex = closestIndex;
                e.Use();
            }
            // �������һ���µ�
            else if (e.button == 0)
            {
                // ����ͼλ�ü���ʱ�䣨���룩
                float normalizedViewPos = (e.mousePosition.x - rect.x) / rect.width;
                float timeInMs = normalizedViewPos * viewTimeMs;
                float normalizedTime = timeInMs / maxTimeMs; // ��һ����0-1��Χ

                // ����չ��Χ�ڵ�λ�ü���ʵ��ֵ
                float normalizedValue = 1 - (e.mousePosition.y - rect.y) / rect.height;
                float value = Mathf.Lerp(minValue, maxValue, normalizedValue);

                // ����µĹؼ�֡
                currentCurve.AddKey(normalizedTime, value);

                // �������йؼ�֡
                SortCurveKeys(currentCurve);

                // �ҵ�����ӵĵ������
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
            // �ƶ�ѡ�еĵ�

            // ����ͼλ�ü���ʱ�䣨���룩
            float normalizedViewPos = (e.mousePosition.x - rect.x) / rect.width;
            float timeInMs = normalizedViewPos * viewTimeMs;
            float normalizedTime = timeInMs / maxTimeMs; // ��һ����0-1��Χ

            // ������0-1��Χ��
            normalizedTime = Mathf.Clamp01(normalizedTime);

            // ����չ��Χ�ڵ�λ�ü���ʵ��ֵ
            float normalizedValue = 1 - (e.mousePosition.y - rect.y) / rect.height;
            float value = Mathf.Lerp(minValue, maxValue, normalizedValue);

            // ���¹ؼ�֡
            Keyframe key = currentCurve.keys[selectedPointIndex];
            key.time = normalizedTime;
            key.value = value;
            currentCurve.MoveKey(selectedPointIndex, key);

            // �������йؼ�֡
            SortCurveKeys(currentCurve);

            // �ҵ��ƶ���ĵ������
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
            // ɾ��ѡ�еĵ�
            currentCurve.RemoveKey(selectedPointIndex);
            selectedPointIndex = -1;
            e.Use();
        }

        // ���Ԥ�谴ť
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Ԥ������"))
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

        if (GUILayout.Button("��ʱ��Ԥ��"))
        {
            // ����һ����ʱ�䷶Χ��Ԥ������
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
        EditorGUILayout.LabelField("�������߹켣�༭�� (X��: ˮƽλ��, Y��: ��ֱλ��)");

        // �������ź�ľ��δ�С
        float scaledWidth = 400 * zoomLevel;
        float scaledHeight = 400 * zoomLevel;
        Rect rect = GUILayoutUtility.GetRect(scaledWidth, scaledHeight);

        // ���Ʊ���������
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 1));
        DrawGrid(rect, 20 * zoomLevel, 0.2f, Color.gray);
        DrawGrid(rect, 100 * zoomLevel, 0.4f, Color.gray);

        // ����������
        Handles.color = Color.white;
        Handles.DrawLine(new Vector3(rect.x, rect.y + rect.height / 2), new Vector3(rect.x + rect.width, rect.y + rect.height / 2));
        Handles.DrawLine(new Vector3(rect.x + rect.width / 2, rect.y), new Vector3(rect.x + rect.width / 2, rect.y + rect.height));

        // �����������ǩ
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = Color.white;

        // X���ǩ
        for (float x = -1; x <= 1; x += 0.5f)
        {
            float xPos = rect.x + rect.width / 2 + x * rect.width / 2;
            Handles.Label(new Vector3(xPos, rect.y + rect.height + 5), x.ToString("F1"), labelStyle);
        }

        // Y���ǩ
        for (float y = -1; y <= 1; y += 0.5f)
        {
            float yPos = rect.y + rect.height / 2 - y * rect.height / 2;
            Handles.Label(new Vector3(rect.x - 30, yPos), y.ToString("F1"), labelStyle);
        }

        // ���ƺ����켣�ĵ�
        float scale = Mathf.Min(rect.width, rect.height) * 0.4f;
        Vector2 center = new Vector2(rect.x + rect.width / 2, rect.y + rect.height / 2);

        // ���ƺ����켣
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

            // �պ����ߣ��������һ��͵�һ��
            if (breathingPoints.Count > 2)
            {
                Vector2 start = breathingPoints[breathingPoints.Count - 1];
                Vector2 end = breathingPoints[0];

                Vector2 startPos = center + new Vector2(start.x * scale, -start.y * scale);
                Vector2 endPos = center + new Vector2(end.x * scale, -end.y * scale);

                Handles.DrawLine(startPos, endPos);
            }
        }

        // ���ƹ켣��
        for (int i = 0; i < breathingPoints.Count; i++)
        {
            Vector2 point = breathingPoints[i];
            Vector2 pos = center + new Vector2(point.x * scale, -point.y * scale);

            Color pointColor = (selectedPointIndex == i) ? Color.yellow : Color.cyan;
            Handles.color = pointColor;
            Handles.DrawSolidDisc(pos, Vector3.forward, 5f);

            // ��ʾ�������������
            Handles.Label(pos + Vector2.right * 10, $"{i}: ({point.x:F2}, {point.y:F2})");
        }

        // �������¼�
        Event e = Event.current;
        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            // ��������ĵ�
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

            // ������һ���㣬ѡ����
            if (closestIndex >= 0)
            {
                selectedPointIndex = closestIndex;
                e.Use();
            }
            // �������һ���µ�
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
            // �ƶ�ѡ�еĵ�
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
            // ɾ��ѡ�еĵ�
            breathingPoints.RemoveAt(selectedPointIndex);
            selectedPointIndex = -1;
            e.Use();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("��ӵ�"))
        {
            if (breathingPoints == null)
                breathingPoints = new List<Vector2>();

            breathingPoints.Add(Vector2.zero);
        }

        if (GUILayout.Button("ɾ��ѡ�е�") && selectedPointIndex >= 0 && selectedPointIndex < breathingPoints.Count)
        {
            breathingPoints.RemoveAt(selectedPointIndex);
            selectedPointIndex = -1;
        }

        if (GUILayout.Button("����ΪĬ�Ϲ켣"))
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
        // ��ȡ���йؼ�֡
        Keyframe[] keys = curve.keys;

        // ��ʱ������
        System.Array.Sort(keys, (a, b) => a.time.CompareTo(b.time));

        // �ؽ����߲����������Ĺؼ�֡
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

        // ���� RecoilPattern ���� UnityEngine.Object�����Բ���ֱ�Ӷ������ SetDirty
        // ��Ҫ�ڵ��ô˷����ĵط�ȷ�����ı�����

        Close();
    }
}