using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace JanSharp.Internal
{
    public enum CustomPickupsAutoHoldMode
    {
        /// <summary>
        /// <para>When in desktop this behaves the same way as <see cref="ShortGrab"/>.</para>
        /// <para>Auto hold is only initiated when receiving grab input down and use input down events within
        /// a short period of time.</para>
        /// <para>The input grab up event must not have been received yet before receiving the use input down
        /// event.</para>
        /// <para>Once auto hold is initiated, the next grab input up event will be ignored. The next grab
        /// input up event after that will drop the pickup.</para>
        /// <para>Any other grab inputs will pick pickups up in the grab input down event and drop them with
        /// the grab input up event.</para>
        /// </summary>
        SimultaneousGrabAndUse,
        /// <summary>
        /// <para>When picking up a pickup, if the grab down and up events are within a short period of time,
        /// almost like a click, auto hold is initiated. Which is to say that the grab up event will not
        /// result in the pickup getting dropped. The next grab up event after that will drop the
        /// pickup.</para>
        /// <para>If it is longer than a click, more like a drag, the grab up event will drop the
        /// pickup.</para>
        /// </summary>
        ShortGrab,
        /// <summary>
        /// <para>The way VRCPickups auto hold works.</para>
        /// <para>Grab input down initiates auto hold. The next grab input up gets ignored. The grab input up
        /// event after that drops the pickup.</para>
        /// <para>Since there is no per pickup auto hold option - everything is auto hold - this makes every
        /// grab result in auto hold.</para>
        /// </summary>
        AnyDurationGrab,
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    // Not used in this script, but interacts/pickups do need it.
    [SingletonDependency(typeof(SingletonManager))]
    // The API already has the SingletonScript attribute, however in order for interacts/pickups to be able to
    // resolve the singleton reference to this internal script it must also be marked as a singleton script.
    [SingletonScript("bb7ec25f46ae4ab699263323ebfb58ec")] // Runtime/Prefabs/CustomInteractablesManager.prefab
    public class CustomInteractablesManager : CustomInteractablesManagerAPI
    {
        // This is copy paste from the xml annotations above. This is absolutely horrible, but whatever it's better than nothing.
        [Tooltip("SimultaneousGrabAndUse:\n"
            + "When in desktop this behaves the same way as ShortGrab.\n"
            + "Auto hold is only initiated when receiving grab input down and use input down events within a "
                + "short period of time.\n"
            + "The input grab up event must not have been received yet before receiving the use input down event.\n"
            + "Once auto hold is initiated, the next grab input up event will be ignored. The next grab "
                + "input up event after that will drop the pickup.\n"
            + "Any other grab inputs will pick pickups up in the grab input down event and drop them with "
                + "the grab input up event.\n"
            + "\n"
            + "ShortGrab:\n"
            + "When picking up a pickup, if the grab down and up events are within a short period of time, "
                + "almost like a click, auto hold is initiated. Which is to say that the grab up event will not "
                + "result in the pickup getting dropped. The next grab up event after that will drop the pickup.\n"
            + "If it is longer than a click, more like a drag, the grab up event will drop the pickup.\n"
            + "\n"
            + "AnyDurationGrab:\n"
            + "The way VRCPickups auto hold works.\n"
            + "Grab input down initiates auto hold. The next grab input up gets ignored. The grab input up "
                + "event after that drops the pickup.\n"
            + "Since there is no per pickup auto hold option - everything is auto hold - this makes every "
                + "grab result in auto hold.")]
        [SerializeField] private CustomPickupsAutoHoldMode autoHoldMode;
        public CustomPickupsAutoHoldMode AutoHoldMode
        {
            get => autoHoldMode;
            set
            {
                autoHoldMode = value;
                UpdateAutoHoldMode();
            }
        }

        [Tooltip("All pickups with 'Attachment Mode' set to 'Use Default Mode From Manager' will use this value.")]
        [SerializeField] private bool defaultAttachmentMode = true;
        public bool DefaultAttachmentMode
        {
            get => defaultAttachmentMode;
            set
            {
                if (defaultAttachmentMode == value)
                    return;
                defaultAttachmentMode = value;
                if (!defaultAttachmentMode)
                    attachedManager.DetachAllWhichUseDefaultModeFromManager();
            }
        }

        [Space]
        public Material highlightMat;
        public GameObject highlightPartPrefab;
        public CustomInteractHandManager leftHand;
        public CustomInteractHandManager rightHand;
        public CustomAttachedPickupsManager attachedManager;
        public CustomPickupController fallbackPickupController;

        [Space]
        public Vector3 onSelectionGainedHaptics;
        public Vector3 onSelectionLostHaptics;
        public Vector3 onSelectionChangedHaptics;
        public Vector3 onPickupHaptics; // Unsure about this one.
        public Vector3 onDropHaptics; // Unsure about this one.
        public Vector3 onPickupAndDetachHaptics; // Very unsure about this one.
        public Vector3 onDropAndAttachHaptics; // Unsure about this one.
        // public Vector3 onInteractHaptics; // I don't believe that makes sense to add.
        // public Vector3 onUseHaptics; // I don't believe that makes sense to add.

        private VRCPlayerApi localPlayer;
        private bool isInVR = true;
        private int fixedUpdateCounter;
        [System.NonSerialized] public float lookVerticalInput;
        public const float VerticalLookDownThreshold = -0.7f;

        private int interactLayerNumber;
        private int pickupLayerNumber;
        private LayerMask interactLayer;
        private LayerMask pickupLayer;
        public override int InteractLayerNumber => interactLayerNumber;
        public override int PickupLayerNumber => pickupLayerNumber;
        public override LayerMask InteractLayer => interactLayer;
        public override LayerMask PickupLayer => pickupLayer;

        public override CustomPickup HeldInLeftHand => leftHand.activePickup;
        public override CustomPickup HeldInRightHand => rightHand.activePickup;
        public override CustomPickup HeldOnDesktop => leftHand.activePickup;
        public override CustomPickup[] AttachedPickupsRaw => attachedManager.attachedPickups;
        public override int AttachedPickupsCount => attachedManager.attachedPickupsCount;
        public override CustomPickup[] AttachedPickups => attachedManager.GetAllAttachedPickups();

        private void Start()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] Manager  Start");
#endif
            localPlayer = Networking.LocalPlayer;
            isInVR = localPlayer.IsUserInVR();

            interactLayerNumber = LayerMask.NameToLayer(InteractLayerName);
            pickupLayerNumber = LayerMask.NameToLayer(PickupLayerName);
            interactLayer = (LayerMask)(1 << interactLayerNumber);
            pickupLayer = (LayerMask)(1 << pickupLayerNumber);

            if (isInVR)
            {
                leftHand.handTrackingType = VRCPlayerApi.TrackingDataType.LeftHand;
                leftHand.pickupHandType = VRC_Pickup.PickupHand.Left;
                leftHand.handType = HandType.LEFT;
                leftHand.droppingHandType = DroppingHandType.Left;
                leftHand.rotationNormalization = Quaternion.AngleAxis(90f, Vector3.forward) * Quaternion.AngleAxis(45f, Vector3.right);
                leftHand.palmDirection = Vector3.up;
                leftHand.coneDirection = Quaternion.AngleAxis(60f, Vector3.up) * Vector3.forward;
                leftHand.offsetVectorShift = Vector3.zero;
                rightHand.handTrackingType = VRCPlayerApi.TrackingDataType.RightHand;
                rightHand.pickupHandType = VRC_Pickup.PickupHand.Right;
                rightHand.handType = HandType.RIGHT;
                rightHand.droppingHandType = DroppingHandType.Right;
                rightHand.rotationNormalization = Quaternion.AngleAxis(90f, Vector3.forward) * Quaternion.AngleAxis(45f, Vector3.right);
                rightHand.palmDirection = Vector3.down;
                rightHand.coneDirection = Quaternion.AngleAxis(-60f, Vector3.up) * Vector3.forward;
                rightHand.offsetVectorShift = Vector3.zero;
            }
            else
            {
                leftHand.handTrackingType = VRCPlayerApi.TrackingDataType.Head;
                leftHand.pickupHandType = VRC_Pickup.PickupHand.None;
                leftHand.handType = HandType.LEFT; // Does not matter, is not used.
                leftHand.droppingHandType = DroppingHandType.None; // Does not matter, is not used.
                leftHand.rotationNormalization = Quaternion.identity;
                leftHand.palmDirection = Vector3.forward;
                leftHand.coneDirection = Vector3.forward;
                leftHand.offsetVectorShift = new Vector3(0.4f, -0.2f, 0.5f); // TODO: should this scale with eye height.
                Destroy(rightHand.gameObject); // Disabled scripts apparently still get VRChat's InoutFoo events, so destroy it instead.
            }

            UpdateAutoHoldMode();
            leftHand.Initialize();
            if (isInVR)
                rightHand.Initialize();
            UpdateEyeHeightLoop();
        }

        private void UpdateAutoHoldMode()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] Manager  UpdateAutoHoldMode");
