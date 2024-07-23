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
        public bool autoHold;
        [Tooltip("Imagine making finger guns with your hands. The index finger would match the forward vector "
            + "(blue), the thumb would match the up vector (green) of this Exact Grip transform.\nIn terms of "
            + "position, this transform would be exactly at your hand tracking position, which I believe to "
            + "be around the palm.")]
        public Transform exactGrip;
    }
}
