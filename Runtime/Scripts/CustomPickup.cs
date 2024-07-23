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
    }
}
