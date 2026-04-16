using System.Collections.Generic;
using Photon.Voice.Unity.Demos;
using UnityEngine;
using System.Collections;

public class AvalanchePathSystem : MonoBehaviour
{
    [Header("Path")]
    public Transform pathRoot;
    public List<Transform> waypoints = new List<Transform>();

    [Header("Movement")]
    public float speed = 6f;
    public bool playOnStart = true;
    public bool loop = false;

    [Header("Particles")]
    public ParticleSystem avalancheParticles;
    public ParticleSystem snowParticles;
    [Header("Capsule Trail")]
    public float capsuleRadius = 1.0f;
    public bool collidersAreTriggers = true;
    public PhysicMaterial colliderMaterial;
    [SerializeField] private float colliderCleanupDelay = 1.0f;
    
    [Header("Debug")]
    [SerializeField] private float travelledDistance;
    [SerializeField] private float totalLength;
    [SerializeField] private bool isPlaying;

    private float[] segmentLengths;
    private float[] cumulativeLengths;
    private CapsuleCollider[] segmentColliders;

    private Transform colliderRoot;
    private Rigidbody colliderRootRb;
    private Coroutine cleanupRoutine;
    
    void Start()
    {
        Rebuild();

        if (playOnStart)
            PlayAvalanche();
        else
            UpdateVisualsAndColliders();
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (!HasValidPath()) return;

        if (isPlaying)
        {
            travelledDistance += speed * Time.deltaTime;

            if (travelledDistance >= totalLength)
            {
                if (loop)
                {
                    travelledDistance %= totalLength;

                    if (avalancheParticles != null)
                    {
                        avalancheParticles.Clear();
                        snowParticles.Clear();
                        avalancheParticles.Play();
                        snowParticles.Play();
                    }
                }
                else
                {
                    travelledDistance = totalLength;
                    isPlaying = false;

                    if (avalancheParticles != null)
                    {
                        avalancheParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                        snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

                    }

                    cleanupRoutine = StartCoroutine(CleanupCollidersAfterDelay(colliderCleanupDelay));
                }
            }
        }

        UpdateVisualsAndColliders();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        CacheWaypoints();
        BuildSegmentData();
        BuildColliderObjects();

        travelledDistance = 0f;
        UpdateVisualsAndColliders();
    }

    [ContextMenu("Play Avalanche")]
    public void PlayAvalanche()
    {
        if (!HasValidPath())
            Rebuild();

        travelledDistance = 0f;
        isPlaying = true;

        if (avalancheParticles != null)
        {
            avalancheParticles.Clear();
            snowParticles.Clear();
            avalancheParticles.Play();
            snowParticles.Play();
        }
    }

