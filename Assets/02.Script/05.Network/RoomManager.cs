using System;
using System.Collections;
using System.Collections.Generic;
using Capstone.Photon;
using TMPro;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public PlayerManager playerManager;

    public GameObject interactor;
    public TextMeshPro playerCount;
    private void Start()
    {
        playerManager.OnPlayerChanged += OnPlayerChanged;

    }

    private void OnPlayerChanged(int player)
    {
        playerCount.text = $"Player : {player}/2";
        interactor.SetActive(player >= 2);
    }
    
    
}
