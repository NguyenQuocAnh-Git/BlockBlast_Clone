using UnityEngine;
using System.Collections.Generic;

public class ParticleEffectManager : MonoBehaviour
{
    [Header("Particle Effects")]
    public ParticleSystem cellDestroyEffect;
    public ParticleSystem lineClearEffect;
    
    [Header("Effect Settings")]
    public bool enableParticleEffects = true;
    public float effectScale = 1f;
    
    private Dictionary<Vector2Int, ParticleSystem> activeEffects = new Dictionary<Vector2Int, ParticleSystem>();
    private GridView grid;
    
    void Start()
    {
        // Find grid reference if not already assigned
        if (grid == null)
        {
            grid = FindObjectOfType<GridView>();
        }
        
        
        
        // Create default effects if not assigned
        if (cellDestroyEffect == null)
        {
            cellDestroyEffect = CreateDefaultCellDestroyEffect();
            
        }
        
        if (lineClearEffect == null)
        {
            lineClearEffect = CreateDefaultLineClearEffect();
            
        }
    }
    
    // Play particle effect at specific grid position
    public void PlayCellDestroyEffect(int x, int y, Color cellColor)
    {
        
        
        if (!enableParticleEffects || cellDestroyEffect == null || grid == null) 
        {
            
            return;
        }
        
        Vector3 worldPos = grid.GetAnchorWorldPosition(x, y);
        
        
        // Create particle system instance
        ParticleSystem effect = Instantiate(cellDestroyEffect, worldPos, Quaternion.identity, transform);
        
        // Scale the effect based on cell size
        effect.transform.localScale = Vector3.one * effectScale;
        
        // Set particle color to match cell color
        var main = effect.main;
        main.startColor = cellColor;
        
        // Play the effect
        effect.Play();
        
        // Auto destroy after effect finishes
        Destroy(effect.gameObject, main.duration + main.startLifetime.constant);
    }
    
    // Play enhanced line clear effect for multiple cells
    public void PlayLineClearEffect(List<Vector2Int> positions, List<Color> colors)
    {
        if (!enableParticleEffects || lineClearEffect == null || grid == null) return;
        
        for (int i = 0; i < positions.Count; i++)
        {
            Vector2Int pos = positions[i];
            Color color = i < colors.Count ? colors[i] : Color.white;
            
            Vector3 worldPos = grid.GetAnchorWorldPosition(pos.x, pos.y);
            
            // Create enhanced particle system instance
            ParticleSystem effect = Instantiate(lineClearEffect, worldPos, Quaternion.identity, transform);
            
            // Scale the effect
            effect.transform.localScale = Vector3.one * effectScale * 1.5f; // Larger for line clear
            
            // Set particle color
            var main = effect.main;
            main.startColor = color;
            
            // Increase emission for line clear
            var emission = effect.emission;
            emission.rateOverTime = emission.rateOverTime.constant * 2f;
            
            // Play the effect
            effect.Play();
            
            // Auto destroy
            Destroy(effect.gameObject, main.duration + main.startLifetime.constant);
        }
        
        
    }
    
    // Create default cell destroy effect
    private ParticleSystem CreateDefaultCellDestroyEffect()
    {
        GameObject effectObj = new GameObject("DefaultCellDestroyEffect");
        ParticleSystem ps = effectObj.AddComponent<ParticleSystem>();
        
        var main = ps.main;
        main.duration = 0.5f;
        main.startLifetime = 0.8f;
        main.startSpeed = 2f;
        main.startSize = 0.2f;
        main.startColor = Color.white;
        main.gravityModifier = 0.5f;
        main.maxParticles = 20;
        
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { 
            new ParticleSystem.Burst(0f, 15) 
        });
        
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;
        
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-1f, 1f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-1f, 1f);
        
        // Add to parent
        effectObj.transform.SetParent(transform);
        
        return ps;
    }
    
    // Create default line clear effect
    private ParticleSystem CreateDefaultLineClearEffect()
    {
        GameObject effectObj = new GameObject("DefaultLineClearEffect");
        ParticleSystem ps = effectObj.AddComponent<ParticleSystem>();
        
        var main = ps.main;
        main.duration = 0.8f;
        main.startLifetime = 1.2f;
        main.startSpeed = 3f;
        main.startSize = 0.3f;
        main.startColor = Color.white;
        main.gravityModifier = 0.3f;
        main.maxParticles = 30;
        
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { 
            new ParticleSystem.Burst(0f, 25) 
        });
        
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;
        
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-2f, 2f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-2f, 2f);
        
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(Color.white, Color.clear);
        
        // Add to parent
        effectObj.transform.SetParent(transform);
        
        return ps;
    }
    
    // Clear all active effects
    public void ClearAllEffects()
    {
        foreach (var kvp in activeEffects)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value.gameObject);
            }
        }
        activeEffects.Clear();
    }
}
