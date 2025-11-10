using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.ForBattle.Indicators;
using Assets.Scripts.ForBattle.Barriers;
using Assets.Scripts.ForBattle;

public class CeliaController : PlayerController
{
    [Header("Celia Stats & Scaling")]
    public float magicMultiplier = 1.0f; // base scaling for spells

    [Header("Spell Costs")]
    public int costFire1 = 1;
    public int costFire2 = 3;
    public int costFire3 = 6;

    public int costShock1 = 1;
    public int costShock2 = 3;
    public int costShock3 = 6;

    public int costIce1 = 1;
    public int costIce2 = 3;
    public int costIce3 = 6;

    public int costWind1 = 1;
    public int costWind2 = 3;
    public int costWind3 = 6;

    [Header("Special Skills")]
    public int costManaField = 2; // 储魔结界
    public int costFocus = 3; // 精神集中
    public int costHaste = 4; // 驱动加速
    public int costMindBurst = 6; // 心灵爆裂 (需光环)

    // spell levels (upgradeable) - for demo keep as ints
    public int fireLevel = 1; // 1..3
    public int shockLevel = 1;
    public int iceLevel = 1;
    public int windLevel = 1;

    // Focus/Charge state
    private bool hasFocusBuff = false; // 下次魔法等级+1

    // Mana Field barrier instance
    private Assets.Scripts.ForBattle.Barriers.ManaFieldBarrier manaFieldInstance;
    [Header("Skill Tunables")]
    [Tooltip("Fire Lv2 AoE radius around caster")]
    public float fireRadiusLv2 = 4.5f;
    [Tooltip("Fire Lv3 AoE radius (large) around caster")]
    public float fireRadiusLv3 = 20f;

    [Tooltip("Shock Lv2 (line) length")]
    public float shockLength = 10f;
    [Tooltip("Shock Lv2 (line) lateral width")]
    public float shockWidth = 2f;
    [Tooltip("Shock sector angle used for preview (degrees)")]
    public float shockAngle = 20f;
    [Tooltip("Shock Lv3 AoE radius shown in preview")]
    public float shockRadiusLv3 = 6f;

    [Tooltip("Ice Lv3 AoE radius")]
    public float iceRadiusLv3 = 5f;

    [Tooltip("Wind Lv2 small AoE radius")]
    public float windRadiusLv2 = 3f;
    [Tooltip("Wind Lv3 AoE radius")]
    public float windRadiusLv3 = 6f;

    [Tooltip("Mind Burst cone length")]
    public float mindBurstLength = 12f;
    [Tooltip("Mind Burst cone angle in degrees")]
    public float mindBurstAngle = 120f;

    [Header("Special Tunables")]
    [Tooltip("If Celia places a Mana Field, grant this initial action point to herself so she can act again immediately")]
    public int extraActPointAfterManaField = 9999;

    // persistent preview sector indicator (used for some AoE skills)
    private GameObject skillSectorIndicator = null;
    // persistent single-target range indicator
    private GameObject skillRangeIndicator = null;
    // temporary reference to the last created skill preview GameObject (to be retagged into chant)
    private GameObject lastSkillPreviewObject = null;
    // chanting state: -1 = none, otherwise original skill index (0..3)
    private int chantingSkillIndex = -1;
    // persistent chant indicator
    private GameObject chantIndicator = null;
    // stored chant data
    private int chantingEffectiveLevel = -1;
    private BattleUnit chantingTarget = null;
    private Vector3 chantingDirection = Vector3.zero;
    private Vector3 chantingArea = Vector3.zero;
    private enum PreviewShape { None, Circle, Sector, Rectangle }
    private PreviewShape currentPreviewShape = PreviewShape.None;
    private float previewRadius = 0f;
    private float previewAngle = 0f;
    private float previewLength = 0f;
    private float previewWidth = 0f;
    private Vector3 previewForward = Vector3.forward;
    private Vector3 previewCenter = Vector3.zero;
    [Header("Chanting")]
    [Tooltip("Color key name passed to BattleIndicatorManager when creating the chant indicator (use entries from IndicatorColorEntry or built-in keys like 'blue')")]
    public string chantColorKey = "blue";

    // mapping from displayed skill list index -> original skill index
    private List<int> displayedToOriginal = null;
    // cache effective levels to detect runtime changes (mana field / focus / haste)
    private int[] cachedEffectiveLevels = new int[4] { -1, -1, -1, -1 };

    private void EnsureDisplayMap()
    {
        // Rebuild mapping according to current availability (MindBurst requires haste buff)
        var origNames = GetSkillNames();
        displayedToOriginal = new List<int>();
        for (int i = 0; i < origNames.Count; i++)
        {
            if (i == 7)
            {
                // MindBurst only shown when haste buff active
                if (unit != null && unit.buffTurns_SpdUp <= 0) continue;
            }
            displayedToOriginal.Add(i);
        }
    }

