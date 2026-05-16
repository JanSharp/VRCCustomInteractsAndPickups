using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomPickupState : UdonSharpBehaviour
    {
        [System.NonSerialized] public CustomPickup pickup;
        [System.NonSerialized] public Transform pickupTransform;

        [System.NonSerialized] public Vector3 primaryHandPosition;
        /// <summary>
        /// <para>Normalized to be sane. Make finger guns, forward is index finger, up is thumb.</para>
        /// </summary>
        [System.NonSerialized] public Quaternion primaryHandRotation;

        [System.NonSerialized] public Vector3 secondaryHandPosition;
        /// <inheritdoc cref="primaryHandRotation"/>
        [System.NonSerialized] public Quaternion secondaryHandRotation;
    }
}
