using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class OneSideCollisionGenerator : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private Transform sourceRoot;
    [SerializeField] private bool includeInactiveObjects = false;

    [Header("Generated Collider")]
    [SerializeField] private string generatedObjectName = "Generated_OneSide_Collider";
    [SerializeField] private float cellSize = 0.35f;
    [SerializeField] private float colliderThickness = 0.25f;

    [Tooltip("If enabled, the climbable/collision side is this object's local +Z direction. If disabled, it uses local -Z.")]
    [SerializeField] private bool frontIsLocalPositiveZ = true;

    [Header("Filtering")]
    [SerializeField] private LayerMask sourceLayerMask = ~0;
    [SerializeField] private bool ignoreParticleRenderers = true;

    [Header("Physics")]
    [SerializeField] private PhysicMaterial physicMaterial;
    [SerializeField] private bool markGeneratedObjectStatic = true;

    [Header("Debug")]
    [SerializeField] private bool addMeshRendererForDebug = false;
    [SerializeField] private Material debugMaterial;

    private const string MeshName = "Generated One-Side Collision Mesh";

    [ContextMenu("Generate One-Side Collider")]
    public void GenerateCollider()
    {
        if (sourceRoot == null)
        {
            Debug.LogError("Source Root is not assigned.", this);
            return;
        }

        if (cellSize <= 0.01f)
        {
            Debug.LogError("Cell Size is too small. Use something like 0.2 ~ 0.5 for VR levels.", this);
            return;
        }

        if (colliderThickness <= 0.01f)
        {
            Debug.LogError("Collider Thickness must be greater than 0. Use something like 0.15 ~ 0.35.", this);
            return;
        }

        Renderer[] renderers = sourceRoot.GetComponentsInChildren<Renderer>(includeInactiveObjects);

        List<Bounds> localBoundsList = new List<Bounds>();

        foreach (Renderer r in renderers)
        {
            if (r == null)
                continue;

            if (ignoreParticleRenderers && r is ParticleSystemRenderer)
                continue;

            if (((1 << r.gameObject.layer) & sourceLayerMask.value) == 0)
                continue;

            if (r.transform.IsChildOf(transform) && r.gameObject.name == generatedObjectName)
                continue;

            Bounds localBounds = ConvertWorldBoundsToLocalBounds(r.bounds);
            localBoundsList.Add(localBounds);
        }

        if (localBoundsList.Count == 0)
        {
            Debug.LogError("No valid renderers found under Source Root.", this);
            return;
        }

        Mesh generatedMesh = BuildOneSideCollisionMesh(localBoundsList);

        if (generatedMesh == null)
        {
            Debug.LogError("Failed to generate collision mesh.", this);
            return;
        }

        GameObject generatedObject = CreateOrReplaceGeneratedObject();

        MeshFilter meshFilter = generatedObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = generatedObject.AddComponent<MeshFilter>();

        meshFilter.sharedMesh = generatedMesh;

        MeshCollider meshCollider = generatedObject.GetComponent<MeshCollider>();
        if (meshCollider == null)
            meshCollider = generatedObject.AddComponent<MeshCollider>();

        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = generatedMesh;
        meshCollider.convex = false;
        meshCollider.sharedMaterial = physicMaterial;

        if (addMeshRendererForDebug)
        {
            MeshRenderer meshRenderer = generatedObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = generatedObject.AddComponent<MeshRenderer>();

            meshRenderer.sharedMaterial = debugMaterial;
            meshRenderer.enabled = true;
        }
        else
        {
            MeshRenderer meshRenderer = generatedObject.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.enabled = false;
        }

        if (markGeneratedObjectStatic)
            generatedObject.isStatic = true;

        Debug.Log(
            $"Generated one-side collider: {generatedMesh.vertexCount} vertices, " +
            $"{generatedMesh.triangles.Length / 3} triangles.",
            generatedObject
        );
    }

    private Bounds ConvertWorldBoundsToLocalBounds(Bounds worldBounds)
    {
        Vector3 c = worldBounds.center;
        Vector3 e = worldBounds.extents;

        Vector3[] corners =
        {
            c + new Vector3(-e.x, -e.y, -e.z),
            c + new Vector3( e.x, -e.y, -e.z),
            c + new Vector3(-e.x,  e.y, -e.z),
            c + new Vector3( e.x,  e.y, -e.z),
            c + new Vector3(-e.x, -e.y,  e.z),
            c + new Vector3( e.x, -e.y,  e.z),
            c + new Vector3(-e.x,  e.y,  e.z),
            c + new Vector3( e.x,  e.y,  e.z)
        };

        Vector3 localMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 localMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (Vector3 corner in corners)
        {
            Vector3 local = transform.InverseTransformPoint(corner);
            localMin = Vector3.Min(localMin, local);
            localMax = Vector3.Max(localMax, local);
        }

        Bounds localBounds = new Bounds();
        localBounds.SetMinMax(localMin, localMax);
        return localBounds;
    }

    private Mesh BuildOneSideCollisionMesh(List<Bounds> localBoundsList)
    {
        Vector2 minXY = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 maxXY = new Vector2(float.MinValue, float.MinValue);

        foreach (Bounds b in localBoundsList)
        {
            minXY.x = Mathf.Min(minXY.x, b.min.x);
            minXY.y = Mathf.Min(minXY.y, b.min.y);
            maxXY.x = Mathf.Max(maxXY.x, b.max.x);
            maxXY.y = Mathf.Max(maxXY.y, b.max.y);
        }

        int width = Mathf.CeilToInt((maxXY.x - minXY.x) / cellSize);
        int height = Mathf.CeilToInt((maxXY.y - minXY.y) / cellSize);

        if (width <= 0 || height <= 0)
            return null;

        float[,] frontDepth = new float[width, height];
        bool[,] filled = new bool[width, height];

        float emptyDepth = frontIsLocalPositiveZ ? float.MinValue : float.MaxValue;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                frontDepth[x, y] = emptyDepth;
                filled[x, y] = false;
            }
        }

        foreach (Bounds b in localBoundsList)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt((b.min.x - minXY.x) / cellSize), 0, width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt((b.max.x - minXY.x) / cellSize), 0, width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt((b.min.y - minXY.y) / cellSize), 0, height - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt((b.max.y - minXY.y) / cellSize), 0, height - 1);

            float candidateDepth = frontIsLocalPositiveZ ? b.max.z : b.min.z;

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    filled[x, y] = true;

                    if (frontIsLocalPositiveZ)
                        frontDepth[x, y] = Mathf.Max(frontDepth[x, y], candidateDepth);
                    else
                        frontDepth[x, y] = Mathf.Min(frontDepth[x, y], candidateDepth);
                }
            }
        }

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        float frontSign = frontIsLocalPositiveZ ? 1f : -1f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!filled[x, y])
                    continue;

                float x0 = minXY.x + x * cellSize;
                float x1 = x0 + cellSize;
                float y0 = minXY.y + y * cellSize;
                float y1 = y0 + cellSize;

                float frontZ = frontDepth[x, y];
                float backZ = frontZ - frontSign * colliderThickness;

                AddBoxCell(
                    vertices,
                    triangles,
                    x0,
                    x1,
                    y0,
                    y1,
                    frontZ,
                    backZ,
                    frontIsLocalPositiveZ
                );
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = MeshName;

        if (vertices.Count > 65000)
            mesh.indexFormat = IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void AddBoxCell(
        List<Vector3> vertices,
        List<int> triangles,
        float x0,
        float x1,
        float y0,
        float y1,
        float frontZ,
        float backZ,
        bool frontPositiveZ
    )
    {
        Vector3 f00 = new Vector3(x0, y0, frontZ);
        Vector3 f10 = new Vector3(x1, y0, frontZ);
        Vector3 f11 = new Vector3(x1, y1, frontZ);
        Vector3 f01 = new Vector3(x0, y1, frontZ);

        Vector3 b00 = new Vector3(x0, y0, backZ);
        Vector3 b10 = new Vector3(x1, y0, backZ);
        Vector3 b11 = new Vector3(x1, y1, backZ);
        Vector3 b01 = new Vector3(x0, y1, backZ);

        if (frontPositiveZ)
        {
            AddQuad(vertices, triangles, f00, f10, f11, f01);
            AddQuad(vertices, triangles, b10, b00, b01, b11);
        }
        else
        {
            AddQuad(vertices, triangles, f10, f00, f01, f11);
            AddQuad(vertices, triangles, b00, b10, b11, b01);
        }

        AddQuad(vertices, triangles, b00, f00, f01, b01); // Left side
        AddQuad(vertices, triangles, f10, b10, b11, f11); // Right side
        AddQuad(vertices, triangles, b10, f10, f00, b00); // Bottom side
        AddQuad(vertices, triangles, f01, f11, b11, b01); // Top side
    }

    private void AddQuad(
        List<Vector3> vertices,
        List<int> triangles,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d
    )
    {
        int startIndex = vertices.Count;

        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);

        triangles.Add(startIndex + 0);
        triangles.Add(startIndex + 1);
        triangles.Add(startIndex + 2);

        triangles.Add(startIndex + 0);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 3);
    }

    private GameObject CreateOrReplaceGeneratedObject()
    {
        Transform existing = transform.Find(generatedObjectName);

        if (existing != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.DestroyObjectImmediate(existing.gameObject);
            else
                Destroy(existing.gameObject);
#else
            Destroy(existing.gameObject);
#endif
        }

        GameObject generatedObject = new GameObject(generatedObjectName);

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.RegisterCreatedObjectUndo(generatedObject, "Create One-Side Collider");
#endif

        generatedObject.transform.SetParent(transform);
        generatedObject.transform.localPosition = Vector3.zero;
        generatedObject.transform.localRotation = Quaternion.identity;
        generatedObject.transform.localScale = Vector3.one;

        return generatedObject;
    }
}