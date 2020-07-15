﻿using Photon.Voice.PUN;
using UnityEngine;

[RequireComponent(typeof(PhotonVoiceView))]
public class PointersController : MonoBehaviour
{
    [SerializeField]
    private GameObject pointerDown;
    [SerializeField]
    private GameObject pointerUp;

    private PhotonVoiceView photonVoiceView;
    private void Start()
    {
        this.photonVoiceView = this.GetComponent<PhotonVoiceView>();
    }

    private void Update()
    {
        this.SetActiveSafe(this.pointerDown, this.photonVoiceView.IsSpeaking);
        this.SetActiveSafe(this.pointerUp, this.photonVoiceView.IsRecording);
    }

    private void SetActiveSafe(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
        {
            go.SetActive(active);
        }
    }
}
