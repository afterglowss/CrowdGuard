using UnityEngine;
using Fusion;
using System.Collections.Generic;

namespace CrowdGuard.Environment
{
    public abstract class BaseSurface : NetworkBehaviour
    {
        [Header("Global VFX/SFX Slots")]
        public AudioClip hitSound;
        public ParticleSystem breakVFX;

        // 파괴 여부를 틱 단위로 안전하게 동기화하고 양 클라이언트 시각/청각 콜백 발생
        [Networked, OnChangedRender(nameof(OnBrokenChanged))]
        public NetworkBool IsBroken { get; set; }

        // 👇 [추가] 오프라인 상태에서도 에러가 나지 않게 값을 읽는 안전망
        public bool GetIsBrokenSafe()
        {
            // Object가 아예 없거나 유효하지 않으면 오프라인 테스트 중인 것으로 간주
            if (Object == null || !Object.IsValid) return false; 
            return IsBroken;
        }

        // 👇 [추가] 오프라인 상태에서도 에러가 나지 않게 값을 쓰는 안전망
        public void SetBrokenSafe(bool value)
        {
            if (Object != null && Object.IsValid)
            {
                IsBroken = value;
            }
            else
            {
                Debug.LogWarning("[BaseSurface] 오프라인 모드: 파괴 상태(IsBroken) 동기화를 생략합니다.");
            }
        }

        public abstract bool OnHitByIceAxe();
        public abstract bool CanInstallAnchor();

        // 고드름 붕괴 등의 무게 계산 처리용 훅 (PlayerRef 기반)
        protected HashSet<PlayerRef> attachedPlayers = new HashSet<PlayerRef>();

        public virtual void OnAxeAttached(PlayerRef playerRef)
        {
            if (!attachedPlayers.Contains(playerRef))
            {
                attachedPlayers.Add(playerRef);
            }
        }

        public virtual void OnAxeDetached(PlayerRef playerRef)
        {
            if (attachedPlayers.Contains(playerRef))
            {
                attachedPlayers.Remove(playerRef);
            }
        }

        protected virtual void OnBrokenChanged()
        {
            // 네트워크를 초월하여 양쪽 클라이언트에서 시각적 연출
            if (IsBroken)
            {
                if (breakVFX != null)
                {
                    Instantiate(breakVFX, transform.position, Quaternion.identity);
                }
                
                // 청크 파괴 처리 (필요에 따라 자식 콜라이더들만 끄게 설계 가능)
                gameObject.SetActive(false); 
            }
        }
    }
}
