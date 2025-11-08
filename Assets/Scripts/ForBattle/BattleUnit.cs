using Assets.Scripts.ForBattle;
using Assets.Scripts.ForBattle.Barriers;
using Assets.Scripts.ForBattle.Indicators;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BattleUnitType
{
    Player,
    Enemy
}
public class BattleUnit : MonoBehaviour
{
    public BattleUnitType unitType;
    public BattleUnitController thisController;
    public SkillSystem skillSystem;
    public BattleIndicatorManager indicatorManager;

    [Header("Base Attributes")]
    [Tooltip("Unit name")]
    public string unitName;

    [Tooltip("Unit icon (for UI)")]
    public Sprite icon;

    [Tooltip("Max HP")]
    public int maxhp;

    [Tooltip("Current HP")]
    public int hp;

    [Tooltip("Attack")]
    public int atk;

    [Tooltip("Magic Attack")]
    public int magicAtk;

    [Tooltip("Defense")]
    public int def;

    [Tooltip("Magic Defense")]
    public int magicDef;

    [Tooltip("Speed")]
    public int spd;

    [Tooltip("Evasion")]
    public int evasion;

    [Tooltip("Critical")]
    public int cri;

    [Header("Battle Attributes")]

    [Tooltip("Battle Max HP")]
    public int battleMaxHp;

    [Tooltip("Battle Current HP")]
    public int battleHp;

    [Tooltip("Battle Attack")]
    public int battleAtk;

    [Tooltip("Battle Magic Attack")]
    public int battleMagicAtk;

    [Tooltip("Battle Defense")]
    public int battleDef;

    [Tooltip("Battle Magic Defense")]
    public int battleMagicDef;

    [Tooltip("Battle Speed")]
    public int battleSpd;

    [Tooltip("Battle Evasion")]
    public int battleEvasion;

    [Tooltip("Battle Critical")]
    public int battleCri;

    [Tooltip("Battle Action Point")]
    public int battleActPoint;

    [Header("Buffs")]
    public int causality;
    public int invisible;
    public int shield;

    public int faytUpAtk;

    public int faytDownDef;

    public int luminaUpCri;
    public int luminaUpEvation;
    public int luminaUpDef;
    public int luninaUpHp;
    public int luminaUpAtk;

    public int luminaExtraBattlePoint;

    public int luminaDownMagicDef;
    public int luminaDownMagicAtk;



    [Tooltip("Battle Position")]
    public Vector2 battlePos;

    [Header("Timed Buffs/Debuffs")]
    // Buff remaining turns
    public int buffTurns_EvasionUp;
    public int buffTurns_CritUp;
    public int buffTurns_DefUp;
    public int buffTurns_Regen;
    public int buffTurns_AttackUp;
    public int buffTurns_SpdUp;

    // Debuff remaining turns
    public int debuffTurns_MagicDefDown;
    public int debuffTurns_MagicAtkDown;

    // Accumulated deltas to revert when turns expire
    public int deltaSpdDef;
    public int deltaCri;
    public int deltaDef;
    public int deltaAtk;
    public int deltaSpd;
    public int deltaMagicDef;
    public int deltaMagicAtk;

    // Regen per turn value (used while buffTurns_Regen >0)
    public int regenPerTurn;

    [Header("Camera Follow")]
    [Tooltip("Optional camera root. If null uses unit transform")]
    public Transform cameraRoot;

    // Controller component (can be PlayerController, AIController, etc.)
    [HideInInspector]
    public BattleUnitController controller;

    [Header("Floating HP Text Settings")]
    [Tooltip("Vertical offset above unit for HP text")]
    public float hpTextHeightOffset =2f;
    [Tooltip("HP text font size")]
    public int hpTextFontSize =18;
    [Tooltip("Enable floating HP text over unit")] 
    public bool enableFloatingHpText = true;
    private TextMesh _hpTextMesh;

    private BarrierContribution _lastBarrierContribution; // 上一帧应用的结界加成（用于差分撤销）

    public void AwakeBattleUnit()
    {
        battleMaxHp = maxhp;
        battleHp = hp;
        battleAtk = atk;
        battleMagicAtk = magicAtk;
        battleDef = def;
        battleMagicDef = magicDef;
        battleSpd = spd;
        battleCri = cri;
        battleEvasion = evasion;
        battleActPoint =0;

        // Try to get a controller component on the same GameObject and bind
        if (controller == null)
        {
            controller = GetComponent<BattleUnitController>();
            if (controller != null)
            {
                controller.Bind(this);
            }
        }

        // Ensure head-top HP UI exists on all units
        EnsureHpText();
    }

