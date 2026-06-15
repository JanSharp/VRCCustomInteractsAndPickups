using JanSharp.Internal;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace JanSharp
{
    public enum CustomPickupAttachmentMode
    {
        UseDefaultModeFromManager,
        Disabled,
        Enabled,
    }

    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomPickup : CustomInteractableBase
    {
        public const float VRReachForAlreadyHeldPickups = 0.1f;

        public string useText;
        [Tooltip("Imagine making finger guns with your hands. The index finger would match the forward vector "
            + "(blue), the thumb would match the up vector (green) of this Exact Grip transform.\nIn terms of "
            + "position, this transform would be exactly at your hand tracking position, which I believe to "
            + "be around the palm.")]
        [UnityEngine.Serialization.FormerlySerializedAs("exactGrip")] // TODO: Remove.
        public Transform primaryExactGrip;
        [Tooltip("Imagine making finger guns with your hands. The index finger would match the forward vector "
            + "(blue), the thumb would match the up vector (green) of this Exact Grip transform.\nIn terms of "
            + "position, this transform would be exactly at your hand tracking position, which I believe to "
            + "be around the palm.")]
        public Transform secondaryExactGrip;

        [SerializeField] private CustomPickupAttachmentMode attachmentMode = CustomPickupAttachmentMode.UseDefaultModeFromManager;
        public CustomPickupAttachmentMode AttachmentMode
        {
            get => attachmentMode;
            set
            {
                if (attachmentMode == value)
                    return;
                attachmentMode = value;
                if (!AttachmentIsEnabled)
                    Detach();
            }
        }

        [Tooltip("When held with both hands and the primary hand drops this pickup, should the other hand "
            + "become the new primary holding hand? Otherwise the other stays secondary, and there is no "
            + "primary.")]
        public bool droppingTransfersPrimaryHand = true;

        public CustomPickupController pickupController;

        [Tooltip("Each Listener can define any or all of these:\n"
            + "public override void OnPickup()\n"
            + "public override void OnDrop()\n"
            + "public override void OnPickupUseDown()\n"
            + "public override void OnPickupUseUp()\n"
            + "public void OnPickupAttach()\n"
            + "public void OnPickupDetach()\n"
            + "public void OnPickupStateChanged()")]
        [Space]
        public UdonSharpBehaviour[] listeners;

        private uint preventAttachment = 0u;
        public bool PreventAttachment => preventAttachment != 0u;
        public void IncrementPreventAttachment()
        {
            preventAttachment++;
            Detach();
        }
        public void DecrementPreventAttachment() => preventAttachment--;

        public bool AttachmentIsEnabled
        {
            get
            {
                if (attachmentMode != CustomPickupAttachmentMode.UseDefaultModeFromManager)
                    return attachmentMode != CustomPickupAttachmentMode.Enabled;
                EnsureHasManagerRef();
                return manager.DefaultAttachmentMode;
            }
        }

        public bool CanAttach => preventAttachment == 0u && AttachmentIsEnabled;

        [System.NonSerialized] public bool receivedOnDestroy = false;

        public const float InterpolationDuration = 0.15f;
        [System.NonSerialized] public float interpolationProgress = 1f;
        private uint preventInterpolation = 0u;
        public bool PreventInterpolation => preventInterpolation != 0u;
        public void IncrementPreventInterpolation()
        {
            preventInterpolation++;
            StopInterpolation();
        }
        public void DecrementPreventInterpolation() => preventInterpolation--;
        public void StartInterpolation()
        {
            if (preventInterpolation == 0u)
                interpolationProgress = 0f;
        }
        public void StopInterpolation() => interpolationProgress = 1f;

        /// <summary>
        /// <para><see langword="true"/> whenever <see cref="isHeldByPrimaryHand"/> and or
        /// <see cref="isHeldBySecondaryHand"/> is <see langword="true"/>.</para>
        /// <para>A variable rather than a property purely for performance (micro optimization)
        /// reasons.</para>
        /// </summary>
        [System.NonSerialized] public bool isHeld;

        [System.NonSerialized] public bool isHeldByPrimaryHand;
        /// <summary>
        /// <para>One of <see cref="VRCPlayerApi.TrackingDataType.LeftHand"/> (VR),
        /// <see cref="VRCPlayerApi.TrackingDataType.RightHand"/> (VR) or
        /// <see cref="VRCPlayerApi.TrackingDataType.Head"/> (desktop).</para>
        /// </summary>
        [System.NonSerialized] public VRCPlayerApi.TrackingDataType primaryHeldTrackingType;
        /// <summary>
        /// <para>In hand local space, in other words relative to hand position, rotated by hand
        /// rotation.</para>
        /// </summary>
        [System.NonSerialized] public Vector3 primaryOffsetVector;
        /// <summary>
        /// <para>In hand local space, in other words relative to hand rotation.</para>
        /// </summary>
        [System.NonSerialized] public Quaternion primaryOffsetRotation;

        [System.NonSerialized] public bool isHeldBySecondaryHand;
        /// <inheritdoc cref="primaryHeldTrackingType"/>
        [System.NonSerialized] public VRCPlayerApi.TrackingDataType secondaryHeldTrackingType;
        /// <inheritdoc cref="primaryOffsetVector"/>
        [System.NonSerialized] public Vector3 secondaryOffsetVector;
        /// <inheritdoc cref="primaryOffsetRotation"/>
        [System.NonSerialized] public Quaternion secondaryOffsetRotation;

        [System.NonSerialized] public bool usedHermiteCurveWhenLastPickedUp;

        /// <summary>
        /// <para>When <see langword="true"/>, read the <see cref="attachedOffsetVector"/> and
        /// <see cref="attachedOffsetRotation"/> to know what offsets the pickup has in relation to the
        /// attached bone <see cref="attachedToBone"/>.</para>
        /// <para><see langword="true"/> inside of <c>OnPickupAttach()</c>, <see langword="false"/> inside of
        /// <c>OnPickupDetach()</c>.</para>
        /// </summary>
        [System.NonSerialized] public bool isAttached;
        /// <summary>
        /// <para>The bone the pickup is either currently or was last attached to.</para>
        /// <para>The value is undefined if the pickup has never been attached yet.</para>
        /// </summary>
        [System.NonSerialized] public HumanBodyBones attachedToBone;
        /// <summary>
        /// <para>In bone local space, in other words relative to bone position, rotated by bone
        /// rotation.</para>
        /// </summary>
        [System.NonSerialized] public Vector3 attachedOffsetVector;
        /// <summary>
        /// <para>In bone local space, in other words relative to bone rotation.</para>
        /// </summary>
        [System.NonSerialized] public Quaternion attachedOffsetRotation;
        /// <summary>
        /// <para>For use by the <see cref="CustomAttachedPickupsManager"/> script only.</para>
        /// </summary>
        [System.NonSerialized] public int internalAttachedIndex;

        private int ongoingStateModifications;

        public override bool CanInteract() => !PreventInteraction;

        public override float GetEffectiveVRReach() => isHeld ? VRReachForAlreadyHeldPickups : vRReach;

        public void DispatchOnPickup()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  DispatchOnPickup");
#endif
            foreach (UdonSharpBehaviour listener in listeners)
                if (listener != null)
                    listener.SendCustomEvent("_onPickup");
        }

        public void DispatchOnDrop()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  DispatchOnDrop");
#endif
            foreach (UdonSharpBehaviour listener in listeners)
                if (listener != null)
                    listener.SendCustomEvent("_onDrop");
        }

        public void DispatchOnPickupUseDown()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  DispatchOnPickupUseDown");
