using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.Scripts.ForBattle.UI
{
    /// <summary>
    /// 战斗UI控制器，管理战斗菜单显示与输入
    /// </summary>
    public class BattleCanvasController : MonoBehaviour
    {
        [Header("UI References")]
        public Canvas battleCanvas;
        public GameObject actionMenuPanel;

        [Header("Info Display")]
        public TextMeshProUGUI unitNameText;
        public TextMeshProUGUI hpText;
        public TextMeshProUGUI actionPromptText;

        public GameObject UIs;

        public GameObject attackChosen;
        public GameObject swapChosen; // 原 itemsChosen 改为换人
        public GameObject escapeChosen;
        public GameObject skillChosen;

        public BattleActionType Choice;

        private System.Action<BattleActionType> onActionSelected;

        [Header("Skill List")]
        public SkillListController skillListController; // optional, assign in Inspector

        public enum BattleActionType : int
        {
            Attack = 0,
            Item = 1,   // 语义改为 换人，但保持索引兼容输入
            Escape = 2,
            Skill = 3,
        }

        public void Refresh()
        {
            attackChosen.SetActive(false);
            if (swapChosen != null) swapChosen.SetActive(false);
            escapeChosen.SetActive(false);
            skillChosen.SetActive(false);
            if (Choice == BattleActionType.Attack)
                attackChosen.SetActive(true);
            else if (Choice == BattleActionType.Item)
            {
                if (swapChosen != null) swapChosen.SetActive(true);
            }
            else if (Choice == BattleActionType.Escape)
                escapeChosen.SetActive(true);
            else if (Choice == BattleActionType.Skill)
                skillChosen.SetActive(true);

            // Show or hide skill list panel via controller
            if (skillListController != null)
            {
                if (Choice == BattleActionType.Skill)
                    skillListController.Show();
                else
                    skillListController.Hide();
            }
        }
        void Start()
        {
            // 不隐藏整个 Canvas，使行动条等常显
            HideUI();
            if (battleCanvas != null) battleCanvas.gameObject.SetActive(true);
        }

        /// <summary>
        /// 显示战斗UI并等待玩家选择行动
        /// </summary>
        public void ShowUI(BattleUnit unit, System.Action<BattleActionType> callback)
        {
            if (battleCanvas != null) battleCanvas.gameObject.SetActive(true); // 始终保持显示

            //UIs.SetActive(true);

            if (actionMenuPanel != null)
                actionMenuPanel.SetActive(true);

            // 更新单位信息显示
            if (unitNameText != null)
                unitNameText.text = unit.unitName;
            if (hpText != null)
                hpText.text = $"HP: {unit.battleHp}/{unit.battleMaxHp}";

            if (actionPromptText != null)
                actionPromptText.text = "选择行动 (Q/E 切换)";

            onActionSelected = callback;
        }

        public void HideUI()
        {
            //仅隐藏操作菜单，不隐藏主 Canvas（行动条等仍显示）
            if (actionMenuPanel != null) actionMenuPanel.SetActive(false);
            onActionSelected = null;
            //UIs.SetActive(false);

            if (skillListController != null)
                skillListController.Hide();
        }

        public void UpdatePrompt(string prompt)
        {
            if (actionPromptText != null)
                actionPromptText.text = prompt;
        }

        private void SelectAction(BattleActionType actionType)
        {
            onActionSelected?.Invoke(actionType);
        }

        void Update()
        {
            // 键盘快捷键支持（QE切换暂时用数字键代替）
            if (onActionSelected != null && actionMenuPanel != null && actionMenuPanel.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Q))
                    SelectAction(BattleActionType.Attack);
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.E))
                    SelectAction(BattleActionType.Skill);
                else if (Input.GetKeyDown(KeyCode.Alpha3))
                    SelectAction(BattleActionType.Item); // 换人
                else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Escape))
                    SelectAction(BattleActionType.Escape);
            }
        }
    }
}
