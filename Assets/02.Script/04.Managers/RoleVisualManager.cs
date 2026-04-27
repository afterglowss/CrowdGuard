using CrowdGuard.Climbing.Tools.Common;
using UnityEngine;

public class RoleVisualManager : MonoBehaviour
{
    public static RoleVisualManager Instance { get; private set; }

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
        // RoleManager가 살아있다면 역할을 즉시 읽어 적용
        // (Spawned() 타이밍보다 Start()가 늦게 올 경우의 안전망)
        if (RoleManager.Instance != null && RoleManager.Instance.Runner != null)
        {
            var runner = RoleManager.Instance.Runner;
            if (RoleManager.Instance.Roles.TryGet(runner.LocalPlayer, out PlayerRole role))
            {
                UpdateShaderGlobal(role == PlayerRole.Leader);
                return;
            }
        }
        // 역할 데이터가 아직 없으면 기본값(Leader) 유지 — Spawned()에서 덮어씀
        UpdateShaderGlobal(true);
    }

    /// <summary>
    /// GamePlayerModel.Spawned()에서 역할 확정 후 호출
    /// </summary>
    public void SetRole(bool isLeader)
    {
        UpdateShaderGlobal(isLeader);
    }

    private void UpdateShaderGlobal(bool isLeader)
    {
        Shader.SetGlobalFloat("_IsLeader", isLeader ? 1f : 0f);
    }
}
