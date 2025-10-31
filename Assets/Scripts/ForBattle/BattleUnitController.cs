using System.Collections;
using UnityEngine;

/// <summary>
/// Base controller component for BattleUnit behavior. Derive from this to implement
/// player-controlled or AI-controlled turn behavior. The TurnManager will call
/// `ExecuteTurn` when it's this unit's turn; the method should yield while the
/// action is playing.
/// </summary>
public abstract class BattleUnitController : MonoBehaviour
{
    public BattleUnit unit;

    // Called by BattleUnit or external setup to bind the data object
    public virtual void Bind(BattleUnit battleUnit)
    {
        unit = battleUnit;
    }

    // Called when the battle starts for initialization
    public virtual void OnBattleStart() { }

    // Called when the battle ends for cleanup
    public virtual void OnBattleEnd() { }

    // Execute the unit's turn. The TurnManager will StartCoroutine on this.
    // Implementations should yield while animations/effects are playing.
    public abstract IEnumerator ExecuteTurn(BattleTurnManager turnManager);
}
