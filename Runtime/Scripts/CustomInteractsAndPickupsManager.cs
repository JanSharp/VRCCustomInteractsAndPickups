using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomInteractsAndPickupsManager : UdonSharpBehaviour
    {
        public Material highlightMat;
        public GameObject highlightPartPrefab;
        public CustomInteractHandManager leftHand;
        public CustomInteractHandManager rightHand;

        private VRCPlayerApi localPlayer;
        private bool isInVR = true;

        private void Start()
        {
            localPlayer = Networking.LocalPlayer;
            isInVR = localPlayer.IsUserInVR();
            if (isInVR)
            {
                leftHand.trackingHandType = VRCPlayerApi.TrackingDataType.LeftHand;
                leftHand.handType = HandType.LEFT;
                leftHand.rotationNormalization = Quaternion.AngleAxis(90f, Vector3.forward);
                leftHand.offsetVectorShift = Vector3.zero;
                leftHand.manager = this;
                rightHand.trackingHandType = VRCPlayerApi.TrackingDataType.RightHand;
                leftHand.handType = HandType.RIGHT;
                rightHand.rotationNormalization = Quaternion.AngleAxis(90f, Vector3.back);
                rightHand.offsetVectorShift = Vector3.zero;
                rightHand.manager = this;
            }
            else
            {
                leftHand.trackingHandType = VRCPlayerApi.TrackingDataType.Head;
                leftHand.handType = HandType.LEFT; // Does not matter, is not used.
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
