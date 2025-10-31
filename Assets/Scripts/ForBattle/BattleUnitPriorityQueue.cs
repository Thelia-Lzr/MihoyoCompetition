using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BattleUnit优先队列，按battleActPoint降序排列（高优先级在队首）
/// </summary>
public class BattleUnitPriorityQueue
{
    public List<BattleUnit> units = new List<BattleUnit>();
    

    // 添加单位并排序

    public void Clear()
    {
        units.Clear();
    }
    public void Add(BattleUnit unit)
    {
        units.Add(unit);
        Sort();
    }

    // 添加多个单位
    public void AddRange(IEnumerable<BattleUnit> unitsToAdd)
    {
        if (unitsToAdd == null) return;
        units.AddRange(unitsToAdd);
        Sort();
    }

    // 移除单位
    public void Remove(BattleUnit unit)
    {
        units.Remove(unit);
    }

    // 获取队首单位（不移除）
    public BattleUnit Peek()
    {
        if (units.Count == 0) return null;
        return units[0];
    }

    // 弹出队首单位（移除并返回）
    public BattleUnit Pop()
    {
        if (units.Count == 0) return null;
        BattleUnit top = units[0];
        units.RemoveAt(0);
        return top;
    }

    // 获取所有单位
    public List<BattleUnit> GetAll()
    {
        return units;
    }

    // 按battleActPoint降序排序
    public void Sort()
    {
        units.Sort((a, b) => b.battleActPoint.CompareTo(a.battleActPoint));
    }

    // 队列数量
    public int Count => units.Count;
}
