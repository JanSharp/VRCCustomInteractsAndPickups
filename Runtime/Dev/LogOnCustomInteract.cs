using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LogOnCustomInteract : UdonSharpBehaviour
    {
        public override void Interact()
        {
            Debug.Log($"[CustomInteractsAndPickups] LogOnCustomInteract  Interact");
        }

        public void OnInteractDown()
        {
            Debug.Log($"[CustomInteractsAndPickups] LogOnCustomInteract  OnInteractDown");
        }
    }
}
