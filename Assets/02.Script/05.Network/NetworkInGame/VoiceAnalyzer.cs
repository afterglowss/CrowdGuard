using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoiceAnalyzer : MonoBehaviour
{
    private string _device;
    private AudioClip _micClip;
    private const int SampleWindow = 128; // 분석할 샘플 데이터의 양

    public float MicLoudness { get; private set; } // 외부에서 읽어갈 수치 (0~100)
    /// <summary>
    /// 1초마다 최대 음량 반환
    /// </summary>
    public event Action<int> OnSpeakLoud;

    private const float LoopSize = 1f;
    private float accumulatedLoudness = 0f;
    private int sampleCount = 0;
    public float CurrentAverageLoudness { get; private set; }

    void Start() {
        // 첫 번째 마이크 장치를 선택
        if (Microphone.devices.Length > 0)
        {
            _device = Microphone.devices[0];
            // 마이크 입력을 루프 형태로 시작 (마이크명, 루프여부, 길이, 샘플레이트)
            _micClip = Microphone.Start(_device, true, 10, 44100);
        }
        else
        {
            Debug.LogError("마이크를 찾을 수 없습니다!");
        }
        StartCoroutine(UpdateMicAverage());
    }

    private IEnumerator UpdateMicAverage() {
        while (true) {
            // 1초 동안 기다림 (초당 1번만 계산)
            yield return new WaitForSeconds(LoopSize);
            var maxLoudness = Mathf.FloorToInt(accumulatedLoudness * 100);
            Debug.Log($"{LoopSize}초 최대 소음: {maxLoudness}");
            OnSpeakLoud?.Invoke(maxLoudness);
            accumulatedLoudness = 0f;

        }
    }

    void Update() {
        // 데이터 수집은 프레임별로 하되, 연산 결과는 코루틴에서 처리
        float frameLoudness = GetLoudnessFromMicrophone();
        accumulatedLoudness = frameLoudness > accumulatedLoudness ? frameLoudness : accumulatedLoudness;
    }

    /// <summary>
    /// 최대 음량을 float값으로 변환하여 반환
    /// </summary>
    /// <returns>최대 음량 반환</returns>
    private float GetLoudnessFromMicrophone()
    {
        float levelMax = 0;
        float[] waveData = new float[SampleWindow];
        
        // 현재 마이크가 기록 중인 위치를 파악
        int micPosition = Microphone.GetPosition(_device) - (SampleWindow + 1);
        if (micPosition < 0) return 0;

        // 클립에서 데이터 추출
        _micClip.GetData(waveData, micPosition);

        // 가장 큰 진폭(Peak) 찾기
        for (int i = 0; i < SampleWindow; i++)
        {
            float wavePeak = waveData[i] * waveData[i]; // 제곱하여 양수화
            if (levelMax < wavePeak)
            {
                levelMax = wavePeak;
            }
        }
        
        return Mathf.Sqrt(levelMax); // 다시 루트를 씌워 원래 진폭 복원 (RMS와 유사)
    }
}
