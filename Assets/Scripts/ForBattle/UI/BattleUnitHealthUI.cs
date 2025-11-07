using UnityEngine;
using TMPro;

/// <summary>
/// 在单位头顶显示 “当前血量/满血” 文本的简易组件。
/// 自动创建一个世界空间 TextMeshPro 文本并跟随单位朝向相机。
/// </summary>
[DisallowMultipleComponent]
public class BattleUnitHealthUI : MonoBehaviour
{
    [Tooltip("绑定的战斗单位")] public BattleUnit unit;
    [Tooltip("文本在单位头顶的偏移")]
    public Vector3 worldOffset = new Vector3(0, 2.0f, 0);
    [Tooltip("字体大小")] public float fontSize = 5f;
    [Tooltip("文本颜色")] public Color textColor = Color.white;
    [Tooltip("是否始终朝向主相机")] public bool faceCamera = true;

    private TextMeshPro _tmp;
    private Transform _billboard;

    private void Awake()
    {
        if (unit == null) unit = GetComponent<BattleUnit>();
        // 创建承载文本的节点
        var go = new GameObject("HP_Text");
        _billboard = go.transform;
        _billboard.SetParent(transform, false);
        _billboard.localPosition = worldOffset;

        _tmp = go.AddComponent<TextMeshPro>();
        _tmp.text = "";
        _tmp.fontSize = fontSize;
        _tmp.color = textColor;
        _tmp.alignment = TextAlignmentOptions.Center;
        _tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        _tmp.enableWordWrapping = false;
        _tmp.richText = false;
        _tmp.sortingOrder = 100; // 排在前面
    }

    private void LateUpdate()
    {
        if (unit != null)
        {
            // 实时显示 battleHp/battleMaxHp；若未初始化则用 hp/maxhp
            int cur = unit.battleHp > 0 ? unit.battleHp : unit.hp;
            int max = unit.battleMaxHp > 0 ? unit.battleMaxHp : unit.maxhp;
            _tmp.text = $"{cur}/{max}";
        }

        if (faceCamera)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                var dir = _billboard.position - cam.transform.position;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    _billboard.rotation = Quaternion.LookRotation(dir, cam.transform.up);
                }
            }
        }
    }
}
