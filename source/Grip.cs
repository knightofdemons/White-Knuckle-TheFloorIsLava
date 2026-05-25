using UnityEngine;

namespace TheFloorIsLava;

internal sealed class Grip
{
    private bool _savedInfinite;
    private bool _applied;

    public void Enter(ENT_Player player)
    {
        if (_applied)
            return;
        _savedInfinite = GameRefs.GetInfiniteStamina(player);
        GameRefs.SetInfiniteStamina(player, true);
        _applied = true;
    }

    public void Exit(ENT_Player player)
    {
        if (!_applied)
            return;
        GameRefs.SetInfiniteStamina(player, _savedInfinite);
        _applied = false;
    }

    public void Regen(ENT_Player player, float dt, float perSecond) =>
        GameRefs.AddGripToAllHands(player, perSecond * dt);
}
