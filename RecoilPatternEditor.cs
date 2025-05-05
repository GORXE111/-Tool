#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class RecoilPatternEditor : EditorWindow
{
    private RecoilPattern targetPattern;
    private WeaponRecoilController targetController;

    private Vector2 scrollPosition;
    private bool showHorizontalCurveEditor = true;
    private bool showVerticalCurveEditor = true;
    private bool showBreathingEditor = true;
    private bool showProceduralSettings = true;
    private bool showTestingSettings = true;

    private float testRPM = 600f;
    private bool showTimeInMilliseconds = false;

    private Rect curveRect = new Rect(0, 0, 400, 200);
    private Rect breathingEditorRect = new Rect(0, 0, 400, 400);

    private GameObject targetPlane;
    private float targetDistance = 10f;

    // �༭״̬
    private enum EditMode { None, Horizontal, Vertical, Breathing }
    private EditMode currentEditMode = EditMode.None;
    private int selectedPointIndex = -1;

    [MenuItem("Tools/Weapon Recoil Editor")]
    public static void ShowWindow()
    {
        GetWindow<RecoilPatternEditor>("�������༭��");
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical(GUILayout.Width(250));
        DrawSettingsPanel();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical();
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawMainEditArea();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSettingsPanel()
    {
        EditorGUILayout.LabelField("������ģʽ����", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        targetController = EditorGUILayout.ObjectField("������������", targetController, typeof(WeaponRecoilController), true) as WeaponRecoilController;

        if (targetController != null && targetController.recoilPattern != null)
        {
            targetPattern = targetController.recoilPattern;

            EditorGUILayout.Space();

            // ��������
            showTestingSettings = EditorGUILayout.Foldout(showTestingSettings, "�����������");
            if (showTestingSettings)
            {
                EditorGUI.indentLevel++;

                // �ӵ�Ԥ����
                targetController.bulletPrefab = EditorGUILayout.ObjectField("�ӵ�Ԥ����", targetController.bulletPrefab, typeof(GameObject), false) as GameObject;

                // �����
                targetController.firePoint = EditorGUILayout.ObjectField("�����", targetController.firePoint, typeof(Transform), true) as Transform;

                // �ӵ��ٶ�
                targetController.bulletSpeed = EditorGUILayout.FloatField("�ӵ��ٶ�", targetController.bulletSpeed);

                // ��ϻ����
                targetController.magazineSize = EditorGUILayout.IntField("��ϻ����", targetController.magazineSize);

                // �������
                testRPM = EditorGUILayout.FloatField("������� (RPM)", testRPM);

                // Ŀ�����
                targetDistance = EditorGUILayout.Slider("Ŀ�����", targetDistance, 5f, 50f);

                // ����/�Ƴ�Ŀ��ƽ��
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("����Ŀ��ƽ��"))
                {
                    CreateTargetPlane();
                }

                if (GUILayout.Button("�Ƴ�Ŀ��ƽ��"))
                {
                    RemoveTargetPlane();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // ������ģʽѡ��
            targetPattern.horizontalPatternType = (RecoilPattern.PatternType)EditorGUILayout.EnumPopup("ˮƽ������ģʽ", targetPattern.horizontalPatternType);
            targetPattern.verticalPatternType = (RecoilPattern.PatternType)EditorGUILayout.EnumPopup("��ֱ������ģʽ", targetPattern.verticalPatternType);

            EditorGUILayout.Space();

            // ͨ�ò���
            EditorGUILayout.LabelField("ͨ�ò���", EditorStyles.boldLabel);

            // ʱ�䵥λ��ʾѡ��
            showTimeInMilliseconds = EditorGUILayout.Toggle("��ʾ���뵥λ", showTimeInMilliseconds);
            float displayDuration = showTimeInMilliseconds ? targetPattern.recoilDuration * 1000f : targetPattern.recoilDuration;
            string timeUnit = showTimeInMilliseconds ? "����" : "��";

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("����������ʱ��");
            displayDuration = EditorGUILayout.FloatField(displayDuration);
            EditorGUILayout.LabelField(timeUnit, GUILayout.Width(30));
            EditorGUILayout.EndHorizontal();

            if (showTimeInMilliseconds)
                targetPattern.recoilDuration = displayDuration / 1000f;
            else
                targetPattern.recoilDuration = displayDuration;

            // ��������������
            targetPattern.baseHorizontalRecoil = EditorGUILayout.FloatField("����ˮƽ������", targetPattern.baseHorizontalRecoil);
            targetPattern.baseVerticalRecoil = EditorGUILayout.FloatField("������ֱ������", targetPattern.baseVerticalRecoil);

            targetPattern.maxRecoilX = EditorGUILayout.FloatField("���ˮƽ������", targetPattern.maxRecoilX);
            targetPattern.maxRecoilY = EditorGUILayout.FloatField("���ֱ������", targetPattern.maxRecoilY);
            targetPattern.recoverySpeed = EditorGUILayout.FloatField("�ָ��ٶ�", targetPattern.recoverySpeed);

            EditorGUILayout.Space();

            // ���򻯲���
            showProceduralSettings = EditorGUILayout.Foldout(showProceduralSettings, "����ģʽ����");
            if (showProceduralSettings)
            {
                EditorGUI.indentLevel++;
                targetPattern.baseHorizontalStrength = EditorGUILayout.FloatField("����ˮƽǿ��", targetPattern.baseHorizontalStrength);
                targetPattern.baseVerticalStrength = EditorGUILayout.FloatField("������ֱǿ��", targetPattern.baseVerticalStrength);
                targetPattern.horizontalRandomness = EditorGUILayout.Slider("ˮƽ�����", targetPattern.horizontalRandomness, 0f, 1f);
                targetPattern.verticalRandomness = EditorGUILayout.Slider("��ֱ�����", targetPattern.verticalRandomness, 0f, 1f);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // ������������
            EditorGUILayout.LabelField("������������", EditorStyles.boldLabel);
            targetPattern.useCustomBreathingPath = EditorGUILayout.Toggle("ʹ���Զ�������켣", targetPattern.useCustomBreathingPath);
            targetPattern.breathingIntensityX = EditorGUILayout.Slider("ˮƽ����ǿ��", targetPattern.breathingIntensityX, 0f, 1f);
            targetPattern.breathingIntensityY = EditorGUILayout.Slider("��ֱ����ǿ��", targetPattern.breathingIntensityY, 0f, 1f);
            targetPattern.breathingFrequency = EditorGUILayout.Slider("����Ƶ��", targetPattern.breathingFrequency, 0.1f, 5f);

            EditorGUILayout.Space();

            // ���԰�ť
            EditorGUILayout.LabelField("���Կ���", EditorStyles.boldLabel);

            // ��ʾ��ҩ״̬
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField($"ʣ�൯ҩ: {targetController.remainingBullets} / {targetController.magazineSize}");

                if (BulletManager.Instance != null)
                {
                    EditorGUILayout.LabelField($"�����е��ӵ���: {BulletManager.Instance.GetBulletCount()}");
                    EditorGUILayout.LabelField($"�����еĻ��б����: {BulletManager.Instance.GetMarkerCount()}");
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("�������"))
            {
                if (Application.isPlaying)
                {
                    targetController.Fire();
                }
                else
                {
                    Debug.Log("ֻ��������ģʽ�²������");
                }
            }

            if (GUILayout.Button(targetController.isFiring ? "ֹͣ���" : "��ʼ���"))
            {
                if (Application.isPlaying)
                {
                    if (targetController.isFiring)
                    {
                        targetController.StopFiring();
                    }
                    else
                    {
                        targetController.StartFiring(testRPM);
                    }
                }
                else
                {
                    Debug.Log("ֻ��������ģʽ�²������");
                }
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("���ò���"))
            {
                if (Application.isPlaying)
                {
                    targetController.ResetTest();
                }
                else
                {
                    Debug.Log("ֻ��������ģʽ�����ò���");
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(targetController);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("��ѡ��һ��WeaponRecoilController���", MessageType.Info);

            if (GUILayout.Button("����������"))
            {
                CreateNewWeapon();
            }
        }
    }

    private void DrawMainEditArea()
    {
        if (targetPattern == null) return;

        /// ˮƽ���������߱༭��
        showHorizontalCurveEditor = EditorGUILayout.Foldout(showHorizontalCurveEditor, "ˮƽ����������");
        if (showHorizontalCurveEditor)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"ˮƽ��������ʱ��仯���� (X��: ʱ��({(showTimeInMilliseconds ? "����" : "��")}), Y��: ǿ��)");

            if (GUILayout.Button("�༭ˮƽ����������"))
            {
                RecoilCurveEditorWindow.ShowWindow(targetPattern, RecoilCurveEditorWindow.CurveType.Horizontal);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();

        // ��ֱ���������߱༭��
        showVerticalCurveEditor = EditorGUILayout.Foldout(showVerticalCurveEditor, "��ֱ����������");
        if (showVerticalCurveEditor)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"��ֱ��������ʱ��仯���� (X��: ʱ��({(showTimeInMilliseconds ? "����" : "��")}), Y��: ǿ��)");

            if (GUILayout.Button("�༭��ֱ����������"))
            {
                RecoilCurveEditorWindow.ShowWindow(targetPattern, RecoilCurveEditorWindow.CurveType.Vertical);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();

        // �����켣�༭��
        showBreathingEditor = EditorGUILayout.Foldout(showBreathingEditor, "���������켣");
        if (showBreathingEditor)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("���������켣�༭�� (X��: ˮƽλ��, Y��: ��ֱλ��)");

            if (GUILayout.Button("�༭���������켣"))
            {
                RecoilCurveEditorWindow.ShowWindow(targetPattern, RecoilCurveEditorWindow.CurveType.Breathing);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();


    }

    private void DrawCurveEditor(Rect rect, AnimationCurve curve, Color curveColor, EditMode editMode)
    {
        // ���Ʊ���������
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 1));
        DrawGrid(rect, 20, 0.2f, Color.gray);
        DrawGrid(rect, 100, 0.4f, Color.gray);

        // ����������
        Handles.color = Color.white;
        Handles.DrawLine(new Vector3(rect.x, rect.y + rect.height / 2), new Vector3(rect.x + rect.width, rect.y + rect.height / 2));
        Handles.DrawLine(new Vector3(rect.x, rect.y), new Vector3(rect.x, rect.y + rect.height));

        // ��������
        Handles.color = curveColor;
        Vector3 prevPoint = Vector3.zero;
        bool firstPoint = true;

        for (float t = 0; t <= 1; t += 0.01f)
        {
            float value = curve.Evaluate(t);
            float x = rect.x + t * rect.width;
            float y = rect.y + rect.height / 2 - value * rect.height / 2;

            Vector3 point = new Vector3(x, y, 0);
            if (!firstPoint)
            {
                Handles.DrawLine(prevPoint, point);
            }
            prevPoint = point;
            firstPoint = false;
        }

        // ���ƹؼ�֡��
        for (int i = 0; i < curve.keys.Length; i++)
        {
            Keyframe key = curve.keys[i];
            float x = rect.x + key.time * rect.width;
            float y = rect.y + rect.height / 2 - key.value * rect.height / 2;

            Color pointColor = (currentEditMode == editMode && selectedPointIndex == i) ? Color.yellow : curveColor;
            Handles.color = pointColor;
            Handles.DrawSolidDisc(new Vector3(x, y, 0), Vector3.forward, 5f);
        }

        // ��������¼�
        Event e = Event.current;
        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            currentEditMode = editMode;

            // ��������ĵ�
            float minDist = float.MaxValue;
            int closestIndex = -1;

            for (int i = 0; i < curve.keys.Length; i++)
            {
                Keyframe key = curve.keys[i];
                float x = rect.x + key.time * rect.width;
                float y = rect.y + rect.height / 2 - key.value * rect.height / 2;

                float dist = Vector2.Distance(e.mousePosition, new Vector2(x, y));
                if (dist < minDist && dist < 10f)
                {
                    minDist = dist;
                    closestIndex = i;
                }
            }

            // ��������һ���㣬ѡ����
            if (closestIndex >= 0)
            {
                selectedPointIndex = closestIndex;
                e.Use();
            }
            // �������һ���µ�
            else if (e.button == 0)
            {
                float time = Mathf.Clamp01((e.mousePosition.x - rect.x) / rect.width);
                float value = -((e.mousePosition.y - rect.y - rect.height / 2) / (rect.height / 2));

                // ����µĹؼ�֡
                curve.AddKey(time, value);

                // ��������ؼ�֡
                SortCurveKeys(curve);

                // �ҵ�����ӵĵ������
                for (int i = 0; i < curve.keys.Length; i++)
                {
                    if (Mathf.Approximately(curve.keys[i].time, time) &&
                        Mathf.Approximately(curve.keys[i].value, value))
                    {
                        selectedPointIndex = i;
                        break;
                    }
                }

                e.Use();
                EditorUtility.SetDirty(targetController);
            }
        }
        else if (e.type == EventType.MouseDrag && rect.Contains(e.mousePosition) &&
                 currentEditMode == editMode && selectedPointIndex >= 0 && selectedPointIndex < curve.keys.Length)
        {
            // �ƶ�ѡ�еĵ�
            Keyframe key = curve.keys[selectedPointIndex];

            float time = Mathf.Clamp01((e.mousePosition.x - rect.x) / rect.width);
            float value = -((e.mousePosition.y - rect.y - rect.height / 2) / (rect.height / 2));

            // ���¹ؼ�֡
            key.time = time;
            key.value = value;
            curve.MoveKey(selectedPointIndex, key);

            // ��������ؼ�֡
            SortCurveKeys(curve);

            // �ҵ��ƶ���ĵ������
            for (int i = 0; i < curve.keys.Length; i++)
            {
                if (Mathf.Approximately(curve.keys[i].time, time) &&
                    Mathf.Approximately(curve.keys[i].value, value))
                {
                    selectedPointIndex = i;
                    break;
                }
            }

            e.Use();
            EditorUtility.SetDirty(targetController);
        }
        else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete &&
                 currentEditMode == editMode && selectedPointIndex >= 0 && selectedPointIndex < curve.keys.Length)
        {
            // ɾ��ѡ�еĵ�
            curve.RemoveKey(selectedPointIndex);
            selectedPointIndex = -1;
            e.Use();
            EditorUtility.SetDirty(targetController);
        }
    }

    private void SortCurveKeys(AnimationCurve curve)
    {
        // ��ȡ���йؼ�֡
        Keyframe[] keys = curve.keys;

        // ��ʱ������
        System.Array.Sort(keys, (a, b) => a.time.CompareTo(b.time));

        // ������߲�������������Ĺؼ�֡
        curve.keys = keys;
    }

    private void DrawBreathingPathEditor(Rect rect)
    {
        if (targetPattern.breathingPoints == null || targetPattern.breathingPoints.Count == 0)
        {
            targetPattern.breathingPoints = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(0.25f, 1f),
                new Vector2(0.5f, 0f),
                new Vector2(0.75f, -1f),
                new Vector2(1f, 0f)
            };
        }

        // ���Ʊ���������
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 1));
        DrawGrid(rect, 20, 0.2f, Color.gray);
        DrawGrid(rect, 100, 0.4f, Color.gray);

        // ����������
        Handles.color = Color.white;
        Handles.DrawLine(new Vector3(rect.x, rect.y + rect.height / 2), new Vector3(rect.x + rect.width, rect.y + rect.height / 2));
        Handles.DrawLine(new Vector3(rect.x + rect.width / 2, rect.y), new Vector3(rect.x + rect.width / 2, rect.y + rect.height));

        // �������ź����ĵ�
        float scale = Mathf.Min(rect.width, rect.height) * 0.4f;
        Vector2 center = new Vector2(rect.x + rect.width / 2, rect.y + rect.height / 2);

        // ���ƺ����켣
        if (targetPattern.breathingPoints.Count > 1)
        {
            Handles.color = Color.cyan;
            for (int i = 0; i < targetPattern.breathingPoints.Count - 1; i++)
            {
                Vector2 start = targetPattern.breathingPoints[i];
                Vector2 end = targetPattern.breathingPoints[i + 1];

                Vector2 startPos = center + new Vector2(start.x * scale, -start.y * scale);
                Vector2 endPos = center + new Vector2(end.x * scale, -end.y * scale);

                Handles.DrawLine(startPos, endPos);
            }

            // ����Ƿ�����ߣ��������һ��͵�һ��
            if (targetPattern.breathingPoints.Count > 2)
            {
                Vector2 start = targetPattern.breathingPoints[targetPattern.breathingPoints.Count - 1];
                Vector2 end = targetPattern.breathingPoints[0];

                Vector2 startPos = center + new Vector2(start.x * scale, -start.y * scale);
                Vector2 endPos = center + new Vector2(end.x * scale, -end.y * scale);

                Handles.DrawLine(startPos, endPos);
            }
        }

        // ���ƹ켣��
        for (int i = 0; i < targetPattern.breathingPoints.Count; i++)
        {
            Vector2 point = targetPattern.breathingPoints[i];
            Vector2 pos = center + new Vector2(point.x * scale, -point.y * scale);

            Color pointColor = (currentEditMode == EditMode.Breathing && selectedPointIndex == i) ? Color.yellow : Color.cyan;
            Handles.color = pointColor;
            Handles.DrawSolidDisc(pos, Vector3.forward, 5f);

            // ���Ƶ������
            Handles.Label(pos + Vector2.right * 10, i.ToString());
        }

        // ��������¼�
        Event e = Event.current;
        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            currentEditMode = EditMode.Breathing;

            // ��������ĵ�
            float minDist = float.MaxValue;
            int closestIndex = -1;

            for (int i = 0; i < targetPattern.breathingPoints.Count; i++)
            {
                Vector2 point = targetPattern.breathingPoints[i];
                Vector2 pos = center + new Vector2(point.x * scale, -point.y * scale);

                float dist = Vector2.Distance(e.mousePosition, pos);
                if (dist < minDist && dist < 10f)
                {
                    minDist = dist;
                    closestIndex = i;
                }
            }

            // ��������һ���㣬ѡ����
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

                targetPattern.breathingPoints.Add(newPoint);
                selectedPointIndex = targetPattern.breathingPoints.Count - 1;

                e.Use();
                EditorUtility.SetDirty(targetController);
            }
        }
        else if (e.type == EventType.MouseDrag && rect.Contains(e.mousePosition) &&
                 currentEditMode == EditMode.Breathing && selectedPointIndex >= 0 && selectedPointIndex < targetPattern.breathingPoints.Count)
        {
            // �ƶ�ѡ�еĵ�
            Vector2 newPoint = new Vector2(
                (e.mousePosition.x - center.x) / scale,
                -(e.mousePosition.y - center.y) / scale
            );

            targetPattern.breathingPoints[selectedPointIndex] = newPoint;

            e.Use();
            EditorUtility.SetDirty(targetController);
        }
        else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete &&
                 currentEditMode == EditMode.Breathing && selectedPointIndex >= 0 && selectedPointIndex < targetPattern.breathingPoints.Count)
        {
            // ɾ��ѡ�еĵ�
            targetPattern.breathingPoints.RemoveAt(selectedPointIndex);
            selectedPointIndex = -1;
            e.Use();
            EditorUtility.SetDirty(targetController);
        }
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

    private void CreateNewWeapon()
    {
        // �����µ�GameObject
        GameObject newWeaponObj = new GameObject("WeaponRecoilController");
        targetController = newWeaponObj.AddComponent<WeaponRecoilController>();
        targetController.recoilPattern = new RecoilPattern();

        // ����Ĭ�ϲ���
        targetController.magazineSize = 30;
        targetController.bulletSpeed = 100f;

        // ���������
        GameObject firePointObj = new GameObject("FirePoint");
        firePointObj.transform.parent = newWeaponObj.transform;
        firePointObj.transform.localPosition = new Vector3(0, 0, 1);  // ǰ��1��
        targetController.firePoint = firePointObj.transform;

        // ����һ���Ӿ�ģ��
        GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        model.transform.parent = newWeaponObj.transform;
        model.transform.localPosition = Vector3.zero;
        model.transform.localScale = new Vector3(0.1f, 0.1f, 0.5f);

        Selection.activeGameObject = newWeaponObj;
        EditorUtility.SetDirty(targetController);
    }

    private void CreateTargetPlane()
    {
        if (targetPlane != null)
        {
            RemoveTargetPlane();
        }

        // ����Ŀ��ƽ��
        targetPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        targetPlane.name = "RecoilTestTarget";

        // ����λ�ú���ת
        if (targetController != null && targetController.firePoint != null)
        {
            targetPlane.transform.position = targetController.firePoint.position + targetController.firePoint.forward * targetDistance;
            targetPlane.transform.rotation = Quaternion.LookRotation(-targetController.firePoint.forward);
        }
        else
        {
            // Ĭ��λ��
            targetPlane.transform.position = new Vector3(0, 0, targetDistance);
            targetPlane.transform.rotation = Quaternion.Euler(90, 0, 0);
        }

        // ���ô�С
        targetPlane.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

        // ȷ����ײ��������ȷ
        MeshCollider collider = targetPlane.GetComponent<MeshCollider>();
        if (collider == null)
        {
            collider = targetPlane.AddComponent<MeshCollider>();
        }
        collider.convex = false;
        collider.isTrigger = false;

        // ��Ӹ��岢����Ϊ��̬
        Rigidbody rb = targetPlane.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // ��Ӳ���
        Renderer renderer = targetPlane.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.2f, 0.2f, 0.2f);
            renderer.material = mat;

            // �����������
            Texture2D gridTexture = CreateGridTexture();
            mat.mainTexture = gridTexture;
        }
    }

    private Texture2D CreateGridTexture()
    {
        int size = 512;
        Texture2D texture = new Texture2D(size, size);
        Color backgroundColor = new Color(0.2f, 0.2f, 0.2f);
        Color lineColor = new Color(1f, 1f, 1f, 0.5f);

        // ��䱳��
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, backgroundColor);
            }
        }

        // ����������
        int gridSize = 32;
        for (int i = 0; i < size; i += gridSize)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, i, lineColor);
                texture.SetPixel(i, x, lineColor);
            }
        }

        // ��������ʮ����
        int center = size / 2;
        int crossSize = 5;
        for (int i = -crossSize; i <= crossSize; i++)
        {
            for (int j = -crossSize; j <= crossSize; j++)
            {
                if (Mathf.Abs(i) <= 1 || Mathf.Abs(j) <= 1)
                {
                    int x = center + i;
                    int y = center + j;
                    if (x >= 0 && x < size && y >= 0 && y < size)
                    {
                        texture.SetPixel(center + i, center + j, Color.red);
                    }
                }
            }
        }

        texture.Apply();
        return texture;
    }

    private void RemoveTargetPlane()
    {
        if (targetPlane != null)
        {
            DestroyImmediate(targetPlane);
            targetPlane = null;
        }
    }
}
#endif