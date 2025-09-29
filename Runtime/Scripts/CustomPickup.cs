using JanSharp.Internal;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace JanSharp
{
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomPickup : CustomInteractableBase
    {
        public string useText;
        [Tooltip("Imagine making finger guns with your hands. The index finger would match the forward vector "
            + "(blue), the thumb would match the up vector (green) of this Exact Grip transform.\nIn terms of "
            + "position, this transform would be exactly at your hand tracking position, which I believe to "
            + "be around the palm.")]
        public Transform exactGrip;
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

        [System.NonSerialized] public bool isHeld;
        /// <summary>
        /// <para>One of <see cref="VRCPlayerApi.TrackingDataType.LeftHand"/> (VR),
        /// <see cref="VRCPlayerApi.TrackingDataType.RightHand"/> (VR) or
        /// <see cref="VRCPlayerApi.TrackingDataType.Head"/> (desktop).</para>
        /// </summary>
        [System.NonSerialized] public VRCPlayerApi.TrackingDataType heldTrackingType;
        [System.NonSerialized] public Vector3 heldOffsetVector;
        [System.NonSerialized] public Quaternion heldOffsetRotation;

        [System.NonSerialized] public bool usedHermiteCurveWhenLastPickedUp;

        /// <summary>
        /// <para>When <see langword="true"/>, read the <see cref="Transform.localPosition"/> and
        /// <see cref="Transform.localRotation"/> to know what offsets the pickup has in relation to the
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
        /// <para>Usable inside of <c>OnPickupStateChanged</c> to compare to
        /// <see cref="isHeld"/>.</para>
        /// <para>Read only.</para>
        /// </summary>
        [System.NonSerialized] public bool prevIsHeld;
        /// <summary>
        /// <para>Usable inside of <c>OnPickupStateChanged</c> to compare to
        /// <see cref="heldTrackingType"/>.</para>
        /// <para>Read only.</para>
        /// </summary>
        [System.NonSerialized] public VRCPlayerApi.TrackingDataType prevHeldTrackingType;
        /// <summary>
        /// <para>Usable inside of <c>OnPickupStateChanged</c> to compare to
        /// <see cref="heldOffsetVector"/>.</para>
        /// <para>Read only.</para>
        /// </summary>
        [System.NonSerialized] public Vector3 prevHeldOffsetVector;
        /// <summary>
        /// <para>Usable inside of <c>OnPickupStateChanged</c> to compare to
        /// <see cref="heldOffsetRotation"/>.</para>
        /// <para>Read only.</para>
        /// </summary>
        [System.NonSerialized] public Quaternion prevHeldOffsetRotation;
        /// <summary>
        /// <para>Usable inside of <c>OnPickupStateChanged</c> to compare to
        /// <see cref="isAttached"/>.</para>
        /// <para>Read only.</para>
        /// </summary>
        [System.NonSerialized] public bool prevIsAttached;
        /// <summary>
        /// <para>Usable inside of <c>OnPickupStateChanged</c> to compare to
        /// <see cref="attachedToBone"/>.</para>
        /// <para>Read only.</para>
        /// </summary>
        [System.NonSerialized] public HumanBodyBones prevAttachedToBone;
        private int ongoingStateModifications;

        public override bool CanInteract() => !PreventInteraction && !isHeld;

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
            if (prevIsHeld == isHeld // The vast majority of the time isHeld or isAttached will differ,
                && prevIsAttached == isAttached // making this if condition short circuit pretty quickly.
                && (!isHeld // Value differences only matter if it was and still is held.
                    || (prevHeldTrackingType == heldTrackingType
                    && prevHeldOffsetVector == heldOffsetVector
                    && prevHeldOffsetRotation == heldOffsetRotation))
                && (!isAttached // Value differences only matter if it was and still is attached.
                    || prevAttachedToBone == attachedToBone))
            {
                return;
            }
            DispatchOnPickupStateChanged();
            prevIsHeld = isHeld;
            prevHeldTrackingType = heldTrackingType;
            prevHeldOffsetVector = heldOffsetVector;
            prevHeldOffsetRotation = heldOffsetRotation;
            prevIsAttached = isAttached;
            prevAttachedToBone = attachedToBone;
        }

        private void OnDestroy()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  OnDestroy");
#endif
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

        public void ForceBeingPickedUp(VRCPlayerApi.TrackingDataType heldTrackingType, bool useHermiteCurve = false)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  ForceBeingPickedUp");
#endif
            EnsureHasManagerRef();
            CustomInteractHandManager hand = manager.GetHandForTrackingType(heldTrackingType);
            hand.ForcePickup(this, useHermiteCurve);
        }

        public void ForceBeingPickedUp(
            VRCPlayerApi.TrackingDataType heldTrackingType,
            Vector3 heldOffsetVector,
            Quaternion heldOffsetRotation,
            bool useHermiteCurve = false)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  ForceBeingPickedUp");
#endif
            this.heldOffsetVector = heldOffsetVector;
            this.heldOffsetRotation = heldOffsetRotation;
            EnsureHasManagerRef();
            CustomInteractHandManager hand = manager.GetHandForTrackingType(heldTrackingType);
            hand.ForcePickupUsingExistingOffset(this, useHermiteCurve);
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
            Detach();
            this.attachedToBone = attachedToBone;
            EnsureHasManagerRef();
            manager.attachedManager.AttachToBone(this, attachedToBone);
            FinishStateModification();
        }
    }
}
