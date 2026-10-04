using UnityEngine;

[CreateAssetMenu(fileName = "NewAttackData", menuName = "Combat/Attack Data")]
public class AttackData : ScriptableObject
{
    [Header("Basic")]
    public string attackName;
    public float damage = 10f;
    public float warningDuration = 1f;
    public float cooldown = 1f;

    [Header("Range / Hit")]
    public float radius = 2f; // attackRadius
    public Vector3 centerOffset = new Vector3(0f, 1f, 0f); // attackCenterOffset
    public LayerMask targetLayer = ~0; // attackLayerMask
    public AttackShape attackShape = AttackShape.Radial;
    [Range(1f, 360f)] public float sectorAngle = 90f;

    [Header("Attack Effect")]
    public GameObject attackEffect;
    [Min(0f)] public float effectForwardOffset = 1.25f;
    [Min(0.01f)] public float effectScale = 1f;
    public float effectLifetime = 0.3f;

    [Header("Weapon Swing Visual")]
    [Min(0.1f)] public float weaponVisualScale = 2f;

    [Header("Ranged Projectile")]
    [Min(0.1f)] public float projectileSpeed = 18f;
    [Min(0.1f)] public float projectileMaxDistance = 14f;
    [Min(0.01f)] public float projectileRadius = 0.12f;
    [Min(0.1f)] public float projectileVisualLength = 1.6f;
    [Min(0.01f)] public float projectileTrailWidth = 0.1f;
    [Min(0.01f)] public float projectileTrailDuration = 0.12f;

    [Header("Attack Mode")]
    public AttackMode attackmode = AttackMode.UnarmedMelee;
}

public enum AttackShape
{
    Radial,
    Sector
}

public enum AttackMode
{
    UnarmedMelee, // 徒手近戰
    WeaponMelee,  // 近戰武器
    WeaponRanged  // 遠程武器
}
