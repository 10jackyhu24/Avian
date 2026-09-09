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

    [Header("Attack Effect")]
    public GameObject attackEffect;
    public float effectLifetime = 0.3f;

    [Header("Attack Mode")]
    public AttackMode attackmode = AttackMode.UnarmedMelee;
}

public enum AttackMode
{
    UnarmedMelee, // 徒手近戰
    WeaponMelee,  // 近戰武器
    WeaponRanged  // 遠程武器
}