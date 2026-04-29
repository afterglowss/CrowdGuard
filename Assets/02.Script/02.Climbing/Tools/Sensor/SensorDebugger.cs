using UnityEngine;
using MSEX; // HazardManager 및 Data 클래스가 위치한 네임스페이스 (글로벌이라 가정)

namespace MSEX.Climbing.Tools.DebugScripts
{
    /// <summary>
    /// VR 테스트 환경에서 가짜 재난 데이터를 발생시키고 기즈모를 시각화하는 디버그 스크립트.
    /// 배포 빌드 시 불필요한 연산을 줄이기 위해 기즈모 렌더링은 에디터 전용으로 분리합니다.
    /// </summary>
    public class SensorDebugger : MonoBehaviour
    {
        [Header("Debug Settings")]
        [Tooltip("VR 기준 플레이어의 머리(카메라) 트랜스폼. 비워두면 Camera.main을 찾습니다.")]
        [SerializeField] private Transform playerHeadset; 
        [SerializeField] private float hazardSpawnDistance = 15f;
        
        [Header("Runtime Info (Read Only)")]
        [SerializeField] private Vector3 lastHazardLocation;
        [SerializeField] private bool hasActiveHazard = false;
        
        private GameObject dummyVisualSphere;
        
        [ContextMenu("Spawn Dummy Avalanche (눈사태)")]
        public void SpawnDummyAvalanche()
        {
            SpawnHazard(new AvalancheData 
            { 
                Width = 4f, 
                Speed = 10f 
            });
        }

        [ContextMenu("Spawn Dummy Rockfall (낙석)")]
        public void SpawnDummyRockfall()
        {
            SpawnHazard(new RockfallData 
            { 
                RockCount = 5, 
                FallRadius = 2f 
            });
        }

        [ContextMenu("Spawn Dummy Blizzard (눈보라)")]
        public void SpawnDummyBlizzard()
        {
            SpawnHazard(new BlizzardData 
            { 
                Duration = 5f, 
                FreezeMultiplier = 2f 
            });
        }

        private void SpawnHazard(HazardData hazardData)
        {
            Transform origin = (playerHeadset != null) ? playerHeadset : Camera.main?.transform;
            if (origin == null) origin = transform;

            // 랜덤 구면 방향 추출 (위쪽 지형에서 내려올 확률이 높으므로 Y를 양수화)
            Vector3 randomDir = Random.onUnitSphere;
            if (randomDir.y < 0) randomDir.y = -randomDir.y; 
            
            hazardData.Location = origin.position + (randomDir * hazardSpawnDistance);
            lastHazardLocation = hazardData.Location;
            hasActiveHazard = true;

            // VR 인게임 화면에서도 육안으로 위치를 볼 수 있도록 마젠타색 스피어 스폰
            if (dummyVisualSphere == null)
            {
                dummyVisualSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dummyVisualSphere.name = "DummyHazard_Visual";
                Destroy(dummyVisualSphere.GetComponent<Collider>()); // 불필요한 충돌체 제거
                
                var renderer = dummyVisualSphere.GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = Color.magenta;
            }
            dummyVisualSphere.transform.position = lastHazardLocation;
            dummyVisualSphere.transform.localScale = Vector3.one * 2f; // 눈에 띄게 크게 렌더링

            // 재난 매니저로 핑 전송!
            if (HazardManager.Instance != null)
            {
                HazardManager.Instance.TriggerHazardExternal(hazardData);
                Debug.Log($"[SensorDebugger] Fired {hazardData.GetType().Name} at {lastHazardLocation}");
            }
            else
            {
                Debug.LogWarning("[SensorDebugger] HazardManager instance is null!");
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!hasActiveHazard) return;

            Transform origin = (playerHeadset != null) ? playerHeadset : Camera.main?.transform;
            if (origin == null) return;

            // 경로 선 그리기
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin.position, lastHazardLocation);
            
            // 위험 발원지 모델링
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawSphere(lastHazardLocation, 1.5f);
        }
#endif
    }
}
