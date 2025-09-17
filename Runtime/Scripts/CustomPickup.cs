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
            + "public void OnPickupDetach()")]
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

        private void OnDestroy()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  OnDestroy");
#endif
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
            Debug.Log($"[CustomInteractsAndPickupsDebug] CustomPickup {this.name}  Drop");
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
            if (isAttached)
            {
                if (attachedToBone == this.attachedToBone)
                    return;
                Detach();
            }
            this.attachedToBone = attachedToBone;
            EnsureHasManagerRef();
            manager.attachedManager.AttachToBone(this, attachedToBone);
        }
    }
}
