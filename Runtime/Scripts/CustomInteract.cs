using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Data;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomInteract : CustomInteractableBase
    {
        [Space]
        public UdonSharpBehaviour[] listeners;

        public override bool CanInteract() => !PreventInteraction;

        public void DispatchOnInteract()
        {
            foreach (UdonSharpBehaviour listener in listeners)
                listener.SendCustomEvent("_interact");
        }
    }
}
