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
        public Color healColor = Color.green;

        public void CauseDamage(BattleUnit unit, BattleUnit source, int damage, DamageType damageType)
        {
            if (unit == null || source == null) return;
            if (unit.causality > 0)
            {
                unit.causality = Mathf.Max(0, unit.causality - 1);
                // 生成飘字提示免疫
                StartCoroutine(SpawnPopup(unit.transform.position, "因果律免疫", Color.yellow));
                return; //免疫本次伤害
            }

            int criIndex;
            criIndex = UnityEngine.Random.Range(0, 100);
            int lastSpdDef =  Mathf.FloorToInt(unit.battleSpdDef * (1-source.battleCri * .01f));
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
                Debug.Log($"{unit.unitName} 已被 {source.unitName} 击败");
                // 从回合队列移除（如果可用）
                if (battleUnitPriorityQueue != null)
                {
                    battleUnitPriorityQueue.Remove(unit);
                }
                // TODO: 播放死亡动画、禁用控制器等
            }
        }

        /// <summary>
        /// 治疗指定单位，回复 amount 点生命（不会超过最大生命）。使用绿色飘字。
        /// </summary>
        public void Heal(BattleUnit unit, int amount)
        {
            if (unit == null) return;
            if (amount <= 0) return;

            int before = unit.battleHp;
            int maxHp = Mathf.Max(0, unit.battleMaxHp);
            int newHp = Mathf.Clamp(before + amount, 0, maxHp);
            int applied = Mathf.Max(0, newHp - before);

            unit.battleHp = newHp;

            // 生成绿色“+数字”的飘字
            StartCoroutine(SpawnHealPopup(unit.transform.position, applied));
        }

        /// <summary>
        /// 显示一条自定义的飘字文本（用于提示/信息）。
        /// </summary>
        public void ShowPopup(string text, Vector3 worldPos, Color color)
        {
            StartCoroutine(SpawnPopup(worldPos, text, color));
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

        private IEnumerator SpawnHealPopup(Vector3 worldPos, int healAmount)
        {
            GameObject go = new GameObject("HealPopup");
            // 头顶偏移 + 横向随机避免重叠
            go.transform.position = worldPos + Vector3.up * 1.2f + new Vector3(Random.Range(-0.2f, 0.2f), 0f, 0f);

            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = $"+{healAmount}";
            tm.fontSize = popupFontSize;
            tm.color = healColor;
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

        private IEnumerator SpawnPopup(Vector3 worldPos, string text, Color color)
        {
            GameObject go = new GameObject("Popup");
            //头顶偏移 + 横向随机避免重叠
            go.transform.position = worldPos + Vector3.up *1.2f + new Vector3(Random.Range(-0.2f,0.2f),0f,0f);

            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = text ?? string.Empty;
            tm.fontSize = popupFontSize;
            tm.color = color;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize =0.04f;

            var rend = go.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                rend.sortingOrder =500;
            }

            Camera cam = Camera.main;
            if (cam == null && Camera.allCamerasCount >0)
            {
                cam = Camera.allCameras[0];
            }

            float elapsed =0f;
            Vector3 startPos = go.transform.position;
            Vector3 endPos = startPos + Vector3.up * popupRiseDistance;
            Color startColor = tm.color;

            while (elapsed < popupDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / popupDuration);

                go.transform.position = Vector3.Lerp(startPos, endPos, EaseOutQuad(t));

                if (cam != null)
                {
                    Vector3 dir = go.transform.position - cam.transform.position;
                    if (dir.sqrMagnitude >0.0001f)
                        go.transform.rotation = Quaternion.LookRotation(dir, cam.transform.up);
                }

                Color c = startColor;
                c.a = Mathf.Lerp(1f,0f, t);
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

        /// <summary>
        /// 在单位回合开始时结算持续效果（再生）并递减持续回合，到期回退属性。
        /// 若未使用这些字段，可安全忽略。
        /// </summary>
        public void OnUnitTurnStart(BattleUnit u)
        {
            if (u == null) return;

            // 再生
            if (u.buffTurns_Regen > 0 && u.regenPerTurn > 0)
            {
                Heal(u, u.regenPerTurn);
            }

            // 递减持续时间并在到期时回退增益/减益
            TickAndRestore(ref u.buffTurns_EvasionUp, ref u.deltaSpdDef, ref u.battleSpdDef);
            TickAndRestore(ref u.buffTurns_CritUp,   ref u.deltaCri,   ref u.battleCri);
            TickAndRestore(ref u.buffTurns_DefUp,    ref u.deltaDef,   ref u.battleDef);
            TickAndRestore(ref u.buffTurns_AttackUp, ref u.deltaAtk,   ref u.battleAtk);
            TickAndRestore(ref u.buffTurns_SpdUp,    ref u.deltaSpd,   ref u.battleSpd);

            if (u.buffTurns_Regen > 0) u.buffTurns_Regen = Mathf.Max(0, u.buffTurns_Regen - 1);
            if (u.buffTurns_Regen == 0) u.regenPerTurn = 0;

            // Debuff
            TickAndRestore(ref u.debuffTurns_MagicDefDown, ref u.deltaMagicDef, ref u.battleMagicDef);
            TickAndRestore(ref u.debuffTurns_MagicAtkDown, ref u.deltaMagicAtk, ref u.battleMagicAtk);
        }

        private void TickAndRestore(ref int turns, ref int delta, ref int targetStat)
        {
            if (turns > 0)
            {
                turns--;
                if (turns == 0 && delta != 0)
                {
                    targetStat -= delta;
                    delta = 0;
                }
            }
        }
    }
}