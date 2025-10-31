using System.Collections;
using UnityEngine;

namespace Assets.Scripts.ForBattle
{
    public enum DamageType
    {
        Physics = 0,
        Magic = 1,
        True = 2,
    }
    public class SkillSystem : MonoBehaviour
    {
        [Header("引用")]
        public BattleTurnManager battleTurnManager;
        private BattleUnitPriorityQueue battleUnitPriorityQueue;

        void Start()
        {
            if (battleTurnManager == null)
            {
                Debug.LogWarning("SkillSystem: battleTurnManager 未绑定，请在 Inspector 中设置。");
            }
            else
            {
                battleUnitPriorityQueue = battleTurnManager.turnOrder;
            }
            
            
        }

        [Header("飘字设置")]
        public float popupDuration = 1.0f;
        public float popupRiseDistance = 1.0f;
        public int popupFontSize = 96; // increased default font size
        public Color damageColor = Color.red;

        public void CauseDamage(BattleUnit unit, int damage, DamageType damageType)
        {
            if (unit == null) return;

            // 先用 float 计算，再四舍五入为 int，避免隐式转换错误
            float rawDamage = 0f;
            switch (damageType)
            {
                case DamageType.True:
                    rawDamage = damage;
                    break;
                case DamageType.Physics:
                    rawDamage = damage * (1f - unit.battleDef * 0.01f);
                    break;
                case DamageType.Magic:
                    rawDamage = damage * (1f - unit.battleMagicDef * 0.01f);
                    break;
            }

            int actualDamage = Mathf.Max(0, Mathf.RoundToInt(rawDamage));

            // 扣血并限制最小值
            unit.battleHp = Mathf.Max(0, unit.battleHp - actualDamage);

            // 生成飘字
            StartCoroutine(SpawnDamagePopup(unit.transform.position, actualDamage));

            // 处理死亡逻辑（简单示例）
            if (unit.battleHp <= 0)
            {
                Debug.Log($"{unit.unitName} 已被击败");
                // 从回合队列移除（如果可用）
                if (battleUnitPriorityQueue != null)
                {
                    battleUnitPriorityQueue.Remove(unit);
                }
                // TODO: 播放死亡动画、禁用控制器等
            }
        }

        private IEnumerator SpawnDamagePopup(Vector3 worldPos, int damageAmount)
        {
            GameObject go = new GameObject("DamagePopup");
            // 头顶偏移 + 横向随机避免重叠
            go.transform.position = worldPos + Vector3.up * 1.2f + new Vector3(Random.Range(-0.2f, 0.2f), 0f, 0f);

            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = $"-{damageAmount}";
            tm.fontSize = popupFontSize;
            tm.color = damageColor;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.04f; // larger character size for visibility

            var rend = go.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                rend.sortingOrder = 500;
            }

            // cache main camera for billboarding
            Camera cam = Camera.main;
            if (cam == null && Camera.allCamerasCount > 0)
            {
                cam = Camera.allCameras[0];
            }

            float elapsed = 0f;
            Vector3 startPos = go.transform.position;
            Vector3 endPos = startPos + Vector3.up * popupRiseDistance;
            Color startColor = tm.color;

            while (elapsed < popupDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / popupDuration);

                go.transform.position = Vector3.Lerp(startPos, endPos, EaseOutQuad(t));

                // 面向摄像机（保持朝向，使用摄像机的 up 保持 upright）
                if (cam != null)
                {
                    Vector3 dir = go.transform.position - cam.transform.position;
                    if (dir.sqrMagnitude > 0.0001f)
                        go.transform.rotation = Quaternion.LookRotation(dir, cam.transform.up);
                }

                Color c = startColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                tm.color = c;

                yield return null;
            }

            Destroy(go);
        }

        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        void Update()
        {
        }
    }
}