#endif
            CustomPickupsAutoHoldMode actualValue = !isInVR && autoHoldMode == CustomPickupsAutoHoldMode.SimultaneousGrabAndUse
                ? CustomPickupsAutoHoldMode.ShortGrab
                : autoHoldMode;
            leftHand.autoHoldMode = actualValue;
            if (rightHand != null)
                rightHand.autoHoldMode = actualValue;
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

        // TODO: Maybe move the other hand input events here too.
        public override void InputLookVertical(float value, UdonInputEventArgs args)
        {
            // Even though we only care about this value in VR, just set it unconditionally in here as to not
            // waste the performance on a branch in this function.
            lookVerticalInput = value;
        }

        private void Update()
        {
            leftHand.UpdateHand();
            if (isInVR)
                rightHand.UpdateHand();
            attachedManager.UpdateAttachedPickups();
        }

        private void FixedUpdate()
        {
            // 25 updates per second in desktop (Up to 40 ms input delay).
            // 12.5 updates per second per hand in VR (Up to 80 ms input delay).
            if (fixedUpdateCounter == 0)
                leftHand.FixedUpdateHand();
            else if (fixedUpdateCounter == 2)
                if (isInVR)
                    rightHand.FixedUpdateHand();
                else
                    leftHand.FixedUpdateHand();
            fixedUpdateCounter = (fixedUpdateCounter + 1) % 4;
        }

        public void DropPickup(CustomPickup pickup, bool preventAttachment = false)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] Manager  DropPickup");
