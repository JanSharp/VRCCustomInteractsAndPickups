using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace JanSharp
{
    [SingletonScript("bb7ec25f46ae4ab699263323ebfb58ec")] // Runtime/Prefabs/CustomInteractablesManager.prefab
    public abstract class CustomInteractablesManagerAPI : UdonSharpBehaviour
    {
        public const string InteractLayerName = "Interactive";
        public const string PickupLayerName = "Pickup";
        public abstract int InteractLayerNumber { get; }
        public abstract int PickupLayerNumber { get; }
        public abstract LayerMask InteractLayer { get; }
        public abstract LayerMask PickupLayer { get; }

        public abstract CustomPickup HeldInLeftHand { get; }
        public abstract CustomPickup HeldInRightHand { get; }
        public abstract CustomPickup HeldOnDesktop { get; }
        /// <summary>
        /// <para>A reference to the internal array, must not be modified.</para>
        /// <para>Only elements up to <see cref="AttachedPickupsCount"/> are used.</para>
        /// <para>Does not contain any <see langword="null"/> elements (within the used range).</para>
        /// </summary>
        public abstract CustomPickup[] AttachedPickupsRaw { get; }
        public abstract int AttachedPickupsCount { get; }
        /// <summary>
        /// <para>A new instance of an array.</para>
        /// <para>Does not contain any <see langword="null"/> elements.</para>
        /// </summary>
        public abstract CustomPickup[] AttachedPickups { get; }

        /// <summary>
        /// <para>Get the rotation to rotate a hand's tracking data rotation by in order to make the forward
        /// direction point close to the direction of the index finger and the up direction relatively aligned
        /// with the thumb.</para>
        /// </summary>
        /// <param name="trackingType"><see cref="VRCPlayerApi.TrackingDataType.LeftHand"/> or
        /// <see cref="VRCPlayerApi.TrackingDataType.RightHand"/>.</param>
        /// <returns></returns>
        public abstract Quaternion GetHandRotationNormalization(VRCPlayerApi.TrackingDataType trackingType);

        public abstract Vector3 GetClosestPoint(Transform pickupTransform, Vector3 handPosition);
    }
}
