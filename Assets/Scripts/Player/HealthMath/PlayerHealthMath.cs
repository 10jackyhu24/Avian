using UnityEngine;

public static class PlayerHealthMath {
    public static float AfterDamage(float currentHealth, float damage) {
        if (damage <= 0f) {
            return currentHealth;
        }

        return Mathf.Max(0f, currentHealth - damage);
    }
}
