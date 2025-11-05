using System.Collections.Generic;
using UnityEngine;

public class VillagerNPC : ScenarioNPC
{
    protected override void Awake()
    {
        interactKey = KeyCode.F;
        advanceKey = KeyCode.F;
        interactPromptText = "按 F 交互";
        if (string.IsNullOrEmpty(npcName)) npcName = "村民";
        base.Awake();
    }

    protected override IList<string> BuildDialogue(ScenarioContext context)
    {
        return new List<string>
        {
            "欢迎来到加拉诺镇，你们……看起来不像是本地人吧",
            "据说最近圣王国和联盟之间又开始不太平了，来这里歇脚的商队也少了许多"
        };
    }
}