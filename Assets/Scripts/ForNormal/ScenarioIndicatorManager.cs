using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 简单的 Scenario 指示器管理器：在两个由 ScenarioSystem 标签键引用的 GameObject 之间创建指定颜色的连线。
/// 使用 LineRenderer 创建一条直线，自动跟随两端 GameObject 的位置。
/// </summary>
public class ScenarioIndicatorManager : MonoBehaviour
{
    [System.Serializable]
    public struct IndicatorColorEntry
    {
        public string name;
        public Color color;
    }

    public IndicatorColorEntry[] colors;
    private Dictionary<string, Color> colorMap = new Dictionary<string, Color>();

    private class Link
    {
        public string fromKey;
        public string toKey;
        public GameObject fromObj;
        public GameObject toObj;
        public GameObject lineObj;
        public LineRenderer lr;
    }

    private readonly List<Link> activeLinks = new List<Link>();

    [Header("Line Settings")]
    public float lineWidth = 0.05f;
    public Material lineMaterial;

    void Awake()
    {
        RebuildColorMap();

        // use default material if none provided
        if (lineMaterial == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) lineMaterial = new Material(shader);
        }
    }

    private void RebuildColorMap()
    {
        colorMap.Clear();
        if (colors == null) return;
        for (int i = 0; i < colors.Length; i++)
        {
            var entry = colors[i];
            if (!string.IsNullOrEmpty(entry.name)) colorMap[entry.name] = entry.color;
        }
    }

    private Color ResolveColor(string colorKey, Color fallback)
    {
        if (!string.IsNullOrEmpty(colorKey) && colorMap.TryGetValue(colorKey, out var c)) return c;
        // simple special-case like BattleIndicatorManager: "blue"
        if (!string.IsNullOrEmpty(colorKey) && colorKey.ToLowerInvariant() == "blue")
        {
            var baseBlue = new Color(0.2f, 0.6f, 1f, fallback.a);
            return baseBlue;
        }
        return fallback;
    }

    private void EnsureMaterialSupportsAlpha(Material mat, Color col)
    {
        if (mat == null) return;
        if (col.a >= 0.99f) return;
        var name = mat.shader != null ? mat.shader.name : string.Empty;
        if (!(name.Contains("Sprite") || name.Contains("Unlit")))
        {
            var spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null) mat.shader = spriteShader;
        }
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
    }

    void Update()
    {
        // update all active links
        for (int i = activeLinks.Count - 1; i >= 0; i--)
        {
            var link = activeLinks[i];
            if (link == null || link.lr == null)
            {
                activeLinks.RemoveAt(i);
                continue;
            }

            // try resolve objects if missing
            if (link.fromObj == null && !string.IsNullOrEmpty(link.fromKey)) link.fromObj = ScenarioSystem.Instance != null ? ScenarioSystem.Instance.GetObject(link.fromKey) : null;
            if (link.toObj == null && !string.IsNullOrEmpty(link.toKey)) link.toObj = ScenarioSystem.Instance != null ? ScenarioSystem.Instance.GetObject(link.toKey) : null;

            if (link.fromObj == null || link.toObj == null)
            {
                // if either end lost, destroy line and remove
                if (link.lineObj != null) Destroy(link.lineObj);
                activeLinks.RemoveAt(i);
                continue;
            }

            Vector3 a = link.fromObj.transform.position;
            Vector3 b = link.toObj.transform.position;
            link.lr.SetPosition(0, a);
            link.lr.SetPosition(1, b);
        }
    }

    /// <summary>
    /// Create a persistent colored line between two scenario-referenced objects by their ScenarioSystem keys.
    /// Returns the created Line GameObject, or null if creation failed.
    /// </summary>
    public GameObject CreateLink(string fromKey, string toKey, Color color)
    {
        if (ScenarioSystem.Instance == null) return null;
        var fromObj = ScenarioSystem.Instance.GetObject(fromKey);
        var toObj = ScenarioSystem.Instance.GetObject(toKey);
        if (fromObj == null || toObj == null) return null;

        // create holder
        GameObject lineGO = new GameObject($"ScenarioLink_{fromKey}_to_{toKey}");
        lineGO.transform.SetParent(this.transform, true);

        var lr = lineGO.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.widthMultiplier = lineWidth;
        lr.useWorldSpace = true;
        lr.material = lineMaterial != null ? new Material(lineMaterial) : new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        EnsureMaterialSupportsAlpha(lr.material, color);
        lr.numCapVertices = 4;

        // initial positions
        lr.SetPosition(0, fromObj.transform.position);
        lr.SetPosition(1, toObj.transform.position);

        var link = new Link { fromKey = fromKey, toKey = toKey, fromObj = fromObj, toObj = toObj, lineObj = lineGO, lr = lr };
        activeLinks.Add(link);
        return lineGO;
    }

    /// <summary>
    /// Create link by color key defined in the color table. Uses fallback if key not found.
    /// </summary>
    public GameObject CreateLink(string fromKey, string toKey, string colorKey, Color fallback)
    {
        var col = ResolveColor(colorKey, fallback);
        return CreateLink(fromKey, toKey, col);
    }

    public void RemoveLink(GameObject lineObj)
    {
        if (lineObj == null) return;
        for (int i = activeLinks.Count - 1; i >= 0; i--)
        {
            if (activeLinks[i].lineObj == lineObj)
            {
                Destroy(activeLinks[i].lineObj);
                activeLinks.RemoveAt(i);
                return;
            }
        }
    }

    public void ClearAllLinks()
    {
        foreach (var l in activeLinks)
        {
            if (l != null && l.lineObj != null) Destroy(l.lineObj);
        }
        activeLinks.Clear();
    }
}
