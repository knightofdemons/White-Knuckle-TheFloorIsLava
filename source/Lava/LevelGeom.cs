using UnityEngine;

namespace TheFloorIsLava.Lava;

internal static class LevelGeom
{
    public static float MinUpDot(float maxTiltDegrees) =>
        Mathf.Cos(maxTiltDegrees * Mathf.Deg2Rad);

    public static bool IsFloorHit(RaycastHit hit, float minUpDot)
    {
        if (hit.normal.y < minUpDot)
            return false;
        return IsFloorCollider(hit.collider);
    }

    public static bool IsFloorCollider(Collider col)
    {
        if (col == null || col.isTrigger)
            return false;
        if (col.GetComponentInParent<LavaZone>() != null)
            return false;
        if (col.GetComponentInParent<CL_Handhold>() != null)
            return false;
        if (col.GetComponentInParent<ENT_Player>() != null)
            return false;

        var rb = col.attachedRigidbody;
        return rb == null || rb.isKinematic;
    }
}
