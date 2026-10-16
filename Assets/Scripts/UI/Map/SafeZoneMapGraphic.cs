using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Separate overlay so the safe-zone preview remains visible through map fog.
public class SafeZoneMapGraphic : MaskableGraphic {
    private const int Segments = 192;
    private readonly List<float> angles = new List<float>(Segments + 4);
    private ZoneSnapshot snapshot;
    private float worldSize;
    private float lineWidth = 1.25f;
    private Color outsideColor;
    private Color edgeColor;

    public void SetZone(ZoneSnapshot value, float size, bool largeMap, Color outside, Color edge) {
        float width = largeMap ? 2f : 1.25f;
        if (worldSize == size && lineWidth == width && snapshot.Center == value.Center &&
            snapshot.Radius == value.Radius && snapshot.TargetCenter == value.TargetCenter &&
            snapshot.TargetRadius == value.TargetRadius && snapshot.Phase == value.Phase &&
            outsideColor == outside && edgeColor == edge) return;
        snapshot = value;
        worldSize = Mathf.Max(1f, size);
        lineWidth = width;
        outsideColor = outside;
        edgeColor = edge;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper helper) {
        helper.Clear();
        if (worldSize <= 0f) return;
        Rect rect = rectTransform.rect;
        Vector2 center = WorldToRect(snapshot.Center, rect);
        float radius = snapshot.Radius / worldSize * rect.width;
        DrawOutside(helper, rect, center, radius);
        DrawCircle(helper, center, radius, edgeColor, false);
        if (snapshot.Phase != ZonePhase.Complete) {
            Vector2 target = WorldToRect(snapshot.TargetCenter, rect);
            DrawCircle(helper, target, snapshot.TargetRadius / worldSize * rect.width, Color.white, true);
            float arm = lineWidth * 2.5f;
            AddQuad(helper, target + new Vector2(-arm, -lineWidth * 0.5f),
                target + new Vector2(-arm, lineWidth * 0.5f), target + new Vector2(arm, lineWidth * 0.5f),
                target + new Vector2(arm, -lineWidth * 0.5f), Color.white);
            AddQuad(helper, target + new Vector2(-lineWidth * 0.5f, -arm),
                target + new Vector2(-lineWidth * 0.5f, arm), target + new Vector2(lineWidth * 0.5f, arm),
                target + new Vector2(lineWidth * 0.5f, -arm), Color.white);
        }
    }

    private Vector2 WorldToRect(Vector2 world, Rect rect) {
        return new Vector2(rect.xMin + (world.x / worldSize + 0.5f) * rect.width,
            rect.yMin + (world.y / worldSize + 0.5f) * rect.height);
    }

    private void DrawOutside(VertexHelper helper, Rect rect, Vector2 center, float radius) {
        angles.Clear();
        for (int i = 0; i < Segments; i++) angles.Add(i * Mathf.PI * 2f / Segments);
        // Include all rectangle corners so the tint covers the complete map.
        AddCornerAngle(new Vector2(rect.xMin, rect.yMin) - center);
        AddCornerAngle(new Vector2(rect.xMin, rect.yMax) - center);
        AddCornerAngle(new Vector2(rect.xMax, rect.yMin) - center);
        AddCornerAngle(new Vector2(rect.xMax, rect.yMax) - center);
        angles.Sort();
        Color32 tint = outsideColor;
        for (int i = 0; i < angles.Count; i++) {
            Vector2 a = new Vector2(Mathf.Cos(angles[i]), Mathf.Sin(angles[i]));
            float next = angles[(i + 1) % angles.Count];
            Vector2 b = new Vector2(Mathf.Cos(next), Mathf.Sin(next));
            AddQuad(helper, center + a * radius, RayToBorder(rect, center, a),
                RayToBorder(rect, center, b), center + b * radius, tint);
        }
    }

    private void AddCornerAngle(Vector2 direction) {
        float angle = Mathf.Atan2(direction.y, direction.x);
        angles.Add(angle < 0f ? angle + Mathf.PI * 2f : angle);
    }

    private static Vector2 RayToBorder(Rect rect, Vector2 center, Vector2 direction) {
        float x = Mathf.Abs(direction.x) < 0.00001f ? float.PositiveInfinity
            : ((direction.x > 0f ? rect.xMax : rect.xMin) - center.x) / direction.x;
        float y = Mathf.Abs(direction.y) < 0.00001f ? float.PositiveInfinity
            : ((direction.y > 0f ? rect.yMax : rect.yMin) - center.y) / direction.y;
        return center + direction * Mathf.Min(x, y);
    }

    private void DrawCircle(VertexHelper helper, Vector2 center, float radius, Color32 color, bool dashed) {
        float inner = Mathf.Max(0f, radius - lineWidth * 0.5f);
        float outer = radius + lineWidth * 0.5f;
        for (int i = 0; i < Segments; i++) {
            if (dashed && (i / 4) % 2 != 0) continue;
            float angle = i * Mathf.PI * 2f / Segments;
            float next = (i + 1) * Mathf.PI * 2f / Segments;
            Vector2 a = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 b = new Vector2(Mathf.Cos(next), Mathf.Sin(next));
            AddQuad(helper, center + a * inner, center + a * outer,
                center + b * outer, center + b * inner, color);
        }
    }

    private static void AddQuad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color) {
        int start = helper.currentVertCount;
        helper.AddVert(a, color, Vector2.zero);
        helper.AddVert(b, color, Vector2.zero);
        helper.AddVert(c, color, Vector2.zero);
        helper.AddVert(d, color, Vector2.zero);
        helper.AddTriangle(start, start + 1, start + 2);
        helper.AddTriangle(start, start + 2, start + 3);
    }
}
