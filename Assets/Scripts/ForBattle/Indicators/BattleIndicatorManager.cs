using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.ForBattle.Indicators
{
    /// <summary>
    /// 战斗范围指示管理：创建/更新/清理各种形状的范围显示
    /// </summary>
    public class BattleIndicatorManager : MonoBehaviour
    {
        [Header("Indicator Prefabs (Optional)")]
        public GameObject sectorIndicatorPrefab;
        public GameObject circleIndicatorPrefab;
        public GameObject targetMarkerPrefab;
        // 可选的长方形预制件（若为空则使用 Cube生成）
        public GameObject rectangleIndicatorPrefab;

        [Header("Indicator Materials")]
        public Material indicatorMaterial;
        // 默认颜色
        public Color validColor = new Color(0f,0.5f,1f,0.3f);
        public Color invalidColor = new Color(1f,0f,0f,0.3f);
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

        // 标签常量
        public static class Tags
        {
            public const string MovementRange = "MovementRange";
            public const string AttackRange = "AttackRange";
            public const string SkillRange = "SkillRange";
            public const string SkillPreview = "SkillPreview";
            public const string AreaSelection = "AreaSelection";
            public const string DirectionSelection = "DirectionSelection";
            public const string TargetMarker = "TargetMarker";
            public const string Chant = "Chant"; // persistent magic chant indicator
            public const string Barrier = "Barrier"; //结界指示器标签
            public const string ChainCircle = "ChainCircle"; // 连携蓝圈
            public const string None = "";
        }

        private Dictionary<string, List<GameObject>> taggedIndicators = new Dictionary<string, List<GameObject>>();
        private List<GameObject> allIndicators = new List<GameObject>();
        private List<GameObject> debugMarkers = new List<GameObject>();
        private Dictionary<string, Color> colorMap = new Dictionary<string, Color>();

        private Dictionary<GameObject, float> barrierOffsets = new Dictionary<GameObject, float>();
        private float barrierBaseYOffset =0.0015f; // 初始贴地高度
        private float barrierStepYOffset =0.0004f; // 每个后生成结界增加的高度

        private void Awake()
        {
            RebuildColorMap();
        }

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
            // 内置关键字：blue => 使用 fallback 的 alpha
            if (!string.IsNullOrEmpty(colorKey) && colorKey.ToLowerInvariant() == "blue")
            {
                var baseBlue = new Color(0.2f,0.6f,1f, fallback.a);
                return baseBlue;
            }
            return fallback;
        }

        private void ApplyColorToIndicator(GameObject indicator, Color color)
        {
            if (indicator == null) return;

            var lineRenderers = indicator.GetComponentsInChildren<LineRenderer>(true);
            if (lineRenderers != null && lineRenderers.Length > 0)
            {
                foreach (var lr in lineRenderers)
                {
                    lr.startColor = color;
                    lr.endColor = color;
                    EnsureMaterialSupportsAlpha(lr.material, color);
                }
            }

            var renderers = indicator.GetComponentsInChildren<Renderer>(true);
            if (renderers != null && renderers.Length > 0)
            {
                foreach (var rend in renderers)
                {
                    if (rend.material != null)
                    {
                        rend.material.color = color;
                        EnsureMaterialSupportsAlpha(rend.material, color);
                    }
                }
            }
        }

        /// <summary>
        /// 若颜色是半透明（alpha<0.99）则确保材质支持透明渲染（切换到 Sprite/Default 或配置混合）。
        /// </summary>
        private void EnsureMaterialSupportsAlpha(Material mat, Color col)
        {
            if (mat == null) return;
            if (col.a >= 0.99f) return; // 近乎不透明，无需特殊处理

            // 若当前 Shader 不支持透明，则切换到 Sprites/Default
            var name = mat.shader != null ? mat.shader.name : string.Empty;
            if (!(name.Contains("Sprite") || name.Contains("Unlit")))
            {
                var spriteShader = Shader.Find("Sprites/Default");
                if (spriteShader != null)
                {
                    mat.shader = spriteShader;
                }
            }

            // 标准透明混合设置
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            // 设定一个较高的队列值以确保在地面之上（如与其他透明对象排序可再调整）
            mat.renderQueue = 3000;
        }

        // 新增：公开的重着色接口，允许外部（如结界）动态修改已有指示器的颜色
        public void RecolorIndicator(GameObject indicator, Color color)
        {
            ApplyColorToIndicator(indicator, color);
        }

        //让结界层级最低，目标>技能>普通
        private void ApplyLayering(GameObject indicator, string tag)
        {
            if (indicator == null) return;
            int baseQueueBarrier =2900;
            int baseQueueNormal =3000;
            int baseQueueHighlight =3100;

            int chosenQueue = baseQueueNormal;
            int chosenSorting =0;
            if (tag == Tags.Barrier)
            {
                chosenQueue = baseQueueBarrier;
                chosenSorting =0;
            }
            else if (tag == Tags.TargetMarker)
            {
                chosenQueue = baseQueueHighlight;
                chosenSorting =20;
            }
            else if (tag == Tags.AttackRange || tag == Tags.SkillRange || tag == Tags.SkillPreview)
            {
                chosenQueue = baseQueueNormal +50;
                chosenSorting =10;
            }
            else if (tag == Tags.ChainCircle)
            {
                chosenQueue = baseQueueNormal +55; // 略高于普通范围
                chosenSorting =11;
            }
            else
            {
                chosenQueue = baseQueueNormal;
                chosenSorting =5;
            }

            var renderers = indicator.GetComponentsInChildren<Renderer>(true);
            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    if (r.material != null)
                    {
                        r.material.renderQueue = chosenQueue;
                        if (r.material.HasProperty("_ZWrite")) r.material.SetInt("_ZWrite",0); //透明不写入深度，避免遮挡
                    }
                }
            }
            var lineRenderers = indicator.GetComponentsInChildren<LineRenderer>(true);
            if (lineRenderers != null)
            {
                foreach (var lr in lineRenderers)
                {
                    lr.sortingOrder = chosenSorting;
                }
            }
        }

        // ===== 新增：长方形指示器（面向 forward 的向前长方形）=====
        /// <summary>
        /// 在 origin.transform.position 前方生成一个长方形指示器，朝向由 forward 指定
        /// indicator 的中心将位于 origin.position + forward.normalized * (length/2)
        /// </summary>
        public GameObject CreateRectangleIndicator(Transform origin, float length, float width, Vector3 forward, string tag = "", bool clearSameTag = false, string colorKey = null)
        {
            if (clearSameTag && !string.IsNullOrEmpty(tag))
            {
                ClearIndicatorsByTag(tag);
            }

            if (origin == null) return null;

            GameObject indicator = null;
            if (rectangleIndicatorPrefab != null)
            {
                indicator = Instantiate(rectangleIndicatorPrefab, origin.position, Quaternion.identity);
                indicator.transform.localScale = new Vector3(width,0.01f, length);
            }
            else
            {
                // 使用 Cube生成一个平面长方形
                indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
                indicator.name = string.IsNullOrEmpty(tag) ? "RectangleIndicator" : $"RectangleIndicator_{tag}";
                // 去掉碰撞体
                var col = indicator.GetComponent<Collider>();
                if (col != null) Destroy(col);
                indicator.transform.localScale = new Vector3(width,0.01f, length);
            }

            if (indicator != null)
            {
                Vector3 fwd = forward;
                fwd.y =0f;
                if (fwd.sqrMagnitude <0.001f) fwd = origin.forward;
                fwd.Normalize();

                float yOff = GetYOffsetForTag(tag, hollow: true);
                indicator.transform.position = origin.position + fwd * (length *0.5f) + Vector3.up * yOff;
                indicator.transform.rotation = Quaternion.LookRotation(fwd);
                indicator.transform.SetParent(this.transform, true);

                var col = ResolveColor(colorKey, validColor);
                ApplyColorToIndicator(indicator, col);

                ApplyLayering(indicator, tag);
                TrackIndicator(indicator, tag);
            }

            if (debugShowIndicatorMarker) CreateDebugMarker(origin.position);
            return indicator;
        }

        /// <summary>
        /// 更新已有的长方形指示器的位置/朝向/尺寸
        /// </summary>
        public void UpdateRectangleIndicator(GameObject indicator, Transform origin, float length, float width, Vector3 forward, string colorKey = null)
        {
            if (indicator == null || origin == null) return;
            Vector3 fwd = forward; fwd.y =0f; if (fwd.sqrMagnitude <0.001f) fwd = origin.forward; fwd.Normalize();
            indicator.transform.position = origin.position + fwd * (length *0.5f) + Vector3.up *0.01f;
            indicator.transform.rotation = Quaternion.LookRotation(fwd);
            indicator.transform.localScale = new Vector3(width,0.01f, length);

            if (!string.IsNullOrEmpty(colorKey))
            {
                var col = ResolveColor(colorKey, validColor);
                ApplyColorToIndicator(indicator, col);
            }
        }

        // =====其它已有指示器方法（不变）=====

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
                indicator.transform.localScale = new Vector3(radius *2f,1f, radius *2f);
            }
            else
            {
                indicator = CreateSectorMesh(center.position, radius, angle);
            }

            if (indicator != null)
            {
                float yOff = GetYOffsetForTag(tag, hollow: false);
                indicator.transform.position = center.position + Vector3.up * yOff;
                indicator.transform.SetParent(this.transform, true);
                indicator.name = string.IsNullOrEmpty(tag) ? "SectorIndicator" : $"SectorIndicator_{tag}";

                var col = ResolveColor(colorKey, validColor);
                ApplyColorToIndicator(indicator, col);
                ApplyLayering(indicator, tag);
                TrackIndicator(indicator, tag);
            }

            if (debugShowIndicatorMarker) CreateDebugMarker(center.position);
            return indicator;
        }

        public GameObject CreateCircleIndicator(Vector3 worldPos, float radius, bool isValid = true, bool hollow = false, string tag = "", bool clearSameTag = false, string colorKey = null)
        {
            if (clearSameTag && !string.IsNullOrEmpty(tag))
            {
                ClearIndicatorsByTag(tag);
            }

            float yOff = GetYOffsetForTag(tag, hollow);
            float lineWidth = GetLineWidthForTag(tag);
            float solidHeight = GetSolidHeightForTag(tag);

            GameObject indicator;
            if (hollow)
            {
                int segments =64;
                GameObject go = new GameObject(string.IsNullOrEmpty(tag) ? "HollowCircleIndicator" : $"HollowCircle_{tag}");
                go.transform.position = worldPos + Vector3.up * yOff;
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.loop = true;
                lr.positionCount = segments;
                lr.widthMultiplier = lineWidth;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.material = indicatorMaterial != null ? new Material(indicatorMaterial) : new Material(Shader.Find("Sprites/Default"));
                Color fallback = isValid ? validColor : invalidColor;
                Color col = ResolveColor(colorKey, fallback);
                lr.startColor = col;
                lr.endColor = col;
                for (int i =0; i < segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI *2f;
                    Vector3 p = new Vector3(Mathf.Cos(angle) * radius,0f, Mathf.Sin(angle) * radius);
                    lr.SetPosition(i, worldPos + p + Vector3.up * yOff);
                }
                indicator = go;
            }
            else
            {
                if (circleIndicatorPrefab != null)
                {
                    indicator = Instantiate(circleIndicatorPrefab, worldPos, Quaternion.identity);
                    indicator.transform.localScale = new Vector3(radius *2f, solidHeight, radius *2f);
                }
                else
                {
                    indicator = CreateCircleMesh(worldPos, radius);
                    // 调整高度更薄
                    var mr = indicator.GetComponent<MeshRenderer>();
                    if (mr != null && mr.material != null)
                    {
                        if (mr.material.HasProperty("_ZWrite")) mr.material.SetInt("_ZWrite",0);
                    }
                    indicator.transform.localScale = new Vector3(radius *2f, solidHeight, radius *2f);
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
            // 动态结界高度覆盖静态 yOff
            if (tag == Tags.Barrier)
            {
                yOff = RegisterBarrier(indicator);
            }
            indicator.transform.position = worldPos + Vector3.up * yOff;

            // 对非空心情况应用颜色
            if (!hollow)
            {
                Color fallback = isValid ? validColor : invalidColor;
                Color col = ResolveColor(colorKey, fallback);
                ApplyColorToIndicator(indicator, col);
            }

            ApplyLayering(indicator, tag);
            TrackIndicator(indicator, tag);

            if (debugShowIndicatorMarker) CreateDebugMarker(worldPos);
            return indicator;
        }

        public void UpdateSectorRotation(GameObject indicator, Transform center, Vector3 forward)
        {
            if (indicator == null) return;
            string tag = GetTagForIndicator(indicator);
            float yOff = GetYOffsetForTag(tag, hollow: false);
            indicator.transform.position = center.position + Vector3.up * yOff;
            indicator.transform.rotation = Quaternion.LookRotation(forward);
        }

        public void UpdateSectorIndicator(GameObject indicator, Vector3 center, float radius, float angle)
        {
            if (indicator == null) return;

            string tag = GetTagForIndicator(indicator);
            float yOff = GetYOffsetForTag(tag, hollow: false);
            indicator.transform.position = center + Vector3.up * yOff;
            indicator.transform.localScale = new Vector3(radius *2f,1f, radius *2f);

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

            string tag = GetTagForIndicator(indicator);
            var lr = indicator.GetComponent<LineRenderer>();
            if (lr != null)
            {
                float yOff = GetYOffsetForTag(tag, hollow: true);
                indicator.transform.position = worldPos + Vector3.up * yOff;
                int segments = lr.positionCount;
                for (int i =0; i < segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI *2f;
                    Vector3 p = new Vector3(Mathf.Cos(angle) * radius,0f, Mathf.Sin(angle) * radius);
                    lr.SetPosition(i, worldPos + p + Vector3.up * yOff);
                }
                Color col = isValid ? validColor : invalidColor;
                lr.startColor = col;
                lr.endColor = col;
            }
            else
            {
                float yOff = GetYOffsetForTag(tag, hollow: false);
                indicator.transform.position = worldPos + Vector3.up * yOff;
                indicator.transform.localScale = new Vector3(radius *2f, GetSolidHeightForTag(tag), radius *2f);

                var rend = indicator.GetComponent<Renderer>();
                if (rend != null && rend.material != null)
                {
                    rend.material.color = isValid ? validColor : invalidColor;
                }
            }
        }

        public void UpdateCircleIndicatorKeepColor(GameObject indicator, Vector3 worldPos, float radius)
        {
            if (indicator == null) return;
            string tag = GetTagForIndicator(indicator);
            var lr = indicator.GetComponent<LineRenderer>();
            if (lr != null)
            {
                float yOff = tag == Tags.Barrier ? GetBarrierYOffset(indicator) : GetYOffsetForTag(tag, hollow: true);
                indicator.transform.position = worldPos + Vector3.up * yOff;
                int segments = lr.positionCount;
                if (segments <=0) segments =64;
                if (lr.positionCount != segments) lr.positionCount = segments;
                for (int i =0; i < segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI *2f;
                    Vector3 p = new Vector3(Mathf.Cos(angle) * radius,0f, Mathf.Sin(angle) * radius);
                    lr.SetPosition(i, worldPos + p + Vector3.up * yOff);
                }
            }
            else
            {
                float yOff = tag == Tags.Barrier ? GetBarrierYOffset(indicator) : GetYOffsetForTag(tag, hollow: false);
                indicator.transform.position = worldPos + Vector3.up * yOff;
                indicator.transform.localScale = new Vector3(radius *2f, GetSolidHeightForTag(tag), radius *2f);
            }
        }

        // 自定义 y 偏移与线宽/厚度，避免遮挡（用于 Barrier）
        public void UpdateCircleIndicatorCustom(GameObject indicator, Vector3 worldPos, float radius, float yOffset, bool keepColor = true, float? solidHeight = null, float? lineWidth = null)
        {
            if (indicator == null) return;
            var lr = indicator.GetComponent<LineRenderer>();
            if (lr != null)
            {
                indicator.transform.position = worldPos + Vector3.up * yOffset;
                int segments = lr.positionCount;
                if (segments <=0) segments =64;
                if (lr.positionCount != segments) lr.positionCount = segments;
                if (lineWidth.HasValue) lr.widthMultiplier = lineWidth.Value;
                for (int i =0; i < segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI *2f;
                    Vector3 p = new Vector3(Mathf.Cos(angle) * radius,0f, Mathf.Sin(angle) * radius);
                    lr.SetPosition(i, worldPos + p + Vector3.up * yOffset);
                }
            }
            else
            {
                indicator.transform.position = worldPos + Vector3.up * yOffset;
                var scale = indicator.transform.localScale;
                indicator.transform.localScale = new Vector3(radius *2f, solidHeight ??0.003f, radius *2f);
            }
        }

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
            ApplyLayering(marker, Tags.TargetMarker);

            return marker;
        }

        // convenience overloads
        public GameObject CreateSectorIndicator(Transform center, float radius, float angle)
        {
            return CreateSectorIndicator(center, radius, angle, Tags.None, false, null);
        }

        public GameObject CreateCircleIndicator(Vector3 worldPos, float radius, bool isValid = true, bool hollow = false)
        {
            return CreateCircleIndicator(worldPos, radius, isValid, hollow, Tags.None, false, null);
        }

        // 新增：长方形便捷重载（无 tag）
        public GameObject CreateRectangleIndicator(Transform origin, float length, float width, Vector3 forward)
        {
            return CreateRectangleIndicator(origin, length, width, forward, Tags.None, false, null);
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

        public void ClearIndicatorsByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return;

            if (taggedIndicators.TryGetValue(tag, out List<GameObject> indicators))
            {
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
            // no-op for now
        }

        public void ClearTargetMarkers()
        {
            DeleteAllTargetMarkers();
        }

        public void ClearAll()
        {
            ClearIndicators();
        }

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

            allIndicators.Remove(indicator);

            foreach (var kvp in taggedIndicators)
            {
                kvp.Value.Remove(indicator);
            }

            if (barrierOffsets.ContainsKey(indicator))
            {
                barrierOffsets.Remove(indicator);
            }

            Destroy(indicator);
        }

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

        // 清理临时（非持久）指示器
        public void ClearEphemeralIndicators()
        {
            ClearIndicatorsByTag(Tags.MovementRange);
            ClearIndicatorsByTag(Tags.AttackRange);
            ClearIndicatorsByTag(Tags.SkillRange);
            ClearIndicatorsByTag(Tags.SkillPreview);
            ClearIndicatorsByTag(Tags.DirectionSelection);
            ClearIndicatorsByTag(Tags.TargetMarker);
            ClearIndicatorsByTag(Tags.AreaSelection);
            ClearIndicatorsByTag(Tags.ChainCircle); // 清理连携圈
            // 不清理 Tags.Barrier
        }

        //统一的高度偏移与厚度规则，确保层级：Barrier < Movement < Attack < SkillRange < SkillPreview < Target
        private float GetYOffsetForTag(string tag, bool hollow)
        {
            switch (tag)
            {
                case Tags.Barrier: return 0.0015f; //结界最贴地
                case Tags.MovementRange: return hollow ?0.012f :0.014f;
                case Tags.AttackRange: return hollow ?0.016f :0.018f;
                case Tags.ChainCircle: return (hollow ?0.0175f :0.0195f) +1.0f; // 连携蓝圈浮空+1m
                case Tags.SkillRange: return hollow ?0.020f :0.022f;
                case Tags.SkillPreview: return hollow ?0.024f :0.026f;
                case Tags.TargetMarker: return 0.10f;
                default: return hollow ?0.012f :0.014f;
            }
        }
        private float GetLineWidthForTag(string tag)
        {
            switch (tag)
            {
                case Tags.Barrier: return 0.01f; //结界更细
                case Tags.MovementRange: return 0.035f;
                case Tags.AttackRange: return 0.04f;
                case Tags.ChainCircle: return 0.045f; // 稍显眼
                case Tags.SkillRange: return 0.05f;
                case Tags.SkillPreview: return 0.05f;
                default: return 0.04f;
            }
        }
        private float GetSolidHeightForTag(string tag)
        {
            switch (tag)
            {
                case Tags.Barrier: return 0.0015f;
                case Tags.MovementRange: return 0.006f;
                case Tags.AttackRange: return 0.007f;
                case Tags.ChainCircle: return 0.0075f;
                case Tags.SkillRange: return 0.008f;
                case Tags.SkillPreview: return 0.008f;
                default: return 0.006f;
            }
        }
        private string GetTagForIndicator(GameObject indicator)
        {
            foreach (var kv in taggedIndicators)
            {
                var list = kv.Value;
                if (list != null && list.Contains(indicator)) return kv.Key;
            }
            return Tags.None;
        }

        /// <summary>
        /// Change the tag associated with an existing indicator GameObject.
        /// This moves the indicator from its previous tracking bucket to the new one
        /// and reapplies layering rules for the new tag.
        /// </summary>
        public void ChangeIndicatorTag(GameObject indicator, string newTag)
        {
            if (indicator == null) return;
            string oldTag = GetTagForIndicator(indicator);
            if (oldTag == newTag) return;

            // remove from old tag list
            if (!string.IsNullOrEmpty(oldTag) && taggedIndicators.TryGetValue(oldTag, out var oldList))
            {
                oldList.Remove(indicator);
            }

            // add to new tag list
            if (string.IsNullOrEmpty(newTag)) newTag = Tags.None;
            if (!taggedIndicators.TryGetValue(newTag, out var newList))
            {
                newList = new List<GameObject>();
                taggedIndicators[newTag] = newList;
            }
            if (!newList.Contains(indicator)) newList.Add(indicator);

            // reapply layering for the indicator under the new tag
            ApplyLayering(indicator, newTag);
        }
        private float RegisterBarrier(GameObject indicator)
        {
            if (indicator == null) return barrierBaseYOffset;
            float y = barrierBaseYOffset + barrierOffsets.Count * barrierStepYOffset;
            barrierOffsets[indicator] = y;
            return y;
        }
        private float GetBarrierYOffset(GameObject indicator)
        {
            if (indicator != null && barrierOffsets.TryGetValue(indicator, out var y)) return y;
            return barrierBaseYOffset;
        }

        // ——省略：其余 Create/Update/Delete 接口保持不变 ——

        //便捷 API：连携蓝圈
        public GameObject CreateChainCircle(Vector3 worldPos, float radius, bool hollow = true, bool clearSameTag = false)
        {
            return CreateCircleIndicator(worldPos, radius, true, hollow, Tags.ChainCircle, clearSameTag, "blue");
        }
        public GameObject ShowChainCircle(Vector3 worldPos, float radius)
        {
            return CreateChainCircle(worldPos, radius, true, true);
        }
        public void DeleteChainCircle(GameObject indicator)
        {
            DeleteCircleIndicator(indicator);
        }
    }
}