    // Helper: sum extra magic level provided by ManaFieldBarrier instances affecting this unit
    private int GetManaFieldLevelBonus()
    {
        int bonus = 0;
        foreach (var b in Assets.Scripts.ForBattle.Barriers.BarrierBase.ActiveBarriers)
        {
            if (b == null) continue;
            if (b is Assets.Scripts.ForBattle.Barriers.ManaFieldBarrier m)
            {
                if (b.IsUnitAffected(unit)) bonus += m.extraMagicLevel;
            }
        }
        return bonus;
    }

    // Effective spell level considering base level, focus buff, and ManaField bonuses (clamped to 3)
    private int GetEffectiveSpellLevel(int skillIndex)
    {
        int baseLevel = 1;
        switch (skillIndex)
        {
            case 0: baseLevel = fireLevel; break;
            case 1: baseLevel = shockLevel; break;
            case 2: baseLevel = iceLevel; break;
            case 3: baseLevel = windLevel; break;
            default: baseLevel = 1; break;
        }
        int bonus = 0;
        if (hasFocusBuff) bonus += 1;
        bonus += GetManaFieldLevelBonus();
        int eff = Mathf.Clamp(baseLevel + bonus, 1, 3);
        return eff;
    }

    protected override List<string> GetSkillNames()
    {
        // Return names that reflect current *effective* levels for each elemental group
        int f = GetEffectiveSpellLevel(0);
        int s = GetEffectiveSpellLevel(1);
        int i = GetEffectiveSpellLevel(2);
        int w = GetEffectiveSpellLevel(3);
        string fireName = f == 1 ? "火球术" : (f == 2 ? "火山爆发" : "陨石打击");
        string shockName = s == 1 ? "闪雷术" : (s == 2 ? "电流喷涌" : "雷神之锤");
        string iceName = i == 1 ? "寒冰箭" : (i == 2 ? "大气冰枪" : "钻石星尘");
        string windName = w == 1 ? "疾风刃" : (w == 2 ? "漩涡龙卷" : "列克斯卡利伯");

        return new List<string>
        {
            fireName,
            shockName,
            iceName,
            windName,
            "储魔结界",
            "精神集中",
            "驱动加速",
            "心灵爆裂"
        };
    }

    protected override string GetSkillExtraInfo(int index
    )
    {
        // If MindBurst requires haste buff, hide its extra info when not available
        if (index == 7 && unit != null && unit.buffTurns_SpdUp <= 0)
        {
            return null;
        }

        switch (index)
        {
            case 0:
                int effLvl = GetEffectiveSpellLevel(0);
                int effCost = GetSpellCostForLevel(0, effLvl);
                return $"火系 (Lv{effLvl}) | {FormatBpCost(effCost)}";
            case 1:
                effLvl = GetEffectiveSpellLevel(1);
                effCost = GetSpellCostForLevel(1, effLvl);
                return $"电系 (Lv{effLvl}) | {FormatBpCost(effCost)}";
            case 2:
                effLvl = GetEffectiveSpellLevel(2);
                effCost = GetSpellCostForLevel(2, effLvl);
                return $"冰系 (Lv{effLvl}) | {FormatBpCost(effCost)}";
            case 3:
                effLvl = GetEffectiveSpellLevel(3);
                effCost = GetSpellCostForLevel(3, effLvl);
                return $"风系 (Lv{effLvl}) | {FormatBpCost(effCost)}";
            case 4: return $"结界 | {FormatBpCost(costManaField)}";
            case 5: return $"辅助 | {FormatBpCost(costFocus)}";
            case 6: return $"光环 | {FormatBpCost(costHaste)}";
            case 7: return $"大技 (需光环) | {FormatBpCost(costMindBurst)}";
        }
        return null;
    }

    private int GetSpellCost(int groupIndex)
    {
        switch (groupIndex)
        {
            case 0: return fireLevel == 1 ? costFire1 : (fireLevel == 2 ? costFire2 : costFire3);
            case 1: return shockLevel == 1 ? costShock1 : (shockLevel == 2 ? costShock2 : costShock3);
            case 2: return iceLevel == 1 ? costIce1 : (iceLevel == 2 ? costIce2 : costIce3);
            case 3: return windLevel == 1 ? costWind1 : (windLevel == 2 ? costWind2 : costWind3);
        }
        return 0;
    }

    // Get BP cost for a given elemental group at a specified level (1..3)
    private int GetSpellCostForLevel(int groupIndex, int level)
    {
        level = Mathf.Clamp(level, 1, 3);
        switch (groupIndex)
        {
            case 0: return level == 1 ? costFire1 : (level == 2 ? costFire2 : costFire3);
            case 1: return level == 1 ? costShock1 : (level == 2 ? costShock2 : costShock3);
            case 2: return level == 1 ? costIce1 : (level == 2 ? costIce2 : costIce3);
            case 3: return level == 1 ? costWind1 : (level == 2 ? costWind2 : costWind3);
        }
        return 0;
    }

