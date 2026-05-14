
using Fusion;
using TMPro;
using UnityEngine;

public class ResultPanel : NetworkBehaviour
{
    public GameObject resultPanel;
    public TextMeshProUGUI resultText;

    bool isShowed = false;
    
    [Rpc(RpcSources.All,RpcTargets.All)]
    void RPC_ShowResultPanel()
    {
        // 여러번 실행되지 않도록 제한(굳이 필요 없긴함)
        if (isShowed) return;
        isShowed = true;
        
        // 값을 DataManager에서 가져옴.
        var dataManager = DataManager.Instance;
        var text = "Null";
        if (dataManager)
        {
            var time = dataManager.timer.GetTime();
            var anchorCount = dataManager.AnchorCount;
            var fallCount =  dataManager.FallCount;
            text = $"Time : {Timer.ConvertTimeToString(time)} \nAnchor : {anchorCount} \nFall : {fallCount}";
        }
        else Debug.LogWarning("DataManager가 존재하지 않습니다.");
        
        // text 패널을 활성화
        resultPanel.SetActive(true);
        
        // Text에 해당 값을 규격에 맞게 작성
        resultText.text = text;
    }

    /// <summary>
    /// 정상에 도달해 게임을 종료할 때 사용하는 함수
    /// </summary>
    public void OnInteract()
    {
        // 게임 종료 함수 실행
        GameManager.Instance?.GameEnd();
        // 지금까지 누적된 데이터들을 가져와서 표시
        RPC_ShowResultPanel();
    } 
}
