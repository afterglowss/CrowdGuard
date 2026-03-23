using UnityEngine;

[ExecuteAlways]
public class RopeSag : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Transform player1;
    [SerializeField] private Transform player2;
    [SerializeField] private int resolution = 20; 
    [SerializeField] private float sagAmount = 2.0f;
    [SerializeField] private float maxRopeLength = 10.0f;
    [SerializeField] private float baseSag = 2.0f;
    [SerializeField] private float ropeRadius = 0.1f; // Prevents the center from clipping halfway into the floor
    [SerializeField] private float elasticity = 5.0f;
    [SerializeField] private float damping = 0.5f;
    [SerializeField] private LayerMask collisionLayers;
    
    private Vector3 _currentControlPos;
    private Vector3 _velocity;
    void Start() => _currentControlPos = GetTargetControlPoint();

    void Update()
    {
        Vector3 target = GetTargetControlPoint();

        // Simple Spring Physics (Acceleration toward target)
        Vector3 force = (target - _currentControlPos) * elasticity;
        _velocity = (_velocity + force * Time.deltaTime) * damping;
        _currentControlPos += _velocity * Time.deltaTime;

        DrawBezier(_currentControlPos);
    }


    

    Vector3 GetTargetControlPoint()
    {
        float currentDistance = Vector3.Distance(player1.position, player2.position);

        // Tension: 1.0 when close (full sag), 0.0 when at max length (straight)
        float tensionFactor = Mathf.Clamp01(1.0f - (currentDistance / maxRopeLength));
        float dynamicSag = baseSag * tensionFactor;

        Vector3 midPoint = Vector3.Lerp(player1.position, player2.position, 0.5f);
        Vector3 targetSag = midPoint + (Vector3.down * dynamicSag);

        // Collision Check (Same as before, using dynamicSag)
        if (Physics.Raycast(midPoint, Vector3.down, out RaycastHit hit, dynamicSag + ropeRadius, collisionLayers))
        {
            return hit.point + (Vector3.up * ropeRadius);
        }

        return targetSag;
    }
    void DrawBezier(Vector3 controlPoint)
    {
        lineRenderer.positionCount = resolution;
        for (int i = 0; i < resolution; i++)
        {
            float t = i / (float)(resolution - 1);
            Vector3 pos = Mathf.Pow(1 - t, 2) * player1.position +
                          2 * (1 - t) * t * controlPoint +
                          Mathf.Pow(t, 2) * player2.position;
            lineRenderer.SetPosition(i, pos);
        }
    }
}
