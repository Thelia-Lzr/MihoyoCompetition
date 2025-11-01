# 指示器系统重构总结

## 问题诊断

**原问题**：场上存在冗余的选择框（指示器）
- 创建新指示器时没有清理旧的
- 手动管理容易遗漏
- 不同类型的指示器混在一起

## 解决方案：标签系统 (Tag System)

### 核心设计

1. **标签分组**：每个指示器可以有一个标签（tag）
2. **智能清理**：创建时可选择自动清理同标签旧指示器
3. **独立共存**：不同标签的指示器可以同时存在

### 预定义标签

```csharp
BattleIndicatorManager.Tags.MovementRange  // 移动范围
BattleIndicatorManager.Tags.AttackRange        // 攻击范围
BattleIndicatorManager.Tags.SkillPreview // 技能预览
BattleIndicatorManager.Tags.AreaSelection// 区域选择
BattleIndicatorManager.Tags.DirectionSelection // 方向选择
BattleIndicatorManager.Tags.TargetMarker       // 目标标记
BattleIndicatorManager.Tags.None               // 无标签
```

## 代码改动

### BattleIndicatorManager.cs

**新增功能**：
- 标签常量类 `Tags`
- 带标签的 Create 方法（新参数：`tag`, `clearSameTag`）
- `ClearIndicatorsByTag(string tag)` 方法
- 内部跟踪：`Dictionary<string, List<GameObject>> taggedIndicators`

**示例**：
```csharp
// 创建移动范围圈，自动清理旧的
GameObject moveRange = indicatorManager.CreateCircleIndicator(
    position, 
    5f, 
    true,  // 有效
    true,  // 空心圈
    BattleIndicatorManager.Tags.MovementRange,  // 标签
    true   // clearSameTag - 清理同标签旧指示器
);
```

### PlayerController.cs

**改动**：
1. 移动范围圈使用 `MovementRange` 标签
2. 切换菜单选项时按标签清理
3. 简化了指示器管理逻辑

**关键代码**：
```csharp
// 创建移动范围圈（自动清理旧的）
movementRangeIndicator = indicatorManager.CreateCircleIndicator(
    GetGroundPosition(originalPosition), 
 moveRange, 
    true, 
    true,
    BattleIndicatorManager.Tags.MovementRange,
    true  // 自动清理
);

// 切换到非技能选项时，清理技能预览
if (not skill mode)
{
    indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.SkillPreview);
}
```

### WarriorController.cs

**改动**：
1. 移除手动跟踪的字段（`skillPreviewCircle`, `skillPreviewTargetMarker`）
2. 移除 `ClearSkillPreview()` 方法
3. 技能预览使用 `SkillPreview` 标签 + 自动清理

**简化前**：
```csharp
// 需要手动跟踪和清理
private GameObject skillPreviewCircle = null;

void ShowSkillPreview(int index)
{
    ClearSkillPreview();  // 手动清理
    skillPreviewCircle = indicatorManager.CreateCircleIndicator(...);
}

void ClearSkillPreview()
{
    if (skillPreviewCircle != null)
    {
      indicatorManager.DeleteCircleIndicator(skillPreviewCircle);
        skillPreviewCircle = null;
    }
}
```

**简化后**：
```csharp
// 使用标签自动管理
void ShowSkillPreview(int index)
{
    // 直接创建，自动清理旧的
    indicatorManager.CreateCircleIndicator(
        pos, radius, valid, false,
        BattleIndicatorManager.Tags.SkillPreview,
  true  // 自动清理同标签旧指示器
    );
}
```

## 效果展示

### 场景 1：移动 + 攻击同时显示

```csharp
// 移动范围圈（MovementRange 标签）
GameObject moveRange = indicatorManager.CreateCircleIndicator(
    playerPos, 5f, true, true, Tags.MovementRange, true);

// 攻击范围圈（AttackRange 标签）- 可以共存
GameObject attackRange = indicatorManager.CreateCircleIndicator(
    playerPos, 10f, true, true, Tags.AttackRange, true);

// ? 两个圈同时显示，不冲突
```

### 场景 2：切换技能时自动清理

