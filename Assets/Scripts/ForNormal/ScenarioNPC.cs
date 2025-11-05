using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.ForBattle.Audio;

/// <summary>
/// 可对话 NPC 的基类：
/// - 距离检测 + 按键交互（默认 F）
/// - 使用对话框 UI（DialoguePanel/Name/Dialogue）显示内容
/// - 支持基于 UI 的选项选择（依赖 DialogueChoicePanel）
/// 子类可覆写 BuildDialogue/OnInteract/OnDialogueFinished/TryGetChoices/OnChoiceSelected 实现自定义逻辑。
/// </summary>
public abstract class ScenarioNPC : MonoBehaviour
{
    [Header("基本信息")]
    public string npcName = "NPC";

    [Header("交互设置")]
    [Tooltip("与玩家交互的半径")] public float interactRadius = 2.5f;
    [Tooltip("交互按键")] public KeyCode interactKey = KeyCode.F;
    [Tooltip("按键推进下一句")] public KeyCode advanceKey = KeyCode.F;
    [Tooltip("每句对白的默认显示时长(秒)，为0表示必须按键推进")] public float autoAdvanceSeconds = 0f;

    [Header("玩家引用(可选)")]
    public Transform player; // 若为空将自动查找带有 WASDPlayerController 或 "Player" Tag 的对象

    [Header("提示显示(可选)")]
    [Tooltip("靠近提示相对NPC的位置偏移")] public Vector3 promptOffset = new Vector3(0, 1.8f, 0);
    [Tooltip("提示文本大小")] public int promptFontSize = 48;
    [Tooltip("靠近提示的文本")] public string interactPromptText = "按 F 交互";

    [Header("UI 接线")]
    [Tooltip("对话面板(必须包含 Name 和 Dialogue 子对象)")] public GameObject dialoguePanel;
    [Tooltip("Name 文本(TMP，可空)")] public TMP_Text uiNameText;
    [Tooltip("Dialogue 文本(TMP，可空)")] public TMP_Text uiDialogueText;

    [Header("选项设置")]
    [Tooltip("选项 UI 面板组件")] public DialogueChoicePanel choicePanel;
    [Tooltip("是否启用对白选项功能")] public bool enableChoices = false;

    [Header("动画(可选)")]
    [Tooltip("NPC 的 Animator，留空则在自身或子物体中自动查找")] public Animator npcAnimator;
    [Tooltip("在 Awake 时强制进入待机")] public bool forceIdleOnAwake = true;
    [Tooltip("表示移动或待机的布尔参数名(为 true 表示移动) 默认 IsMoving")] public string animMovingBoolParam = "IsMoving";
    [Tooltip("表示速度的浮点参数名，默认 Speed")] public string animSpeedParam = "Speed";
    [Tooltip("可选：待机触发器或布尔参数名，默认 Idle")] public string animIdleParam = "Idle";

    protected bool isPlayerInRange;
    protected bool isTalking;

    private GameObject promptGO;
    private TextMesh promptText;
    private Coroutine talkRoutine;

    // 记录开始对话的帧，避免同一按键触发立即跳过首句
    private int _talkStartFrame = -1;

    protected virtual void Awake()
    {
        if (player == null)
        {
            var pc = FindObjectOfType<WASDPlayerController>();
            if (pc != null) player = pc.transform;
            if (player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }
        }

        // 创建交互提示（世界空间 TextMesh）
        promptGO = new GameObject("InteractPrompt");
        promptGO.transform.SetParent(transform);
        promptGO.transform.localPosition = promptOffset;
        promptText = promptGO.AddComponent<TextMesh>();
        promptText.text = interactPromptText;
        promptText.fontSize = Mathf.Max(12, promptFontSize);
        promptText.characterSize = 0.04f;
        promptText.alignment = TextAlignment.Center;
        promptText.anchor = TextAnchor.LowerCenter;
        SetPromptVisible(false);

        EnsureDialoguePanelWired();
        SetDialogueVisible(false);
        if (choicePanel == null) choicePanel = FindObjectOfType<DialogueChoicePanel>(includeInactive: true);

        // 动画：进入待机
        if (npcAnimator == null) npcAnimator = GetComponentInChildren<Animator>();
        if (forceIdleOnAwake) ApplyIdleState();
    }

