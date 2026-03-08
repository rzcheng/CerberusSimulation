using UnityEngine;
using CesiumForUnity;

// Restricts Cesium tile loading to a radius around MDRS.
// Attach to the same GameObject as the Cesium3DTileset.
public class MDRSAreaExcluder : CesiumTileExcluder
{
    [Tooltip("How far from the MDRS origin to allow tiles to load (meters).")]
    public float radiusMeters = 4000f;

    float radiusSq;

    protected override void OnEnable()
    {
        radiusSq = radiusMeters * radiusMeters;
        base.OnEnable();
    }

    void OnValidate()
    {
        radiusSq = radiusMeters * radiusMeters;
    }

    public override bool ShouldExclude(Cesium3DTile tile)
    {
        // Use closest point on the bounding box (not center) so large ancestor
        // tiles that contain MDRS aren't wrongly excluded along with their children.
        Vector3 closest = tile.bounds.ClosestPoint(Vector3.zero);
        float distSq = closest.x * closest.x + closest.z * closest.z;
        return distSq > radiusSq;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, radiusMeters);
    }
}
