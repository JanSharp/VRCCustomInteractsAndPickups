using System.Linq;
using UdonSharpEditor;
using UnityEditor;

namespace JanSharp
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(CustomPickup))]
    public class CustomPickupEditor : Editor
    {
        private SerializedObject so;

        public void OnEnable()
        {
            so = serializedObject;
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(targets))
                return;
            CustomInteractableEditorUtil.DrawLayerHelpBoxAndButtons(
                "Pickup",
                CustomInteractablesManagerAPI.PickupLayerName,
                targets.Cast<CustomPickup>().Select(i => i.transform));
            EditorGUILayout.Space();
            so.Update();
            DrawPropertiesExcluding(so, "m_Script");
            so.ApplyModifiedProperties();
        }
    }
}
