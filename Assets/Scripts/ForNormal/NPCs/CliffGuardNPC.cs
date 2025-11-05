using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商队护卫克里夫：根据任务“送花”推进状态。
/// 任务变量键：Quest.DeliverFlower 状态：0=未接取, 1=进行中, 2=已完成(未领奖), 3=已结束
/// </summary>
public class CliffGuardNPC : ScenarioNPC
{
    private const string QuestKey = "Quest.DeliverFlower";

    protected override void Awake()
    {
        if (string.IsNullOrEmpty(npcName)) npcName = "克里夫";
        base.Awake();
    }

    protected override IList<string> BuildDialogue(ScenarioContext ctx)
    {
        int state = ctx.GetInt(QuestKey, 0);
        var lines = new List<string>();
        switch (state)
        {
            case 0: // 未接取
                lines.Add("……联盟和圣王国……呵，无论谁赢了，对我们这些小人物来说又有什么区别呢。");
                return lines;
            case 1: // 进行中
                lines.Add("……嗯？有什么事吗？我们认识吗？");
                lines.Add("这是……送给我的？星火花……呵，会送这种花的，也只有花店的莉娜了吧。她还记得我喜欢这个啊……");
                lines.Add("请替我谢谢她。并且……请告诉她，我决定再参加一次任务。不是为了佣金什么的，只是那个商队的人都是我的朋友，仅此而已。");
                return lines;
            case 2: // 已完成(未领奖)
                lines.Add("在出发前……我得先鼓起勇气去做另一件事。");
                lines.Add("至少，要亲口告诉她我的心意。");
                return lines;
            case 3: // 已结束
                lines.Add("在出发前……我得先鼓起勇气去做另一件事。");
                lines.Add("至少，要亲口告诉她我的心意。");
                return lines;
        }
        return lines;
    }

    protected override void OnDialogueFinished(ScenarioContext ctx)
    {
        int state = ctx.GetInt(QuestKey, 0);
        if (state == 1)
        {
            // 提交
            ctx.SetInt(QuestKey, 2);
            ShowWorldPopup("任务更新：送花(已完成)", Color.yellow);
        }
    }
}
