using UnityEngine;

public static class BlockColliderBuilder
{
    // Fit collider cho block
    public static void FitColliderToChildren(GameObject block)
    {
        var renderers = block.GetComponentsInChildren<SpriteRenderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers)
            bounds.Encapsulate(r.bounds);

        BoxCollider2D col = block.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        // world → local
        col.size = bounds.size * 3f; // Tăng kích thước collider lên 3 lần
        col.offset = block.transform.InverseTransformPoint(bounds.center);
    }
}