    private void EnsureHpText()
    {
        if (!enableFloatingHpText) return;
        if (_hpTextMesh != null) return;
        // 创建一个子物体用于显示血量
        GameObject go = new GameObject("HpText");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.up * hpTextHeightOffset;
        _hpTextMesh = go.AddComponent<TextMesh>();
        _hpTextMesh.fontSize = hpTextFontSize; // 字体大小
        _hpTextMesh.color = Color.red; // 红色
        _hpTextMesh.anchor = TextAnchor.MiddleCenter;
        _hpTextMesh.alignment = TextAlignment.Center;
        _hpTextMesh.characterSize =0.08f; // 控制整体缩放（可调）
        _hpTextMesh.text = "";
    }

    private void UpdateHpFloatingText()
    {
        if (!enableFloatingHpText) return;
        if (_hpTextMesh == null) EnsureHpText();
        if (_hpTextMesh == null) return;
        _hpTextMesh.text = $"{battleHp}/{battleMaxHp}";
        // 始终朝向主摄像机（简易 Billboard）
        var cam = Camera.main;
        if (cam != null)
        {
            //只旋转Y以保持文字竖直
            Vector3 camForward = cam.transform.forward;
            camForward.y =0f;
            if (camForward.sqrMagnitude >0.001f)
            {
                _hpTextMesh.transform.rotation = Quaternion.LookRotation(camForward);
            }
        }
    }

    private void ApplyBarrierEffects()
    {
        // 移除上一帧加成
        if (!_lastBarrierContribution.EqualsTo(BarrierContribution.Zero))
        {
            battleAtk -= _lastBarrierContribution.atk;
            battleDef -= _lastBarrierContribution.def;
            battleMagicAtk -= _lastBarrierContribution.magicAtk;
            battleMagicDef -= _lastBarrierContribution.magicDef;
            battleEvasion -= _lastBarrierContribution.evasion;
            battleCri -= _lastBarrierContribution.cri;
        }

        BarrierContribution total = BarrierContribution.Zero;
        foreach (var barrier in BarrierBase.ActiveBarriers)
        {
            if (barrier == null) continue;
            if (!barrier.IsUnitAffected(this)) continue;
            var add = barrier.EvaluateContribution(this);
            total = BarrierContribution.Add(total, add);
        }

        battleAtk += total.atk;
        battleDef += total.def;
        battleMagicAtk += total.magicAtk;
        battleMagicDef += total.magicDef;
        battleEvasion += total.evasion;
        battleCri += total.cri;
        _lastBarrierContribution = total;
    }

    public void Flush()
    {
        battleAtk = atk;
        battleMagicAtk = magicAtk;
        battleDef = def;
        battleMagicDef = magicDef;
        battleSpd = spd;
        battleCri = cri;
        battleEvasion = evasion;
        // 清空上一帧结界加成，避免回合刷新后在 Update 中被误减
        _lastBarrierContribution = BarrierContribution.Zero;
        if (invisible >0)
        {
            invisible -=1;
        }
        if (faytUpAtk >0)
        {
            faytUpAtk -=1;
            battleAtk += Mathf.RoundToInt(atk * .2f);
            faytDownDef -=1;
            battleDef -= Mathf.RoundToInt(def * .3f);
        }
        if (luminaUpCri >0)
        {
            luminaUpCri -=1;
            battleCri +=15;
        }
        if (luminaUpEvation >0)
        {
            luminaUpEvation -=1;
            battleEvasion +=15;
        }
        if (luminaDownMagicDef >0)
        {
            luminaDownMagicDef -=1;
            battleMagicDef -= Mathf.RoundToInt(magicDef * .4f);
        }
        if (luminaDownMagicAtk >0)
        {
            luminaDownMagicAtk -=1;
            battleMagicAtk -= Mathf.RoundToInt(magicAtk * .4f);
        }
    }

    public void EndBattle()
    {
        Destroy(this.gameObject);
    }
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        UpdateHpFloatingText();
        ApplyBarrierEffects();
    }
}