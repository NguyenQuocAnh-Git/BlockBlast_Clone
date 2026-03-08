using UnityEngine;

public static class BlockColliderBuilder
{
    // Fit collider cho block
    public static void FitColliderToChildren(GameObject block)
    {
        // Previously created a BoxCollider2D to enable OnMouse events.
        // We are removing colliders and switching to ray-based selection,
        // so ensure any existing BoxCollider2D components are removed.
        var existingCols = block.GetComponentsInChildren<BoxCollider2D>();
        foreach (var c in existingCols)
        {
            Object.DestroyImmediate(c);
        }
    }
}
