using UnityEngine;

namespace TheFloorIsLava;

internal sealed class HealthPool
{
    public float Current { get; private set; }
    public float Max { get; private set; } = 100f;
    public bool Dead => Current <= 0f;

    public void Reset(float max = 100f)
    {
        Max = max;
        Current = max;
    }

    public void Damage(float amount) => Current = Mathf.Max(0f, Current - amount);

    public void Heal(float amount) => Current = Mathf.Min(Max, Current + amount);
}
