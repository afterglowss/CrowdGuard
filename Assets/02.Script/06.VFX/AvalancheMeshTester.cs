using UnityEngine;

public class AvalancheMeshTester : MonoBehaviour
{
    public AvalancheMesh avalancheMesh;
    [Range(0f, 1f)] public float fillHeight = 1f;
    public float snowEndY = -10f;
    public bool rebuild;

    private void Start()
    {
        avalancheMesh.BuildMesh(fillHeight, snowEndY);
    }

    private void Update()
    {
        avalancheMesh.AnimateFrontEdge(Time.time, 4f);

        if (rebuild)
        {
            rebuild = false;
            avalancheMesh.BuildMesh(fillHeight, snowEndY);
        }
    }
}