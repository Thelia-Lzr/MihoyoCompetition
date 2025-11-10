using Assets.Scripts.ForBattle.Audio;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrainingManagerNPC : ScenarioNPC
{
    public SfxPlayer sfx;
    protected override void Awake()
    {
        npcName = string.IsNullOrEmpty(npcName) ? "训练场经理" : npcName;
        enableChoices = true;
        base.Awake();
    }

    protected override IList<string> BuildDialogue(ScenarioContext context)
    {
        return new List<string>
        {
            "要来训练一下你们的战斗技巧吗？"
        };
    }

    protected override bool TryGetChoices(ScenarioContext context, int lineIndex, out IList<string> choices)
    {
        if (lineIndex == 0)
        {
            choices = new List<string>
            {
                "A：挑战“暴徒”（触发“普通战斗”）",
                "B：挑战“凶恶巨兽”（触发“Boss战斗”）",
                "C：还是算了（结束事件）"
            };
            return true;
        }
        choices = null;
        return false;
    }

    protected override bool OnChoiceSelected(ScenarioContext context, int lineIndex, int choiceIndex)
    {
        switch (choiceIndex)
        {
            case 0:
                ShowWorldPopup("即将开始：普通战斗（暴徒）", Color.yellow);
                // 跳转到 SampleScene
                sfx.StopAll();
                UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
                Debug.Log("加载场景 SampleScene 以进行普通战斗");
                break;
            case 1:
                ShowWorldPopup("即将开始：Boss战斗（凶恶巨兽）", Color.yellow);
                break;
            default:
                ShowWorldPopup("已取消训练。", Color.gray);
                break;
        }
        // 选择后结束对话
        return true;
    }
}
