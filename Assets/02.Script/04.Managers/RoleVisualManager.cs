using CrowdGuard.Climbing.Tools.Common;
using UnityEngine;

public class RoleVisualManager : MonoBehaviour
{
    public static RoleVisualManager Instance { get; private set; }

    [Tooltip("리더(true) / 서포터(false). 에디터에서 직접 토글해 셰이더 미리보기 가능.")]
    public bool isLeader = true;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // RoleManager가 아직 살아있으면 역할을 직접 읽어 적용 (Spawned() 타이밍 안전망)
        if (RoleManager.Instance != null && RoleManager.Instance.Runner != null)
        {
            var runner = RoleManager.Instance.Runner;
            if (RoleManager.Instance.Roles.TryGet(runner.LocalPlayer, out PlayerRole role))
            {
                SetRole(role == Role.Role.Leader);
                return;
            }
        }
        // 역할 데이터가 없으면 현재 isLeader 값 그대로 적용
        UpdateShaderGlobal();
    }

    /// <summary>
    /// GamePlayerModel.Spawned()에서 역할 확정 후 호출
    /// </summary>
    public void SetRole(bool leader)
    {
        isLeader = leader;
        UpdateShaderGlobal();
    }

    private void UpdateShaderGlobal()
    {
        Shader.SetGlobalFloat("_isLeader", isLeader ? 1f : 0f);
    }

    // 인스펙터에서 isLeader를 토글할 때마다 호출됨 (에디터 + 플레이 모드 모두)
    private void OnValidate()
    {
        UpdateShaderGlobal();
#if UNITY_EDITOR
        // 편집 모드에서는 씬 뷰가 자동 갱신되지 않으므로 강제 리페인트
        UnityEditor.SceneView.RepaintAll();
#endif
    }
}
