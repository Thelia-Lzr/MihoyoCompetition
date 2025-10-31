using System.Collections;
using UnityEngine;

/// <summary>
/// 简易 AI Controller 示例：选取第一个可用目标并执行一段模拟动作。
/// 可根据需要替换为更复杂的决策逻辑。
/// </summary>
public class AIController : BattleUnitController
{
    public float actionDuration = 0.8f;

    public override IEnumerator ExecuteTurn(BattleTurnManager turnManager)
    {
        if (unit == null || turnManager == null)
        {
            yield break;
        }

        Debug.Log($"[AIController] {unit.unitName} 开始 AI 回合");

        //选择目标：在 turnOrder 中找到与自己不同的第一个单位
        BattleUnit target = null;
        var list = turnManager.turnOrder.GetAll();
        foreach (var u in list)
        {
            if (u != null && u != unit)
            {
                target = u;
                break;
            }
        }

        // 如果找到了目标，面向目标并播放动作镜头（如果 cameraController 可用）
        if (target != null)
        {
            Debug.Log($"[AIController] {unit.unitName}目标为 {target.unitName}");
            if (turnManager.cameraController != null)
            {
                // 短暂切换动作镜头然后回到聚焦
                yield return turnManager.cameraController.PlayActionCam(unit.transform, target.transform, new Vector3(0f, 1.0f, -1.5f), actionDuration);
            }
            else
            {
                yield return new WaitForSeconds(actionDuration);
            }
        }
        else
        {
            // 没有目标就等一小段时间
            yield return new WaitForSeconds(0.2f);
        }

        Debug.Log($"[AIController] {unit.unitName} 回合结束");
    }
}
