using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.ForBattle.Indicators
{
    /// <summary>
    /// 战斗范围指示器管理器，负责创建和管理各种技能范围指示
    /// </summary>
    public class BattleIndicatorManager : MonoBehaviour
    {
        [Header("Indicator Prefabs (Optional)")]
        public GameObject sectorIndicatorPrefab;
        public GameObject circleIndicatorPrefab;
        public GameObject targetMarkerPrefab;

        [Header("Indicator Materials")]
        public Material indicatorMaterial;
        // 默认有效颜色由绿色改为蓝色（含透明度）
        public Color validColor = new Color(0f,0.5f,1f,0.3f);
        public Color invalidColor = new Color(1f, 0f, 0f, 0.3f);
        public Color targetColor = Color.red;

        [System.Serializable]
        public struct IndicatorColorEntry
        {
            public string name;
            public Color color;
        }

        public IndicatorColorEntry[] colors;

        [Header("Debug")]
        public bool debugShowIndicatorMarker = false;

        // 指示器标签常量
        public static class Tags
        {
            public const string MovementRange = "MovementRange";
            public const string AttackRange = "AttackRange";
            public const string SkillRange = "SkillRange";
            public const string SkillPreview = "SkillPreview";
            public const string AreaSelection = "AreaSelection";
            public const string DirectionSelection = "DirectionSelection";
            public const string TargetMarker = "TargetMarker";
            public const string None = "";
        }

        // 跟踪所有指示器及其标签
        private Dictionary<string, List<GameObject>> taggedIndicators = new Dictionary<string, List<GameObject>>();
        private List<GameObject> allIndicators = new List<GameObject>();
        private List<GameObject> debugMarkers = new List<GameObject>();

        //颜色表（name -> Color）
        private Dictionary<string, Color> colorMap = new Dictionary<string, Color>();

        private void Awake()
        {
            RebuildColorMap();
        }

        /// <summary>
        ///重新构建颜色表，运行时可调用以刷新 Inspector 中的配置。
        /// </summary>
        public void RebuildColorMap()
        {
            colorMap.Clear();
            if (colors == null) return;
            for (int i =0; i < colors.Length; i++)
            {
                var entry = colors[i];
                if (!string.IsNullOrEmpty(entry.name))
                {
                    colorMap[entry.name] = entry.color;
                }
            }
        }

        private Color ResolveColor(string colorKey, Color fallback)
        {
            if (!string.IsNullOrEmpty(colorKey) && colorMap.TryGetValue(colorKey, out var c))
            {
                return c;
            }
            return fallback;
        }

        private void ApplyColorToIndicator(GameObject indicator, Color color)
        {
            if (indicator == null) return;

            // 应用于所有 LineRenderer
            var lineRenderers = indicator.GetComponentsInChildren<LineRenderer>(true);
            if (lineRenderers != null && lineRenderers.Length >0)
            {
                foreach (var lr in lineRenderers)
                {
                    lr.startColor = color;
                    lr.endColor = color;
                }
            }

            // 应用于所有 Renderer 的 material.color
            var renderers = indicator.GetComponentsInChildren<Renderer>(true);
            if (renderers != null && renderers.Length >0)
            {
                foreach (var rend in renderers)
                {
                    if (rend.material != null)
                    {
                        rend.material.color = color;
                    }
                }
            }
        }

        // ===== 创建方法（带标签） =====

        /// <summary>
        /// 创建扇形指示器，支持标签分组管理
        /// </summary>
        /// <param name="tag">指示器标签，用于分组管理。相同标签的指示器可以一起清理</param>
        /// <param name="clearSameTag">是否自动清理相同标签的旧指示器</param>
        /// <param name="colorKey">颜色表键名（可选）。若提供，将覆盖默认颜色</param>
        public GameObject CreateSectorIndicator(Transform center, float radius, float angle, string tag = "", bool clearSameTag = false, string colorKey = null)
        {
            if (clearSameTag && !string.IsNullOrEmpty(tag))
            {
                ClearIndicatorsByTag(tag);
            }

            GameObject indicator;
            if (sectorIndicatorPrefab != null)
            {
                indicator = Instantiate(sectorIndicatorPrefab, center.position, Quaternion.identity);
                indicator.transform.localScale = new Vector3(radius *2f, 1f, radius *2f);
            }
            else
            {
                indicator = CreateSectorMesh(center.position, radius, angle);
            }

            if (indicator != null)
            {
                indicator.transform.position = center.position + Vector3.up *0.01f;
                indicator.transform.SetParent(this.transform, true);
                indicator.name = string.IsNullOrEmpty(tag) ? "SectorIndicator" : $"SectorIndicator_{tag}";

                //颜色：若提供 colorKey，覆盖默认 validColor
                var col = ResolveColor(colorKey, validColor);
                ApplyColorToIndicator(indicator, col);

                TrackIndicator(indicator, tag);
            }

            if (debugShowIndicatorMarker) CreateDebugMarker(center.position);
            return indicator;
        }

        /// <summary>
        /// 创建圆形指示器，支持标签分组管理
        /// </summary>
        /// <param name="tag">指示器标签</param>
        /// <param name="clearSameTag">是否自动清理相同标签的旧指示器</param>
        /// <param name="colorKey">颜色表键名（可选）。若提供，将覆盖默认颜色（会根据 isValid 与 invalidColor/validColor 二选一作为回退）</param>
        public GameObject CreateCircleIndicator(Vector3 worldPos, float radius, bool isValid = true, bool hollow = false, string tag = "", bool clearSameTag = false, string colorKey = null)
        {
            if (clearSameTag && !string.IsNullOrEmpty(tag))
            {
                ClearIndicatorsByTag(tag);
            }

            GameObject indicator;
            if (hollow)
            {
                int segments =64;
                GameObject go = new GameObject(string.IsNullOrEmpty(tag) ? "HollowCircleIndicator" : $"HollowCircle_{tag}");
                go.transform.position = worldPos + Vector3.up *0.01f;
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.loop = true;
                lr.positionCount = segments;
                lr.widthMultiplier =0.05f;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                if (indicatorMaterial != null)
                {
                    lr.material = new Material(indicatorMaterial);
                }
                else
                {
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                }
                Color fallback = isValid ? validColor : invalidColor;
                Color col = ResolveColor(colorKey, fallback);
                lr.startColor = col;
                lr.endColor = col;
                for (int i =0; i < segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI *2f;
                    Vector3 p = new Vector3(Mathf.Cos(angle) * radius,0f, Mathf.Sin(angle) * radius);
                    lr.SetPosition(i, worldPos + p + Vector3.up *0.01f);
                }
                indicator = go;
            }
            else
            {
                if (circleIndicatorPrefab != null)
                {
                    indicator = Instantiate(circleIndicatorPrefab, worldPos, Quaternion.identity);
                    indicator.transform.localScale = new Vector3(radius *2f, 0.1f, radius *2f);
                }
                else
                {
                    indicator = CreateCircleMesh(worldPos, radius);
                }

                if (indicator != null)
                {
                    indicator.name = string.IsNullOrEmpty(tag) ? "CircleIndicator" : $"Circle_{tag}";
                }
            }

            if (indicator == null)
            {
                Debug.LogWarning("BattleIndicatorManager: CreateCircleIndicator failed.");
                return null;
            }

            indicator.transform.SetParent(this.transform, true);
            indicator.transform.position = worldPos + Vector3.up *0.02f;

            // 对非空心情况应用颜色
            if (!hollow)
            {
                Color fallback = isValid ? validColor : invalidColor;
                Color col = ResolveColor(colorKey, fallback);
                ApplyColorToIndicator(indicator, col);
            }

            TrackIndicator(indicator, tag);

            if (debugShowIndicatorMarker) CreateDebugMarker(worldPos);
            return indicator;
        }

        /// <summary>
        /// 创建目标标记
        /// </summary>
        /// <param name="clearPrevious">是否清除之前的目标标记（默认true，保持单选行为）</param>
        /// <param name="colorKey">颜色表键名（可选）。若提供，将覆盖默认 targetColor</param>
        public GameObject CreateTargetMarker(Transform target, bool clearPrevious = true, string colorKey = null)
        {
            if (clearPrevious)
            {
                ClearIndicatorsByTag(Tags.TargetMarker);
            }

            GameObject marker;
            if (targetMarkerPrefab != null)
            {
                marker = Instantiate(targetMarkerPrefab, target.position + Vector3.up *0.1f, Quaternion.identity);
            }
            else
            {
                marker = CreateTargetMarkerMesh(target.position + Vector3.up *0.1f);
            }

            marker.transform.SetParent(target);
            marker.name = "TargetMarker";

            // 应用颜色
            var tCol = ResolveColor(colorKey, targetColor);
            ApplyColorToIndicator(marker, tCol);

            TrackIndicator(marker, Tags.TargetMarker);

            return marker;
        }

        // ===== 向后兼容的简化方法 =====

        public GameObject CreateSectorIndicator(Transform center, float radius, float angle)
        {
            return CreateSectorIndicator(center, radius, angle, Tags.None, false, null);
        }

        public GameObject CreateCircleIndicator(Vector3 worldPos, float radius, bool isValid = true, bool hollow = false)
        {
            return CreateCircleIndicator(worldPos, radius, isValid, hollow, Tags.None, false, null);
        }

        public GameObject CreateTargetMarker(Transform target)
        {
            return CreateTargetMarker(target, true, null);
        }

        // ===== 更新方法 =====

        public void UpdateSectorRotation(GameObject indicator, Transform center, Vector3 forward)
        {
            if (indicator == null) return;
            indicator.transform.position = center.position + Vector3.up *0.01f;
            indicator.transform.rotation = Quaternion.LookRotation(forward);
        }

        public void UpdateSectorIndicator(GameObject indicator, Vector3 center, float radius, float angle)
        {
            if (indicator == null) return;

            indicator.transform.position = center + Vector3.up *0.01f;
            indicator.transform.localScale = new Vector3(radius *2f, 1f, radius *2f);

            var mf = indicator.GetComponent<MeshFilter>();
            if (mf != null)
            {
                Mesh mesh = new Mesh();
                int segments =32;
                float angleRad = angle * Mathf.Deg2Rad;
                float halfAngle = angleRad /2f;

                Vector3[] vertices = new Vector3[segments +2];
                int[] triangles = new int[segments *3];
                vertices[0] = Vector3.zero;

                for (int i =0; i <= segments; i++)
                {
                    float currentAngle = -halfAngle + (angleRad * i / segments);
                    vertices[i +1] = new Vector3(Mathf.Sin(currentAngle) * radius,0f, Mathf.Cos(currentAngle) * radius);

                    if (i < segments)
                    {
                        triangles[i *3] =0;
                        triangles[i *3 +1] = i +1;
                        triangles[i *3 +2] = i +2;
                    }
                }

                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mf.mesh = mesh;
            }
        }

        public void UpdateCircleIndicator(GameObject indicator, Vector3 worldPos, float radius, bool isValid = true)
        {
            if (indicator == null) return;

            var lr = indicator.GetComponent<LineRenderer>();
            if (lr != null)
            {
                indicator.transform.position = worldPos + Vector3.up *0.01f;
                int segments = lr.positionCount;
                for (int i =0; i < segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI *2f;
                    Vector3 p = new Vector3(Mathf.Cos(angle) * radius,0f, Mathf.Sin(angle) * radius);
                    lr.SetPosition(i, worldPos + p + Vector3.up *0.01f);
                }
                Color col = isValid ? validColor : invalidColor;
                lr.startColor = col;
                lr.endColor = col;
            }
            else
            {
                indicator.transform.position = worldPos + Vector3.up *0.02f;
                indicator.transform.localScale = new Vector3(radius *2f, 0.01f, radius *2f);

                var rend = indicator.GetComponent<Renderer>();
                if (rend != null && rend.material != null)
                {
                    rend.material.color = isValid ? validColor : invalidColor;
                }
            }
        }

        public void UpdateTargetMarker(GameObject marker, Vector3 position)
        {
            if (marker == null) return;
            marker.transform.position = position + Vector3.up *0.1f;
        }

        public void UpdateTargetMarkerColor(GameObject marker, Color color)
        {
            if (marker == null) return;

            var lineRenderers = marker.GetComponentsInChildren<LineRenderer>();
            foreach (var lr in lineRenderers)
            {
                lr.startColor = color;
                lr.endColor = color;
            }
        }

        // ===== 删除方法 =====

        public void DeleteSectorIndicator(GameObject indicator)
        {
            UntrackAndDestroy(indicator);
        }

        public void DeleteCircleIndicator(GameObject indicator)
        {
            UntrackAndDestroy(indicator);
        }

        public void DeleteTargetMarker(GameObject marker)
        {
            UntrackAndDestroy(marker);
        }

        /// <summary>
        /// 按标签清除指示器
        /// </summary>
        public void ClearIndicatorsByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return;

            if (taggedIndicators.TryGetValue(tag, out List<GameObject> indicators))
            {
                //复制列表避免在迭代时修改
                var indicatorsCopy = new List<GameObject>(indicators);
                foreach (var indicator in indicatorsCopy)
                {
                    if (indicator != null)
                    {
                        Destroy(indicator);
                    }
                }
                indicators.Clear();
            }
        }

        public void DeleteAllTargetMarkers()
        {
            ClearIndicatorsByTag(Tags.TargetMarker);
        }

        // ===== 向后兼容方法 =====

        public GameObject ShowSectorIndicator(Transform center, float radius, float angle)
        {
            return CreateSectorIndicator(center, radius, angle);
        }

        public GameObject ShowCircleIndicator(Vector3 worldPos, float radius, bool isValid = true, bool hollow = false)
        {
            return CreateCircleIndicator(worldPos, radius, isValid, hollow);
        }

        public void ShowTargetMarker(Transform target)
        {
            CreateTargetMarker(target);
        }

        public void DestroyIndicator(GameObject indicator)
        {
            UntrackAndDestroy(indicator);
        }

        public void ClearIndicators()
        {
            foreach (var ind in allIndicators)
            {
                if (ind != null) Destroy(ind);
            }
            allIndicators.Clear();
            taggedIndicators.Clear();

            foreach (var dm in debugMarkers)
            {
                if (dm != null) Destroy(dm);
            }
            debugMarkers.Clear();
        }

        public void ClearAuxIndicators()
        {
            // 保留以向后兼容
        }

        public void ClearTargetMarkers()
        {
            DeleteAllTargetMarkers();
        }

        public void ClearAll()
        {
            ClearIndicators();
        }

        // ===== 内部跟踪方法 =====

        private void TrackIndicator(GameObject indicator, string tag)
        {
            if (indicator == null) return;

            allIndicators.Add(indicator);

            if (!string.IsNullOrEmpty(tag))
            {
                if (!taggedIndicators.ContainsKey(tag))
                {
                    taggedIndicators[tag] = new List<GameObject>();
                }
                taggedIndicators[tag].Add(indicator);
            }
        }

        private void UntrackAndDestroy(GameObject indicator)
        {
            if (indicator == null) return;

            // 从所有列表中移除
            allIndicators.Remove(indicator);

            foreach (var kvp in taggedIndicators)
            {
                kvp.Value.Remove(indicator);
            }

            Destroy(indicator);
        }

        // ===== 内部网格生成方法 =====

        private GameObject CreateSectorMesh(Vector3 center, float radius, float angle)
        {
            GameObject go = new GameObject("SectorIndicator");
            go.transform.position = center;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();

            Mesh mesh = new Mesh();
            int segments =32;
            float angleRad = angle * Mathf.Deg2Rad;
            float halfAngle = angleRad /2f;

            Vector3[] vertices = new Vector3[segments +2];
            int[] triangles = new int[segments *3];
            vertices[0] = Vector3.zero;

            for (int i =0; i <= segments; i++)
            {
                float currentAngle = -halfAngle + (angleRad * i / segments);
                vertices[i +1] = new Vector3(Mathf.Sin(currentAngle) * radius,0f, Mathf.Cos(currentAngle) * radius);

                if (i < segments)
                {
                    triangles[i *3] =0;
                    triangles[i *3 +1] = i +1;
                    triangles[i *3 +2] = i +2;
                }
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.mesh = mesh;

            if (indicatorMaterial != null)
            {
                mr.material = new Material(indicatorMaterial);
            }
            else
            {
                Shader s = Shader.Find("Unlit/Color");
                if (s != null) mr.material = new Material(s);
                else mr.material = new Material(Shader.Find("Sprites/Default"));
            }
            mr.material.color = validColor;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.enabled = true;

            return go;
        }

        private GameObject CreateCircleMesh(Vector3 center, float radius)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "CircleIndicator";
            go.transform.position = center;

            float height =0.01f;
            go.transform.localScale = new Vector3(radius *2f, height, radius *2f);

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                if (indicatorMaterial != null)
                {
                    mr.material = new Material(indicatorMaterial);
                }
                else
                {
                    Shader s = Shader.Find("Unlit/Color");
                    if (s != null) mr.material = new Material(s);
                    else mr.material = new Material(Shader.Find("Sprites/Default"));
                }

                Color c = validColor;
                mr.material.color = c;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.material.renderQueue =3000;
                if (mr.material.HasProperty("_ZWrite")) mr.material.SetInt("_ZWrite",0);
                mr.enabled = true;
            }

            return go;
        }

        private GameObject CreateTargetMarkerMesh(Vector3 position)
        {
            GameObject go = new GameObject("TargetMarker");
            go.transform.position = position;

            for (int ring =0; ring <2; ring++)
            {
                GameObject ringObj = new GameObject($"Ring{ring}");
                ringObj.transform.SetParent(go.transform);
                ringObj.transform.localPosition = Vector3.zero;

                LineRenderer lr = ringObj.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.loop = true;
                lr.widthMultiplier =0.05f + ring *0.02f;
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = targetColor;
                lr.endColor = targetColor;
                lr.positionCount =32;

                float radius =0.5f + ring *0.1f;
                for (int i =0; i <32; i++)
                {
                    float angle = (float)i /32f * Mathf.PI *2f;
                    lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius,0f, Mathf.Sin(angle) * radius));
                }
            }

            return go;
        }

        private void CreateDebugMarker(Vector3 pos)
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "DEBUG_IndicatorMarker";
            s.transform.position = pos + Vector3.up *0.2f;
            s.transform.localScale = Vector3.one *0.2f;
            var rend = s.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                rend.material = new Material(Shader.Find("Unlit/Color"));
                rend.material.color = Color.magenta;
            }
            var col = s.GetComponent<Collider>();
            if (col != null) Destroy(col);
            s.transform.SetParent(this.transform, true);
            debugMarkers.Add(s);
        }

        void OnDestroy()
        {
            ClearAll();
        }
    }
}
