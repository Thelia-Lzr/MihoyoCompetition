# BattleIndicatorManager API 指南 v2.1

本文档说明如何使用 `BattleIndicatorManager` 创建、更新和删除战斗指示器。

## 核心设计理念

1. **独立创建**：每次调用 Create 方法都会创建新的独立对象
2. **标签管理**：通过标签（Tag）分组管理指示器，避免冗余
3. **智能清理**：可选择自动清理同标签旧指示器
4. **类型专用**：每种指示器类型都有专门的 Create/Update/Delete 方法

## 标签系统 (Tag System)

### 预定义标签

```csharp
BattleIndicatorManager.Tags.MovementRange        // 移动范围
BattleIndicatorManager.Tags.AttackRange  // 攻击范围
BattleIndicatorManager.Tags.SkillPreview         // 技能预览
BattleIndicatorManager.Tags.AreaSelection        // 区域选择
BattleIndicatorManager.Tags.DirectionSelection   // 方向选择
BattleIndicatorManager.Tags.TargetMarker         // 目标标记
BattleIndicatorManager.Tags.None// 无标签（默认）
```

### 标签的作用

1. **分组管理**：相同标签的指示器可以一起清理
2. **防止冗余**：创建时可选择自动清除同标签旧指示器
3. **独立共存**：不同标签的指示器可以同时显示

**示例**：
- 移动范围圈（MovementRange）和攻击范围圈（AttackRange）可以同时存在
- 但不会同时存在两个移动范围圈（clearSameTag=true时）

## API 方法一览

### 扇形指示器 (Sector Indicator)

#### 创建（带标签）
```csharp
GameObject CreateSectorIndicator(
    Transform center, 
    float radius, 
    float angle, 
    string tag = "", // 标签
    bool clearSameTag = false  // 是否清理同标签旧指示器
)
```

**示例**：
```csharp
// 创建方向选择扇形，自动清理旧的
GameObject sector = indicatorManager.CreateSectorIndicator(
    transform, 
    10f, 
    60f,
    BattleIndicatorManager.Tags.DirectionSelection,
    true  // 清理旧的方向选择指示器
);
```

#### 创建（简化版，向后兼容）
```csharp
GameObject CreateSectorIndicator(Transform center, float radius, float angle)
```

#### 更新
```csharp
void UpdateSectorRotation(GameObject indicator, Transform center, Vector3 forward)
void UpdateSectorIndicator(GameObject indicator, Vector3 center, float radius, float angle)
```

#### 删除
```csharp
void DeleteSectorIndicator(GameObject indicator)
```

---

### 圆形指示器 (Circle Indicator)

#### 创建（带标签）
```csharp
GameObject CreateCircleIndicator(
    Vector3 worldPos, 
    float radius, 
    bool isValid = true, 
    bool hollow = false,
    string tag = "",     // 标签
    bool clearSameTag = false  // 是否清理同标签旧指示器
)
```

**示例**：
```csharp
// 创建移动范围圈，自动清理旧的
GameObject moveRange = indicatorManager.CreateCircleIndicator(
    position, 
    5f, 
    true, 
    true,  // 空心圈
    BattleIndicatorManager.Tags.MovementRange,
    true   // 清理旧的移动范围指示器
);

// 创建技能预览圈，自动清理旧的
GameObject skillPreview = indicatorManager.CreateCircleIndicator(
    mousePos, 
    3f, 
    valid, 
    false,// 填充圆
    BattleIndicatorManager.Tags.SkillPreview,
    true    // 清理旧的技能预览
);
```

#### 创建（简化版，向后兼容）
```csharp
GameObject CreateCircleIndicator(Vector3 worldPos, float radius, bool isValid = true, bool hollow = false)
```

#### 更新
```csharp
void UpdateCircleIndicator(GameObject indicator, Vector3 worldPos, float radius, bool isValid = true)
```

#### 删除
```csharp
void DeleteCircleIndicator(GameObject indicator)
```

---

### 目标标记 (Target Marker)

#### 创建
```csharp
GameObject CreateTargetMarker(Transform target, bool clearPrevious = true)
```

**示例**：
```csharp
// 创建目标标记，自动清除之前的（单选模式）
GameObject marker = indicatorManager.CreateTargetMarker(enemy.transform, true);

// 创建多个目标标记（多选模式，不推荐）
GameObject marker1 = indicatorManager.CreateTargetMarker(enemy1.transform, false);
GameObject marker2 = indicatorManager.CreateTargetMarker(enemy2.transform, false);
```

