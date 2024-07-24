using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomPickup : CustomInteractiveBase
    {
        public string useText;
        public bool autoHold;
        [Tooltip("Imagine making finger guns with your hands. The index finger would match the forward vector "
            + "(blue), the thumb would match the up vector (green) of this Exact Grip transform.\nIn terms of "
            + "position, this transform would be exactly at your hand tracking position, which I believe to "
            + "be around the palm.")]
        public Transform exactGrip;
        [Space]
        public UdonSharpBehaviour[] listeners;

        [System.NonSerialized] public bool isHeld;
        /// <summary>
        /// <para>One of <see cref="VRCPlayerApi.TrackingDataType.LeftHand"/> (VR),
        /// <see cref="VRCPlayerApi.TrackingDataType.RightHand"/> (VR) or
        /// <see cref="VRCPlayerApi.TrackingDataType.Head"/> (desktop).</para>
        /// </summary>
        [System.NonSerialized] public VRCPlayerApi.TrackingDataType heldTrackingData;
        [System.NonSerialized] public Vector3 heldOffsetVector;
        [System.NonSerialized] public Quaternion heldOffsetRotation;

        public void DispatchOnPickup()
        {
            foreach (UdonSharpBehaviour listener in listeners)
                listener.SendCustomEvent("_onPickup");
        }

        public void DispatchOnDrop()
        {
            foreach (UdonSharpBehaviour listener in listeners)
                listener.SendCustomEvent("_onDrop");
        }

        public void DispatchOnPickupUseDown()
        {
            foreach (UdonSharpBehaviour listener in listeners)
                listener.SendCustomEvent("_onPickupUseDown");
        }

        public void DispatchOnPickupUseUp()
        {
            foreach (UdonSharpBehaviour listener in listeners)
                listener.SendCustomEvent("_onPickupUseUp");
        }

        public void Drop()
        {
            if (!isHeld)
                return;
            manager.DropPickup(this);
        }
    }
}