    protected virtual void OnDestroy()
    {
        if (promptGO != null) Destroy(promptGO);
    }

    protected virtual void Update()
    {
        // 让提示面向相机
        FaceToCamera(promptGO);

        // 距离检测
        isPlayerInRange = CheckPlayerInRange();
        SetPromptVisible(isPlayerInRange && !isTalking);

        if (isPlayerInRange && Input.GetKeyDown(interactKey) && !isTalking)
        {
            TryStartTalk();
        }

        // 对话进行中允许推进(忽略启动对话的同一帧)
        if (isTalking && autoAdvanceSeconds <= 0f && Time.frameCount > _talkStartFrame && Input.GetKeyDown(advanceKey))
        {
            _requestAdvance = true;
        }
    }

    private void ApplyIdleState()
    {
        if (npcAnimator == null) return;
        // Speed = 0
        TrySetFloat(animSpeedParam, 0f);
        // IsMoving = false
        TrySetBool(animMovingBoolParam, false);
        // Idle: 触发器或布尔
        if (!TrySetTrigger(animIdleParam))
        {
            TrySetBool(animIdleParam, true);
        }
    }

    private bool HasParam(string name, AnimatorControllerParameterType type)
    {
        if (npcAnimator == null || string.IsNullOrEmpty(name)) return false;
        foreach (var p in npcAnimator.parameters)
        {
            if (p.name == name && p.type == type) return true;
        }
        return false;
    }

    private void TrySetFloat(string name, float value)
    {
        if (HasParam(name, AnimatorControllerParameterType.Float))
        {
            npcAnimator.SetFloat(name, value);
        }
    }

    private void TrySetBool(string name, bool value)
    {
        if (HasParam(name, AnimatorControllerParameterType.Bool))
        {
            npcAnimator.SetBool(name, value);
        }
    }

    private bool TrySetTrigger(string name)
    {
        if (HasParam(name, AnimatorControllerParameterType.Trigger))
        {
            npcAnimator.ResetTrigger(name);
            npcAnimator.SetTrigger(name);
            return true;
        }
        return false;
    }

    private bool CheckPlayerInRange()
    {
        if (player == null) return false;
        return Vector3.Distance(player.position, transform.position) <= interactRadius;
    }

