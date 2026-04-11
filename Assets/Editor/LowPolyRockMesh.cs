using UnityEngine;
using System.Collections.Generic;

namespace LowPolyRocks
{
    /// <summary>
    /// Builds an organic low-poly rock mesh.
    ///
    /// Key techniques that make it look like a real rock rather than a sphere/die:
    ///   1. Asymmetric per-axis random scaling BEFORE displacement breaks the sphere silhouette
    ///   2. fBm (fractal Brownian motion) noise with 4 octaves for natural surface variation
    ///   3. Domain-warped noise (the sample position is itself offset by another noise field)
    ///      — this creates the organic "folded" look of real stone
    ///   4. Several random "attractor" points that create large bumps/dents distributed
    ///      asymmetrically over the surface — no two rocks look alike
    ///   5. Y-base flattening so the bottom sits flush on a ground plane
    ///   6. Flat-shading (unshared vertices) for the low-poly faceted look
    /// </summary>
    public static class LowPolyRockMesh
    {
        // ─────────────────────────────────────────────────────────────────────
        public static Mesh Build(
            int seed,
            float radius,
            int subdivisions,
            float noiseStrength,
            float noiseFrequency,
            float flattenY,
            float sharpness)          // 0 = smooth bumps, 1 = sharp angular cuts
        {
            Random.InitState(seed);

            // ── 1. Icosphere base ────────────────────────────────────────────
            var (verts, tris) = Icosphere(Mathf.Clamp(subdivisions, 0, 4));

            // ── 2. Random asymmetric axis scale ──────────────────────────────
            // This is the single biggest step for breaking sphere symmetry.
            // We stretch/squash X, Y, Z independently before any noise is applied.
            float sx = Random.Range(0.72f, 1.28f);
            float sy = Random.Range(0.48f, 0.95f) * flattenY;   // Y is always a bit flat
            float sz = Random.Range(0.72f, 1.28f);

            // Also tilt the whole shape slightly so it doesn't sit bolt-upright
            Quaternion tilt = Quaternion.Euler(
                Random.Range(-18f, 18f), 0f, Random.Range(-14f, 14f));

            // ── 3. Random attractor / repulsor points ────────────────────────
            // These create organic lumps — like the big protruding top of the
            // reference rock, and the various bumps on satellite rocks.
            int attractorCount = Random.Range(2, 5);
            var attractors = new (Vector3 dir, float strength, float falloff)[attractorCount];
            for (int a = 0; a < attractorCount; a++)
            {
                attractors[a] = (
                    Random.onUnitSphere,
                    Random.Range(-0.18f, 0.32f),          // negative = dent, positive = bulge
                    Random.Range(0.55f, 1.40f)            // how wide the influence is
                );
            }

            // ── 4. Unique noise domain offset (makes every seed unique) ──────
            Vector3 noiseOffset = new Vector3(
                seed * 0.17321f + 3.7f,
                seed * 0.31416f + 1.1f,
                seed * 0.27182f + 7.3f);

            // ── 5. Displace each vertex ──────────────────────────────────────
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 dir = verts[i]; // unit sphere direction

                // a) fBm noise displacement (4 octaves with domain warp)
                float fbm = FBm(dir * noiseFrequency + noiseOffset, 4, sharpness);

                // b) Attractor contributions
                float attract = 0f;
                foreach (var (aDir, strength, falloff) in attractors)
                {
                    float dot = (Vector3.Dot(dir, aDir) + 1f) * 0.5f; // 0..1
                    attract += strength * Mathf.Pow(dot, falloff * 3f);
                }

                // c) Combine: base radius + noise bump + attractors
                float r = radius * (1f + fbm * noiseStrength + attract);
                r = Mathf.Max(r, radius * 0.25f); // prevent inside-out faces

                // d) Apply asymmetric axis scale + tilt
                Vector3 v = dir * r;
                v = new Vector3(v.x * sx, v.y * sy, v.z * sz);
                v = tilt * v;

                verts[i] = v;
            }

            // ── 6. Push bottom vertices down so rock sits on ground ──────────
            // Find min Y and translate so the rock rests at Y=0
            float minY = float.MaxValue;
            foreach (var v in verts) if (v.y < minY) minY = v.y;
            for (int i = 0; i < verts.Count; i++)
                verts[i] = new Vector3(verts[i].x, verts[i].y - minY, verts[i].z);

            // ── 7. Flat-shade: unshare all vertices ──────────────────────────
            var flatVerts = new Vector3[tris.Count];
            var flatNormals = new Vector3[tris.Count];
            var flatTris = new int[tris.Count];

            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = verts[tris[i]];
                Vector3 b = verts[tris[i + 1]];
                Vector3 c = verts[tris[i + 2]];

                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

                flatVerts[i] = a; flatVerts[i + 1] = b; flatVerts[i + 2] = c;
                flatNormals[i] = flatNormals[i + 1] = flatNormals[i + 2] = normal;
                flatTris[i] = i; flatTris[i + 1] = i + 1; flatTris[i + 2] = i + 2;
            }

