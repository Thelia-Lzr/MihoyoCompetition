# PlayerController 使用说明

## 概述
`PlayerController` 是战斗系统中玩家角色的基础控制器，提供了完整的战斗UI、移动、目标选择与范围指示器功能。所有自机角色的特化控制器都应继承此类。

## 已实现功能

### 1. 战斗UI控制
- **显示/隐藏战斗菜单**：自动在回合开始时显示UI
- **行动选择**：攻击、技能、道具、逃跑
- **快捷键支持**：
  - `Q` / `1` - 攻击
  - `E` / `2` - 技能
  - `3` - 道具
  - `4` / `Esc` - 逃跑

### 2. 角色移动
- **WASD移动**：在等待行动选择时可自由移动
- **范围限制**：移动范围由 `moveRange` 参数控制（默认5单位）
- **移动速度**：由 `moveSpeed` 参数控制（默认3单位/秒）

### 3. 三种目标选择模式

#### 模式1：目标选择（Target Selection）
- **用途**：单体技能，选择一个敌方目标
- **操作**：
  - `Tab` - 切换目标
  - `Space` - 确认选择
  - `Esc` - 取消
- **范围指示**：红色圆圈标记
- **示例调用**：
```csharp
yield return UseTargetSelection((target) =>
{
    if (target != null)
    {
        skillSystem.CauseDamage(target, damage, DamageType.Physics);
    }
});
```

#### 模式2：区域选择（Area Selection）
- **用途**：AOE技能，选择一个地面坐标
- **操作**：
  - 鼠标移动 - 选择位置
  - `Space` - 确认选择
  - `Esc` - 取消
- **范围指示**：圆形指示器（绿色=有效，红色=超出范围）
- **示例调用**：
```csharp
yield return UseAreaSelection(radius, (area) =>
{
    // 在 area 位置施放技能
    Debug.Log($"技能释放在: {area}");
});
```

#### 模式3：方向选择（Direction Selection）
- **用途**：方向性技能（如锥形AOE、冲刺）
- **操作**：
  - 鼠标旋转镜头 - 调整方向
  - `Space` - 确认方向
  - `Esc` - 取消
- **范围指示**：扇形指示器
- **示例调用**：
```csharp
yield return UseDirectionSelection((direction) =>
{
    // 使用 direction 向量施放技能
 Debug.Log($"技能方向: {direction}");
});
```

## 如何创建特化角色控制器

### 步骤1：创建新脚本继承 `PlayerController`
```csharp
using System.Collections;
using UnityEngine;

public class MyCharacterController : PlayerController
{
    [Header("My Character Skills")]
    public int specialSkillDamage = 50;
    
    // 重写技能执行方法
    protected override IEnumerator ExecuteSkill()
    {
  // 实现你的技能逻辑
        yield return MySpecialSkill();
    }
    
    private IEnumerator MySpecialSkill()
    {
      // 示例：使用目标选择
        yield return UseTargetSelection((target) =>
        {
if (target != null && skillSystem != null)
            {
           skillSystem.CauseDamage(target, specialSkillDamage, DamageType.Magic);
  }
        });
 }
}
```

### 步骤2：在 Unity Inspector 中设置
1. 将特化控制器组件添加到角色 GameObject 上
2. 绑定以下引用：
   - `battleUI` → BattleCanvasController
   - `indicatorManager` → BattleIndicatorManager
   - `skillSystem` → SkillSystem
   - `cameraController` → BattleCameraController（可选）
3. 调整参数（移动速度、范围等）

## 场景设置

### 需要的组件
1. **BattleCanvasController**（战斗UI）
   - 挂载在 Canvas GameObject 上
   - 配置UI元素引用（按钮、文本等）

2. **BattleIndicatorManager**（范围指示器）
   - 挂载在场景管理器或单独 GameObject 上
   - 可选：配置 Prefab 引用（使用运行时生成也可）

3. **SkillSystem**（技能系统）
   - 管理所有技能效果与伤害计算

### UI Canvas 结构示例
```
Canvas
├── ActionMenuPanel
│   ├── AttackButton
│   ├── SkillButton
│   ├── ItemButton
│   └── EscapeButton
├── UnitInfoPanel
│   ├── UnitNameText
│   └── HPText
└── PromptText
```

## 可重写的虚方法

| 方法 | 用途 | 默认行为 |
|------|------|----------|
| `ExecuteAttack()` | 执行普通攻击 | 使用目标选择，造成物理伤害 |
| `ExecuteSkill()` | 执行技能 | 打印日志（子类应重写） |
| `ExecuteItem()` | 使用道具 | 打印日志 |
| `ExecuteEscape()` | 逃跑 | 打印日志 |
| `HandleMovement()` | WASD移动逻辑 | 限制范围内移动 |

## 参考示例：WarriorController

查看 `Assets/Scripts/ForBattle/UnitController/WarriorController.cs` 了解完整的特化实现，包括：
- 多技能选择菜单
- 目标选择技能（斩击）
- 区域选择技能（旋风斩）

## 参数配置参考

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `moveSpeed` | float | 3f | 移动速度（单位/秒） |
| `moveRange` | float | 5f | 最大移动距离 |
| `targetSelectionRange` | float | 10f | 目标选择最大距离 |
| `sectorAngle` | float | 60f | 扇形指示器角度 |
| `circleRadius` | float | 3f | 默认圆形指示器半径 |

## 注意事项
1. 确保场景中所有 `BattleUnit` 都有正确的 Collider（用于射线检测）
2. 地面需要有 Collider 才能正确进行区域选择
3. 建议使用 Layer 分离单位与地面，并在 `unitLayerMask` 中配置
4. 所有协程方法必须使用 `yield return` 等待完成，避免打断回合流程

## 扩展建议
- 添加技能冷却系统
- 实现技能消耗（MP/能量）
- 添加技能动画与特效触发
- 实现组合技/连击系统
- 添加技能升级/解锁机制
