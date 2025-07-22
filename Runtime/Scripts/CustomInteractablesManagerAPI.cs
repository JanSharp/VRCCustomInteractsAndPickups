using UdonSharp;

namespace JanSharp
{
    [SingletonScript("bb7ec25f46ae4ab699263323ebfb58ec")] // Runtime/Prefabs/CustomInteractablesManager.prefab
    public abstract class CustomInteractablesManagerAPI : UdonSharpBehaviour
    {
        public abstract CustomPickup HeldInLeftHand { get; }
        public abstract CustomPickup HeldInRightHand { get; }
        public abstract CustomPickup HeldOnDesktop { get; }
    }
}
