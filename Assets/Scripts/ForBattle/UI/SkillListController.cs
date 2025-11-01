using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Assets.Scripts.ForBattle.Audio;

namespace Assets.Scripts.ForBattle.UI
{
 /// <summary>
 /// 简单技能列表控制器接口。
 /// - 在 Inspector 中绑定一个 Panel（世界坐标或 UI），并在内部配置多个 Text 元素或使用一个模板生成。
 /// - 提供外部 API：Show(), Hide(), SetSkills(List<string>), SelectIndex(int), Next(), Prev(), GetSelectedIndex(), GetSelectedSkill()
 /// - 支持鼠标滚轮切换（在 Update 中根据 Input.GetAxis("Mouse ScrollWheel") 切换）
 /// </summary>
 public class SkillListController : MonoBehaviour
 {
 [Header("UI")]
 public GameObject panel; // 整个面板
 public RectTransform contentRoot; // 容器，用于填充文本或按钮
 public GameObject itemPrefab; // 一个包含 TextMeshProUGUI 的预制，用于复用

 [Header("Settings")]
 public int visibleItems =5;
 public float itemSpacing =24f;

 private List<string> skills = new List<string>();
 private List<GameObject> itemInstances = new List<GameObject>();
 private int selectedIndex =0;

 void Awake()
 {
 if (panel != null) panel.SetActive(false);
 }

 void Update()
 {
 if (panel == null || !panel.activeSelf) return;

 float scroll = Input.GetAxis("Mouse ScrollWheel");
 if (Mathf.Abs(scroll) >0.001f)
 {
 if (scroll >0f) Prev(); else Next();
 }
 }

 public void Show()
 {
 if (panel != null) panel.SetActive(true);
 RefreshUI();
 }

 public void Hide()
 {
 if (panel != null) panel.SetActive(false);
 }

 public void SetSkills(List<string> list)
 {
 skills = list ?? new List<string>();
 selectedIndex =0;
 RebuildItems();
 RefreshUI();
 }

 public string GetSelectedSkill()
 {
 if (skills == null || skills.Count ==0) return null;
 return skills[Mathf.Clamp(selectedIndex,0, skills.Count -1)];
 }

 public int GetSelectedIndex() => selectedIndex;

 public void SelectIndex(int idx)
 {
 if (skills == null || skills.Count ==0) { selectedIndex =0; return; }
 selectedIndex = Mathf.Clamp(idx,0, skills.Count -1);
 RefreshUI();
 }

 public void Next()
 {
 if (skills == null || skills.Count ==0) return;
 selectedIndex = (selectedIndex +1) % skills.Count;
 RefreshUI();
 // play sfx on skill change
 if (SfxPlayer.Instance != null) SfxPlayer.Instance.Play("ChangeChoice");
 }

 public void Prev()
 {
 if (skills == null || skills.Count ==0) return;
 selectedIndex = (selectedIndex -1);
 if (selectedIndex <0) selectedIndex = skills.Count -1;
 RefreshUI();
 // play sfx on skill change
 if (SfxPlayer.Instance != null) SfxPlayer.Instance.Play("ChangeChoice");
 }

 private void RebuildItems()
 {
 // destroy old
 foreach (var go in itemInstances) if (go != null) Destroy(go);
 itemInstances.Clear();

 if (itemPrefab == null || contentRoot == null) return;

 for (int i =0; i < Mathf.Min(skills.Count, visibleItems); i++)
 {
 var go = Instantiate(itemPrefab, contentRoot);
 go.transform.localPosition = new Vector3(0, -i * itemSpacing,0);
 itemInstances.Add(go);
 }
 }

 private void RefreshUI()
 {
 if (contentRoot == null || itemInstances == null) return;

 // show slice of skills centered at selectedIndex
 int half = visibleItems /2;
 int start = Mathf.Clamp(selectedIndex - half,0, Mathf.Max(0, skills.Count - visibleItems));

 for (int i =0; i < itemInstances.Count; i++)
 {
 var go = itemInstances[i];
 var idx = start + i;
 var txt = go.GetComponentInChildren<TextMeshProUGUI>();
 if (txt != null)
 {
 if (idx < skills.Count)
 txt.text = skills[idx];
 else
 txt.text = "";
 }

 // highlight selected
 go.SetActive(idx < skills.Count);
 if (idx == selectedIndex)
 {
 // example highlight tweak
 var img = go.GetComponent<Image>();
 if (img != null) img.color = Color.yellow;
 }
 else
 {
 var img = go.GetComponent<Image>();
 if (img != null) img.color = Color.white;
 }
 }
 }
 }
}
