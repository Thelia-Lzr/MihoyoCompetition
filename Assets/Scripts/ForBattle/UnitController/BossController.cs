using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.ForBattle;
using Assets.Scripts.ForBattle.Barriers;

// Boss AI: fixed behavior each turn ¡ª places barriers and deals fixed damage. No extra turns are granted.
public class BossController : AIController
{
    [Header("Boss Settings")]
    public int damageToHighLow = 30; // base damage for high/low HP targets
    public float barrierRadius = 3.5f;
    public int barrierDebuffDef = 20; // amount to reduce def/magicDef
    public int barrierDebuffSpd = 10; // amount to reduce speed
    public int barrierExplodeDamage = 60; // damage when barrier disappears

    // no extra-turn tracking; boss does not get extra turns

    public override IEnumerator ExecuteTurn(BattleTurnManager turnManager)
    {
        if (unit == null || turnManager == null) yield break;

        // New behavior: on each boss turn, always place a barrier and perform the fixed damage action.
        // Place barrier first
        yield return StartCoroutine(Skill_HitHighAndLow(turnManager));
        yield return new WaitForSeconds(0.7f);
        yield return StartCoroutine(Skill_PlaceExplosiveDebuffBarrier(turnManager));
        
        // Then perform fixed damage (hit highest and lowest as defined)
        
        yield break;
    }

    private IEnumerator Skill_HitHighAndLow(BattleTurnManager tm)
    {
        // find player units ¡ª prefer using the turnOrder to only consider on-battle units
        var players = new List<BattleUnit>();
        if (tm != null && tm.turnOrder != null)
        {
            var all = tm.turnOrder.GetAll();
            if (all != null)
            {
                foreach (var u in all)
                {
                    if (u == null) continue;
                    // only consider alive on-battle player units
                    if (u.unitType == BattleUnitType.Player && u.battleHp > 0)
                    {
                        var pc = u.controller as PlayerController;
                        if (pc == null || pc.isOnBattle) players.Add(u);
                    }
                }
            }
        }
        else
        {
            foreach (var u in FindObjectsOfType<BattleUnit>())
            {
                if (u == null) continue;
                if (u.unitType == BattleUnitType.Player && u.battleHp > 0)
                {
                    var pc = u.controller as PlayerController;
                    if (pc == null || pc.isOnBattle) players.Add(u);
                }
            }
        }
        if (players.Count == 0)
        {
            yield return new WaitForSeconds(0.3f);
            yield break;
        }

        // highest and lowest by battleHp
        players.Sort((a, b) => b.battleHp.CompareTo(a.battleHp)); // desc
        BattleUnit high = players[0];
        BattleUnit low = players[players.Count - 1];

        var sys = tm != null ? tm.skillSystem : FindObjectOfType<SkillSystem>();

        if (high != null)
        {
            int dmg = Mathf.Max(1, damageToHighLow);
            sys?.CauseDamage(high, unit, dmg, DamageType.Physics);
            sfxPlayer.Play("cut");
        }
        yield return new WaitForSeconds(.3f);
        if (low != null && low != high)
        {
            int dmg2 = Mathf.Max(1, damageToHighLow);
            sys?.CauseDamage(low, unit, dmg2, DamageType.Physics);
            sfxPlayer.Play("cut");
        }

        // simple action delay
        //yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator Skill_PlaceExplosiveDebuffBarrier(BattleTurnManager tm)
    {
        // Prepare lists for AI-side and player-side units. Use turnOrder to restrict to on-battle units when possible.
        List<BattleUnit> aiUnits = new List<BattleUnit>();
        List<BattleUnit> playerUnits = new List<BattleUnit>();
        if (tm != null && tm.turnOrder != null)
        {
            var all = tm.turnOrder.GetAll();
            if (all != null)
            {
                foreach (var u in all)
                {
                    if (u == null) continue;
                    if (u.battleHp <= 0) continue; // skip dead
                    // For player controllers, ensure they are on-battle
                    var pc = u.controller as PlayerController;
                    if (pc != null && !pc.isOnBattle) continue;
                    if (u.unitType == unit.unitType) aiUnits.Add(u);
                    else playerUnits.Add(u);
                }
            }
        }
        else
        {
            foreach (var u in FindObjectsOfType<BattleUnit>())
            {
                if (u == null) continue;
                if (u.battleHp <= 0) continue;
                var pc = u.controller as PlayerController;
                if (pc != null && !pc.isOnBattle) continue;
                if (u.unitType == unit.unitType) aiUnits.Add(u);
                else playerUnits.Add(u);
            }
        }

        // Need at least one player unit to affect
        if (playerUnits.Count == 0)
        {
            yield return new WaitForSeconds(0.3f);
            yield break;
        }

        // find player with highest battleActPoint
        BattleUnit topPlayer = null;
        float bestAP = float.MinValue;
        foreach (var p in playerUnits)
        {
            if (p == null) continue;
            if (p.battleActPoint > bestAP)
            {
                bestAP = p.battleActPoint;
                topPlayer = p;
            }
        }

        var sys = tm != null ? tm.skillSystem : FindObjectOfType<SkillSystem>();
        if (sys == null)
        {
            Debug.LogWarning("BossController: no SkillSystem found");
            yield return new WaitForSeconds(0.3f);
            yield break;
        }

        // Helper to create a barrier at a world position that only affects players (Enemies relative to boss)
        System.Action<Vector3> createBarrierAt = (pos) =>
        {
            sys.PlaceBarrier<BossExplosiveDebuffBarrier>(pos, unit, x =>
            {
                x.durationTurns = 1;
                x.radius = barrierRadius * 0.75f; // Scale radius by 0.75 when placing
                x.explodeOnOwnerNextTurnStart = true;
                // Since owner is the boss (AI), using Enemies will target player-side units
                x.teamFilter = BarrierTeamFilter.Enemies;
                x.explodeDamage = barrierExplodeDamage;
                x.debuffDef = barrierDebuffDef;
                x.debuffSpd = barrierDebuffSpd;
            });
        };

        // place one barrier at boss's own position
        createBarrierAt(unit.transform.position);
        sfxPlayer.Play("light_magic");
        yield return new WaitForSeconds(0.3f);

        // place another barrier under the top player (highest AP) if found and not exactly same position
        if (topPlayer != null)
        {
            if (Vector3.Distance(unit.transform.position, topPlayer.transform.position) > 0.1f)
            {
                createBarrierAt(topPlayer.transform.position);
            }
        }
        sfxPlayer.Play("light_magic");
        //yield return new WaitForSeconds(0.4f);
    }
}
