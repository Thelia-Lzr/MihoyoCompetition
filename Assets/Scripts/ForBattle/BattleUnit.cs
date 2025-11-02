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

    [Header("基础属性")]
    [Tooltip("单位名称")]
    public string unitName;

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

    [Tooltip("单位行动值")]
    public int battleActPoint;

    [Tooltip("单位位置")]
    public Vector2 battlePos;

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
        battleDef = def;
        battleMagicDef = magicDef;
        battleSpd = spd;
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
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        battlePos = transform.position;
    }
}
