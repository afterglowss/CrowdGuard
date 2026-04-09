using UnityEngine;

public class AvalancheController : MonoBehaviour
{
    [Header("References")]
    public AvalancheMesh avalancheMesh;

    [Header("Movement")]
    public float fallSpeed = 8f;
    public float frontAnimSpeed = 3f;

    [Header("Bounds — set via SnowEndMarker or directly")]
    public float snowEndWorldY = -30f;

    private bool _active = false;
    private float _fillProgress = 0f;
    private float _totalHeight;

    [Header("FX")]
    public ParticleSystem debrisParticles;


    void Start()
    {
        _totalHeight = transform.position.y - snowEndWorldY;
        avalancheMesh.BuildMesh(0f, snowEndWorldY);
    }

    void Update()
    {
        if (!_active) return;

        if (_fillProgress < 1f)
        {
            _fillProgress += (fallSpeed / _totalHeight) * Time.deltaTime;
            _fillProgress = Mathf.Clamp01(_fillProgress);
            avalancheMesh.BuildMesh(_fillProgress, snowEndWorldY);
        }

        avalancheMesh.AnimateFrontEdge(Time.time, frontAnimSpeed);
    }

    public void TriggerAvalanche()
    {
        _active = true;
        debrisParticles?.Play();
    }

    public void ResetAvalanche()
    {
        _active = false;
        _fillProgress = 0f;
        avalancheMesh.BuildMesh(0f, snowEndWorldY);
        debrisParticles?.Stop();
    }
}