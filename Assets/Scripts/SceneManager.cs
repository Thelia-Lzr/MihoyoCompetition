using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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

    [Header("系统预制体(可选)")]
    [Tooltip("包含 BattleTurnManager / SkillSystem / BattleIndicatorManager 等系统的预制体。若场景中找不到系统，将优先实例化此预制体。")]
    public GameObject systemsPrefab;
    [Tooltip("备用 Resources 路径(例如 Prefabs/BattleSystems)。当 systemsPrefab 为空时会尝试 Resources.Load 实例化。")]
    public string resourcesPrefabPath = "Prefabs/BattleSystems";

    private void Awake()
    {
        EnsureBattleTurnManager();
    }

    private void EnsureBattleTurnManager()
    {
        // 1) 优先找活动对象
        if (battleTurnManager == null)
        {
            battleTurnManager = FindObjectOfType<BattleTurnManager>();
        }

        // 2) 包含未激活对象
        if (battleTurnManager == null)
        {
            try
            {
#if UNITY_2020_1_OR_NEWER
                var all = FindObjectsOfType<BattleTurnManager>(true); // includeInactive
                if (all != null && all.Length > 0)
                {
                    BattleTurnManager candidate = null;
                    foreach (var m in all)
                    {
                        if (m != null)
                        {
                            if (m.gameObject.activeInHierarchy) { candidate = m; break; }
                            if (candidate == null) candidate = m;
                        }
                    }
                    battleTurnManager = candidate;
                    if (battleTurnManager != null && !battleTurnManager.gameObject.activeSelf)
                    {
                        battleTurnManager.gameObject.SetActive(true);
                    }
                }
#else
                var all = Resources.FindObjectsOfTypeAll(typeof(BattleTurnManager)) as BattleTurnManager[];
                if (all != null && all.Length > 0)
                {
                    BattleTurnManager candidate = null;
                    foreach (var m in all)
                    {
                        if (m == null) continue;
                        // 过滤掉不是场景对象的(如 Prefab 资产)
                        if (m.gameObject.scene.IsValid())
                        {
                            if (m.gameObject.activeInHierarchy) { candidate = m; break; }
                            if (candidate == null) candidate = m;
                        }
                    }
                    battleTurnManager = candidate;
                    if (battleTurnManager != null && !battleTurnManager.gameObject.activeSelf)
                    {
                        battleTurnManager.gameObject.SetActive(true);
                    }
                }
#endif
            }
            catch { }
        }

        // 3) 仍未找到则优先实例化系统预制体
        if (battleTurnManager == null)
        {
            GameObject prefabToSpawn = systemsPrefab;
            if (prefabToSpawn == null && !string.IsNullOrEmpty(resourcesPrefabPath))
            {
                var res = Resources.Load<GameObject>(resourcesPrefabPath);
                if (res != null) prefabToSpawn = res;
            }
            if (prefabToSpawn != null)
            {
                var instance = Instantiate(prefabToSpawn);
                instance.name = prefabToSpawn.name + "(Instanced)";
                // 预制体里应当包含 BattleTurnManager
                battleTurnManager = instance.GetComponentInChildren<BattleTurnManager>(true);
                if (battleTurnManager == null)
                {
                    // 再全局找一次(有时挂在根上)
                    battleTurnManager = FindObjectOfType<BattleTurnManager>();
                }
                Debug.LogWarning("SceneManager: 运行时实例化系统预制体。");
            }
        }

        // 4) 仍未找到则动态创建最小可运行管理器
        if (battleTurnManager == null)
        {
            var go = new GameObject("BattleTurnManager(Auto)");
            battleTurnManager = go.AddComponent<BattleTurnManager>();
            Debug.LogWarning("SceneManager: 运行时自动创建 BattleTurnManager。");
        }

        // 兜底 turnOrder
        if (battleTurnManager.turnOrder == null)
        {
            battleTurnManager.turnOrder = new BattleUnitPriorityQueue();
        }
    }

    IEnumerator testCoroutine()
    {
        EnsureBattleTurnManager();
        if (battleTurnManager == null)
        {
            Debug.LogError("SceneManager: BattleTurnManager 仍未找到，无法开始战斗。");
            yield break;
        }

        AddAllBattleUnitsToTurnOrder();
        // 容错：turnOrder 或 GetAll 为空时不遍历
        if (battleTurnManager.turnOrder != null)
        {
            var list = battleTurnManager.turnOrder.GetAll();
            if (list != null)
            {
                foreach (var unit in list)
                {
                    if (unit == null) continue;
                    Debug.Log("Turn Order Unit: " + unit.unitName);
                    unit.AwakeBattleUnit();
                }
            }
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

        // 确保队列存在
        if (battleTurnManager.turnOrder == null)
        {
            battleTurnManager.turnOrder = new BattleUnitPriorityQueue();
        }

        // 清空已有队列（如果你不想清空可以移除这行）
        battleTurnManager.turnOrder.Clear();

        // 只加入处于战斗中的角色（PlayerController 的 isOnBattle == true）或非 PlayerController 单位
        var filtered = new List<BattleUnit>();
        foreach (var u in units)
        {
            if (u == null) continue;
            // Try to find PlayerController component directly on the GameObject; BattleUnit.controller may not be initialized yet
            var pcComp = u.GetComponent<PlayerController>();
            if (pcComp != null)
            {
                if (pcComp.isOnBattle) filtered.Add(u);
            }
            else
            {
                // non-player units (AI/enemy) are considered on-battle
                filtered.Add(u);
            }
        }

        battleTurnManager.turnOrder.AddRange(filtered);

        Debug.Log($"已添加 {filtered.Count} 个 BattleUnit 到回合队列");
    }
}
