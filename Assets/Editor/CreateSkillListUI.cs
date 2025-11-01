using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.ForBattle.UI;

/// <summary>
/// Editor utility to create a SkillList UI panel, content root and an item prefab,
/// and wire them to an existing SkillListController or BattleCanvasController in the scene.
/// Usage: Tools -> Create Skill List UI
/// </summary>
public static class CreateSkillListUI
{
    [MenuItem("Tools/Create Skill List UI")]
    public static void Create()
    {
        // Ensure there's a Canvas in the scene
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var goCanvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = goCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            goCanvas.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            Undo.RegisterCreatedObjectUndo(goCanvas, "Create Canvas");
        }

        // Create SkillListPanel under Canvas
        GameObject panel = new GameObject("SkillListPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(SkillListController));
        panel.transform.SetParent(canvas.transform, false);
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.7f, 0.5f);
        panelRT.anchorMax = new Vector2(0.95f, 0.9f);
        panelRT.sizeDelta = Vector2.zero;
        var img = panel.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);
        Undo.RegisterCreatedObjectUndo(panel, "Create SkillListPanel");

        // Create contentRoot (scrollable content area)
        GameObject content = new GameObject("ContentRoot", typeof(RectTransform));
        content.transform.SetParent(panel.transform, false);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.05f, 0.05f);
        contentRT.anchorMax = new Vector2(0.95f, 0.95f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;
        Undo.RegisterCreatedObjectUndo(content, "Create SkillList ContentRoot");

        // Create a simple item prefab (Image + TextMeshProUGUI)
        string prefabFolder = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(prefabFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        GameObject item = new GameObject("SkillItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var itemRT = item.GetComponent<RectTransform>();
        itemRT.sizeDelta = new Vector2(200, 24);
        var itemImg = item.GetComponent<Image>();
        itemImg.color = new Color(1f, 1f, 1f, 0.1f);

        // Add TextMeshPro child
        GameObject txtGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(item.transform, false);
        var txt = txtGO.GetComponent<TextMeshProUGUI>();
        txt.text = "Skill Name";
        txt.fontSize = 18;
        txt.color = Color.white;
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = new Vector2(0f, 0f);
        txtRT.anchorMax = new Vector2(1f, 1f);
        txtRT.offsetMin = new Vector2(6f, 2f);
        txtRT.offsetMax = new Vector2(-6f, -2f);

        // Save prefab
        string prefabPath = prefabFolder + "/SkillItem.prefab";
        PrefabUtility.SaveAsPrefabAsset(item, prefabPath, out bool success);
        if (!success)
        {
            Debug.LogWarning("Failed to create SkillItem prefab at " + prefabPath);
        }
        else
        {
            Debug.Log("Created SkillItem prefab at " + prefabPath);
        }

        // Load prefab as asset for assignment
        GameObject itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        // Assign SkillListController fields
        var slc = panel.GetComponent<SkillListController>();
        if (slc != null)
        {
            slc.panel = panel;
            slc.contentRoot = content.GetComponent<RectTransform>();
            slc.itemPrefab = itemPrefab;
            EditorUtility.SetDirty(slc);
        }

        // Try to wire into existing BattleCanvasController
        var bcc = Object.FindObjectOfType<BattleCanvasController>();
        if (bcc != null)
        {
            Undo.RecordObject(bcc, "Assign SkillListController");
            bcc.skillListController = slc;
            EditorUtility.SetDirty(bcc);
            Debug.Log("Assigned SkillListController to BattleCanvasController.skillListController");
        }

        // cleanup temporary item in scene
        Object.DestroyImmediate(item);

        // Select the created panel in editor
        Selection.activeGameObject = panel;

        Debug.Log("SkillList UI created. Configure items/skills at runtime via SkillListController.SetSkills().");
    }
}
