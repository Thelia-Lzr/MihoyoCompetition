using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 对话选项面板（TMP 版）：
/// - 在传入的选项集合上生成可点击/可高亮的条目
/// - 鼠标滚轮切换高亮，左键确认
/// </summary>
public class DialogueChoicePanel : MonoBehaviour
{
    [Header("Wiring")]
    public RectTransform contentRoot;
    public GameObject choiceItemPrefab; // 预制体内必须有 TMP_Text 文本

    [Header("Behavior")]
    public int scrollStep = 1;
    public bool wrapSelection = true;

    [Header("Colors")]
    [Tooltip("选项背景-普通")] public Color bgNormalColor = new Color(1, 1, 1, 0.10f);
    [Tooltip("选项背景-高亮")] public Color bgHighlightColor = new Color(1, 1, 1, 0.35f);
    [Tooltip("文字-普通")] public Color textNormalColor = Color.white;
    [Tooltip("文字-高亮")] public Color textHighlightColor = new Color(1f, 0.95f, 0.6f, 1f);

    private readonly List<GameObject> _items = new();
    private int _current = -1;
    private Action<int> _onChosen;

    // 不再在打开选项时改动鼠标状态
    private void OnEnable()
    {
        // intentionally left blank
    }

    public void Show(IList<string> choices, Action<int> onChosen)
    {
        gameObject.SetActive(true);
        _onChosen = onChosen;
        Clear();
        if (choices == null || choices.Count == 0)
        {
            Finish(-1);
            return;
        }
        for (int i = 0; i < choices.Count; i++)
        {
            var go = Instantiate(choiceItemPrefab, contentRoot);
            go.name = $"Choice_{i + 1}";
            var t = go.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.text = choices[i];
            var btn = go.GetComponentInChildren<Button>(true);
            int index = i;
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => Finish(index));
            }
            _items.Add(go);
        }
        SetCurrent(0);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        Clear();
        _onChosen = null;
        _current = -1;
    }

    private void Update()
    {
        if (!gameObject.activeInHierarchy || _items.Count == 0) return;
        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) > 0.001f)
        {
            int dir = wheel > 0 ? -1 : 1;
            Move(dir * scrollStep);
        }
        if (Input.GetMouseButtonDown(0))
        {
            Finish(_current);
        }
    }

    private void Move(int delta)
    {
        if (_items.Count == 0) return;
        int next = _current + delta;
        if (wrapSelection)
        {
            next = (next % _items.Count + _items.Count) % _items.Count;
        }
        else
        {
            next = Mathf.Clamp(next, 0, _items.Count - 1);
        }
        if (next != _current) SetCurrent(next);
    }

    private void SetCurrent(int index)
    {
        _current = Mathf.Clamp(index, 0, _items.Count - 1);
        for (int i = 0; i < _items.Count; i++)
        {
            var go = _items[i];
            bool active = i == _current;

            // 先设置所有非文本 Graphic 为背景色
            var graphics = go.GetComponentsInChildren<Graphic>(true);
            foreach (var g in graphics)
            {
                // 如果是 TMP_Text 或 UI.Text，跳过到下一轮专门设置
                if (g is TMP_Text || g is Text) continue;
                g.color = active ? bgHighlightColor : bgNormalColor;
            }

            // 再设置文本颜色，避免与背景相同
            var tmpTexts = go.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in tmpTexts)
            {
                t.color = active ? textHighlightColor : textNormalColor;
            }
            var uiTexts = go.GetComponentsInChildren<Text>(true);
            foreach (var t in uiTexts)
            {
                t.color = active ? textHighlightColor : textNormalColor;
            }
        }
    }

    private void Finish(int index)
    {
        _onChosen?.Invoke(index);
    }

    private void Clear()
    {
        foreach (var go in _items)
        {
            if (go != null) Destroy(go);
        }
        _items.Clear();
    }
}
