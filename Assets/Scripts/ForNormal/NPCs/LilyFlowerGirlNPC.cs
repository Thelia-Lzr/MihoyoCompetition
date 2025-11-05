using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 花店女孩莉莉：基于变量的任务对白（任务：送花）。
/// 任务变量键：Quest.DeliverFlower 状态：0=未接取, 1=进行中, 2=已完成(未领奖), 3=已结束
/// </summary>
public class LilyFlowerGirlNPC : ScenarioNPC
{
    private const string QuestKey = "Quest.DeliverFlower";

    protected override void Awake()
    {
        if (string.IsNullOrEmpty(npcName)) npcName = "花店女孩莉莉";
        base.Awake();
    }

    protected override IList<string> BuildDialogue(ScenarioContext ctx)
    {
        int state = ctx.GetInt(QuestKey, 0);
        var lines = new List<string>();
        switch (state)
        {
            case 0: // 未接取
                lines.Add("欢迎光临……啊，是生面孔呢。这些‘星火花’很漂亮吧？在傍晚时会像星星一样微微发光哦。");
                lines.Add("那个……旅行者大人，可以请您帮个忙吗？");
                lines.Add("看到那边长椅上的年轻人了吗？他叫克里夫，最近有点消沉……我想鼓励他一下。");
                lines.Add("能请您帮我把这个送给他吗？就说是……是“一个朋友”送的就好。");
                return lines;
            case 1: // 进行中
                lines.Add("他……收到花了吗？希望这束花能让他振作起来。");
                return lines;
            case 2: // 已完成，未领奖
                lines.Add("怎么样？他喜欢吗？他说了什么吗？");
                lines.Add("他重新振作起来了？太好了！谢谢你，旅行者！");
                lines.Add("这是我的一点心意，请务必收下！");
                return lines;
            case 3: // 已结束
                lines.Add("您说，我下次主动和他打招呼，该说些什么好呢？");
                return lines;
        }
        return lines;
    }

    protected override void OnDialogueFinished(ScenarioContext ctx)
    {
        int state = ctx.GetInt(QuestKey, 0);
        switch (state)
        {
            case 0:
                // 接取任务
                ctx.SetInt(QuestKey, 1);
                ShowWorldPopup("获得：星火花束", Color.cyan);
                break;
            case 2:
                // 领奖，结束
                ctx.SetInt(QuestKey, 3);
                ShowWorldPopup("任务完成：送花", Color.yellow);
                break;
        }
    }
}