#endif
            leftHand.DropPickupIfHeld(pickup, preventAttachment);
            if (isInVR)
                rightHand.DropPickupIfHeld(pickup, preventAttachment);
        }

        public void DetachPickup(CustomPickup pickup)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] Manager  DetachPickup");
#endif
            attachedManager.DetachIfAttached(pickup);
        }

        public void MakeSecondaryHandPrimary(CustomPickup pickup)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] Manager  MakeSecondaryHandPrimary");
#endif
            if (pickup.secondaryHeldTrackingType == VRCPlayerApi.TrackingDataType.RightHand)
                rightHand.BecomePrimaryHand(pickup);
            else
                leftHand.BecomePrimaryHand(pickup);
        }

        public CustomInteractHandManager GetHandForTrackingType(VRCPlayerApi.TrackingDataType trackingType)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] Manager  GetHandForTrackingType");
#endif
            return trackingType == VRCPlayerApi.TrackingDataType.RightHand ? rightHand : leftHand;
        }

        public override Quaternion GetHandRotationNormalization(VRCPlayerApi.TrackingDataType trackingType)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] Manager  GetHandRotationNormalization");
#endif
            return (trackingType == VRCPlayerApi.TrackingDataType.RightHand ? rightHand : leftHand).rotationNormalization;
        }

        public override Vector3 GetClosestPoint(Transform pickupTransform, Vector3 handPosition)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] Manager  GetClosestPoint");
#endif
            float closestDistance = float.PositiveInfinity;
            Vector3 closestPoint = pickupTransform.position; // Default for when there are 0 colliders.
            foreach (Collider collider in pickupTransform.GetComponentsInChildren<Collider>())
            {
                if (collider == null) // Some VRC internal that we're not allowed to access so we get null instead,
                    continue; // even though in normal Unity... this is not possible to be null.
                if (collider.gameObject.layer != pickupLayerNumber)
                    continue;
                Vector3 point = collider.ClosestPoint(handPosition);
                float distance = Vector3.Distance(handPosition, point);
                if (distance >= closestDistance)
                    continue;
                closestDistance = distance;
                closestPoint = point;
            }
            return closestPoint;
        }
    }
}