    private void FaceToCamera(GameObject go)
    {
        if (go == null) return;
        Camera cam = Camera.main ?? (Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null);
        if (cam == null) return;
        Vector3 dir = go.transform.position - cam.transform.position;
        if (dir.sqrMagnitude > 0.0001f)
        {
            go.transform.rotation = Quaternion.LookRotation(dir, cam.transform.up);
        }
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptGO != null)
        {
            promptGO.SetActive(visible);
            if (visible && promptText != null) promptText.text = interactPromptText;
        }
    }

    private void SetDialogueVisible(bool visible)
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(visible);
    }

    protected void TryStartTalk()
    {
        if (isTalking) return;
        var ctx = ScenarioContext.Current;
        var lines = BuildDialogue(ctx);
        if (lines == null || lines.Count == 0)
        {
            OnInteract(ctx);
            return;
        }
        isTalking = true;
        _talkStartFrame = Time.frameCount; // 标记开始帧，避免误触发推进
        SetPromptVisible(false);

        // SFX: 交互开始时播放 one-shot ChangeChoice（若存在）
        if (SfxPlayer.Instance != null)
        {
            SfxPlayer.Instance.Play("ChangeChoice");
        }

        if (talkRoutine != null) StopCoroutine(talkRoutine);
        talkRoutine = StartCoroutine(RunDialogue(lines, ctx));
    }

    private bool _requestAdvance = false;
    private IEnumerator RunDialogue(IList<string> lines, ScenarioContext ctx)
    {
        EnsureDialoguePanelWired();
        SetNameText(npcName);
        SetDialogueVisible(true);
        _requestAdvance = false;

        for (int i = 0; i < lines.Count; i++)
        {
            // 显示对白到 UI
            SetDialogueText(lines[i]);

            // 选项逻辑：使用 UI 面板
            if (enableChoices && choicePanel != null && TryGetChoices(ctx, i, out var choices) && choices != null && choices.Count > 0)
            {
                int chosen = -1;
                bool done = false;
                choicePanel.Show(choices, index => { chosen = index; done = true; });
                while (!done)
                {
                    yield return null;
                }
                choicePanel.Hide();
                bool end = OnChoiceSelected(ctx, i, chosen);
                if (end) break;
                continue;
            }

            // 无选项：按配置推进
            if (autoAdvanceSeconds > 0f)
            {
                float t = 0f;
                _requestAdvance = false;
                while (t < autoAdvanceSeconds && !_requestAdvance)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                _requestAdvance = false;
                while (!_requestAdvance)
                {
                    yield return null;
                }
            }
        }

        OnDialogueFinished(ctx);

        isTalking = false;
        SetDialogueText(string.Empty);
        SetDialogueVisible(false);
        SetPromptVisible(isPlayerInRange);
    }

    private void EnsureDialoguePanelWired()
    {
        if (dialoguePanel == null)
        {
            var found = GameObject.Find("DialoguePanel");
            if (found != null) dialoguePanel = found;
        }
        if (dialoguePanel == null) return;

        // 查找子对象 Name / Dialogue 并绑定 TMP 组件
        Transform nameT = FindChildDeep(dialoguePanel.transform, "Name");
        Transform dialogT = FindChildDeep(dialoguePanel.transform, "Dialogue");

        if (uiNameText == null && nameT != null)
        {
            uiNameText = nameT.GetComponent<TMP_Text>();
        }
        if (uiDialogueText == null && dialogT != null)
        {
            uiDialogueText = dialogT.GetComponent<TMP_Text>();
        }
    }

    private Transform FindChildDeep(Transform root, string childName)
    {
        if (root == null) return null;
        if (root.name == childName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var c = FindChildDeep(root.GetChild(i), childName);
            if (c != null) return c;
        }
        return null;
    }

    private void SetNameText(string value)
    {
        if (uiNameText != null) uiNameText.text = value;
    }

    private void SetDialogueText(string value)
    {
        if (uiDialogueText != null) uiDialogueText.text = value;
    }

    protected virtual string FormatLine(string speaker, string content)
    {
        return content;
    }

    // 接口：子类覆写
    protected virtual IList<string> BuildDialogue(ScenarioContext context) { return new List<string>(); }
    protected virtual void OnInteract(ScenarioContext context) {}
    protected virtual void OnDialogueFinished(ScenarioContext context) {}
    protected virtual bool TryGetChoices(ScenarioContext context, int lineIndex, out IList<string> choices) { choices = null; return false; }
    protected virtual bool OnChoiceSelected(ScenarioContext context, int lineIndex, int choiceIndex) { return false; }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }

    // 公用：在 NPC 头顶显示一条渐隐的世界空间文本
    protected void ShowWorldPopup(string text, Color? color = null, float duration = 1.2f, float rise = 1.0f)
    {
        StartCoroutine(PopupRoutine(text, color ?? Color.yellow, duration, rise));
    }

    private IEnumerator PopupRoutine(string text, Color color, float duration, float rise)
    {
        GameObject go = new GameObject("NPCPopup");
        go.transform.position = transform.position + Vector3.up * 2.0f;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 64;
        tm.color = color;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.characterSize = 0.04f;

        Camera cam = Camera.main;
        if (cam == null && Camera.allCamerasCount > 0) cam = Camera.allCameras[0];

        Vector3 startPos = go.transform.position;
        Vector3 endPos = startPos + Vector3.up * rise;
        Color startColor = tm.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            go.transform.position = Vector3.Lerp(startPos, endPos, 1f - (1f - t) * (1f - t));
            if (cam != null)
            {
                Vector3 dir = go.transform.position - cam.transform.position;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    go.transform.rotation = Quaternion.LookRotation(dir, cam.transform.up);
                }
            }
            var c = startColor; c.a = Mathf.Lerp(1f, 0f, t);
            tm.color = c;
            yield return null;
        }
        Destroy(go);
    }
}