#endif
            foreach (UdonSharpBehaviour listener in listeners)
                if (listener != null)
                    listener.SendCustomEvent("_onPickupUseDown");
        }

        public void DispatchOnPickupUseUp()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  DispatchOnPickupUseUp");
#endif
            foreach (UdonSharpBehaviour listener in listeners)
                if (listener != null)
                    listener.SendCustomEvent("_onPickupUseUp");
        }

        public void DispatchOnPickupAttach()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  DispatchOnPickupAttach");
#endif
            foreach (UdonSharpBehaviour listener in listeners)
                if (listener != null)
                    listener.SendCustomEvent("OnPickupAttach");
        }

        public void DispatchOnPickupDetach()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  DispatchOnPickupDetach");
#endif
            foreach (UdonSharpBehaviour listener in listeners)
                if (listener != null)
                    listener.SendCustomEvent("OnPickupDetach");
        }

        /// <summary>
        /// <para>Can be raised even though nothing changed.</para>
        /// <para>There is no reason to attempt to prevent it from getting raised, because as soon as more
        /// than one listener is involved, all listeners past the first one would no longer have the guarantee
        /// of any values actually differing.</para>
        /// </summary>
        private void DispatchOnPickupStateChanged()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  DispatchOnPickupStateChanged");
#endif
            foreach (UdonSharpBehaviour listener in listeners)
                if (listener != null)
                    listener.SendCustomEvent("OnPickupStateChanged");
        }

        public void BeginStateModification()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  BeginStateModification - ongoingStateModifications: {ongoingStateModifications}");
#endif
            ongoingStateModifications++;
        }

        public void FinishStateModification()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  FinishStateModification - ongoingStateModifications: {ongoingStateModifications}");
#endif
            if ((--ongoingStateModifications) != 0)
                return;
            DispatchOnPickupStateChanged();
        }

        private void OnDestroy()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  OnDestroy");
#endif
            receivedOnDestroy = true;
            // Maybe this'll prevent errors when leaving a world, not sure.
            if (!Utilities.IsValid(Networking.LocalPlayer))
                return;
            if (isHeld)
                Drop();
            else if (isAttached)
                Detach();
        }

        public void Drop()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  Drop");
#endif
            if (!isHeld)
                return;
            manager.DropPickup(this);
        }

        public void Detach()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  Detach");
#endif
            if (!isAttached)
                return;
            manager.attachedManager.DetachIfAttached(this);
        }

        public void ForceBeingPickedUp(VRCPlayerApi.TrackingDataType heldTrackingType)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  ForceBeingPickedUp");
#endif
            EnsureHasManagerRef();
            CustomInteractHandManager hand = manager.GetHandForTrackingType(heldTrackingType);
            hand.ForcePickup(this);
        }

        /// <summary>
        /// <para>Does not change the world position and rotation of the pickup, therefore also does not do
        /// any interpolation. If that is required and or desired, perform said interpolation on the local
        /// position and rotation after calling <see cref="ForceBeingAttached(HumanBodyBones)"/>.</para>
        /// </summary>
        /// <param name="attachedToBone"></param>
        public void ForceBeingAttached(HumanBodyBones attachedToBone)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  ForceBeingAttached");
#endif
            if (isAttached && attachedToBone == this.attachedToBone)
                return;
            BeginStateModification();
            if (isHeld)
                Drop();
            else if (isAttached)
                Detach();
            if (Networking.LocalPlayer.GetBonePosition(attachedToBone) != Vector3.zero)
            {
                EnsureHasManagerRef();
                manager.attachedManager.AttachToBone(this, attachedToBone);
            }
            FinishStateModification();
        }

        public void MakeSecondaryHandPrimary()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  MakeSecondaryHandPrimary");
#endif
            manager.MakeSecondaryHandPrimary(this);
        }
    }
}
