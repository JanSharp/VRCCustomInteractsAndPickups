using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace JanSharp.Internal
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [SingletonDependency(typeof(SingletonManager))] // Not used in this script, but interacts/pickups do need it.
    public class CustomInteractablesManager : CustomInteractablesManagerAPI
    {
        public Material highlightMat;
        public GameObject highlightPartPrefab;
        public CustomInteractHandManager leftHand;
        public CustomInteractHandManager rightHand;

        public Vector3 onSelectionGainedHaptics;
        public Vector3 onSelectionLostHaptics;
        public Vector3 onSelectionChangedHaptics;
        public Vector3 onPickupHaptics; // Very unsure about this one.
        public Vector3 onDropHaptics; // Very unsure about this one.
        // public Vector3 onInteractHaptics; // I don't believe that makes sense to add.
        // public Vector3 onUseHaptics; // I don't believe that makes sense to add.

        private VRCPlayerApi localPlayer;
        private bool isInVR = true;

        public override CustomPickup HeldInLeftHand => leftHand.activePickup;
        public override CustomPickup HeldInRightHand => rightHand.activePickup;
        public override CustomPickup HeldOnDesktop => leftHand.activePickup;

        private void Start()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] Manager  Start");
#endif
            localPlayer = Networking.LocalPlayer;
            isInVR = localPlayer.IsUserInVR();
            if (isInVR)
            {
                leftHand.trackingHandType = VRCPlayerApi.TrackingDataType.LeftHand;
                leftHand.pickupHandType = VRC_Pickup.PickupHand.Left;
                leftHand.handType = HandType.LEFT;
                leftHand.rotationNormalization = Quaternion.AngleAxis(90f, Vector3.forward) * Quaternion.AngleAxis(45f, Vector3.right);
                leftHand.palmDirection = Vector3.up;
                leftHand.offsetVectorShift = Vector3.zero;
                leftHand.manager = this;
                rightHand.trackingHandType = VRCPlayerApi.TrackingDataType.RightHand;
                rightHand.pickupHandType = VRC_Pickup.PickupHand.Right;
                rightHand.handType = HandType.RIGHT;
                rightHand.rotationNormalization = Quaternion.AngleAxis(90f, Vector3.forward) * Quaternion.AngleAxis(45f, Vector3.right);
                rightHand.palmDirection = Vector3.down;
                rightHand.offsetVectorShift = Vector3.zero;
                rightHand.manager = this;
            }
            else
            {
                leftHand.trackingHandType = VRCPlayerApi.TrackingDataType.Head;
                leftHand.pickupHandType = VRC_Pickup.PickupHand.None;
                leftHand.handType = HandType.LEFT; // Does not matter, is not used.
                leftHand.rotationNormalization = Quaternion.identity;
                leftHand.palmDirection = Vector3.zero;
                leftHand.offsetVectorShift = new Vector3(0.4f, -0.2f, 0.5f); // TODO: should this scale with eye height.
                leftHand.manager = this;
                Destroy(rightHand.gameObject); // Disabled scripts apparently still get VRChat's InoutFoo events, so destroy it instead.
            }
            leftHand.Initialize();
            if (isInVR)
                rightHand.Initialize();
            UpdateEyeHeightLoop();
        }

        public override void OnAvatarEyeHeightChanged(VRCPlayerApi player, float prevEyeHeightAsMeters)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] Manager  OnAvatarEyeHeightChanged - player.isLocal: {player.isLocal}");
#endif
            if (!player.isLocal)
                return;
            UpdatePlayerEyeHeight();
        }

        public override void OnAvatarChanged(VRCPlayerApi player)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] Manager  OnAvatarChanged - player.isLocal: {player.isLocal}");
#endif
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
            // The smallest eye height is 0.2, largest 5, with the avatars I've tested anyway.
            // But I believe you can make avatars greater than 5 meters tall, so clamp the top end as well.
            // The low end clamp exists to make truly tiny avatars still be able to reach stuff, while small
            // avatars can reach things relative to their size, not from too far away.
            float eyeHeightScale = Mathf.Clamp(eyeHeight / 2f, 0.25f, 4f);
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

        public void DropPickup(CustomPickup pickup)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] Manager  DropPickup");
#endif
            if (pickup.heldTrackingType == VRCPlayerApi.TrackingDataType.RightHand)
                rightHand.DropActivePickup();
            else
                leftHand.DropActivePickup();
        }

        public CustomInteractHandManager GetHandForTrackingType(VRCPlayerApi.TrackingDataType trackingType)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] Manager  GetHandForTrackingType");
#endif
            return trackingType == VRCPlayerApi.TrackingDataType.RightHand ? rightHand : leftHand;
        }
    }
}
