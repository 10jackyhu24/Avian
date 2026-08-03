using UnityEngine;

// Base class for all item effects
public abstract class ItemEffect : ScriptableObject {
    public abstract void Execute(GameObject user);
}

[CreateAssetMenu(fileName = "HealEffect", menuName = "Inventory/Effects/Heal")]
public class HealEffect : ItemEffect {
    public int healAmount = 50;

    public override void Execute(GameObject user) {
        Debug.Log($"Healed {user.name} for {healAmount} HP");
    }
}

[CreateAssetMenu(fileName = "DamageEffect", menuName = "Inventory/Effects/Damage")]
public class DamageEffect : ItemEffect {
    public int baseDamage = 20;

    public override void Execute(GameObject user) {
        Debug.Log($"Applied {baseDamage} damage from {user.name}");
    }
}

[CreateAssetMenu(fileName = "AttackSpeed", menuName = "Inventory/Effects/Damage")]
public class AttackSpeed : ItemEffect {
    public int attackSpeed = 20;

    public override void Execute(GameObject user) {
        Debug.Log($"Applied attack speed {attackSpeed} from {user.name}");
    }
}