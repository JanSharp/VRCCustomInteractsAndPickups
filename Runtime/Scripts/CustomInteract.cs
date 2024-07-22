using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Data;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomInteract : CustomInteractiveBase
    {
        public UdonSharpBehaviour[] listeners;

        public void DispatchOnInteract()
        {
            foreach (UdonSharpBehaviour listener in listeners)
                listener.SendCustomEvent("_interact");
        }
    }
}
