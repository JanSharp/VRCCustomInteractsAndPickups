using UdonSharp;

namespace JanSharp
{
    [SingletonScript("bb7ec25f46ae4ab699263323ebfb58ec")] // Runtime/Prefabs/CustomInteractablesManager.prefab
    public abstract class CustomInteractablesManagerAPI : UdonSharpBehaviour
    {
        public const float PickupInterpolationDuration = 0.15f;
        public const float MaxPickupInterpolationDuration = 0.5f;
        public abstract CustomPickup HeldInLeftHand { get; }
        public abstract CustomPickup HeldInRightHand { get; }
        public abstract CustomPickup HeldOnDesktop { get; }
        /// <summary>
        /// <para>A new instance of an array.</para>
        /// <para>Could contain <see langword="null"/>, though it is incredibly unlikely and requires a pickup
        /// to get attached after its <c>OnDestroy</c> event has been raised.</para>
        /// </summary>
        public abstract CustomPickup[] AttachedPickups { get; }
    }
}
