using UnityEngine;
using UnityEngine.InputSystem;

public class RoleVisualManager : MonoBehaviour
{
    public static RoleVisualManager Instance { get; private set; }

    [Header("Current Role Settings")]
    public bool isLeader = true;

    [Header("Debug Input Actions")]
    public InputActionProperty toggleRoleAction;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        if (toggleRoleAction.action != null)
        {
            toggleRoleAction.action.performed += OnToggleRolePressed;
            toggleRoleAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (toggleRoleAction.action != null)
        {
            toggleRoleAction.action.performed -= OnToggleRolePressed;
            toggleRoleAction.action.Disable();
        }
    }

    private void Start()
    {
        UpdateShaderGlobal();
    }

    private void OnToggleRolePressed(InputAction.CallbackContext context)
    {
        isLeader = !isLeader;
        Debug.Log($"[RoleVisualManager] Role Toggled! IsLeader: {isLeader}");
        UpdateShaderGlobal();
    }

    public void SetRole(bool leader)
    {
        isLeader = leader;
        UpdateShaderGlobal();
    }

    private void UpdateShaderGlobal()
    {
        // 기획서 렌더링 최적화 1원칙: 글로벌 셰이더 변수를 통한 수만 개의 얼음 텍스처 1프레임 즉각 스왑
        Shader.SetGlobalFloat("_IsLeader", isLeader ? 1f : 0f);
    }
}
