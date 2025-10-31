# TextMeshPro (TMP) 使用说明

## 为什么使用 TextMeshPro？

相比 Unity 原生的 UI Text，TextMeshPro 提供：
- ? **更清晰的文字渲染**（使用 SDF 技术）
- ? **更好的缩放效果**（任意缩放不模糊）
- ? **丰富的样式**（描边、阴影、渐变、材质效果）
- ? **更好的性能**（批处理优化）
- ? **支持富文本标签**（颜色、大小、粗体等）

## 首次使用 TextMeshPro

### 1. 导入 TMP 资源
第一次在项目中使用 TMP 时：
1. 创建任意 TextMeshPro 对象（UI → Text - TextMeshPro）
2. 弹出 "TMP Importer" 窗口
3. 点击 **"Import TMP Essentials"**（必需）
4. 可选：点击 **"Import TMP Examples & Extras"**（示例资源）

### 2. 导入中文字体（重要！）
默认的 TMP 字体不包含中文，需要自定义字体资源：

#### 方法一：使用系统字体
1. **创建字体资源**
   - Window → TextMeshPro → Font Asset Creator
   - Source Font File：选择支持中文的 TTF 字体（如微软雅黑、思源黑体）
   - Character Set：选择 "Custom Characters" 或 "Unicode Range"
   - 如果选 Unicode Range，勾选：
     - `Basic Latin` (0020-007F)
     - `CJK Unified Ideographs` (4E00-9FFF) ← 中文常用字
   - Atlas Resolution：2048 或 4096（根据需要）
   - 点击 **"Generate Font Atlas"**
   - 点击 **"Save"** 保存为 .asset 文件

2. **应用到 TMP 对象**
   - 选中 TextMeshProUGUI 组件
   - Font Asset：选择刚创建的字体资源

#### 方法二：使用预制字体包
- 从 Asset Store 下载中文 TMP 字体包
- 导入项目后直接使用

### 3. 设置默认字体（推荐）
为避免每次都要手动设置：
1. Edit → Project Settings → TextMesh Pro → Settings
2. Default Font Asset：设置为你的中文字体
3. Fallback Font Assets：添加备用字体

## BattleCanvasController 中的 TMP 使用

### 字段类型
```csharp
using TMPro;

public class BattleCanvasController : MonoBehaviour
{
    [Header("Info Display")]
    public TextMeshProUGUI unitNameText;  // 使用 TextMeshProUGUI
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI actionPromptText;
}
```

### 文本赋值（与普通 Text 相同）
```csharp
unitNameText.text = unit.unitName;
hpText.text = $"HP: {unit.battleHp}/{unit.battleMaxHp}";
```

### 富文本示例
TMP 支持更强大的富文本标签：

```csharp
// 颜色（支持 HTML 颜色和预定义颜色）
hpText.text = "<color=red>HP: 50/100</color>";
hpText.text = "<color=#FF0000>HP: 50/100</color>";

// 大小
unitNameText.text = "<size=150%>战士</size>";

// 粗体/斜体
actionPromptText.text = "<b>按空格确认</b>";
actionPromptText.text = "<i>技能冷却中...</i>";

// 组合使用
hpText.text = $"<color=red><b>HP: {hp}</b></color>/<color=green>{maxHp}</color>";

// 渐变
unitNameText.text = "<gradient=\"Red to Blue\">传奇战士</gradient>";

// 描边效果（需要在材质中启用）
// 通过 Inspector 或代码设置 Outline 参数
```

## Unity 场景中的 TMP 配置

### 创建 TMP 文本步骤
1. **右键 Hierarchy → UI → Text - TextMeshPro**
2. **配置基本属性**
   - Font Asset：选择支持中文的字体
   - Font Size：建议 24-36（根据需要）
   - Color：设置颜色
   - Alignment：设置对齐方式

3. **高级样式（可选）**
   - **Extra Settings → Outline**
     - Thickness：0.1-0.3（描边粗细）
     - Color：黑色或对比色
   - **Extra Settings → Underlay**
  - 启用阴影效果
 - **Material Preset**
     - 选择预设材质（如发光、金属效果）

### 在 BattleCanvas 中绑定
```
BattleCanvas
├── UnitNameText (TextMeshPro - Text (UI))
│   ├── Font: 你的中文字体
│   ├── Size: 32
│   ├── Color: 白色
│   └── Alignment: Center
├── HPText (TextMeshPro - Text (UI))
│   ├── Font: 你的中文字体
│   ├── Size: 28
│   └── Outline: 黑色 0.2
└── PromptText (TextMeshPro - Text (UI))
    ├── Font: 你的中文字体
    ├── Size: 24
    └── Color: 黄色
```

## 性能优化建议

### 1. 字体图集优化
- 只包含需要的字符（减少图集大小）
- 使用 Dynamic Font System（运行时按需生成）

### 2. 材质共享
- 相同样式的 TMP 对象使用同一材质
- 避免每个文本都创建新材质

### 3. 静态文本
- 对不常变化的文本，勾选 "Enable Raycast Target = false"
- 减少 UI 重建开销

## 常见问题

### 显示方块/乱码
**原因**：字体资源不包含该字符
**解决**：
1. 重新生成字体资源，包含更多字符
2. 或设置 Fallback Font Assets

### 文字模糊
**原因**：Atlas Resolution 太低或缩放不当
**解决**：
1. 提高 Atlas Resolution（2048/4096/8192）
2. 调整 Sampling Point Size
3. 避免过度缩放

### Inspector 中看不到效果
**原因**：需要在 Game 视图或运行时查看
**解决**：
- TMP 的某些效果（如 SDF 渲染）只在运行时或 Game 视图中可见

### 性能问题
**原因**：字体图集过大或材质过多
**解决**：
1. 减少字符集
2. 使用 Dynamic Font
3. 合并相同样式的文本材质

## 与 BattleCanvasController 集成示例

### 动态更新血量颜色
```csharp
public void ShowUI(BattleUnit unit, System.Action<BattleActionType> callback)
{
    // ...existing code...
    
    // 根据血量百分比改变颜色
    float hpPercent = (float)unit.battleHp / unit.battleMaxHp;
    string hpColor = hpPercent > 0.5f ? "green" : (hpPercent > 0.2f ? "yellow" : "red");
    
    if (hpText != null)
     hpText.text = $"<color={hpColor}>HP: {unit.battleHp}</color>/{unit.battleMaxHp}";
}
```

### 技能提示闪烁
```csharp
public IEnumerator FlashPrompt(string message)
{
    if (actionPromptText != null)
 {
        for (int i = 0; i < 3; i++)
        {
    actionPromptText.text = $"<color=yellow><b>{message}</b></color>";
         yield return new WaitForSeconds(0.3f);
            actionPromptText.text = "";
            yield return new WaitForSeconds(0.3f);
}
    }
}
```

## 推荐字体资源

### 免费中文字体
- **思源黑体** (Noto Sans CJK / Source Han Sans)
- **思源宋体** (Source Han Serif)
- **文泉驿微米黑**
- **站酷系列字体**

### 下载地址
- Google Fonts: https://fonts.google.com/noto/fonts
- GitHub: https://github.com/adobe-fonts/source-han-sans

## 总结

使用 TextMeshPro 后，你的战斗UI将：
- ? 文字更清晰锐利
- ? 支持更丰富的视觉效果
- ? 性能更优
- ? 更易维护和扩展

记得在绑定 BattleCanvasController 时，将文本字段指向 **TextMeshPro - Text (UI)** 对象，而非旧的 UI Text！
