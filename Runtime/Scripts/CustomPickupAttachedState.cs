using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomPickupAttachedState : UdonSharpBehaviour
    {
        [System.NonSerialized] public CustomPickup pickup;
        [System.NonSerialized] public Transform pickupTransform;

        [System.NonSerialized] public Vector3 bonePosition;
        [System.NonSerialized] public Quaternion boneRotation;
    }
}
