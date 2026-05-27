using UnityEngine;

[ExecuteAlways]
public class HitPathPoint : MonoBehaviour
{
    public Color gizmoColor = new Color(1f, 0.7f, 0f, 0.8f);
    public float gizmoRadius = 0.08f;

    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, gizmoRadius);

        // Label with order
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.15f, gameObject.name, UnityEditor.EditorStyles.miniLabel);
#endif
    }
}
