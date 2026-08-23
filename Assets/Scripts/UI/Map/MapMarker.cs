using UnityEngine;

public enum MapMarkerType {
    Merchant,
    Boss,
    Structure,
    Dungeon,
    Quest,
    Custom
}

public class MapMarker : MonoBehaviour {
    [SerializeField] private string markerId;
    [SerializeField] private MapMarkerType markerType = MapMarkerType.Custom;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField] private float discoveryRadius = 20f;

    public string MarkerId => markerId;
    public MapMarkerType MarkerType => markerType;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public float DiscoveryRadius => discoveryRadius;
    public Vector3 WorldPosition => transform.position;
}
