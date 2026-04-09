using UnityEngine;

[ExecuteAlways]
public class AvalancheSnowEndMarker : MonoBehaviour
{
    public AvalancheController target;

    void Update()
    {
        if (target != null)
            target.snowEndWorldY = transform.position.y;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.4f);
        Gizmos.DrawWireCube(transform.position, new Vector3(30f, 0.1f, 30f));

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position, "❄ Snow End");
#endif
    }
}