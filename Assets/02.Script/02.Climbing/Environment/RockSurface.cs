using Fusion;
using SimpleAudioManager;
using UnityEngine;

namespace CrowdGuard.Environment
{
    public class RockSurface : BaseSurface
    {
        [Header("VFX")]
        [SerializeField] private GameObject _rockImpactFX1Prefab;
        [SerializeField] private GameObject _rockImpactFX2Prefab;
        [SerializeField] private GameObject _rockImpactLightPrefab;

        public override bool OnHitByIceAxe(Vector3 contactPoint = default, Vector3 contactNormal = default)
        {
            AudioManager.instance.PlaySFX(AudioManager.SFXType.PickRock, transform);
            SpawnImpactVFX(contactPoint, contactNormal);
            // 바위는 무조건 튕겨 나감
            Debug.Log("[RockSurface] 바위 표면입니다. 바일이 튕겨 나갑니다!");
            return false;
        }

        private void SpawnImpactVFX(Vector3 point, Vector3 normal)
        {
            Quaternion rot = normal.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(normal)
                : Quaternion.identity;

            if (_rockImpactFX1Prefab != null)
                Destroy(Instantiate(_rockImpactFX1Prefab, point, rot), 0.5f);
            if (_rockImpactFX2Prefab != null)
                Destroy(Instantiate(_rockImpactFX2Prefab, point, rot), 0.5f);
            if (_rockImpactLightPrefab != null)
                Destroy(Instantiate(_rockImpactLightPrefab, point, rot), 0.5f);
        }

        public override bool CanInstallAnchor(Vector3 contactPoint = default)
        {
            return false; // 바위에는 앵커 고정 불가능
        }
    }
}
