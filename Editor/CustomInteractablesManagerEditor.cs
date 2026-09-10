using JanSharp.Internal;
using UdonSharpEditor;
using UnityEditor;

namespace JanSharp
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(CustomInteractablesManager))]
    public class CustomInteractablesManagerEditor : Editor
    {
        private static bool internalFoldedOut = false;

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
            if (internalFoldedOut = EditorGUILayout.Foldout(internalFoldedOut, "Internal", toggleOnLabelClick: true))
                DrawPropertiesExcluding(so, "m_Script", "autoHoldMode", "defaultAttachmentMode");
            so.ApplyModifiedProperties();
        }
    }
}
