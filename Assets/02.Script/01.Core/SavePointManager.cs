using UnityEngine;

public class SavePointManager : MonoBehaviour
{
    // 어느 스크립트에서든 쉽게 최근 세이브 지점을 알 수 있도록 싱글톤 설계
    public static SavePointManager Instance { get; private set; }

    [Tooltip("게임 시작 기본 위치 (앵커를 한 번도 안 박고 떨어졌을 때 부활할 곳)")]
    public Vector3 defaultSpawnPosition;

    // 가장 최근에 박힌 안전망(앵커)의 좌표 보관
    private Vector3 lastSafePosition;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject); // 중복 파괴

        lastSafePosition = defaultSpawnPosition;
    }

    private void OnEnable()
    {
        // 앵커가 돌려져서 체결될 때 발생하는 글로벌 옵저버 이벤트 구독
        IceAnchor.OnAnchorSecured += HandleAnchorSecured;
    }

    private void OnDisable()
    {
        IceAnchor.OnAnchorSecured -= HandleAnchorSecured;
    }

    private void HandleAnchorSecured(Vector3 anchorPos)
    {
        lastSafePosition = anchorPos;
        Debug.Log($"[SavePointManager] 세이브 포인트 갱신! 이제 추락하면 이곳({lastSafePosition})에서 부활합니다.");
    }

    public Vector3 GetRespawnPosition()
    {
        // 벽을 뚫지 않게 앵커 지점에서 살짝 떨어진 공중으로 보정 오프셋(예: 뒤로 0.5미터) 적용 가능
        return lastSafePosition + new Vector3(0, 0, -0.5f);
    }
}
