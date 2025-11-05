using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 装备/武器商人：纯对白，无选项与变量修改。
/// </summary>
public class WeaponMerchantNPC : ScenarioNPC
{
    protected override void Awake()
    {
        if (string.IsNullOrEmpty(npcName)) npcName = "装备商人";
        base.Awake();
    }

    protected override IList<string> BuildDialogue(ScenarioContext context)
    {
        return new List<string>
        {
            "我这里都是圣王国生产的优质武器",
            "（好像钱没带够）",
            "不买吗？唉，最近形势紧张起来，来换新武器的佣兵应该会更多才对。"
        };
    }
}
