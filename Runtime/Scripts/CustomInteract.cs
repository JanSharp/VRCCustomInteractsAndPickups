using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomInteract : CustomInteractableBase
    {
        [Tooltip("Each Listener must define at least one of:\n"
            + "public override void Interact() - Raised on down\n"
            + "public void OnInteractDown()")]
        [Space]
        [SerializeField] private UdonSharpBehaviour[] listeners; // Used by editor scripting.
        [HideInInspector][SerializeField] private UdonSharpBehaviour[] actualListeners;
        [HideInInspector][SerializeField] private string[] listenerEventNames;

        public override bool CanInteract() => !PreventInteraction;

        public void DispatchOnInteract()
        {
            for (int i = 0; i < actualListeners.Length; i++)
            {
                UdonSharpBehaviour listener = actualListeners[i];
                if (listener != null)
                    listener.SendCustomEvent(listenerEventNames[i]);
            }
        }
    }
}
