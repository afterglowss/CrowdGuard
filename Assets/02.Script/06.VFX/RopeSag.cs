using UnityEngine;

[ExecuteAlways]
public class RopeSag : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Transform player1;
    [SerializeField] private Transform player2;

    [Header("Rope")]
    [SerializeField] private int segments = 20;
    [SerializeField] private float maxRopeLength = 10f;
    [SerializeField] private float ropeRadius = 0.05f;

    [Header("Simulation")]
    [SerializeField] private int solverIterations = 10;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float damping = 0.98f;      // 0–1: energy retained per frame
    [SerializeField] private LayerMask collisionLayers;

    // --- Verlet particle ---
    private struct Particle
    {
        public Vector3 pos;
        public Vector3 prevPos;
        public bool locked;
    }

    private Particle[] _particles;
    private float _segmentLength;   // rest length of each segment

    // ---------------------------------------------------------------
    void OnEnable() => InitRope();
    void OnValidate() => InitRope();    // rebuilds in Editor when you tweak values

    void InitRope()
    {
        if (player1 == null || player2 == null) return;

        _particles = new Particle[segments + 1];
        float ropeLen = Mathf.Min(Vector3.Distance(player1.position, player2.position),
                                  maxRopeLength);
        _segmentLength = ropeLen / segments;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            _particles[i] = new Particle
            {
                pos = Vector3.Lerp(player1.position, player2.position, t),
                prevPos = Vector3.Lerp(player1.position, player2.position, t),
                locked = (i == 0 || i == segments)
            };
        }

        if (lineRenderer != null)
            lineRenderer.positionCount = segments + 1;
    }

    // ---------------------------------------------------------------
    void Update()
    {
        if (_particles == null || _particles.Length != segments + 1)
            InitRope();

        // --- Recalculate rest length based on tautness ---
        float dist = Vector3.Distance(player1.position, player2.position);

        // If players move beyond maxRopeLength the rope goes taut:
        // clamp total rope length so segments can't stretch further.
        float ropeLen = Mathf.Min(dist, maxRopeLength);
        _segmentLength = ropeLen / segments;

        // --- Anchor locked ends to player positions ---
        _particles[0].pos = player1.position;
        _particles[segments].pos = player2.position;

        float dt = Time.deltaTime;

        // 1. Verlet integration (velocity = pos - prevPos, implicit)
        Simulate(dt);

        // 2. Constraint solve (N iterations for stiffness)
        for (int iter = 0; iter < solverIterations; iter++)
            SolveConstraints();

        // 3. Re-pin anchors after solving (solver may drift them)
        _particles[0].pos = player1.position;
        _particles[segments].pos = player2.position;

        // 4. Draw
        UpdateLineRenderer();
    }

    // ---------------------------------------------------------------
    void Simulate(float dt)
    {
        Vector3 gravityVec = new Vector3(0, gravity * dt * dt, 0);

        for (int i = 0; i <= segments; i++)
        {
            if (_particles[i].locked) continue;

            Vector3 vel = (_particles[i].pos - _particles[i].prevPos) * damping;
            _particles[i].prevPos = _particles[i].pos;
            _particles[i].pos += vel + gravityVec;
        }
    }

    // ---------------------------------------------------------------
    void SolveConstraints()
    {
        for (int i = 0; i < segments; i++)
        {
            ref Particle a = ref _particles[i];
            ref Particle b = ref _particles[i + 1];

            Vector3 delta = b.pos - a.pos;
            float dist = delta.magnitude;
            if (dist < 0.0001f) continue;

            float diff = (dist - _segmentLength) / dist;
            Vector3 offset = delta * (0.5f * diff);

            if (!a.locked) a.pos += offset;
            if (!b.locked) b.pos -= offset;

            // --- Per-segment sphere collision ---
            Vector3 mid = (a.pos + b.pos) * 0.5f;
            Vector3 dir = (b.pos - a.pos).normalized;
            float len = Vector3.Distance(a.pos, b.pos);

            if (Physics.SphereCast(a.pos, ropeRadius, dir,
                                   out RaycastHit hit, len, collisionLayers))
            {
                // Push both particles out of the surface
                Vector3 push = hit.normal * (ropeRadius - hit.distance + 0.001f);
                if (!a.locked) a.pos += push;
                if (!b.locked) b.pos += push;

                // Kill velocity into the surface (damp prevPos along normal)
                if (!a.locked) a.prevPos += push;
                if (!b.locked) b.prevPos += push;
            }
        }
    }

    // ---------------------------------------------------------------
    void UpdateLineRenderer()
    {
        if (lineRenderer == null) return;
        lineRenderer.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
            lineRenderer.SetPosition(i, _particles[i].pos);
    }

    // ---------------------------------------------------------------
    // Visual debugging
    void OnDrawGizmosSelected()
    {
        if (_particles == null) return;
        Gizmos.color = Color.yellow;
        foreach (var p in _particles)
            Gizmos.DrawWireSphere(p.pos, ropeRadius);
    }
}