using UnityEngine;

public enum EnemyArchetype : byte {
    Grunt,
    Raider,
    Spitter,
    MiniBoss,
    Boss
}

public enum EnemyAttackPattern : byte {
    Swipe,
    Bite,
    Projectile,
    Charge,
    Slam
}

public sealed class EnemyArchetypeStats {
    public string Name;
    public int MaxHealth;
    public float VisualScale;
    public float ColliderScale;
    public float PatrolSpeed;
    public float ChaseSpeed;
    public float DetectionRange;
    public float AttackRange;
    public float Damage;
    public float Cooldown;
    public float Windup;
    public float SectorAngle;
    public float ProjectileSpeed;
    public float ChargeSpeed;
    public float ChargeDuration;
    public int MinCoins;
    public int MaxCoins;
    public EnemyAttackPattern Pattern;
}

// One shared network prefab uses these profiles; the server only replicates the archetype ID.
public static class EnemyArchetypes {
    public static EnemyArchetype ForDemoIndex(int index) {
        return (EnemyArchetype)(Mathf.Abs(index) % 5);
    }

    public static EnemyArchetype ForBiomeSpawn(TerrainBiomeMask biome, int index, int roll) {
        // Reserved positions guarantee that each seeded world contains its bosses.
        if (biome == TerrainBiomeMask.Mountain && index % 50 == 0) return EnemyArchetype.Boss;
        if (index % 25 == 0 && index > 0) return EnemyArchetype.MiniBoss;
        if (biome == TerrainBiomeMask.Desert) {
            return roll < 55 ? EnemyArchetype.Spitter :
                roll < 80 ? EnemyArchetype.Raider : EnemyArchetype.Grunt;
        }
        if (biome == TerrainBiomeMask.Mountain) {
            return roll < 40 ? EnemyArchetype.Raider :
                roll < 70 ? EnemyArchetype.Spitter : EnemyArchetype.Grunt;
        }
        return roll < 55 ? EnemyArchetype.Grunt :
            roll < 85 ? EnemyArchetype.Raider : EnemyArchetype.Spitter;
    }

    private static readonly EnemyArchetypeStats Grunt = new EnemyArchetypeStats {
        Name = "Grunt", MaxHealth = 30, VisualScale = 1f, ColliderScale = 1f,
        PatrolSpeed = 2.5f, ChaseSpeed = 4.5f, DetectionRange = 12f,
        AttackRange = 1.8f, Damage = 8f, Cooldown = 1.3f, Windup = 0.35f,
        SectorAngle = 360f, MinCoins = 1, MaxCoins = 3,
        Pattern = EnemyAttackPattern.Swipe
    };

    private static readonly EnemyArchetypeStats Raider = new EnemyArchetypeStats {
        Name = "Raider", MaxHealth = 22, VisualScale = 0.82f, ColliderScale = 0.85f,
        PatrolSpeed = 3.4f, ChaseSpeed = 6.2f, DetectionRange = 15f,
        AttackRange = 2.1f, Damage = 9f, Cooldown = 0.85f, Windup = 0.2f,
        SectorAngle = 85f, MinCoins = 1, MaxCoins = 2,
        Pattern = EnemyAttackPattern.Bite
    };

    private static readonly EnemyArchetypeStats Spitter = new EnemyArchetypeStats {
        Name = "Spitter", MaxHealth = 24, VisualScale = 0.95f, ColliderScale = 0.95f,
        PatrolSpeed = 2f, ChaseSpeed = 3.2f, DetectionRange = 18f,
        AttackRange = 13f, Damage = 7f, Cooldown = 2.1f, Windup = 0.65f,
        SectorAngle = 8f, ProjectileSpeed = 12f, MinCoins = 2, MaxCoins = 4,
        Pattern = EnemyAttackPattern.Projectile
    };

    private static readonly EnemyArchetypeStats MiniBoss = new EnemyArchetypeStats {
        Name = "Charging Warden", MaxHealth = 130, VisualScale = 1.2f, ColliderScale = 1.35f,
        PatrolSpeed = 2.2f, ChaseSpeed = 4.2f, DetectionRange = 18f,
        AttackRange = 7f, Damage = 20f, Cooldown = 4.5f, Windup = 0.85f,
        SectorAngle = 22f, ChargeSpeed = 12f, ChargeDuration = 0.55f,
        MinCoins = 8, MaxCoins = 15,
        Pattern = EnemyAttackPattern.Charge
    };

    private static readonly EnemyArchetypeStats Boss = new EnemyArchetypeStats {
        // Medusa sprites use 100 PPU; the bird enemy sprites use 36 PPU.
        Name = "Medusa", MaxHealth = 500, VisualScale = 6f, ColliderScale = 1.85f,
        PatrolSpeed = 1.6f, ChaseSpeed = 3f, DetectionRange = 24f,
        AttackRange = 5.5f, Damage = 28f, Cooldown = 5.5f, Windup = 1.2f,
        SectorAngle = 360f, ProjectileSpeed = 9f, MinCoins = 30, MaxCoins = 50,
        Pattern = EnemyAttackPattern.Slam
    };

    public static EnemyArchetypeStats Get(EnemyArchetype archetype) {
        return archetype switch {
            EnemyArchetype.Raider => Raider,
            EnemyArchetype.Spitter => Spitter,
            EnemyArchetype.MiniBoss => MiniBoss,
            EnemyArchetype.Boss => Boss,
            _ => Grunt
        };
    }
}
