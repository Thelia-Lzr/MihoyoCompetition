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
        public Color validColor = new Color(0f, 1f, 0f, 0.3f);
        public Color invalidColor = new Color(1f, 0f, 0f, 0.3f);
        public Color targetColor = Color.red;

        [Header("Debug")]
        public bool debugShowIndicatorMarker = false;

        private GameObject currentIndicator;
        private List<GameObject> currentTargetMarkers = new List<GameObject>();
        private List<GameObject> debugMarkers = new List<GameObject>();

        // track which kind of indicator is currently shown
        private enum IndicatorKind { None, Circle, Sector }
        private IndicatorKind currentKind = IndicatorKind.None;

        /// <summary>
        /// 显示扇形指示器（以角色为中心，朝向镜头方向）
        /// </summary>
        public void ShowSectorIndicator(Transform center, float radius, float angle)
        {
            // If we already have a sector indicator, just update it
            if (currentIndicator != null && currentKind == IndicatorKind.Sector)
            {
                currentIndicator.transform.position = center.position + Vector3.up *0.01f;
                currentIndicator.transform.localScale = new Vector3(radius *2f,1f, radius *2f);
                currentIndicator.transform.SetParent(this.transform, true);
                // material color update
                var rend = currentIndicator.GetComponent<Renderer>();
                if (rend != null)
                {
                    if (rend.material != null) rend.material.color = validColor;
                    rend.enabled = true;
                }
                if (debugShowIndicatorMarker) CreateDebugMarker(center.position);
                return;
            }

            ClearIndicators();

            if (sectorIndicatorPrefab != null)
            {
                currentIndicator = Instantiate(sectorIndicatorPrefab, center.position, Quaternion.identity);
                // 设置大小
                currentIndicator.transform.localScale = new Vector3(radius *2f,1f, radius *2f);
            }
            else
            {
                //运行时创建扇形网格
                currentIndicator = CreateSectorMesh(center.position, radius, angle);
            }

            currentKind = IndicatorKind.Sector;

            // Slightly raise to avoid z-fighting with ground
            if (currentIndicator != null)
            {
                currentIndicator.transform.position += Vector3.up *0.01f;
                currentIndicator.transform.SetParent(this.transform, true);
            }

            if (debugShowIndicatorMarker)
            {
                CreateDebugMarker(center.position);
            }
        }

        /// <summary>
        /// 显示圆形指示器（以指定世界坐标为中心）
        /// 优化：如果当前已经显示圆形指示器则更新其位置/大小/颜色，避免每帧销毁重建导致闪烁。
        /// </summary>
        public void ShowCircleIndicator(Vector3 worldPos, float radius, bool isValid = true)
        {
            // If we already have a circle indicator, update it instead of recreating
            if (currentIndicator != null && currentKind == IndicatorKind.Circle)
            {
                // update transform and material
                currentIndicator.transform.position = worldPos + Vector3.up *0.01f;
                float height =0.02f;
                currentIndicator.transform.localScale = new Vector3(radius *2f, height, radius *2f);
                var rendUpd = currentIndicator.GetComponent<Renderer>();
                if (rendUpd != null)
                {
                    if (rendUpd.material == null && indicatorMaterial != null) rendUpd.material = new Material(indicatorMaterial);
                    if (rendUpd.material != null) rendUpd.material.color = isValid ? validColor : invalidColor;
                    rendUpd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rendUpd.receiveShadows = false;
                    rendUpd.enabled = true;
                }
                if (debugShowIndicatorMarker) CreateDebugMarker(worldPos);
                return;
            }

            // otherwise create new indicator
            ClearIndicators();

            if (circleIndicatorPrefab != null)
            {
                currentIndicator = Instantiate(circleIndicatorPrefab, worldPos, Quaternion.identity);
                currentIndicator.transform.localScale = new Vector3(radius *2f,0.1f, radius *2f);
                currentIndicator.transform.localScale = new Vector3(radius *2f,1f, radius *2f);
            }
            else
            {
                currentIndicator = CreateCircleMesh(worldPos, radius);
            }

            if (currentIndicator == null)
            {
                Debug.LogWarning("BattleIndicatorManager: ShowCircleIndicator failed to create indicator.");
                return;
            }

            currentKind = IndicatorKind.Circle;

            // parent under manager for hierarchy clarity
            currentIndicator.transform.SetParent(this.transform, true);

            // 设置颜色
            var rend = currentIndicator.GetComponent<Renderer>();
            if (rend != null)
            {
                if (indicatorMaterial != null)
                {
                    rend.material = new Material(indicatorMaterial);
                }
                // ensure the material supports color/alpha; fallback handled in creation
                rend.material.color = isValid ? validColor : invalidColor;
                // disable shadows for indicator
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
                // ensure visible over ground by using higher render queue if transparent
                if (rend.material.HasProperty("_Color") && rend.material.color.a <1f)
                {
                    rend.material.renderQueue =3000; // Transparent
                }

                // make sure renderer is enabled
                rend.enabled = true;
            }

            // Slightly raise to avoid z-fighting
            currentIndicator.transform.position += Vector3.up *0.02f; // avoid z-fighting

            Debug.Log("BattleIndicatorManager: Created circle indicator at " + currentIndicator.transform.position + " radius=" + radius);

            if (debugShowIndicatorMarker)
            {
                CreateDebugMarker(worldPos);
            }
        }

        /// <summary>
        /// 显示目标标记（红色圆圈）
        /// </summary>
        public void ShowTargetMarker(Transform target)
        {
            ClearTargetMarkers();

            GameObject marker;
            if (targetMarkerPrefab != null)
            {
                marker = Instantiate(targetMarkerPrefab, target.position + Vector3.up *0.1f, Quaternion.identity);
            }
            else
            {
                marker = CreateTargetMarker(target.position + Vector3.up *0.1f);
            }

            marker.transform.SetParent(target);
            currentTargetMarkers.Add(marker);

            Debug.Log("BattleIndicatorManager: Created target marker for " + target.name);
        }

        /// <summary>
        /// 更新扇形指示器朝向（跟随镜头）
        /// </summary>
        public void UpdateSectorRotation(Transform center, Vector3 forward)
        {
            if (currentIndicator != null)
            {
                currentIndicator.transform.position = center.position;
                currentIndicator.transform.position = center.position + Vector3.up *0.01f;
                currentIndicator.transform.rotation = Quaternion.LookRotation(forward);
            }
        }

        /// <summary>
        /// 更新圆形指示器位置
        /// </summary>
        public void UpdateCirclePosition(Vector3 worldPos, bool isValid = true)
        {
            if (currentIndicator != null)
            {
                currentIndicator.transform.position = worldPos;
                currentIndicator.transform.position = worldPos + Vector3.up *0.01f;

                var rend = currentIndicator.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.material.color = isValid ? validColor : invalidColor;
                    rend.enabled = true;
                }
            }
        }

        public void ClearIndicators()
        {
            if (currentIndicator != null)
            {
                Destroy(currentIndicator);
                currentIndicator = null;
                currentKind = IndicatorKind.None;
                Debug.Log("BattleIndicatorManager: Cleared indicators");
            }

            // clear any debug markers
            foreach (var dm in debugMarkers)
            {
                if (dm != null) Destroy(dm);
            }
            debugMarkers.Clear();
        }

        public void ClearTargetMarkers()
        {
            foreach (var marker in currentTargetMarkers)
            {
                if (marker != null) Destroy(marker);
            }
            currentTargetMarkers.Clear();
        }

        public void ClearAll()
        {
            ClearIndicators();
            ClearTargetMarkers();
        }

        // =====运行时网格生成 =====

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

            vertices[0] = Vector3.zero; // 中心点

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

            // choose material
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
            // Use a simple cylinder primitive as a visible ground indicator (flat)
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "CircleIndicator";
            go.transform.position = center;

            // scale: x/z control diameter, y is height
            float height =0.01f; // thinner
            go.transform.localScale = new Vector3(radius *2f, height, radius *2f);

            // remove collider to avoid physics interference
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

                // ensure transparent blending and reduce z fighting by disabling ZWrite when possible
                Color c = validColor;
                mr.material.color = c;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                // force transparent render queue
                mr.material.renderQueue =3000;
                // try to disable ZWrite if shader supports it
                if (mr.material.HasProperty("_ZWrite")) mr.material.SetInt("_ZWrite",0);
                mr.enabled = true;
            }

            return go;
        }

        private GameObject CreateTargetMarker(Vector3 position)
        {
            GameObject go = new GameObject("TargetMarker");
            go.transform.position = position;

            // 创建两个圆环叠加（动画效果可选）
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
