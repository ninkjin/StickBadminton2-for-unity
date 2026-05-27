using UnityEngine;

[ExecuteAlways]
public class GripMarker : MonoBehaviour
{
    public Color color = Color.green;
    public float radius = 0.15f;
    public string label = "";

    [Header("弧线覆盖（勾选后使用自己的值而非父级参数）")]
    public bool overrideArc = false;
    public float arcLength = 1.0f;
    [Range(-360f, 360f)] public float arcAngleStart = 50f;
    [Range(-360f, 360f)] public float arcAngleEnd = -40f;
    [Range(-360f, 360f)] public float arcRotation = 0f;

    void OnDrawGizmos()
    {
        // HitPath 内的点只画小标记 + 连线，不画弧线
        bool isHitPathPoint = transform.parent != null && transform.parent.name.StartsWith("HitPath");

        if (isHitPathPoint)
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(transform.position, radius);

            // 画到下一点的连线
            int idx = transform.GetSiblingIndex();
            if (idx + 1 < transform.parent.childCount)
            {
                Transform next = transform.parent.GetChild(idx + 1);
                Gizmos.color = new Color(color.r, color.g, color.b, 0.5f);
                Gizmos.DrawLine(transform.position, next.position);
            }

#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.15f, gameObject.name, UnityEditor.EditorStyles.miniLabel);
#endif
            return;
        }

        // 握拍标记：画大圆 + 弧线
        Gizmos.color = color;
        Gizmos.DrawWireSphere(transform.position, radius * 1.5f);

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(label))
            UnityEditor.Handles.Label(transform.position + Vector3.up * (radius + 0.2f), label, UnityEditor.EditorStyles.miniLabel);
#endif

        Transform st = transform.parent; // SwingOverlay
        if (st == null) return;
        var parent = st.parent;
        if (parent == null) return;

        float len, aStart, aEnd, hzRadius;
        if (overrideArc)
        {
            len = arcLength;
            aStart = arcAngleStart + arcRotation;
            aEnd = arcAngleEnd + arcRotation;
            TryGetZoneRadius(parent, out hzRadius);
        }
        else
        {
            if (!TryGetParams(parent, out len, out aStart, out aEnd, out hzRadius))
                return;
        }

        DrawArc(transform.position, st, len, aStart, aEnd, hzRadius);
        DrawArcEndpoints(transform.position, st, len, aStart, aEnd);
    }

    bool TryGetParams(Transform character, out float length, out float angleStart, out float angleEnd, out float zoneRadius)
    {
        bool isUnderhand = name.Contains("Underhand");

        var bc = character.GetComponent<BattleCharacter>();
        if (bc != null)
        {
            length = bc.racketLength;
            angleStart = isUnderhand ? bc.racketAngleStartBackhand : bc.racketAngleStart;
            angleEnd = isUnderhand ? bc.racketAngleEndBackhand : bc.racketAngleEnd;
            zoneRadius = bc.hitZoneRadius;
            return true;
        }
        var ai = character.GetComponent<AIController>();
        if (ai != null)
        {
            length = ai.racketLength;
            angleStart = isUnderhand ? ai.racketAngleStartBackhand : ai.racketAngleStart;
            angleEnd = isUnderhand ? ai.racketAngleEndBackhand : ai.racketAngleEnd;
            zoneRadius = ai.hitZoneRadius;
            return true;
        }
        length = 1f; angleStart = 50f; angleEnd = -40f; zoneRadius = 0.5f;
        return false;
    }

    void TryGetZoneRadius(Transform character, out float zoneRadius)
    {
        var bc = character.GetComponent<BattleCharacter>();
        if (bc != null) { zoneRadius = bc.hitZoneRadius; return; }
        var ai = character.GetComponent<AIController>();
        if (ai != null) { zoneRadius = ai.hitZoneRadius; return; }
        zoneRadius = 0.5f;
    }

    void DrawArc(Vector3 gripWorldPos, Transform swingTrans, float len, float angleStart, float angleEnd, float hzRadius)
    {
        float startRad = angleStart * Mathf.Deg2Rad;
        float endRad = angleEnd * Mathf.Deg2Rad;
        Vector2 gripLocal = swingTrans.InverseTransformPoint(gripWorldPos);

        // Sweep arc
        Gizmos.color = new Color(color.r, color.g, color.b, 0.4f);
        int segments = 16;
        for (int i = 0; i < segments; i++)
        {
            float t0 = (float)i / segments;
            float t1 = (float)(i + 1) / segments;
            float a0 = Mathf.Lerp(startRad, endRad, t0);
            float a1 = Mathf.Lerp(startRad, endRad, t1);
            Vector3 p0 = swingTrans.TransformPoint(new Vector3(gripLocal.x - Mathf.Sin(a0) * len, gripLocal.y + Mathf.Cos(a0) * len, 0));
            Vector3 p1 = swingTrans.TransformPoint(new Vector3(gripLocal.x - Mathf.Sin(a1) * len, gripLocal.y + Mathf.Cos(a1) * len, 0));
            Gizmos.DrawLine(p0, p1);
        }

        // Hit zone circle at mid-angle
        float midRad = Mathf.Lerp(startRad, endRad, 0.5f);
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.6f);
        Vector3 midPos = swingTrans.TransformPoint(new Vector3(gripLocal.x - Mathf.Sin(midRad) * len, gripLocal.y + Mathf.Cos(midRad) * len, 0));
        Gizmos.DrawWireSphere(midPos, hzRadius);
    }

    void DrawArcEndpoints(Vector3 gripWorldPos, Transform swingTrans, float len, float angleStart, float angleEnd)
    {
        float startRad = angleStart * Mathf.Deg2Rad;
        float endRad = angleEnd * Mathf.Deg2Rad;
        Vector2 gl = swingTrans.InverseTransformPoint(gripWorldPos);

        // Draw small dots at arc start and end
        Gizmos.color = Color.white;
        Vector3 startPos = swingTrans.TransformPoint(new Vector3(gl.x - Mathf.Sin(startRad) * len, gl.y + Mathf.Cos(startRad) * len, 0));
        Vector3 endPos = swingTrans.TransformPoint(new Vector3(gl.x - Mathf.Sin(endRad) * len, gl.y + Mathf.Cos(endRad) * len, 0));
        Gizmos.DrawWireSphere(startPos, 0.06f);
        Gizmos.DrawWireSphere(endPos, 0.06f);
    }
}