    [ContextMenu("Stop Avalanche")]
    public void StopAvalanche()
    {
        isPlaying = false;

        if (avalancheParticles != null)
        {
            avalancheParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        cleanupRoutine = StartCoroutine(CleanupCollidersAfterDelay(colliderCleanupDelay));
    }
    
    private void CacheWaypoints()
    {
        if (pathRoot != null)
        {
            waypoints.Clear();

            for (int i = 0; i < pathRoot.childCount; i++)
                waypoints.Add(pathRoot.GetChild(i));
        }
    }

    private bool HasValidPath()
    {
        return waypoints != null && waypoints.Count >= 2;
    }

    private void BuildSegmentData()
    {
        if (!HasValidPath())
        {
            segmentLengths = null;
            cumulativeLengths = null;
            totalLength = 0f;
            return;
        }

        int segmentCount = waypoints.Count - 1;
        segmentLengths = new float[segmentCount];
        cumulativeLengths = new float[segmentCount];
        totalLength = 0f;

        for (int i = 0; i < segmentCount; i++)
        {
            cumulativeLengths[i] = totalLength;

            float len = Vector3.Distance(waypoints[i].position, waypoints[i + 1].position);
            segmentLengths[i] = len;
            totalLength += len;
        }
    }

    private void BuildColliderObjects()
    {
        if (colliderRoot == null)
        {
            Transform found = transform.Find("_AvalancheColliderRoot");
            colliderRoot = found != null ? found : new GameObject("_AvalancheColliderRoot").transform;
            colliderRoot.SetParent(transform, false);
        }

        for (int i = colliderRoot.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
                Destroy(colliderRoot.GetChild(i).gameObject);
            else
                DestroyImmediate(colliderRoot.GetChild(i).gameObject);
        }

        colliderRootRb = colliderRoot.GetComponent<Rigidbody>();
        if (colliderRootRb == null)
            colliderRootRb = colliderRoot.gameObject.AddComponent<Rigidbody>();

        colliderRootRb.isKinematic = true;
        colliderRootRb.useGravity = false;

        if (!HasValidPath())
        {
            segmentColliders = null;
            return;
        }

        int segmentCount = waypoints.Count - 1;
        segmentColliders = new CapsuleCollider[segmentCount];

        for (int i = 0; i < segmentCount; i++)
        {
            GameObject go = new GameObject($"Segment_{i:00}");
            go.transform.SetParent(colliderRoot, false);

            CapsuleCollider col = go.AddComponent<CapsuleCollider>();
            col.direction = 2; // Z axis
            col.radius = capsuleRadius;
            col.height = capsuleRadius * 2f + 0.01f;
            col.isTrigger = collidersAreTriggers;
            col.enabled = false;

            if (colliderMaterial != null)
                col.material = colliderMaterial;

            segmentColliders[i] = col;
        }
    }

    private void RemoveColliders()
    {
        if (colliderRoot == null) return;
        for (int i = colliderRoot.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
            {
                Destroy(colliderRoot.GetChild(i).gameObject);
            }
            else
            {
                DestroyImmediate(colliderRoot.GetChild(i).gameObject);
            }
        }
    }
    private void UpdateVisualsAndColliders()
    {
        if (!HasValidPath()) return;

        UpdateParticleFront();

        for (int i = 0; i < segmentColliders.Length; i++)
            UpdateSegmentCollider(i);
    }

    private void UpdateParticleFront()
    {
        if (avalancheParticles == null) return;

        GetPointAndForwardAtDistance(travelledDistance, out Vector3 position, out Vector3 forward);

        avalancheParticles.transform.position = position;
        snowParticles.transform.position = position;
        if (forward.sqrMagnitude > 0.0001f)
            avalancheParticles.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            snowParticles.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private void UpdateSegmentCollider(int i)
    {
        CapsuleCollider col = segmentColliders[i];
        if (col == null) return;

        Vector3 start = waypoints[i].position;
        Vector3 end = waypoints[i + 1].position;
        float segmentLength = segmentLengths[i];

        if (segmentLength <= 0.001f)
        {
            col.enabled = false;
            return;
        }

        float filledLength = Mathf.Clamp(travelledDistance - cumulativeLengths[i], 0f, segmentLength);

        if (filledLength <= 0.01f)
        {
            col.enabled = false;
            return;
        }

        Vector3 dir = (end - start).normalized;
        Vector3 filledEnd = start + dir * filledLength;
        Vector3 mid = (start + filledEnd) * 0.5f;

        Transform t = col.transform;
        t.position = mid;
        t.rotation = Quaternion.FromToRotation(Vector3.forward, dir);

        col.radius = capsuleRadius;
        col.height = filledLength + capsuleRadius * 2f;
        col.center = Vector3.zero;
        col.isTrigger = collidersAreTriggers;

        if (colliderMaterial != null)
            col.material = colliderMaterial;

        col.enabled = true;
    }

    private void GetPointAndForwardAtDistance(float distance, out Vector3 position, out Vector3 forward)
    {
        distance = Mathf.Clamp(distance, 0f, totalLength);

        for (int i = 0; i < segmentLengths.Length; i++)
        {
            float segmentStart = cumulativeLengths[i];
            float segmentLen = segmentLengths[i];
            float segmentEnd = segmentStart + segmentLen;

            if (distance <= segmentEnd || i == segmentLengths.Length - 1)
            {
                float t = segmentLen > 0.001f
                    ? Mathf.InverseLerp(segmentStart, segmentEnd, distance)
                    : 0f;

                Vector3 a = waypoints[i].position;
                Vector3 b = waypoints[i + 1].position;

                position = Vector3.Lerp(a, b, t);
                forward = (b - a).normalized;
                return;
            }
        }

        position = waypoints[waypoints.Count - 1].position;
        forward = (waypoints[waypoints.Count - 1].position - waypoints[waypoints.Count - 2].position).normalized;
    }

    void OnDrawGizmos()
    {
        CacheWaypoints();

        if (!HasValidPath()) return;

        Gizmos.color = Color.cyan;

        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;

            Gizmos.DrawSphere(waypoints[i].position, 0.15f);

            if (i < waypoints.Count - 1 && waypoints[i + 1] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }
    }
    private IEnumerator CleanupCollidersAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        RemoveColliders();
    }
}