#### 更新
```csharp
void UpdateTargetMarker(GameObject marker, Vector3 position)
void UpdateTargetMarkerColor(GameObject marker, Color color)
```

#### 删除
```csharp
void DeleteTargetMarker(GameObject marker)
void DeleteAllTargetMarkers()  // 删除所有目标标记
```

---

### 按标签管理

#### 清除指定标签的所有指示器
```csharp
void ClearIndicatorsByTag(string tag)
```

**示例**：
```csharp
// 清除所有技能预览指示器
indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.SkillPreview);

// 清除所有目标标记
indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.TargetMarker);

// 清除所有移动范围指示器
indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.MovementRange);
```

---

## 完整使用示例

### 示例 1：PlayerController 中的标签使用

```csharp
public override IEnumerator ExecuteTurn(BattleTurnManager turnManager)
{
    // 创建移动范围圈（使用MovementRange标签）
    movementRangeIndicator = indicatorManager.CreateCircleIndicator(
        GetGroundPosition(originalPosition), 
        moveRange, 
        true,  // 有效
     true,  // 空心圈
        BattleIndicatorManager.Tags.MovementRange,  // 标签
   true   // 清理旧的移动范围圈
    );
    
    while (!actionConfirmed)
    {
      if (battleUI.Choice == BattleCanvasController.BattleActionType.Attack)
   {
      // CreateTargetMarker 默认会清除之前的标记（单选）
            if (targetUnderMouse != null)
       {
     indicatorManager.CreateTargetMarker(targetUnderMouse.transform);
        }
        }
        else if (battleUI.Choice == BattleCanvasController.BattleActionType.Skill)
        {
  // 显示技能预览
            ShowSkillPreview(selectedSkillIndex);
      }
   else
  {
// 切换到其他选项时，清理技能预览
            indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.SkillPreview);
        }
        
      yield return null;
    }
    
    // 回合结束：清理所有指示器
    indicatorManager.ClearAll();
}
```

### 示例 2：WarriorController 技能预览（自动清理）

```csharp
protected override void ShowSkillPreview(int index)
{
    if (indicatorManager == null) return;
    
    // 不需要手动清理，使用标签系统自动管理
    
    if (index == 0)  // 单体技能
    {
        BattleUnit target = FindTargetUnderMouse();
    if (target != null)
      {
  // CreateTargetMarker 默认清除旧标记
   indicatorManager.CreateTargetMarker(target.transform);
        }
    }
    else if (index == 1)  // 范围技能
    {
        Vector3 mousePos = GetMouseGroundPosition();
        bool valid = Vector3.Distance(transform.position, mousePos) <= maxRange;
        
        // 使用SkillPreview标签，自动清理旧的技能预览圈
        indicatorManager.CreateCircleIndicator(
            mousePos, 
            whirlwindRadius, 
            valid, 
            false,  // 填充圆
       BattleIndicatorManager.Tags.SkillPreview,  // 标签
            true    // 清理旧的
        );
    }
}
```

### 示例 3：同时显示多种指示器

```csharp
// 移动范围圈（MovementRange 标签）
GameObject moveRange = indicatorManager.CreateCircleIndicator(
    playerPos, 
    5f, 
    true, 
    true,
    BattleIndicatorManager.Tags.MovementRange,
    true
);

// 攻击范围圈（AttackRange 标签）- 可以与移动范围同时存在
GameObject attackRange = indicatorManager.CreateCircleIndicator(
    playerPos, 
    10f, 
    true, 
    true,
    BattleIndicatorManager.Tags.AttackRange,
    true
);

// 技能预览圈（SkillPreview 标签）- 也可以同时存在
GameObject skillPreview = indicatorManager.CreateCircleIndicator(
    mousePos, 
    3f, 
    valid, 
    false,
    BattleIndicatorManager.Tags.SkillPreview,
    true
);

// 三个圈可以同时显示！
```

---

## 向后兼容方法

所有旧方法仍然可用：

```csharp
// 不带标签的简化版本（向后兼容）
GameObject ShowSectorIndicator(Transform, float, float)
GameObject ShowCircleIndicator(Vector3, float, bool, bool)
void ShowTargetMarker(Transform)

// 清理方法
void DestroyIndicator(GameObject)
void ClearIndicators()            // 清除所有非标记指示器
void ClearTargetMarkers()         // 清除所有目标标记
void ClearAll()                // 清除所有
```

