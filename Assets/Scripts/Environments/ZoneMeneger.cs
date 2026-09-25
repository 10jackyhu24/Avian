using UnityEngine;

public class ZoneManager : MonoBehaviour
{
    [Header("Poison Circle")]
    //GameTime: 8 mins
    [SerializeField] private float totalMatchTime = 480f; // 480 secs
    private float currentElapsedTime = 0f;
    private bool isInitialized = false;

    private Vector3 worldCenter;
    private float initialRadius;
    private float currentRadius;

    private void Update()
    {
        if (!isInitialized)
        {
            if (GameState.isPlaying)
            {
                InitializeZone();
            }
            return;
        }

        // 8 mins countdown
        if (currentElapsedTime < totalMatchTime)
        {
            currentElapsedTime += Time.deltaTime;
            float progress = currentElapsedTime / totalMatchTime;
            currentRadius = Mathf.Lerp(initialRadius, initialRadius * 0.1f, progress);
        }
    }

    private void InitializeZone()
    {
        if (TerrainController.Instance != null)
        {
            worldCenter = TerrainController.Instance.GetServerWorldCenter();
            initialRadius = TerrainController.Instance.GetServerWorldSize() * 0.5f;
            currentRadius = initialRadius;

            isInitialized = true;
            Debug.LogError($"[ZoneManager] Get World Center. Center Coordinate：{worldCenter}，Initial Radius：{initialRadius}");
        }
        else
        {
            // 如果地圖還在生成，下一幀會再檢查一次
            Debug.LogWarning("[ZoneManager] Waiting for TerrainController ...");
        }
    }
}