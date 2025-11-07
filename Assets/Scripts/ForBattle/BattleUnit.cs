using Assets.Scripts.ForBattle;
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

    [Header("基础属性")]
    [Tooltip("单位名称")]
    public string unitName;

    [Tooltip("单位图标（用于 UI 显示）")]
    public Sprite icon;

    [Tooltip("单位生命值")]
    public int maxhp;

    [Tooltip("单位当前生命值")]
    public int hp;

    [Tooltip("单位攻击力")]
    public int atk;

    [Tooltip("单位法术攻击力")]
    public int magicAtk;

    [Tooltip("单位物理防御力")]
    public int def;

    [Tooltip("单位法术防御力")]
    public int magicDef;

    [Tooltip("单位速度")]
    public int spd;

    [Tooltip("单位闪避")]
    public int evasion;

    [Tooltip("单位会心")]
    public int cri;

    [Header("战斗内属性")]

    [Tooltip("单位生命值")]
    public int battleMaxHp;

    [Tooltip("单位当前生命值")]
    public int battleHp;

    [Tooltip("单位攻击力")]
    public int battleAtk;

    [Tooltip("单位法术攻击力")]
    public int battleMagicAtk;

    [Tooltip("单位防御力")]
    public int battleDef;

    [Tooltip("单位法术防御力")]
    public int battleMagicDef;

    [Tooltip("单位速度")]
    public int battleSpd;

    [Tooltip("单位闪避")]
    public int battleEvasion;

    [Tooltip("单位会心")]
    public int battleCri;

    [Tooltip("单位行动值")]
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



    [Tooltip("单位位置")]
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
    [Tooltip("相机跟随/观察的根节点(可选)。若为空将使用单位根Transform")]
    public Transform cameraRoot;

    // Controller component (can be PlayerController, AIController, etc.)
    [HideInInspector]
    public BattleUnitController controller;

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
        battleActPoint = 0;

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
        var hpUi = GetComponent<BattleUnitHealthUI>();
        if (hpUi == null)
        {
            hpUi = gameObject.AddComponent<BattleUnitHealthUI>();
        }
        hpUi.unit = this;
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
        if (invisible > 0)
        {
            invisible -= 1;
        }
        if (faytUpAtk > 0)
        {
            faytUpAtk -= 1;
            battleAtk += Mathf.RoundToInt(atk * .2f);
        }
        if (faytDownDef > 0)
        {
            faytDownDef -= 1;
            battleDef -= Mathf.RoundToInt(def * .3f);
        }
        if (luminaUpCri > 0)
        {
            luminaUpCri -= 1;
            battleCri += 15;
        }
        if (luminaUpEvation > 0)
        {
            luminaUpEvation -= 1;
            battleEvasion += 15;
        }
        if (luminaDownMagicDef > 0)
        {
            luminaDownMagicDef -= 1;
            battleMagicDef -= Mathf.RoundToInt(magicDef * .4f);
        }
        if (luminaDownMagicAtk > 0)
        {
            luminaDownMagicAtk -= 1;
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
        
    }
}
