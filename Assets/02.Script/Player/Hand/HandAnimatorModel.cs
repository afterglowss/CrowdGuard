namespace CrowdGuard.Player.Hand
{
    /// <summary>
    /// 손 애니메이션에 필요한 주요 데이터를 보유하는 모델 클래스입니다.
    /// 컨트롤러의 그립(Grip) 및 트리거(Trigger) 입력 값을 저장합니다.
    /// </summary>
    public class HandAnimatorModel
    {
        public float GripValue { get; set; }
        public float TriggerValue { get; set; }
    }
}
