using System.Collections;
using UnityEngine;

/// <summary>
/// 씬에 미리 배치해두는 눈보라 파티클 시스템.
/// HazardManager.blizzardSystems 리스트에 인덱스 순서로 등록하세요.
/// </summary>
public class BlizzardSystem : MonoBehaviour
{
    [SerializeField] private ParticleSystem blizzardParticles;
    [SerializeField, Tooltip("센서가 가리킬 실제 발생 위치. 비워두면 피봇을 사용합니다.")]
    private Transform _spawnOrigin;

    private Coroutine _stopCoroutine;

    public Vector3 GetSpawnPosition() =>
        _spawnOrigin != null ? _spawnOrigin.position : transform.position;

    /// <summary>
    /// 파티클을 재생하고 duration 초 후 자동으로 멈춥니다.
    /// 이미 재생 중이면 처음부터 다시 시작합니다.
    /// </summary>
    public void Activate(float duration)
    {
        if (_stopCoroutine != null)
        {
            StopCoroutine(_stopCoroutine);
            _stopCoroutine = null;
        }

        if (blizzardParticles != null)
        {
            blizzardParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            blizzardParticles.Play();
        }

        _stopCoroutine = StartCoroutine(StopAfterDuration(duration));
    }

    /// <summary>
    /// 외부에서 강제로 멈출 때 사용합니다.
    /// </summary>
    public void Stop()
    {
        if (_stopCoroutine != null)
        {
            StopCoroutine(_stopCoroutine);
            _stopCoroutine = null;
        }

        if (blizzardParticles != null)
            blizzardParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private IEnumerator StopAfterDuration(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (blizzardParticles != null)
            blizzardParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        _stopCoroutine = null;
    }
}
