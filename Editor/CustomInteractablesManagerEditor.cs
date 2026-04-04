using JanSharp.Internal;
using UdonSharpEditor;
using UnityEditor;
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
using UnityEngine;
#endif

namespace JanSharp
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(CustomInteractablesManager))]
    public class CustomInteractablesManagerEditor : Editor
    {
        private SerializedObject so;
        private SerializedProperty autoHoldModeProp;
        private SerializedProperty defaultAttachmentModeProp;

        public void OnEnable()
        {
            so = serializedObject;
            autoHoldModeProp = so.FindProperty("autoHoldMode");
            defaultAttachmentModeProp = so.FindProperty("defaultAttachmentMode");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(targets))
                return;
            so.Update();
            EditorGUILayout.PropertyField(autoHoldModeProp);
            EditorGUILayout.PropertyField(defaultAttachmentModeProp);
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            GUILayout.Label("Debug / Internal", EditorStyles.boldLabel);
            DrawPropertiesExcluding(so, "m_Script", "autoHoldMode", "defaultAttachmentMode");
#endif
            so.ApplyModifiedProperties();
        }
    }
}
