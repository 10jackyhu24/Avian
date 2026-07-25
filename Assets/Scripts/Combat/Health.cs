using UnityEngine;
using Unity.Netcode;

public class Health : NetworkBehaviour
{
    [SerializeField] private float maxHP = 100.0f; //預設初始值100.0，可在Unity介面更改

    public float MaxHP => maxHP; // 給外部可以讀的屬性，名字是MaxHP
    
    //Network變數初始語法(intiValue, readRule, writeRule)
    public NetworkVariable<float> currentHP 
            = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);


    public override void OnNetworkSpawn() {
        if(IsServer) {
            currentHP.Value = maxHP;
        }
    }

    public void TakeDamage(float damage) {
        if(!IsServer || damage < 0f || currentHP.Value < 0f) return;

        currentHP.Value -= damage;
        Debug.Log($"[{name}] got {damage} damage");

        if(currentHP.Value <= 0f) {
            OnDead();
        }
    }

    //死亡後面的東西之後在加
    private void OnDead() {
        Debug.Log($"[{name}] is dead");
    }
}
