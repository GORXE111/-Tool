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

    // 编辑状态
    private enum EditMode { None, Horizontal, Vertical, Breathing }
    private EditMode currentEditMode = EditMode.None;
    private int selectedPointIndex = -1;

    [MenuItem("Tools/Weapon Recoil Editor")]
    public static void ShowWindow()
    {
        GetWindow<RecoilPatternEditor>("后坐力编辑器");
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
        EditorGUILayout.LabelField("后坐力模式设置", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        targetController = EditorGUILayout.ObjectField("后坐力控制器", targetController, typeof(WeaponRecoilController), true) as WeaponRecoilController;

        if (targetController != null && targetController.recoilPattern != null)
        {
            targetPattern = targetController.recoilPattern;

            EditorGUILayout.Space();

            // 测试设置
            showTestingSettings = EditorGUILayout.Foldout(showTestingSettings, "测试射击设置");
            if (showTestingSettings)
            {
                EditorGUI.indentLevel++;

                // 子弹预制体
                targetController.bulletPrefab = EditorGUILayout.ObjectField("子弹预制体", targetController.bulletPrefab, typeof(GameObject), false) as GameObject;

                // 发射点
                targetController.firePoint = EditorGUILayout.ObjectField("发射点", targetController.firePoint, typeof(Transform), true) as Transform;

                // 子弹速度
                targetController.bulletSpeed = EditorGUILayout.FloatField("子弹速度", targetController.bulletSpeed);

                // 弹匣容量
                targetController.magazineSize = EditorGUILayout.IntField("弹匣容量", targetController.magazineSize);

                // 射击速率
                testRPM = EditorGUILayout.FloatField("射击速率 (RPM)", testRPM);

                // 目标距离
                targetDistance = EditorGUILayout.Slider("目标距离", targetDistance, 5f, 50f);

                // 创建/移除目标平面
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("创建目标平面"))
                {
                    CreateTargetPlane();
                }

                if (GUILayout.Button("移除目标平面"))
                {
                    RemoveTargetPlane();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 后坐力模式选择
            targetPattern.horizontalPatternType = (RecoilPattern.PatternType)EditorGUILayout.EnumPopup("水平后坐力模式", targetPattern.horizontalPatternType);
            targetPattern.verticalPatternType = (RecoilPattern.PatternType)EditorGUILayout.EnumPopup("垂直后坐力模式", targetPattern.verticalPatternType);

            EditorGUILayout.Space();

            // 通用参数
            EditorGUILayout.LabelField("通用参数", EditorStyles.boldLabel);

            // 时间单位显示选项
            showTimeInMilliseconds = EditorGUILayout.Toggle("显示毫秒单位", showTimeInMilliseconds);
            float displayDuration = showTimeInMilliseconds ? targetPattern.recoilDuration * 1000f : targetPattern.recoilDuration;
            string timeUnit = showTimeInMilliseconds ? "毫秒" : "秒";

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("后坐力持续时间");
            displayDuration = EditorGUILayout.FloatField(displayDuration);
            EditorGUILayout.LabelField(timeUnit, GUILayout.Width(30));
            EditorGUILayout.EndHorizontal();

            if (showTimeInMilliseconds)
                targetPattern.recoilDuration = displayDuration / 1000f;
            else
                targetPattern.recoilDuration = displayDuration;

            // 基础后坐力基数
            targetPattern.baseHorizontalRecoil = EditorGUILayout.FloatField("基础水平后坐力", targetPattern.baseHorizontalRecoil);
            targetPattern.baseVerticalRecoil = EditorGUILayout.FloatField("基础垂直后坐力", targetPattern.baseVerticalRecoil);

            targetPattern.maxRecoilX = EditorGUILayout.FloatField("最大水平后坐力", targetPattern.maxRecoilX);
            targetPattern.maxRecoilY = EditorGUILayout.FloatField("最大垂直后坐力", targetPattern.maxRecoilY);
            targetPattern.recoverySpeed = EditorGUILayout.FloatField("恢复速度", targetPattern.recoverySpeed);

            EditorGUILayout.Space();

            // 程序化参数
            showProceduralSettings = EditorGUILayout.Foldout(showProceduralSettings, "程序模式设置");
            if (showProceduralSettings)
            {
                EditorGUI.indentLevel++;
                targetPattern.baseHorizontalStrength = EditorGUILayout.FloatField("基础水平强度", targetPattern.baseHorizontalStrength);
                targetPattern.baseVerticalStrength = EditorGUILayout.FloatField("基础垂直强度", targetPattern.baseVerticalStrength);
                targetPattern.horizontalRandomness = EditorGUILayout.Slider("水平随机性", targetPattern.horizontalRandomness, 0f, 1f);
                targetPattern.verticalRandomness = EditorGUILayout.Slider("垂直随机性", targetPattern.verticalRandomness, 0f, 1f);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 呼吸抖动参数
            EditorGUILayout.LabelField("呼吸抖动参数", EditorStyles.boldLabel);
            targetPattern.useCustomBreathingPath = EditorGUILayout.Toggle("使用自定义呼吸轨迹", targetPattern.useCustomBreathingPath);
            targetPattern.breathingIntensityX = EditorGUILayout.Slider("水平呼吸强度", targetPattern.breathingIntensityX, 0f, 1f);
            targetPattern.breathingIntensityY = EditorGUILayout.Slider("垂直呼吸强度", targetPattern.breathingIntensityY, 0f, 1f);
            targetPattern.breathingFrequency = EditorGUILayout.Slider("呼吸频率", targetPattern.breathingFrequency, 0.1f, 5f);

            EditorGUILayout.Space();

            // 测试按钮
            EditorGUILayout.LabelField("测试控制", EditorStyles.boldLabel);

            // 显示弹药状态
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField($"剩余弹药: {targetController.remainingBullets} / {targetController.magazineSize}");

                if (BulletManager.Instance != null)
                {
                    EditorGUILayout.LabelField($"场景中的子弹数: {BulletManager.Instance.GetBulletCount()}");
                    EditorGUILayout.LabelField($"场景中的击中标记数: {BulletManager.Instance.GetMarkerCount()}");
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("单次射击"))
            {
                if (Application.isPlaying)
                {
                    targetController.Fire();
                }
                else
                {
                    Debug.Log("只能在运行模式下测试射击");
                }
            }

            if (GUILayout.Button(targetController.isFiring ? "停止射击" : "开始射击"))
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
                    Debug.Log("只能在运行模式下测试射击");
                }
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("重置测试"))
            {
                if (Application.isPlaying)
                {
                    targetController.ResetTest();
                }
                else
                {
                    Debug.Log("只能在运行模式下重置测试");
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(targetController);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("请选择一个WeaponRecoilController组件", MessageType.Info);

            if (GUILayout.Button("创建新武器"))
            {
                CreateNewWeapon();
            }
        }
    }

    private void DrawMainEditArea()
    {
        if (targetPattern == null) return;

        /// 水平后坐力曲线编辑器
        showHorizontalCurveEditor = EditorGUILayout.Foldout(showHorizontalCurveEditor, "水平后坐力曲线");
        if (showHorizontalCurveEditor)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"水平后坐力随时间变化曲线 (X轴: 时间({(showTimeInMilliseconds ? "毫秒" : "秒")}), Y轴: 强度)");

            if (GUILayout.Button("编辑水平后坐力曲线"))
            {
                RecoilCurveEditorWindow.ShowWindow(targetPattern, RecoilCurveEditorWindow.CurveType.Horizontal);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();

        // 垂直后坐力曲线编辑器
        showVerticalCurveEditor = EditorGUILayout.Foldout(showVerticalCurveEditor, "垂直后坐力曲线");
        if (showVerticalCurveEditor)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"垂直后坐力随时间变化曲线 (X轴: 时间({(showTimeInMilliseconds ? "毫秒" : "秒")}), Y轴: 强度)");

            if (GUILayout.Button("编辑垂直后坐力曲线"))
            {
                RecoilCurveEditorWindow.ShowWindow(targetPattern, RecoilCurveEditorWindow.CurveType.Vertical);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();

        // 呼吸轨迹编辑器
        showBreathingEditor = EditorGUILayout.Foldout(showBreathingEditor, "呼吸抖动轨迹");
        if (showBreathingEditor)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("呼吸抖动轨迹编辑器 (X轴: 水平位置, Y轴: 垂直位置)");

            if (GUILayout.Button("编辑呼吸抖动轨迹"))
            {
                RecoilCurveEditorWindow.ShowWindow(targetPattern, RecoilCurveEditorWindow.CurveType.Breathing);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();


    }

    private void DrawCurveEditor(Rect rect, AnimationCurve curve, Color curveColor, EditMode editMode)
    {
        // 绘制背景和网格
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 1));
        DrawGrid(rect, 20, 0.2f, Color.gray);
        DrawGrid(rect, 100, 0.4f, Color.gray);

        // 绘制坐标轴
        Handles.color = Color.white;
        Handles.DrawLine(new Vector3(rect.x, rect.y + rect.height / 2), new Vector3(rect.x + rect.width, rect.y + rect.height / 2));
        Handles.DrawLine(new Vector3(rect.x, rect.y), new Vector3(rect.x, rect.y + rect.height));

        // 绘制曲线
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

        // 绘制关键帧点
        for (int i = 0; i < curve.keys.Length; i++)
        {
            Keyframe key = curve.keys[i];
            float x = rect.x + key.time * rect.width;
            float y = rect.y + rect.height / 2 - key.value * rect.height / 2;

            Color pointColor = (currentEditMode == editMode && selectedPointIndex == i) ? Color.yellow : curveColor;
            Handles.color = pointColor;
            Handles.DrawSolidDisc(new Vector3(x, y, 0), Vector3.forward, 5f);
        }

        // 处理鼠标事件
        Event e = Event.current;
        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            currentEditMode = editMode;

            // 查找最近的点
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

            // 如果点击了一个点，选中它
            if (closestIndex >= 0)
            {
                selectedPointIndex = closestIndex;
                e.Use();
            }
            // 否则，添加一个新点
            else if (e.button == 0)
            {
                float time = Mathf.Clamp01((e.mousePosition.x - rect.x) / rect.width);
                float value = -((e.mousePosition.y - rect.y - rect.height / 2) / (rect.height / 2));

                // 添加新的关键帧
                curve.AddKey(time, value);

                // 重新排序关键帧
                SortCurveKeys(curve);

                // 找到新添加的点的索引
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
            // 移动选中的点
            Keyframe key = curve.keys[selectedPointIndex];

            float time = Mathf.Clamp01((e.mousePosition.x - rect.x) / rect.width);
            float value = -((e.mousePosition.y - rect.y - rect.height / 2) / (rect.height / 2));

            // 更新关键帧
            key.time = time;
            key.value = value;
            curve.MoveKey(selectedPointIndex, key);

            // 重新排序关键帧
            SortCurveKeys(curve);

            // 找到移动后的点的索引
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
            // 删除选中的点
            curve.RemoveKey(selectedPointIndex);
            selectedPointIndex = -1;
            e.Use();
            EditorUtility.SetDirty(targetController);
        }
    }

    private void SortCurveKeys(AnimationCurve curve)
    {
        // 获取所有关键帧
        Keyframe[] keys = curve.keys;

        // 按时间排序
        System.Array.Sort(keys, (a, b) => a.time.CompareTo(b.time));

        // 清除曲线并重新添加排序后的关键帧
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

        // 绘制背景和网格
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 1));
        DrawGrid(rect, 20, 0.2f, Color.gray);
        DrawGrid(rect, 100, 0.4f, Color.gray);

        // 绘制坐标轴
        Handles.color = Color.white;
        Handles.DrawLine(new Vector3(rect.x, rect.y + rect.height / 2), new Vector3(rect.x + rect.width, rect.y + rect.height / 2));
        Handles.DrawLine(new Vector3(rect.x + rect.width / 2, rect.y), new Vector3(rect.x + rect.width / 2, rect.y + rect.height));

        // 计算缩放和中心点
        float scale = Mathf.Min(rect.width, rect.height) * 0.4f;
        Vector2 center = new Vector2(rect.x + rect.width / 2, rect.y + rect.height / 2);

        // 绘制呼吸轨迹
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

            // 如果是封闭曲线，连接最后一点和第一点
            if (targetPattern.breathingPoints.Count > 2)
            {
                Vector2 start = targetPattern.breathingPoints[targetPattern.breathingPoints.Count - 1];
                Vector2 end = targetPattern.breathingPoints[0];

                Vector2 startPos = center + new Vector2(start.x * scale, -start.y * scale);
                Vector2 endPos = center + new Vector2(end.x * scale, -end.y * scale);

                Handles.DrawLine(startPos, endPos);
            }
        }

        // 绘制轨迹点
        for (int i = 0; i < targetPattern.breathingPoints.Count; i++)
        {
            Vector2 point = targetPattern.breathingPoints[i];
            Vector2 pos = center + new Vector2(point.x * scale, -point.y * scale);

            Color pointColor = (currentEditMode == EditMode.Breathing && selectedPointIndex == i) ? Color.yellow : Color.cyan;
            Handles.color = pointColor;
            Handles.DrawSolidDisc(pos, Vector3.forward, 5f);

            // 绘制点的索引
            Handles.Label(pos + Vector2.right * 10, i.ToString());
        }

        // 处理鼠标事件
        Event e = Event.current;
        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            currentEditMode = EditMode.Breathing;

            // 查找最近的点
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

            // 如果点击了一个点，选中它
            if (closestIndex >= 0)
            {
                selectedPointIndex = closestIndex;
                e.Use();
            }
            // 否则，添加一个新点
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
            // 移动选中的点
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
            // 删除选中的点
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
        // 创建新的GameObject
        GameObject newWeaponObj = new GameObject("WeaponRecoilController");
        targetController = newWeaponObj.AddComponent<WeaponRecoilController>();
        targetController.recoilPattern = new RecoilPattern();

        // 设置默认参数
        targetController.magazineSize = 30;
        targetController.bulletSpeed = 100f;

        // 创建发射点
        GameObject firePointObj = new GameObject("FirePoint");
        firePointObj.transform.parent = newWeaponObj.transform;
        firePointObj.transform.localPosition = new Vector3(0, 0, 1);  // 前方1米
        targetController.firePoint = firePointObj.transform;

        // 创建一个视觉模型
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

        // 创建目标平面
        targetPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        targetPlane.name = "RecoilTestTarget";

        // 设置位置和旋转
        if (targetController != null && targetController.firePoint != null)
        {
            targetPlane.transform.position = targetController.firePoint.position + targetController.firePoint.forward * targetDistance;
            targetPlane.transform.rotation = Quaternion.LookRotation(-targetController.firePoint.forward);
        }
        else
        {
            // 默认位置
            targetPlane.transform.position = new Vector3(0, 0, targetDistance);
            targetPlane.transform.rotation = Quaternion.Euler(90, 0, 0);
        }

        // 设置大小
        targetPlane.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

        // 确保碰撞器设置正确
        MeshCollider collider = targetPlane.GetComponent<MeshCollider>();
        if (collider == null)
        {
            collider = targetPlane.AddComponent<MeshCollider>();
        }
        collider.convex = false;
        collider.isTrigger = false;

        // 添加刚体并设置为静态
        Rigidbody rb = targetPlane.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // 添加材质
        Renderer renderer = targetPlane.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.2f, 0.2f, 0.2f);
            renderer.material = mat;

            // 添加网格纹理
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

        // 填充背景
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, backgroundColor);
            }
        }

        // 绘制网格线
        int gridSize = 32;
        for (int i = 0; i < size; i += gridSize)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, i, lineColor);
                texture.SetPixel(i, x, lineColor);
            }
        }

        // 绘制中心十字线
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