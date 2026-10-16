using System;
using UnityEngine;

public enum ZonePhase {
    Waiting,
    Shrinking,
    Complete
}

public struct ZoneSnapshot {
    public Vector2 Center;
    public float Radius;
    public Vector2 TargetCenter;
    public float TargetRadius;
    public int StageIndex;
    public ZonePhase Phase;
    public float SecondsRemaining;

    public bool IsOutside(Vector3 position) {
        return (new Vector2(position.x, position.z) - Center).sqrMagnitude > Radius * Radius;
    }
}

// Pure timeline shared by the server, clients, world overlay and minimap.
public sealed class ZoneSchedule {
    public const int StageCount = 3;
    private readonly Vector2[] centers = new Vector2[StageCount + 1];
    private readonly float[] radii = new float[StageCount + 1];
    private readonly float totalDuration;

    public ZoneSchedule(int seed, Vector2 center, float initialRadius, float finalRadius, float duration) {
        totalDuration = Mathf.Max(1f, duration);
        centers[0] = center;
        radii[0] = Mathf.Max(1f, initialRadius);
        radii[1] = radii[0] * 0.45f;
        radii[2] = radii[0] * 0.18f;
        radii[3] = Mathf.Clamp(finalRadius, 0.01f, radii[2] * 0.8f);

        System.Random random = new System.Random(seed ^ 0x514F3A27);
        for (int stage = 1; stage <= StageCount; stage++) {
            // Keeping the entire target inside its parent also keeps it inside
            // every intermediate circle as the boundary moves toward it.
            float availableOffset = radii[stage - 1] - radii[stage];
            for (int attempt = 0; ; attempt++) {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = availableOffset * (0.25f + (float)random.NextDouble() * 0.5f);
                Vector2 candidate = centers[stage - 1] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                bool unique = true;
                for (int previous = 0; previous < stage; previous++) {
                    if ((candidate - centers[previous]).sqrMagnitude < availableOffset * availableOffset * 0.0001f) {
                        unique = false;
                        break;
                    }
                }
                if (unique) {
                    centers[stage] = candidate;
                    break;
                }
                if (attempt >= 64) {
                    throw new InvalidOperationException("Could not choose distinct safe-zone centers.");
                }
            }
        }
    }

    public ZoneSnapshot Evaluate(float elapsedSeconds) {
        float elapsed = Mathf.Clamp(elapsedSeconds, 0f, totalDuration);
        float cursor = 0f;
        for (int stage = 0; stage < StageCount; stage++) {
            // Defaults: wait 90, shrink 90, wait 60, shrink 90, wait 60,
            // shrink 90 seconds. Scaling retains the exact requested duration.
            float wait = totalDuration * (stage == 0 ? 90f : 60f) / 480f;
            float shrink = totalDuration * 90f / 480f;
            if (elapsed < cursor + wait) {
                return Snapshot(stage, ZonePhase.Waiting, 0f, cursor + wait - elapsed);
            }
            cursor += wait;
            if (elapsed < cursor + shrink && elapsed < totalDuration) {
                return Snapshot(stage, ZonePhase.Shrinking, (elapsed - cursor) / shrink, cursor + shrink - elapsed);
            }
            cursor += shrink;
        }
        return new ZoneSnapshot {
            Center = centers[StageCount],
            Radius = radii[StageCount],
            TargetCenter = centers[StageCount],
            TargetRadius = radii[StageCount],
            StageIndex = StageCount - 1,
            Phase = ZonePhase.Complete,
            SecondsRemaining = 0f
        };
    }

    private ZoneSnapshot Snapshot(int stage, ZonePhase phase, float progress, float remaining) {
        return new ZoneSnapshot {
            Center = Vector2.Lerp(centers[stage], centers[stage + 1], progress),
            Radius = Mathf.Lerp(radii[stage], radii[stage + 1], progress),
            TargetCenter = centers[stage + 1],
            TargetRadius = radii[stage + 1],
            StageIndex = stage,
            Phase = phase,
            SecondsRemaining = remaining
        };
    }
}
