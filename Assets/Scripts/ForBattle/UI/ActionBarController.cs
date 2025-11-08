using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.ForBattle.UI
{
    /// <summary>
    /// 行动条控制器：显示所有战斗单位的行动进度。
    /// 要求：
    ///1. 条总长度1000（像素）。右侧为0 行动值，左侧为达到10000（执行中）。
    ///2. 使用 BattleUnit.icon作为指示物。
    ///3. 在 BattleTurnManager 推进行动点的0.5s 内播放位置移动动画。
    ///4. 单位进入执行阶段（达到阈值并开始回合）时高亮其图标（可放大）。
    ///5. 行动条始终显示，不随玩家回合隐藏。
    /// </summary>
    public class ActionBarController : MonoBehaviour
    {
        [Header("References")]
        public BattleTurnManager turnManager;
        [Tooltip("行动条容器 (RectTransform)，长度应为1000px")] public RectTransform barContainer;
        [Tooltip("单位图标预制，需包含 Image组件")] public GameObject iconPrefab;

        [Header("Style")]
        [Tooltip("图标在执行阶段的缩放倍数")] public float executingScale = 1.3f;
        [Tooltip("普通阶段的缩放倍数")] public float normalScale = 1.0f;
        [Tooltip("行动条总长度（像素）")] public float barLength = 1000f;
        [Tooltip("行动值最大可视值（左端）。内部阈值映射到该值")] public float displayMaxValue = 10000f;

        [Header("Behavior")]
        [Tooltip("若在启动时尚未建立单位图标，是否自动尝试构建")] public bool autoBuild = true;

        private readonly Dictionary<BattleUnit, RectTransform> iconMap = new Dictionary<BattleUnit, RectTransform>();
        private Coroutine animateCoroutine;

        private void Awake()
        {
            if (turnManager == null) turnManager = FindObjectOfType<BattleTurnManager>();
            if (barContainer == null) barContainer = GetComponent<RectTransform>();
        }

        private void Start()
        {
            if (barContainer != null) barLength = Mathf.Max(1f, barContainer.rect.width);
        }

        private void OnEnable()
        {
            SubscribeEvents();
            if (autoBuild) RebuildIcons();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (turnManager == null) return;
            turnManager.OnActionBarAdvance += HandleAdvance;
            turnManager.OnUnitTurnStart += HandleUnitTurnStart;
            turnManager.OnUnitTurnEnd += HandleUnitTurnEnd;
        }

        private void UnsubscribeEvents()
        {
            if (turnManager == null) return;
            turnManager.OnActionBarAdvance -= HandleAdvance;
            turnManager.OnUnitTurnStart -= HandleUnitTurnStart;
            turnManager.OnUnitTurnEnd -= HandleUnitTurnEnd;
        }

        private void RebuildIcons()
        {
            foreach (var kv in iconMap)
            {
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            }
            iconMap.Clear();

            if (turnManager == null || turnManager.turnOrder == null)
            {
                Debug.LogWarning("[ActionBar] turnManager/turnOrder is null, skip RebuildIcons.");
                return;
            }
            var all = turnManager.turnOrder.GetAll();
            Debug.Log($"[ActionBar] RebuildIcons units={all?.Count ?? 0}");
            foreach (var u in all)
            {
                if (u == null) continue;
                CreateIcon(u);
            }
            RefreshImmediatePositions();
        }

        private void EnsureIconsForCurrentUnits()
        {
            if (turnManager == null || turnManager.turnOrder == null) return;
            var all = turnManager.turnOrder.GetAll();
            bool changed = false;

            foreach (var u in all)
            {
                if (u == null) continue;
                if (!iconMap.ContainsKey(u)) { CreateIcon(u); changed = true; }
            }
            var toRemove = new List<BattleUnit>();
            foreach (var kv in iconMap)
            {
                if (!all.Contains(kv.Key))
                {
                    if (kv.Value != null) Destroy(kv.Value.gameObject);
                    toRemove.Add(kv.Key);
                    changed = true;
                }
            }
            foreach (var u in toRemove) iconMap.Remove(u);
            if (changed) RefreshImmediatePositions();
        }

        private GameObject InstantiateIconGO()
        {
            if (barContainer == null) return null;
            if (iconPrefab != null)
            {
                return Instantiate(iconPrefab, barContainer);
            }
            var go = new GameObject("ActionIcon_Default", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(barContainer, false);
            rt.sizeDelta = new Vector2(48, 48);
            var img = go.GetComponent<Image>();
            img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            img.color = Color.white;
            img.preserveAspect = true;
            return go;
        }

        private void CreateIcon(BattleUnit unit)
        {
            if (barContainer == null) return;
            var go = InstantiateIconGO();
            if (go == null) return;
            go.name = "ActionIcon_" + unit.unitName;
            var img = go.GetComponent<Image>();
            if (img != null && unit.icon != null)
            {
                img.sprite = unit.icon;
                img.preserveAspect = true;
            }
            var rt = go.GetComponent<RectTransform>();
            iconMap[unit] = rt;
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one * normalScale;
        }

        // 将 battleActPoint 显示在行动条上对应的位置
        private float CalcDisplayValue(BattleUnit u)
        {
            if (turnManager == null) return 0f;
            float ratio = Mathf.Clamp01(u.battleActPoint / (float)turnManager.actionPointThreshold);
            return ratio * displayMaxValue;
        }

        private float CalcPosXFromDisplayValue(float displayValue)
        {
            // 最右侧(0) -> 条右端; 最左侧(max) -> 条左端
            // 假设 barContainer 左上为 (0,0) 宽度为 barLength
            return barLength - (displayValue / displayMaxValue) * barLength;
        }

        private float ClampXToBar(RectTransform rt, float x)
        {
            float halfW = (rt != null ? Mathf.Max(1f, rt.rect.width) : 48f) * 0.5f;
            return Mathf.Clamp(x, halfW, Mathf.Max(halfW, barLength - halfW));
        }

        private void RefreshImmediatePositions()
        {
            foreach (var kv in iconMap)
            {
                var u = kv.Key; var rt = kv.Value;
                if (u == null || rt == null) continue;
                float displayValue = CalcDisplayValue(u);
                float x = CalcPosXFromDisplayValue(displayValue);
                x = ClampXToBar(rt, x);
                rt.anchoredPosition = new Vector2(x, 0f);
            }
        }

        private void HandleAdvance(Dictionary<BattleUnit, int> prev, Dictionary<BattleUnit, int> next, float duration)
        {
            if (animateCoroutine != null) StopCoroutine(animateCoroutine);
            animateCoroutine = StartCoroutine(AnimateAdvance(prev, next, duration));
        }

        private IEnumerator AnimateAdvance(Dictionary<BattleUnit, int> prev, Dictionary<BattleUnit, int> next, float duration)
        {
            float t = 0f;
            var startX = new Dictionary<BattleUnit, float>();
            var endX = new Dictionary<BattleUnit, float>();
            foreach (var kv in iconMap)
            {
                var u = kv.Key; var rt = kv.Value;
                if (u == null || rt == null) continue;
                int prevVal = prev.ContainsKey(u) ? prev[u] : u.battleActPoint;
                int nextVal = next.ContainsKey(u) ? next[u] : u.battleActPoint;
                float startRatio = Mathf.Clamp01(prevVal / (float)turnManager.actionPointThreshold);
                float endRatio = Mathf.Clamp01(nextVal / (float)turnManager.actionPointThreshold);
                float startDisplay = startRatio * displayMaxValue;
                float endDisplay = endRatio * displayMaxValue;
                startX[u] = ClampXToBar(rt, CalcPosXFromDisplayValue(startDisplay));
                endX[u] = ClampXToBar(rt, CalcPosXFromDisplayValue(endDisplay));
            }
            while (t < duration)
            {
                t += Time.deltaTime;
                float lerp = Mathf.Clamp01(t / duration);
                foreach (var kv in iconMap)
                {
                    var u = kv.Key; var rt = kv.Value;
                    if (u == null || rt == null) continue;
                    if (!startX.ContainsKey(u) || !endX.ContainsKey(u)) continue;
                    float x = Mathf.Lerp(startX[u], endX[u], lerp);
                    rt.anchoredPosition = new Vector2(x, 0f);
                }
                yield return null;
            }
            RefreshImmediatePositions();
            animateCoroutine = null;
        }

        private void HandleUnitTurnStart(BattleUnit u)
        {
            if (u != null && iconMap.ContainsKey(u))
            {
                var rt = iconMap[u];
                rt.localScale = Vector3.one * executingScale;
                rt.SetAsLastSibling();
            }
        }

        private void HandleUnitTurnEnd(BattleUnit u)
        {
            if (u != null && iconMap.ContainsKey(u))
            {
                var rt = iconMap[u];
                rt.localScale = Vector3.one * normalScale;
            }
        }

        private void Update()
        {
            if (autoBuild) EnsureIconsForCurrentUnits();
            RefreshImmediatePositions();
        }

        public void AddUnit(BattleUnit u)
        {
            if (u == null || iconMap.ContainsKey(u)) return;
            CreateIcon(u);
            RefreshImmediatePositions();
        }

        public void RemoveUnit(BattleUnit u)
        {
            if (u == null) return;
            if (iconMap.TryGetValue(u, out var rt))
            {
                if (rt != null) Destroy(rt.gameObject);
            }
            iconMap.Remove(u);
        }
    }
}
