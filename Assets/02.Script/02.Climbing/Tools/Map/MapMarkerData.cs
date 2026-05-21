using System;
using CrowdGuard.Climbing.Tools.Common;
using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Map
{
    /// <summary>
    /// 지도 표시 계층으로 전달되는 마커 데이터입니다.
    /// </summary>
    [Serializable]
    public struct MapMarkerData
    {
        public MapMarkerType Type;
        public Vector3 WorldPosition;
        public Vector3 Forward;
        public string Label;
        public bool IsVisible;
        public PlayerRole OwnerRole;

        public MapMarkerData(
            MapMarkerType type,
            Vector3 worldPosition,
            Vector3 forward,
            string label,
            bool isVisible = true,
            PlayerRole ownerRole = PlayerRole.None)
        {
            Type = type;
            WorldPosition = worldPosition;
            Forward = forward;
            Label = label;
            IsVisible = isVisible;
            OwnerRole = ownerRole;
        }
    }
}
