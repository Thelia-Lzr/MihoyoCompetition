# PlayerController 快速设置指南

## 已创建的文件

### 核心脚本
1. **PlayerController.cs** - 玩家控制器基类
   - 路径：`Assets/Scripts/ForBattle/UnitController/PlayerController.cs`
   - 功能：战斗UI、WASD移动、三种目标选择模式

2. **BattleCanvasController.cs** - 战斗UI控制器
   - 路径：`Assets/Scripts/ForBattle/UI/BattleCanvasController.cs`
   - 功能：菜单显示、按钮事件、快捷键
   - **使用 TextMeshPro (TMP) 进行文本显示**

3. **BattleIndicatorManager.cs** - 范围指示器管理器
   - 路径：`Assets/Scripts/ForBattle/Indicators/BattleIndicatorManager.cs`
   - 功能：扇形、圆形、目标标记指示器

4. **WarriorController.cs** - 示例特化控制器
   - 路径：`Assets/Scripts/ForBattle/UnitController/WarriorController.cs`
   - 功能：演示如何继承并实现自定义技能

## Unity 场景设置步骤

### 第一步：创建战斗UI Canvas

1. **创建 UI Canvas**
   - 右键 Hierarchy → UI → Canvas
   - 重命名为 "BattleCanvas"
   - Canvas Scaler 设置为 Scale With Screen Size (推荐 1920x1080)

2. **创建行动菜单面板**
   ```
   BattleCanvas
   └── ActionMenuPanel (Panel)
       ├── AttackButton (Button - Text: "攻击 [Q]")
       ├── SkillButton (Button - Text: "技能 [E]")
    ├── ItemButton (Button - Text: "道具 [3]")
    └── EscapeButton (Button - Text: "逃跑 [4]")
   ```

3. **创建信息显示（使用 TextMeshPro）**
   ```
   BattleCanvas
   ├── UnitNameText (TextMeshPro - Text (UI) - 显示单位名称)
   ├── HPText (TextMeshPro - Text (UI) - 显示血量)
 └── PromptText (TextMeshPro - Text (UI) - 显示操作提示)
   ```
   
   **重要：创建 TextMeshPro 文本的步骤**
   - 右键 Hierarchy → UI → Text - TextMeshPro
   - 如果是第一次使用 TMP，会弹出导入窗口，点击 "Import TMP Essentials"
   - 设置字体、大小、颜色等属性

4. **添加 BattleCanvasController 组件**
   - 选中 BattleCanvas GameObject
   - Add Component → BattleCanvasController
   - 在 Inspector 中拖拽绑定所有UI元素引用
   - **注意：文本字段现在需要绑定 TextMeshProUGUI 组件**

### 第二步：创建指示器管理器

1. **创建空 GameObject**
   - 右键 Hierarchy → Create Empty
   - 重命名为 "BattleIndicatorManager"

2. **添加组件**
   - Add Component → BattleIndicatorManager

3. **配置材质（可选）**
   - 创建半透明材质用于指示器
   - 或者让系统运行时自动生成

### 第三步：配置角色 GameObject

假设你有一个玩家角色 GameObject：

1. **添加必要组件**
   - `BattleUnit` 组件（应该已有）
   - 移除旧的简单 `PlayerController`
   - Add Component → `PlayerController`（或你的特化控制器如 `WarriorController`）

2. **配置 PlayerController 引用**
   在 Inspector 中设置：
   - **Battle UI** → 拖拽 BattleCanvas
   - **Indicator Manager** → 拖拽 BattleIndicatorManager GameObject
   - **Skill System** → 拖拽场景中的 SkillSystem GameObject
   - **Camera Controller** → 拖拽 BattleCameraController（如果有）

3. **调整参数**
   - Move Speed: 3~5（推荐）
   - Move Range: 3~8（根据地图大小）
   - Target Selection Range: 10~15
   - Sector Angle: 60~90
   - Unit Layer Mask: 选择单位所在的 Layer

### 第四步：场景环境配置

1. **地面 Collider**
   - 确保地面有 MeshCollider 或 BoxCollider
   - 用于区域选择的射线检测

2. **单位 Collider**
 - 所有 BattleUnit 需要有 Collider（推荐 CapsuleCollider）
   - 用于目标选择射线检测

3. **Layer 设置（推荐）**
   - 创建 Layer "BattleUnit"
   - 将所有战斗单位设为该 Layer
   - 在 PlayerController 的 Unit Layer Mask 中勾选此 Layer

### 第五步：测试

1. **基础测试**
   - 运行场景
   - 等待玩家角色回合
   - 应该自动显示战斗UI

2. **移动测试**
   - 使用 WASD 移动角色
   - 检查是否有范围限制

3. **攻击测试**
   - 按 Q 或点击"攻击"按钮
   - 按 Tab 切换目标（应该显示红色圆圈）
   - 按 Space 确认攻击

4. **技能测试（如果使用 WarriorController）**
   - 按 E 或点击"技能"
   - 按 1 选择斩击（目标选择）
   - 按 2 选择旋风斩（区域选择）

## 常见问题排查

### UI 不显示
- 检查 BattleCanvas 的 Canvas Scaler 设置
- 确保 EventSystem 存在于场景中
- 检查 BattleCanvasController 的引用是否都已绑定

### 无法选择目标
- 检查敌方单位是否有 Collider
- 检查目标是否在 targetSelectionRange 内
- 确保单位的 BattleUnitType 设置正确（Player vs Enemy）

### 指示器不显示
- 检查 BattleIndicatorManager 是否在场景中
- 查看 Console 是否有材质相关错误
- 尝试调整指示器的 Y 坐标（避免被地面遮挡）

### 移动范围不正确
- 调整 PlayerController 的 moveRange 参数
- 检查角色初始位置是否正确记录

## 高级配置

### 自定义指示器外观
编辑 `BattleIndicatorManager` 的参数：
- `validColor` - 有效范围颜色（默认绿色半透明）
- `invalidColor` - 无效范围颜色（默认红色半透明）
- `targetColor` - 目标标记颜色（默认红色）

### 使用预制体
如果要使用自定义的指示器 Prefab：
1. 创建你的指示器 Prefab（3D 模型或粒子效果）
2. 在 BattleIndicatorManager 中绑定：
   - `sectorIndicatorPrefab`
   - `circleIndicatorPrefab`
   - `targetMarkerPrefab`

### 添加音效
在 PlayerController 的相应方法中添加：
```csharp
protected override IEnumerator ExecuteAttack()
{
    AudioSource.PlayClipAtPoint(attackSound, transform.position);
    // ...existing code...
}
```

## 下一步开发建议

1. **为每个角色创建特化控制器**
   - 复制 `WarriorController.cs` 作为模板
   - 重写 `ExecuteSkill()` 实现独特技能

2. **完善技能系统**
   - 在 SkillSystem 中添加更多技能效果方法
   - 实现 Buff/Debuff 系统
   - 添加技能动画触发

3. **UI 美化**
   - 使用 UI 动画（Animator）
   - 添加技能图标
   - 显示技能冷却/消耗

4. **AI 控制器**
   - 创建继承自 `BattleUnitController` 的 AIController
   - 实现敌方AI决策逻辑

## 联系与支持
查看详细文档：`PlayerController_README.md`
