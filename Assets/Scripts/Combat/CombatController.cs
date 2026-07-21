using UnityEngine;
using Unity.Netcode;
using System.Collections;


public class CombatController : NetworkBehaviour
{
    [Header("Attack Setting")]
    [SerializeField] private AttackData CurrentAttack; //讓腳本知道是哪種攻擊
    [SerializeField] private Transform OriginPoint; //攻擊發起點(通常會放一個空的gameObj)
    [SerializeField] private LayerMask TargetLayer; //目標layer 攻擊只作用在特定layer(打算用一個Enemy Layer，或是創一個"canAttack Layer")

    public NetworkVariable<bool> IsCombating = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsAttacking = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public void EntryCombat() {
        if(!IsServer) return;

        IsCombating.Value = true;
    }

    public void ExitCombat() {
        if(!IsServer) return;

        IsCombating.Value = false;   
    }
        
    public void RequestAttact() {
        if(!IsOwner) return;

        RequestAttactServerRpc();
    }

    [ServerRpc] 
    private void RequestAttactServerRpc() {
        //如果不在攻擊模式，或已經在攻擊流程裡
        if(!IsCombating.Value || IsAttacking.Value) return;

        if(CurrentAttack == null) {
            Debug.LogWarning("CurrentAttack is empty");
            return;
        }

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine() {
        IsAttacking.Value = true;
        ShowWarningClientRpc(OriginPoint.position, CurrentAttack.radius);
        yield return new WaitForSeconds(CurrentAttack.warningDuration);
        DoAttactDamage();
        IsAttacking.Value = false;
    }

    private void DoAttactDamage() {
        Collider[] hits = CheckColliderByShape(CurrentAttack);
            
        foreach (Collider hit in hits) {
            Health health = hit.GetComponent<Health>();
            if (health != null) {
                health.TakeDamage(CurrentAttack.damage);
            }
        }
    }

    private Collider[] CheckColliderByShape(AttackData attackData) {
        switch (attackData.attackShape) {
            case AttackShape.Circle:
                return Physics.OverlapSphere(OriginPoint.position, attackData.radius, TargetLayer);
            
            case AttackShape.Rectangle: 
                return Physics.OverlapBox(
                OriginPoint.position,
                new Vector3(attackData.rectangleLength, 1f, attackData.rectangleWidth) * 0.5f,
                OriginPoint.rotation,
                TargetLayer);

            case AttackShape.Line:
                return Physics.OverlapBox(
                OriginPoint.position + OriginPoint.forward * (attackData.lineLength* 0.5f),
                new Vector3(attackData.lineWidth * 0.5f, 0.5f, attackData.lineLength * 0.5f),
                OriginPoint.rotation,
                TargetLayer);
             
            case AttackShape.Cone:
                {
                    Collider[] sphereHits = Physics.OverlapSphere(
                        OriginPoint.position,
                        attackData.coneRadius,
                        TargetLayer);

                    Collider[] result = new Collider[sphereHits.Length];
                    int count = 0;

                    foreach (Collider hit in sphereHits)
                    {
                        Vector3 directionToTarget = (hit.transform.position - OriginPoint.position).normalized;
                        float angleToTarget = Vector3.Angle(OriginPoint.forward, directionToTarget);

                        if (angleToTarget <= attackData.coneAngle * 0.5f)
                        {
                            result[count] = hit;
                            count++;
                        }
                    }

                    Collider[] finalResult = new Collider[count];
                    for (int i = 0; i < count; i++)
                    {
                        finalResult[i] = result[i];
                    }

                    return finalResult;
                }
                
            default:
                Debug.LogWarning("Not Found Attack Shape");
                return null;
        }
    }

    [ClientRpc]
    private void ShowWarningClientRpc(Vector3 position, float range1) {
        Debug.Log($"Warning is showed at {position}");
    }

    [ClientRpc]
    private void HideWarningClientRpc() {
        Debug.Log("Warning is hided");
    }
    
}
