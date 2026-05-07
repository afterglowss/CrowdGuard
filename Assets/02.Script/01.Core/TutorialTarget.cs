using UnityEngine;

/// <summary>
/// 도구 프리팹에 붙이는 튜토리얼 데이터 컴포넌트.
/// TutorialUIController가 시선 감지 시 이 값을 읽어 UI를 업데이트합니다.
/// </summary>
public class TutorialTarget : MonoBehaviour
{
    [Header("튜토리얼 텍스트")]
    public string header;

    [TextArea(2, 6)]
    public string description;
}