            // ── 8. Assemble mesh ─────────────────────────────────────────────
            var mesh = new Mesh { name = $"LowPolyRock_{seed}" };
            if (flatVerts.Length > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = flatVerts;
            mesh.normals = flatNormals;
            mesh.triangles = flatTris;

            // Planar XZ UVs
            var uvs = new Vector2[flatVerts.Length];
            float uvScale = 1f / (radius * 2f);
            for (int i = 0; i < flatVerts.Length; i++)
                uvs[i] = new Vector2(flatVerts[i].x * uvScale + 0.5f,
                                     flatVerts[i].z * uvScale + 0.5f);
            mesh.uv = uvs;

            mesh.RecalculateBounds();
            return mesh;
        }

        // ─────────────────────────────────────────────────────────────────────
        // fBm  (fractal Brownian Motion)
        // Adds multiple octaves of noise at increasing frequencies and
        // decreasing amplitudes.  Domain warp: the sample position is first
        // offset by a low-frequency noise field, breaking repetition and adding
        // the "folded stone" quality.
        // sharpness 0 = smooth lumps,  1 = hard ridges
        // ─────────────────────────────────────────────────────────────────────
        private static float FBm(Vector3 p, int octaves, float sharpness)
        {
            // Domain warp — offset p by a coarse noise field
            Vector3 warp = new Vector3(
                ValueNoise(p + new Vector3(1.7f, 9.2f, 3.4f)),
                ValueNoise(p + new Vector3(8.3f, 2.8f, 5.1f)),
                ValueNoise(p + new Vector3(4.1f, 6.7f, 1.9f)));
            p += warp * 0.55f;

            float val = 0f;
            float amp = 0.5f;
            float freq = 1f;
            float total = 0f;

            for (int o = 0; o < octaves; o++)
            {
                float n = ValueNoise(p * freq);
                // Blend smooth vs ridge noise based on sharpness
                float ridged = 1f - Mathf.Abs(n * 2f - 1f);  // creates sharp ridges
                n = Mathf.Lerp(n, ridged, sharpness);

                val += n * amp;
                total += amp;
                amp *= 0.52f;   // each octave is quieter
                freq *= 2.03f;   // and finer
            }
            return val / total;   // normalised 0..1
        }

        // ─────────────────────────────────────────────────────────────────────
        // Icosphere
        // ─────────────────────────────────────────────────────────────────────
        private static (List<Vector3>, List<int>) Icosphere(int subdivisions)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                N(-1, t, 0), N( 1, t, 0), N(-1,-t, 0), N( 1,-t, 0),
                N( 0,-1, t), N( 0, 1, t), N( 0,-1,-t), N( 0, 1,-t),
                N( t, 0,-1), N( t, 0, 1), N(-t, 0,-1), N(-t, 0, 1),
            };
            var f = new List<int>
            {
                0,11,5, 0,5,1,  0,1,7,  0,7,10, 0,10,11,
                1,5,9,  5,11,4, 11,10,2,10,7,6,  7,1,8,
                3,9,4,  3,4,2,  3,2,6,  3,6,8,  3,8,9,
                4,9,5,  2,4,11, 6,2,10, 8,6,7,  9,8,1,
            };

            var mid = new Dictionary<long, int>();
            for (int s = 0; s < subdivisions; s++)
            {
                mid.Clear();
                var nf = new List<int>();
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2];
                    int ab = MidPt(a, b, v, mid);
                    int bc = MidPt(b, c, v, mid);
                    int ca = MidPt(c, a, v, mid);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = nf;
            }
            return (v, f);
        }

        private static Vector3 N(float x, float y, float z)
            => new Vector3(x, y, z).normalized;

        private static int MidPt(int a, int b, List<Vector3> v, Dictionary<long, int> cache)
        {
            long key = a < b ? ((long)a << 32 | (uint)b) : ((long)b << 32 | (uint)a);
            if (cache.TryGetValue(key, out int idx)) return idx;
            idx = v.Count;
            v.Add(((v[a] + v[b]) * 0.5f).normalized);
            cache[key] = idx;
            return idx;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3-D value noise — smooth trilinear interpolation over a lattice
        // ─────────────────────────────────────────────────────────────────────
        private static float ValueNoise(Vector3 p)
        {
            int xi = Mathf.FloorToInt(p.x), yi = Mathf.FloorToInt(p.y), zi = Mathf.FloorToInt(p.z);
            float xf = p.x - xi, yf = p.y - yi, zf = p.z - zi;
            float u = Fade(xf), vv = Fade(yf), w = Fade(zf);

            return Mathf.Lerp(
                Mathf.Lerp(Mathf.Lerp(H(xi, yi, zi), H(xi + 1, yi, zi), u),
                           Mathf.Lerp(H(xi, yi + 1, zi), H(xi + 1, yi + 1, zi), u), vv),
                Mathf.Lerp(Mathf.Lerp(H(xi, yi, zi + 1), H(xi + 1, yi, zi + 1), u),
                           Mathf.Lerp(H(xi, yi + 1, zi + 1), H(xi + 1, yi + 1, zi + 1), u), vv), w);
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float H(int x, int y, int z)
        {
            int n = x * 1619 + y * 31337 + z * 6971;
            n = (n >> 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }
    }
}