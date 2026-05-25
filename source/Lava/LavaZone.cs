using UnityEngine;

namespace TheFloorIsLava.Lava;

/// <summary>Pure marker — detection is arithmetic, no Unity physics dependency.</summary>
public sealed class LavaZone : MonoBehaviour
{
    public float Radius { get; private set; }

    public void Setup(float radius) => Radius = radius;
}
