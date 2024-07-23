﻿using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomInteractsAndPickupsManager : UdonSharpBehaviour
    {
        public Material highlightMat;
        public GameObject highlightPartPrefab;
        public CustomInteractHandData leftHand;
        public CustomInteractHandData rightHand;

        private VRCPlayerApi localPlayer;
        private bool isInVR = true;

        private void Start()
        {
            localPlayer = Networking.LocalPlayer;
            isInVR = localPlayer.IsUserInVR();
            if (isInVR)
            {
                leftHand.handType = VRCPlayerApi.TrackingDataType.LeftHand;
                leftHand.rotationNormalization = Quaternion.AngleAxis(90f, Vector3.forward);
                leftHand.offsetVectorShift = Vector3.zero;
                leftHand.manager = this;
                rightHand.handType = VRCPlayerApi.TrackingDataType.RightHand;
                rightHand.rotationNormalization = Quaternion.AngleAxis(90f, Vector3.back);
                rightHand.offsetVectorShift = Vector3.zero;
                rightHand.manager = this;
            }
            else
            {
                leftHand.handType = VRCPlayerApi.TrackingDataType.Head;
                leftHand.rotationNormalization = Quaternion.identity;
                leftHand.offsetVectorShift = new Vector3(0.4f, -0.2f, 0.5f); // TODO: should this scale with eye height.
                leftHand.manager = this;
                rightHand.gameObject.SetActive(false);
            }
            leftHand.Initialize();
            if (isInVR)
                rightHand.Initialize();
            UpdateEyeHeightLoop();
        }

        public override void OnAvatarEyeHeightChanged(VRCPlayerApi player, float prevEyeHeightAsMeters)
        {
            if (!player.isLocal)
                return;
            UpdatePlayerEyeHeight();
        }

        public override void OnAvatarChanged(VRCPlayerApi player)
        {
            if (!player.isLocal)
                return;
            UpdatePlayerEyeHeight();
        }

        /// <summary>
        /// <para>Major trust issues. The 2 events above should handle everything already.</para>
        /// </summary>
        public void UpdateEyeHeightLoop()
        {
            UpdatePlayerEyeHeight();
            SendCustomEventDelayedSeconds(nameof(UpdateEyeHeightLoop), 10f);
        }

        private void UpdatePlayerEyeHeight()
        {
            float eyeHeight = localPlayer.GetAvatarEyeHeightAsMeters();
            // Being twice has big only increases scale by 0.5f instead of 1f.
            // Being twice as small only reduces scale by 0.25f instead of 0.5f.
            float eyeHeightScale = (eyeHeight / 2f - 1f) / 2f + 1f;
            leftHand.SetEyeHeightScale(eyeHeightScale);
            if (isInVR)
                rightHand.SetEyeHeightScale(eyeHeightScale);
        }

        private void Update()
        {
            leftHand.UpdateHand();
            if (isInVR)
                rightHand.UpdateHand();
        }
    }
}