    // helper: try to pick a unit under cursor
    private BattleUnit RaycastUnitUnderCursor(Camera cam)
    {
        if (cam == null) return null;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit h, 100f))
        {
            return h.collider.GetComponentInParent<BattleUnit>();
        }
        return null;
    }

    // fallback nearest in arbitrary range
    private BattleUnit FindNearestInRange(float maxRange)
    {
        BattleUnit best = null; float bestD = float.MaxValue;
        foreach (var u in FindObjectsOfType<BattleUnit>())
        {
            if (!IsValidTarget(u)) continue;
            float d = Vector3.Distance(transform.position, u.transform.position);
            if (d <= maxRange && d < bestD) { best = u; bestD = d; }
        }
        return best;
    }

    // Play spell SFX based on group: 0=fire,1=shock,2=ice,3=wind and effective level
    private void PlaySpellSfx(int group, int level)
    {
        if (sfxPlayer == null) return;
        switch (group)
        {
            case 0: // fire
                if (level == 1) sfxPlayer.Play("fire_small"); else sfxPlayer.Play("fire_middle");
                break;
            case 1: // shock/electric
                if (level == 1) sfxPlayer.Play("thunder_small"); else sfxPlayer.Play("thunder_middle");
                break;
            case 2: // ice
                if (level == 1) sfxPlayer.Play("ice_small"); else sfxPlayer.Play("ice_middle");
                break;
            case 3: // wind
                if (level == 1) sfxPlayer.Play("aero_small"); else sfxPlayer.Play("aero_middle");
                break;
        }
    }

    protected override IEnumerator TryQuickCastSkill(int index)
    {
        // Map displayed index (from UI) to original skill index
        int origIndex = MapDisplayedToOriginal(index);
        if (origIndex < 0) { skillReselectRequested = true; yield break; }
        index = origIndex;

        var tm = FindObjectOfType<BattleTurnManager>();

        // Spell groups 0..3 - elemental spells must be chanted first
        if (index >= 0 && index <= 3)
        {
            // If this is a release call (we are currently chanting this skill), perform the actual cast without spending BP again
            if (chantingSkillIndex == index)
            {
                //tm.battlePoints -= 1;
                // compute effective level (includes focus and mana field bonuses)
                int level = chantingEffectiveLevel >= 0 ? chantingEffectiveLevel : GetEffectiveSpellLevel(index);
                // decide target/reference
                BattleUnit targetRel = chantingTarget;
                if (targetRel == null)
                {
                    Camera camRel = Camera.main;
                    float pickRangeRel = GetSkillCastRange(index);
                    targetRel = RaycastUnitUnderCursor(camRel) ?? FindNearestInRange(pickRangeRel);
                }

                // perform group-specific behaviors (same as normal cast but BP already spent at chant start)
                if (index == 0)
                {
                    if (level == 1)
                    {
                        int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * 0.8f));
                        skillSystem?.CauseDamage(targetRel, unit, dmg, DamageType.Magic);
                    }
                    else if (level == 2)
                    {
                        float radius = fireRadiusLv2;
                        int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * 1.2f));
                        foreach (var u in FindObjectsOfType<BattleUnit>())
                        {
                            if (!IsValidTarget(u)) continue;
                            if (Vector3.Distance(transform.position, u.transform.position) <= radius)
                                skillSystem?.CauseDamage(u, unit, dmg, DamageType.Magic);
                        }
                    }
                    else
                    {
                        int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * 1.6f));
                        foreach (var u in FindObjectsOfType<BattleUnit>())
                        {
                            if (!IsValidTarget(u)) continue;
                            skillSystem?.CauseDamage(u, unit, dmg, DamageType.Magic);
                            u.burnTurns = Mathf.Max(u.burnTurns, 3);
                        }
                    }
                    PlaySpellSfx(0, level);
                    hasFocusBuff = false;
                    // cleanup chant state after release
                    StopChanting();
                    skillReselectRequested = false;
                    yield return new WaitForSeconds(0.4f);
                    yield break;
                }
                else if (index == 1)
                {
                    if (level == 1)
                    {
                        int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * 0.8f));
                        skillSystem?.CauseDamage(targetRel, unit, dmg, DamageType.Magic);
                    }
                    else if (level == 2)
                    {
                        Vector3 forward = Camera.main != null ? Camera.main.transform.forward : transform.forward;
                        forward.y = 0f; forward.Normalize();
                        float length = shockLength; float width = shockWidth;
                        int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * 1.2f));
                        foreach (var u in FindObjectsOfType<BattleUnit>())
                        {
                            if (!IsValidTarget(u)) continue;
                            Vector3 to = u.transform.position - transform.position; to.y = 0f;
                            float fwd = Vector3.Dot(to, forward);
                            float lat = Mathf.Abs(Vector3.Dot(to, Vector3.Cross(Vector3.up, forward)));
                            if (fwd >= 0f && fwd <= length && lat <= width * 0.5f)
                                skillSystem?.CauseDamage(u, unit, dmg, DamageType.Magic);
                        }
                    }
                    else
                    {
                        int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * 2.2f));
                        skillSystem?.CauseDamage(targetRel, unit, dmg, DamageType.Magic);
                        targetRel.blakeStun = Mathf.Max(targetRel.blakeStun, 1);
                    }
                    PlaySpellSfx(1, level);
                    hasFocusBuff = false;
                    StopChanting();
                    skillReselectRequested = false;
                    yield return new WaitForSeconds(0.4f);
                    yield break;
                }
                else if (index == 2)
                {
                    if (level == 1)
                    {
                        int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * 0.8f));
                        skillSystem?.CauseDamage(targetRel, unit, dmg, DamageType.Magic);
                    }
                    else if (level == 2)
                    {
                        int hitDmg = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * 0.7f));
                        for (int i = 0; i < 3; i++) { skillSystem?.CauseDamage(targetRel, unit, hitDmg, DamageType.Magic); yield return new WaitForSeconds(0.15f); }
                    }
                    else
                    {
                        int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * 1.4f));
                        float radius = iceRadiusLv3;
                        foreach (var u in FindObjectsOfType<BattleUnit>())
                        {
                            if (!IsValidTarget(u)) continue;
                            if (Vector3.Distance(transform.position, u.transform.position) <= radius)
                            {
                                skillSystem?.CauseDamage(u, unit, dmg, DamageType.Magic);
                                u.debuffTurns_MagicDefDown = Mathf.Max(u.debuffTurns_MagicDefDown, 2);
                            }
                        }
                    }
                    PlaySpellSfx(2, level);
                    hasFocusBuff = false;
                    StopChanting();
                    skillReselectRequested = false;
                    yield return new WaitForSeconds(0.45f);
                    yield break;
                }
                else // wind
                {
                    if (level == 1)
                    {
                        int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * 0.8f));
                        skillSystem?.CauseDamage(targetRel, unit, dmg, DamageType.Magic);
                    }
                    else if (level == 2)
                    {
                        float radius = windRadiusLv2; int hitDmg = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * 1.0f));
                        for (int i = 0; i < 3; i++)
                        {
                            foreach (var u in FindObjectsOfType<BattleUnit>())
                            {
                                if (!IsValidTarget(u)) continue;
                                if (Vector3.Distance(transform.position, u.transform.position) <= radius)
                                    skillSystem?.CauseDamage(u, unit, hitDmg, DamageType.Magic);
                            }
                            yield return new WaitForSeconds(0.12f);
                        }
                    }
                    else
                    {
                        float radius = windRadiusLv3;
                        int hitDmg = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * 1.4f));
                        for (int round = 0; round < 5; round++)
                        {
                            foreach (var u in FindObjectsOfType<BattleUnit>())
                            {
                                if (!IsValidTarget(u)) continue;
                                if (Vector3.Distance(transform.position, u.transform.position) <= radius)
                                {
                                    skillSystem?.CauseDamage(u, unit, hitDmg, DamageType.Magic);
                                    u.blindTurns = Mathf.Max(u.blindTurns, 2);
                                    u.blindEvasionDelta = Mathf.Max(u.blindEvasionDelta, 20);
                                    u.battleEvasion = Mathf.Max(0, u.battleEvasion - u.blindEvasionDelta);
                                }
                            }
                            yield return new WaitForSeconds(0.1f);
                        }
                    }
                    PlaySpellSfx(3, level);
                    hasFocusBuff = false;
                    StopChanting();
                    skillReselectRequested = false;
                    yield return new WaitForSeconds(0.4f);
                    yield break;
                }
            }

            // Otherwise this is the initial selection: start chanting instead of immediate cast
            // compute effective level and cost, and attempt to spend BP now
            int levelInit = GetEffectiveSpellLevel(index);
            int costInit = GetSpellCostForLevel(index, levelInit);
            if (tm == null || !tm.TrySpendBattlePoints(costInit)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }

            // Start chanting and create persistent chant indicator copied from preview
            StartChanting(index);
            skillReselectRequested = false; yield return new WaitForSeconds(0.15f); yield break;
        }
        else
        {
            // special skills
            switch (index)
            {
                case 4: // mana field
                    {
                        if (manaFieldInstance != null)
                        {
                            Destroy(manaFieldInstance.gameObject);
                            manaFieldInstance = null;
                        }
                        if (tm != null && !tm.TrySpendBattlePoints(costManaField)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
                        var sys = skillSystem ?? FindObjectOfType<SkillSystem>();
                        if (sys != null)
                        {
                            var bar = sys.PlaceBarrier<Assets.Scripts.ForBattle.Barriers.ManaFieldBarrier>(GetGroundPosition(transform.position), unit, b =>
                            {
                                b.durationTurns = 3;
                                b.radius = targetSelectionRange;
                                b.teamFilter = BarrierTeamFilter.Allies;
                                b.owner = unit;
                            });
                            manaFieldInstance = bar;
                        }
                        // grant immediate extra turn: request BattleTurnManager to assign an initial action point after this turn
                        var btm = FindObjectOfType<BattleTurnManager>();
                        if (btm != null && unit != null)
                        {
                            // The extra turn granted by Mana Field should be treated as an extra turn
                            btm.SetInitialActPointForUnit(unit, extraActPointAfterManaField, true);
                        }
                        // play light magic SFX for barrier placement
                        if (sfxPlayer != null) sfxPlayer.Play("light_magic");
                        skillReselectRequested = false;
                        yield return new WaitForSeconds(0.2f);
                        yield break;
                    }
                case 5: // focus
                    if (tm != null && !tm.TrySpendBattlePoints(costFocus)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
                    hasFocusBuff = true;
                    // buff magic atk
                    unit.buffTurns_AttackUp = Mathf.Max(unit.buffTurns_AttackUp, 2);
                    int delta = Mathf.RoundToInt(unit.magicAtk * 0.2f);
                    unit.deltaMagicAtk += delta;
                    unit.battleMagicAtk += delta;
                    // also next BP cost reduction - handled by Barrier? we store a local token on unit
                    unit.luminaExtraBattlePoint = Mathf.Max(unit.luminaExtraBattlePoint, 2); // repurpose as temp BP discount
                    // play light magic SFX for focus buff
                    if (sfxPlayer != null) sfxPlayer.Play("light_magic");
                    skillReselectRequested = false; yield return new WaitForSeconds(0.2f); yield break;
                case 6: // haste aura
                    if (tm != null && !tm.TrySpendBattlePoints(costHaste)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
                    unit.buffTurns_SpdUp = Mathf.Max(unit.buffTurns_SpdUp, 3);
                    int dsp = Mathf.RoundToInt(unit.spd * 0.4f);
                    unit.deltaSpd += dsp;
                    unit.battleSpd += dsp;
                    if (sfxPlayer != null) sfxPlayer.Play("light_magic");
                    skillReselectRequested = false; yield return new WaitForSeconds(0.2f); yield break;
                case 7: // mind burst
                    // Mind Burst only allowed when haste (spd up) buff is active
                    if (unit.buffTurns_SpdUp <= 0) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
                    if (tm != null && !tm.TrySpendBattlePoints(costMindBurst)) { if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break; }
                    foreach (var u in FindObjectsOfType<BattleUnit>())
                    {
                        if (!IsValidTarget(u)) continue;
                        int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.battleMagicAtk * 1.8f));
                        skillSystem?.CauseDamage(u, unit, dmg, DamageType.Magic);
                    }
                    // Play ice middle SFX for Mind Burst
                    PlaySpellSfx(2, 2);
                    skillReselectRequested = false; yield return new WaitForSeconds(0.6f); yield break;
            }
        }

        if (sfxPlayer != null) sfxPlayer.Play("Error"); skillReselectRequested = true; yield break;
    }

    protected override void ShowSkillPreview(int index)
    {
        // Map displayed index (from UI) to original skill index
        int orig = MapDisplayedToOriginal(index);
        if (orig < 0) return;
        index = orig;
        if (indicatorManager == null) return;

        // If we are currently chanting and have already created/retagged a chant indicator,
        // do not recreate or clear preview indicators – keep the chant indicator persistent.
        if (chantIndicator != null)
        {
            return;
        }

        // Do not clear SkillRange/SkillPreview every frame to avoid destroying/recreating indicators
        // Keep target marking to base class; only update/create persistent indicators here.

        // Default radius helper
        float radius = GetSkillCastRange(index);

        // Elemental groups 0..3
        if (index >= 0 && index <= 3)
        {
            // compute effective level (includes focus and mana field bonuses)
            int level = GetEffectiveSpellLevel(index);

            // Determine whether this is a single-target style preview (show range circle + target marker)
            bool singleTargetPreview = false;
            if (index == 0 && level == 1) singleTargetPreview = true; // Fire Lv1
            // Shock: Lv1 and Lv3 are single-target styles (Lv2 is a line)
            if (index == 1 && (level == 1 || level == 3)) singleTargetPreview = true; // Shock Lv1/3
            if (index == 2 && (level == 1 || level == 2)) singleTargetPreview = true; // Ice Lv1/2
            if (index == 3 && level == 1) singleTargetPreview = true; // Wind Lv1

            if (singleTargetPreview)
            {
                // show cast range circle (follows caster) - fixed size for single-target skills
                radius = 5f;
                Vector3 center = GetGroundPosition(transform.position);
                if (skillRangeIndicator == null)
                {
                    // Use SkillPreview tag for single-target persistent indicator so it's not cleared by other SkillRange usage
                    skillRangeIndicator = indicatorManager.CreateCircleIndicator(center, radius, true, true, BattleIndicatorManager.Tags.SkillPreview, false);
                }
                else
                {
                    // update position/size without changing color
                    indicatorManager.UpdateCircleIndicatorKeepColor(skillRangeIndicator, center, radius);
                }
                // store preview params
                currentPreviewShape = PreviewShape.Circle;
                previewRadius = radius;
                previewCenter = center;

                return;
            }

            // non-single-target behaviors preserved from previous implementation
            switch (index)
            {
                case 0: // Fire
                    if (level == 1)
                    {
                        // handled above
                    }
                    else if (level == 2)
                    {
                        // volcano: AoE around caster
                        radius = fireRadiusLv2;
                        var center = GetGroundPosition(transform.position);
                        // store reference so StartChanting can retag this preview into a chant indicator
                        skillSectorIndicator = indicatorManager.CreateCircleIndicator(center, radius, true, false, BattleIndicatorManager.Tags.SkillPreview, true, "Fire");
                        currentPreviewShape = PreviewShape.Circle;
                        previewRadius = radius;
                        previewCenter = center;
                    }
                    else
                    {
                        // level3: global/large AoE - show large circle
                        radius = fireRadiusLv3;
                        var center = GetGroundPosition(transform.position);
                        skillSectorIndicator = indicatorManager.CreateCircleIndicator(center, radius, true, false, BattleIndicatorManager.Tags.SkillPreview, true, "Fire");
                    }
                    break;
                case 1: // Shock
                    if (level == 1)
                    {
                        // handled above
                    }
                    else if (level == 2)
                    {
                        // line attack: use rectangular indicator aligned to camera forward
                        float len = shockLength;
                        float width = shockWidth;
                        Vector3 forward = Camera.main != null ? Camera.main.transform.forward : transform.forward;
                        forward.y = 0f; forward.Normalize();
                        if (skillSectorIndicator == null)
                        {
                            skillSectorIndicator = indicatorManager.CreateRectangleIndicator(transform, len, width, forward, BattleIndicatorManager.Tags.SkillPreview, true, "Lightening");
                        }
                        else
                        {
                            // rectangle indicator update
                            indicatorManager.UpdateRectangleIndicator(skillSectorIndicator, transform.position != null ? transform : transform, len, width, forward, "Lightening");
                        }
                        currentPreviewShape = PreviewShape.Rectangle;
                        previewLength = len;
                        previewWidth = width;
                        previewForward = forward;
                        previewCenter = GetGroundPosition(transform.position);
                    }
                    else if (level == 3)
                    {
                        // handled as single-target preview above
                    }
                    else
                    {
                        radius = shockRadiusLv3;
                        var center = GetGroundPosition(transform.position);
                        skillSectorIndicator = indicatorManager.CreateCircleIndicator(center, radius, true, true, BattleIndicatorManager.Tags.SkillPreview, true);
                    }
                    break;
                case 2: // Ice
                    if (level == 1 || level == 2)
                    {
                        // handled above
                    }
                    else
                    {
                        // AoE around caster
                        radius = iceRadiusLv3;
                        var center = GetGroundPosition(transform.position);
                        skillSectorIndicator = indicatorManager.CreateCircleIndicator(center, radius, true, false, BattleIndicatorManager.Tags.SkillPreview, true, "Ice");
                    }
                    break;
                case 3: // Wind
                    if (level == 1)
                    {
                        // handled above
                    }
                    else if (level == 2)
                    {
                        radius = windRadiusLv2;
                        var center = GetGroundPosition(transform.position);
                        skillSectorIndicator = indicatorManager.CreateCircleIndicator(center, radius, true, false, BattleIndicatorManager.Tags.SkillPreview, true, "Ice");
                    }
                    else
                    {
                        radius = windRadiusLv3;
                        var center = GetGroundPosition(transform.position);
                        skillSectorIndicator = indicatorManager.CreateCircleIndicator(center, radius, true, false, BattleIndicatorManager.Tags.SkillPreview, true, "Ice");
                    }
                    break;
            }
            return;
        }

        // Special skills
        switch (index)
        {
            case 4: // Mana Field - show barrier radius
                {
                    var center = GetGroundPosition(transform.position);
                    indicatorManager.CreateCircleIndicator(center, targetSelectionRange, true, false, BattleIndicatorManager.Tags.SkillPreview, true);
                    break;
                }
            case 5: // Focus - small self buff indicator
                {
                    var center = GetGroundPosition(transform.position);
                    indicatorManager.CreateCircleIndicator(center, 1.2f, true, false, BattleIndicatorManager.Tags.SkillPreview, true);
                    break;
                }
            case 6: // Haste aura
                {
                    var center = GetGroundPosition(transform.position);
                    indicatorManager.CreateCircleIndicator(center, 1.2f, true, false, BattleIndicatorManager.Tags.SkillPreview, true);
                    break;
                }
            case 7: // Mind Burst - big cone / area
                {
                    float len = mindBurstLength;
                    float ang = mindBurstAngle;
                    if (skillSectorIndicator == null)
                    {
                        skillSectorIndicator = indicatorManager.CreateSectorIndicator(transform, len, ang, BattleIndicatorManager.Tags.SkillPreview, true, "Mind");
                    }
                    else
                    {
                        indicatorManager.UpdateSectorIndicator(skillSectorIndicator, transform.position, len, ang);
                    }
                    Camera cam2 = Camera.main ?? (Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null);
                    if (cam2 != null)
                    {
                        Vector3 camFwd2 = cam2.transform.forward; camFwd2.y = 0f;
                        if (camFwd2.sqrMagnitude > 0.001f)
                            indicatorManager.UpdateSectorRotation(skillSectorIndicator, transform, camFwd2.normalized);
                    }
                    break;
                }
        }
    }

    // Mark which Celia skills are single-target so base class handles target cycling/marking
    protected override bool IsSkillSingleTarget(int skillIndex)
    {
        // skillIndex may be displayed index from UI; map to original
        int orig = MapDisplayedToOriginal(skillIndex);
        if (orig >= 0) skillIndex = orig;
        // Fire lv1, Shock lv1, Shock lv3, Ice lv1/2, Wind lv1 are treated as single-target styles
        if (skillIndex >= 0 && skillIndex <= 3)
        {
            int eff = GetEffectiveSpellLevel(skillIndex);
            if (skillIndex == 0 && eff == 1) return true;
            if (skillIndex == 1 && (eff == 1 || eff == 3)) return true; // Shock Lv1 or Lv3 single-target
            if (skillIndex == 2 && (eff == 1 || eff == 2)) return true;
            if (skillIndex == 3 && eff == 1) return true;
        }
        return false;
    }

    // Public API to change spell levels and refresh UI
    public void SetSpellLevel(int groupIndex, int level)
    {
        level = Mathf.Clamp(level, 1, 3);
        switch (groupIndex)
        {
            case 0: fireLevel = level; break;
            case 1: shockLevel = level; break;
            case 2: iceLevel = level; break;
            case 3: windLevel = level; break;
            default: return;
        }
        UpdateSkillUI();
    }

    public void LevelUpSpell(int groupIndex)
    {
        switch (groupIndex)
        {
            case 0: fireLevel = Mathf.Clamp(fireLevel + 1, 1, 3); break;
            case 1: shockLevel = Mathf.Clamp(shockLevel + 1, 1, 3); break;
            case 2: iceLevel = Mathf.Clamp(iceLevel + 1, 1, 3); break;
            case 3: windLevel = Mathf.Clamp(windLevel + 1, 1, 3); break;
            default: return;
        }
        UpdateSkillUI();
    }

    // Override to provide display strings that may exclude unavailable large-skill
    protected override List<string> GetSkillDisplayStrings()
    {
        var names = GetSkillNames();
        if (names == null) return null;
        EnsureDisplayMap();
        var list = new List<string>(displayedToOriginal.Count);
        foreach (var orig in displayedToOriginal)
        {
            string extra = GetSkillExtraInfo(orig);
            if (!string.IsNullOrEmpty(extra)) list.Add($"{names[orig]} ({extra})");
            else list.Add(names[orig]);
        }
        return list;
    }

    private void UpdateSkillUI()
    {
        // Update skill list shown in UI if open
        if (battleUI != null && battleUI.skillListController != null)
        {
            var display = GetSkillDisplayStrings();
            if (display != null)
            {
                battleUI.skillListController.SetSkills(display);
            }
        }
    }

    // Map from displayed index (as used by UI/PlayerController) to original skill index
    private int MapDisplayedToOriginal(int displayedIndex)
    {
        if (displayedToOriginal == null) EnsureDisplayMap();
        if (displayedToOriginal == null) return -1;
        if (displayedIndex < 0 || displayedIndex >= displayedToOriginal.Count) return -1;
        return displayedToOriginal[displayedIndex];
    }

    void Update()
    {
        // detect runtime changes to effective levels (mana field / focus)
        bool changed = false;
        for (int k = 0; k < 4; k++)
        {
            int eff = GetEffectiveSpellLevel(k);
            if (cachedEffectiveLevels[k] != eff)
            {
                cachedEffectiveLevels[k] = eff;
                changed = true;
            }
        }
        if (changed)
        {
            // refresh UI names
            UpdateSkillUI();
            // destroy persistent preview indicators so they'll be recreated to match new behavior
            if (indicatorManager != null)
            {
                if (skillRangeIndicator != null)
                {
                    indicatorManager.DestroyIndicator(skillRangeIndicator);
                    skillRangeIndicator = null;
                }
                if (skillSectorIndicator != null)
                {
                    indicatorManager.DestroyIndicator(skillSectorIndicator);
                    skillSectorIndicator = null;
                }
            }
        }
    }

    // Ensure single-target style skills use a fixed cast range (5f)
    protected override float GetSkillCastRange(int index)
    {
        // Map displayed index to original if needed
        int orig = MapDisplayedToOriginal(index);
        if (orig >= 0) index = orig;

        if (index >= 0 && index <= 3)
        {
            int eff = GetEffectiveSpellLevel(index);
            if ((index == 0 && eff == 1) || (index == 1 && eff == 1) || (index == 1 && eff == 3) || (index == 2 && (eff == 1 || eff == 2)) || (index == 3 && eff == 1))
            {
                return 5f;
            }
        }

        return base.GetSkillCastRange(index);
    }

    private void StartChanting(int index)
    {
        // Cancel any existing chant
        StopChanting();

        chantingSkillIndex = index;
        // lock current selection (target/direction/area) so release uses the same choices
        chantingTarget = preselectedTarget;
        chantingDirection = selectedDirection;
        chantingArea = selectedArea;
        // grant initial action value so unit will come back quickly (based on effective level)
        int eff = GetEffectiveSpellLevel(chantingSkillIndex);
        chantingEffectiveLevel = eff;
        int apInit = eff == 1 ? 9000 : (eff == 2 ? 7000 : 4000);
        var tm = FindObjectOfType<BattleTurnManager>();
        if (tm != null && unit != null)
        {
            // Mark this scheduled return as an extra turn (won't grant auto BP on start)
            tm.SetInitialActPointForUnit(unit, apInit, true);
        }
        // Play preparation sound and chant start sound
        if (sfxPlayer != null)
        {
            sfxPlayer.Play("magic_prepare");
            sfxPlayer.Play("chant_start");
        }

        // If a preview indicator exists (skillRangeIndicator or skillSectorIndicator), retag it to Chant and store ref
        if (indicatorManager != null)
        {
            if (skillRangeIndicator != null)
            {
                lastSkillPreviewObject = skillRangeIndicator;
                indicatorManager.ChangeIndicatorTag(lastSkillPreviewObject, BattleIndicatorManager.Tags.Chant);
                chantIndicator = lastSkillPreviewObject;
                Debug.Log("Celia StartChanting: reused skillRangeIndicator as chantIndicator");
            }
            else if (skillSectorIndicator != null)
            {
                lastSkillPreviewObject = skillSectorIndicator;
                indicatorManager.ChangeIndicatorTag(lastSkillPreviewObject, BattleIndicatorManager.Tags.Chant);
                chantIndicator = lastSkillPreviewObject;
            }
            else
            {
                // no existing preview object to retag: create a fallback chant indicator using preview params
                chantIndicator = CreateFallbackChantIndicator();
            }
        }
    }

    // create a fallback chant indicator if no preview object exists; keeps previous behavior
    private GameObject CreateFallbackChantIndicator()
    {
        if (indicatorManager == null) return null;
        switch (currentPreviewShape)
        {
            case PreviewShape.Circle:
                return indicatorManager.CreateCircleIndicator(previewCenter, previewRadius, true, false, BattleIndicatorManager.Tags.Chant, true, string.IsNullOrEmpty(chantColorKey) ? null : chantColorKey);
            case PreviewShape.Rectangle:
                return indicatorManager.CreateRectangleIndicator(transform, previewLength, previewWidth, previewForward, BattleIndicatorManager.Tags.Chant, true, string.IsNullOrEmpty(chantColorKey) ? null : chantColorKey);
            case PreviewShape.Sector:
                var go = indicatorManager.CreateSectorIndicator(transform, previewLength, previewAngle, BattleIndicatorManager.Tags.Chant, true, string.IsNullOrEmpty(chantColorKey) ? null : chantColorKey);
                indicatorManager.UpdateSectorRotation(go, transform, previewForward);
                return go;
            default:
                return indicatorManager.CreateCircleIndicator(GetGroundPosition(transform.position), 1.2f, true, false, BattleIndicatorManager.Tags.Chant, true, string.IsNullOrEmpty(chantColorKey) ? null : chantColorKey);
        }
    }

    private void StopChanting()
    {
        if (chantIndicator != null)
        {
            // If the chantIndicator was actually the original preview object we retagged, try to restore its original tag
            if (lastSkillPreviewObject != null && chantIndicator == lastSkillPreviewObject)
            {
                // restore tag to SkillPreview so base preview logic can manage it
                indicatorManager.ChangeIndicatorTag(lastSkillPreviewObject, BattleIndicatorManager.Tags.SkillPreview);
                // keep the preview object reference assigned so ShowSkillPreview can continue updating it
            }
            else
            {
                Destroy(chantIndicator);
            }
            chantIndicator = null;
        }
        chantingSkillIndex = -1;
        chantingEffectiveLevel = -1;
        chantingTarget = null;
        chantingDirection = Vector3.zero;
        chantingArea = Vector3.zero;
        currentPreviewShape = PreviewShape.None;
        previewRadius = previewAngle = previewLength = previewWidth = 0f;
        previewForward = Vector3.forward;
        previewCenter = Vector3.zero;
        lastSkillPreviewObject = null;
    }

    public override IEnumerator ExecuteTurn(BattleTurnManager turnManager)
    {
        // If currently chanting, this turn is the release turn: auto-release the chanted skill and end turn immediately
        if (chantingSkillIndex >= 0)
        {
            int chantingIndex = chantingSkillIndex;
            // Do not StopChanting() before releasing: TryQuickCastSkill expects chanting state to be present
            // so it can use stored chantingEffectiveLevel/chantingTarget/etc. Release now.
            yield return TryQuickCastSkill(chantingIndex);
            // After release, clean up chant state
            StopChanting();
            yield break;
        }

        // Otherwise fall back to normal player turn flow
        yield return base.ExecuteTurn(turnManager);
    }
}

