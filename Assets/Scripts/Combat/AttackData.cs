using UnityEngine;


[CreateAssetMenu(fileName = "NewAttackData", menuName = "Combat/Attack Data")]
public class AttackData : ScriptableObject
{
    [Header("Basic")]
    public string attackName;
    public float damage = 10f;
    public float warningDuration = 1f;
    public float cooldown = 1f;

    [Header("Warning")]
    public GameObject warningPrefab;

    [Header("Attack Shape")]
    public AttackShape attackShape = AttackShape.Circle;

    [Header("Circle")]
    public float radius = 2f;

    [Header("Cone")]
    public float coneRadius = 2f;
    public float coneAngle = 90f;

    [Header("Line")]
    public float lineLength = 4f;
    public float lineWidth = 1f;

    [Header("Rectangle")]
    public float rectangleLength = 4f;
    public float rectangleWidth = 2f;
}

public enum AttackShape
{
    Circle,
    Cone,
    Line,
    Rectangle
}