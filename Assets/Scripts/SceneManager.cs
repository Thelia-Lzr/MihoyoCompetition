using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SceneStatus has two possible values used to indicate the current scene mode.
/// Placed in the same file as SceneManager for simple visibility.
/// </summary>
public enum SceneStatus
{
    Normal,
    Battle
}

public class SceneManager : MonoBehaviour
{
    //[Header("绑定的gameobject")]
    //public GameObject CameraFocusObject;

    [Header("场景状态")]
    public SceneStatus currentStatus = SceneStatus.Normal;

    [Header("引用")]
    public BattleTurnManager battleTurnManager;

    IEnumerator testCoroutine()
    {
        // 第一步
        Debug.Log("开始");
        // 等待 2 秒
        yield return new WaitForSeconds(2f);
        // 第二步
        Debug.Log("2秒后继续");

        
        AddAllBattleUnitsToTurnOrder();
        foreach (var unit in battleTurnManager.turnOrder.GetAll())
        {
            Debug.Log("Turn Order Unit: " + unit.unitName);
            unit.AwakeBattleUnit();
        }
        setStatus(SceneStatus.Battle);
        battleTurnManager.StartBattle();
    }

    public void setStatus(SceneStatus newStatus)
    {
        //previousStatus = currentStatus;
        currentStatus = newStatus;
    }

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(testCoroutine());
    }


    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// 将场景中所有 BattleUnit（active）一次性加入到 BattleTurnManager 的 turnOrder
    /// 可在 Inspector 中点击某个按钮或在运行时调用
    /// </summary>
    public void AddAllBattleUnitsToTurnOrder()
    {
        if (battleTurnManager == null)
        {
            Debug.LogWarning("battleTurnManager 未绑定，无法添加单位到回合队列");
            return;
        }

        // 找到场景中所有活动的 BattleUnit
        BattleUnit[] units = FindObjectsOfType<BattleUnit>();
        if (units == null || units.Length == 0)
        {
            Debug.Log("场景中没有找到 BattleUnit");
            return;
        }

        // 清空已有队列（如果你不想清空可以移除这行）
        battleTurnManager.turnOrder.Clear();

        // 加入所有单位
        battleTurnManager.turnOrder.AddRange(units);

        Debug.Log($"已添加 {units.Length} 个 BattleUnit 到回合队列");
    }
}
