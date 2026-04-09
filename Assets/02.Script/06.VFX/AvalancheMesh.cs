using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AvalancheMesh : MonoBehaviour
{
    [Header("Shape")]
    public int radialSegments = 10;
    public int heightSegments = 8;
    public float maxRadius = 12f;
    public float spreadAngle = 35f;

    [Header("Noise / Danger")]
    public float edgeJitter = 1.8f;
    public float surfaceNoise = 0.6f;

    private Mesh _mesh;
    private Vector3[] _baseVerts;

    public void BuildMesh(float fillProgress, float snowEndWorldY)
    {
        if (_mesh == null)
        {
            _mesh = new Mesh { name = "AvalancheMesh" };
            GetComponent<MeshFilter>().mesh = _mesh;
        }

        var verts = new List<Vector3>();
        var tris = new List<int>();
        var colors = new List<Color>();

        float totalHeight = Mathf.Abs(snowEndWorldY - transform.position.y);
        float currentHeight = totalHeight * fillProgress;

        // Single origin tip — requirement #1
        verts.Add(Vector3.zero);
        colors.Add(Color.white);

        for (int h = 1; h <= heightSegments; h++)
        {
            float t = (float)h / heightSegments;
            float y = -t * currentHeight;
            float radius = Mathf.Tan(spreadAngle * Mathf.Deg2Rad) * Mathf.Abs(y);
            radius = Mathf.Min(radius, maxRadius);

            bool isFrontEdge = (h == heightSegments);

            for (int r = 0; r < radialSegments; r++)
            {
                float angle = (float)r / radialSegments * Mathf.PI * 2f;
                float jitter = isFrontEdge
                    ? Random.Range(-edgeJitter, edgeJitter)
                    : Random.Range(-surfaceNoise, surfaceNoise) * 0.3f;

                float x = Mathf.Cos(angle) * (radius + jitter);
                float z = Mathf.Sin(angle) * (radius + jitter);
                float yFinal = y + (isFrontEdge ? jitter * 0.5f : 0f);

                verts.Add(new Vector3(x, yFinal, z));

                Color c = isFrontEdge
                    ? Color.Lerp(new Color(0.7f, 0.85f, 1f), Color.white, Random.value)
                    : Color.Lerp(Color.white, new Color(0.85f, 0.92f, 1f), t);
                colors.Add(c);
            }
        }

        // Tip to first ring
        for (int r = 0; r < radialSegments; r++)
        {
            int next = (r + 1) % radialSegments;
            tris.Add(0);
            tris.Add(r + 1);
            tris.Add(next + 1);
        }

        // Ring to ring
        for (int h = 0; h < heightSegments - 1; h++)
        {
            int ringStart = 1 + h * radialSegments;
            int nextRingStart = ringStart + radialSegments;

            for (int r = 0; r < radialSegments; r++)
            {
                int a = ringStart + r;
                int b = ringStart + (r + 1) % radialSegments;
                int c = nextRingStart + r;
                int d = nextRingStart + (r + 1) % radialSegments;

                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }

        _mesh.Clear();
        _mesh.SetVertices(verts);
        _mesh.SetTriangles(tris, 0);
        _mesh.SetColors(colors);
        _mesh.RecalculateNormals();
        _baseVerts = verts.ToArray();
    }

    public void AnimateFrontEdge(float time, float speed)
    {
        if (_mesh == null || _baseVerts == null) return;

        var verts = (Vector3[])_baseVerts.Clone();
        int frontRingStart = 1 + (heightSegments - 1) * radialSegments;

        for (int i = frontRingStart; i < verts.Length; i++)
        {
            float wave = Mathf.Sin(time * speed + i * 1.3f) * edgeJitter * 0.4f;
            verts[i] += new Vector3(wave, wave * 0.5f, wave);
        }

        _mesh.SetVertices(verts);
        _mesh.RecalculateNormals();
    }
}