using SimpleAudioManager;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class TentExitDoor : MonoBehaviour
{
    [Header("References")]
    [Tooltip("문을 바라볼 때 표시할 World Space Canvas (Exit 텍스트 포함)")]
    public GameObject exitUI;

    [Header("Gaze Settings")]
    [Tooltip("레이캐스트 최대 인식 거리 (m)")]
    public float maxGazeDistance = 3.0f;
    [Tooltip("문 오브젝트가 속한 레이어를 지정하세요. 다른 콜라이더는 무시합니다.")]
    public LayerMask doorLayer;

    private bool isGazing = false;
    private bool wasTriggerPressed = false;

    private static readonly List<InputDevice> _controllers = new List<InputDevice>();

    private void Update()
    {
        UpdateGaze();

        if (isGazing)
            CheckTriggerInput();
    }

    private void UpdateGaze()
    {
        if (Camera.main == null) return;

        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, maxGazeDistance, doorLayer)
                   && hitInfo.collider.gameObject == gameObject;

        if (hit != isGazing)
        {
            isGazing = hit;
            if (exitUI != null)
                exitUI.SetActive(isGazing);

            // 시선 벗어나면 트리거 상태 초기화
            if (!isGazing)
                wasTriggerPressed = false;
        }
    }

    private void CheckTriggerInput()
    {
        bool currentlyPressed = IsTriggerPressed();

        // 이전 프레임에 안 눌렸다가 이번 프레임에 눌린 순간만 반응 (엣지 트리거)
        if (currentlyPressed && !wasTriggerPressed)
        {
            AudioManager.instance.PlaySFX(AudioManager.SFXType.TentDoor, transform);
            TentInteriorController.Instance?.ExitTent();
        }

        wasTriggerPressed = currentlyPressed;
    }

    private bool IsTriggerPressed()
    {
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller, _controllers);

        foreach (var device in _controllers)
        {
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed)
                return true;
        }
        return false;
    }

    private void OnDisable()
    {
        isGazing = false;
        wasTriggerPressed = false;
        if (exitUI != null)
            exitUI.SetActive(false);
    }
}