---

## 最佳实践

### 1. 使用标签避免冗余

```csharp
// ? 好的做法：使用标签 + 自动清理
GameObject indicator = indicatorManager.CreateCircleIndicator(
    pos, radius, true, true,
    BattleIndicatorManager.Tags.MovementRange,
    true  // 自动清理旧的
);

// ? 不好的做法：手动管理容易遗漏
if (oldIndicator != null)
{
    indicatorManager.DeleteCircleIndicator(oldIndicator);
}
oldIndicator = indicatorManager.CreateCircleIndicator(pos, radius, true, true);
```

### 2. 按标签清理

```csharp
// ? 好的做法：切换状态时按标签清理
if (exitingSkillMode)
{
    indicatorManager.ClearIndicatorsByTag(BattleIndicatorManager.Tags.SkillPreview);
}

// ? 不好的做法：清除所有（可能误删其他重要指示器）
if (exitingSkillMode)
{
    indicatorManager.ClearAll();  // 移动范围圈也被删了！
}
```

### 3. 标签命名

```csharp
// ? 使用预定义标签
BattleIndicatorManager.Tags.MovementRange
BattleIndicatorManager.Tags.SkillPreview

// ?? 自定义标签也可以，但要保持一致
const string MyCustomTag = "CustomFeature";
indicatorManager.CreateCircleIndicator(..., MyCustomTag, true);
indicatorManager.ClearIndicatorsByTag(MyCustomTag);
```

### 4. 清理时机

```csharp
// ? 在状态切换时按标签清理
void SwitchToAttackMode()
{
    indicatorManager.ClearIndicatorsByTag(Tags.SkillPreview);
    // MovementRange 和其他标签不受影响
}

// ? 回合结束时清理所有
void OnTurnEnd()
{
    indicatorManager.ClearAll();
}
```

---

## 常见问题

### Q: 如何同时显示移动范围和攻击范围？
A: 使用不同的标签：
```csharp
var moveRange = indicatorManager.CreateCircleIndicator(
    pos, 5f, true, true, Tags.MovementRange, true);
var attackRange = indicatorManager.CreateCircleIndicator(
    pos, 10f, true, true, Tags.AttackRange, true);
// 两者可以同时存在
```

### Q: clearSameTag=true 会影响其他标签吗？
A: 不会。只清理相同标签的指示器：
```csharp
// 已存在：MovementRange 标签的圈
// 创建新的 SkillPreview 标签的圈，不会清理 MovementRange
indicatorManager.CreateCircleIndicator(
    pos, 3f, true, false, Tags.SkillPreview, true);
```

### Q: 不使用标签会怎样？
A: 可以正常工作，但需要手动管理：
```csharp
// 不使用标签（tag=""或Tags.None）
GameObject indicator = indicatorManager.CreateCircleIndicator(
 pos, radius, true, true);
    
// 需要手动删除
indicatorManager.DeleteCircleIndicator(indicator);
```

### Q: 如何避免指示器闪烁？
A: 方案1：使用 Update 方法而不是重建
```csharp
// 首次创建
GameObject circle = indicatorManager.CreateCircleIndicator(
    pos, radius, true, false, Tags.SkillPreview, true);

// 每帧更新
while (previewing)
{
    indicatorManager.UpdateCircleIndicator(circle, newPos, radius, valid);
    yield return null;
}
```

方案2：使用标签自动清理（会有轻微闪烁，但代码简单）
```csharp
// 每帧重建（自动清理旧的）
while (previewing)
{
    indicatorManager.CreateCircleIndicator(
        newPos, radius, valid, false, 
    Tags.SkillPreview, true);  // 自动清理旧的
    yield return null;
}
```

---

## 更新日志

### v2.1 (当前版本)
- ? 新增标签系统（Tag System）
- ? 支持按标签分组管理
- ? 自动清理同标签旧指示器（clearSameTag 参数）
- ? 预定义常用标签常量
- ? 简化 Controller 代码，减少手动管理
- ? 防止冗余指示器的出现

### v2.0
- 新增类型专用的 Create/Update/Delete 方法
- 所有创建的指示器完全独立
- 保持向后兼容旧 API

### v1.0
- 基础的 Show/Clear 方法
- 主/辅助指示器系统
- 位置锁定功能
