using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.IO;
using System.Linq;

/// <summary>
/// 一键创建对话选项 UI：ChoicePanel + ContentRoot + ChoiceItem 预制体，并自动接线到 DialogueChoicePanel。
/// 菜单：Tools/Create Dialogue Choice UI
/// </summary>
public static class CreateDialogueChoiceUI
{
    [MenuItem("Tools/Create Dialogue Choice UI")] 
    public static void Create()
    {
        // 找/建 Canvas
        Canvas canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var goCanvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = goCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = goCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            Undo.RegisterCreatedObjectUndo(goCanvas, "Create Canvas");
        }

        // 找/建 EventSystem
        if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        // 创建 ChoicePanel
        GameObject panel = GameObject.Find("ChoicePanel");
        bool createdPanel = false;
        if (panel == null)
        {
            panel = new GameObject("ChoicePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            createdPanel = true;
        }

        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.65f, 0.1f);
        panelRT.anchorMax = new Vector2(0.95f, 0.4f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        var bg = panel.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        // 创建 ContentRoot
        RectTransform contentRoot = null;
        var existingContent = panel.transform.Find("ContentRoot");
        if (existingContent != null)
        {
            contentRoot = existingContent as RectTransform;
        }
        else
        {
            var content = new GameObject("ContentRoot", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(panel.transform, false);
            contentRoot = content.GetComponent<RectTransform>();
            contentRoot.anchorMin = new Vector2(0.05f, 0.05f);
            contentRoot.anchorMax = new Vector2(0.95f, 0.95f);
            contentRoot.offsetMin = Vector2.zero;
            contentRoot.offsetMax = Vector2.zero;
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // 创建 ChoiceItem 预制体
        string prefabFolder = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(prefabFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        string prefabPath = Path.Combine(prefabFolder, "DialogueChoiceItem.prefab").Replace("\\", "/");

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabAsset == null)
        {
            // 在场景中构建临时对象
            GameObject item = new GameObject("DialogueChoiceItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var itemRT = item.GetComponent<RectTransform>();
            itemRT.sizeDelta = new Vector2(500, 44);
            var itemImg = item.GetComponent<Image>();
            itemImg.color = new Color(1f, 1f, 1f, 0.1f);

            // 文本：优先使用 TMP_Text，如果不可用则回退到普通 Text
            GameObject label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(item.transform, false);

            var tmpType = FindTypeAnywhere("TMPro.TextMeshProUGUI");
            if (tmpType != null)
            {
                label.AddComponent(tmpType);
                var p = tmpType.GetProperty("text");
                p?.SetValue(label.GetComponent(tmpType), "选项", null);
                var pFont = tmpType.GetProperty("fontSize");
                pFont?.SetValue(label.GetComponent(tmpType), 22f, null);
                var pColor = tmpType.GetProperty("color");
                pColor?.SetValue(label.GetComponent(tmpType), Color.white, null);
            }
            else
            {
                var text = label.AddComponent<Text>();
                text.text = "选项";
                text.fontSize = 22;
                text.alignment = TextAnchor.MiddleLeft;
                text.color = Color.white;
            }

            var labelRT = label.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 0);
            labelRT.anchorMax = new Vector2(1, 1);
            labelRT.offsetMin = new Vector2(12, 6);
            labelRT.offsetMax = new Vector2(-12, -6);

            // 保存为预制体
            PrefabUtility.SaveAsPrefabAsset(item, prefabPath, out bool success);
            UnityEngine.Object.DestroyImmediate(item);
            if (!success)
            {
                Debug.LogWarning("Failed to create DialogueChoiceItem prefab at " + prefabPath);
            }
            else
            {
                Debug.Log("Created DialogueChoiceItem prefab at " + prefabPath);
            }
            prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        // 为 ChoicePanel 添加 DialogueChoicePanel 组件（通过反射避免编译依赖）
        var dcpType = FindTypeAnywhere("DialogueChoicePanel");
        Component dcpComp = null;
        if (dcpType != null)
        {
            dcpComp = panel.GetComponent(dcpType);
            if (dcpComp == null)
            {
                dcpComp = panel.AddComponent(dcpType);
            }
        }
        else
        {
            Debug.LogError("未找到 DialogueChoicePanel 类型，请确保脚本存在于项目并能编译。");
        }

        // 反射接线字段 contentRoot / choiceItemPrefab
        if (dcpComp != null)
        {
            var t = dcpComp.GetType();
            var fRoot = t.GetField("contentRoot");
            var fPrefab = t.GetField("choiceItemPrefab");
            Undo.RecordObject(panel, "Wire DialogueChoicePanel");
            if (fRoot != null) fRoot.SetValue(dcpComp, contentRoot);
            if (fPrefab != null) fPrefab.SetValue(dcpComp, prefabAsset);
            EditorUtility.SetDirty(panel);
        }

        if (createdPanel)
        {
            Undo.RegisterCreatedObjectUndo(panel, "Create ChoicePanel");
        }

        // 选中面板
        Selection.activeGameObject = panel;

        Debug.Log("Dialogue Choice UI created. Panel: ChoicePanel, Prefab: " + prefabPath);
    }

    private static System.Type FindTypeAnywhere(string typeName)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var asm in assemblies)
        {
            try
            {
                var t = asm.GetTypes().FirstOrDefault(x => x.Name == typeName || x.FullName == typeName);
                if (t != null) return t;
            }
            catch { }
        }
        return null;
    }
}