```csharp
// 选择技能1 - 创建圈1
indicatorManager.CreateCircleIndicator(
    pos1, 3f, true, false, Tags.SkillPreview, true);

// 切换到技能2 - 自动清理圈1，创建圈2
indicatorManager.CreateCircleIndicator(
    pos2, 5f, true, false, Tags.SkillPreview, true);

// ? 场上只有一个技能预览圈
```

### 场景 3：按需清理

```csharp
// 退出技能模式 - 只清理技能预览
indicatorManager.ClearIndicatorsByTag(Tags.SkillPreview);
// ? MovementRange、AttackRange 等其他指示器不受影响
```

## 向后兼容

所有旧 API 仍然可用：
```csharp
// 旧方法（不带标签）
GameObject indicator = indicatorManager.CreateCircleIndicator(pos, radius, true, true);

// 新方法（带标签）
GameObject indicator = indicatorManager.CreateCircleIndicator(
    pos, radius, true, true, Tags.MovementRange, true);
```

## 优势总结

### 1. **防止冗余**
- ? 自动清理同标签旧指示器
- ? 不会出现多个移动范围圈的情况

### 2. **简化代码**
- ? 无需手动跟踪指示器引用
- ? 无需写 `ClearSkillPreview()` 等清理方法
- ? 减少代码量约 30-40%

### 3. **灵活共存**
- ? 不同标签的指示器可以同时显示
- ? 移动范围 + 攻击范围 + 技能预览可以并存

### 4. **精准清理**
- ? `ClearIndicatorsByTag()` 只清理指定类型
- ? 不会误删其他重要指示器

### 5. **易于维护**
- ? 标签集中定义（`BattleIndicatorManager.Tags`）
- ? 一处修改，全局生效
- ? 减少bug风险

## 使用建议

### 1. 优先使用标签
```csharp
// ? 推荐
indicatorManager.CreateCircleIndicator(
    pos, radius, valid, hollow, Tags.SkillPreview, true);

// ?? 除非有特殊需求，否则不推荐
indicatorManager.CreateCircleIndicator(pos, radius, valid, hollow);
```

### 2. 合理设置 clearSameTag
```csharp
// ? 通常情况下使用 true（防止冗余）
clearSameTag: true

// ?? 只有需要同时显示多个同类指示器时才用 false
clearSameTag: false
```

### 3. 状态切换时按标签清理
```csharp
// ? 精准清理
void ExitSkillMode()
{
    indicatorManager.ClearIndicatorsByTag(Tags.SkillPreview);
}

// ? 避免无脑 ClearAll（可能误删）
void ExitSkillMode()
{
    indicatorManager.ClearAll();  // 移动范围圈也没了！
}
```

## 测试检查清单

- [ ] 创建移动范围圈时，旧的被自动清理
- [ ] 移动范围圈和攻击范围圈可以同时显示
- [ ] 切换技能时，旧的技能预览圈被清理
- [ ] 退出技能模式时，只清理技能预览，保留移动范围圈
- [ ] 目标标记切换时，旧标记被清理（单选行为）
- [ ] 回合结束时，所有指示器被清理

## 文件清单

### 修改的文件
1. `Assets/Scripts/ForBattle/Indicators/BattleIndicatorManager.cs` - 核心标签系统
2. `Assets/Scripts/ForBattle/UnitController/PlayerController.cs` - 使用标签
3. `Assets/Scripts/ForBattle/UnitController/WarriorController.cs` - 简化代码

### 新增的文件
1. `Assets/Scripts/ForBattle/Indicators/INDICATOR_API_GUIDE.md` - API 文档 v2.1
2. 本文件 - 重构总结

## 未来扩展

如需添加新类型的指示器：

1. 在 `BattleIndicatorManager.Tags` 中添加新标签常量
   ```csharp
   public const string BuffArea = "BuffArea";
   ```

2. 使用新标签创建指示器
   ```csharp
   indicatorManager.CreateCircleIndicator(
       pos, radius, true, false, Tags.BuffArea, true);
   ```

3. 按需清理
 ```csharp
   indicatorManager.ClearIndicatorsByTag(Tags.BuffArea);
   ```

---

**重构完成日期**：2024
**版本**：BattleIndicatorManager v2.1
