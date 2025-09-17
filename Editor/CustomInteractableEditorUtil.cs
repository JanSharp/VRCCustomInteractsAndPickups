using System.Collections.Generic;
using System.Linq;
using NUnit.Framework.Constraints;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    public static class CustomInteractableEditorUtil
    {
        public static void SetLayer(GameObject[] gameObjects, int layer)
        {
            SerializedObject goSo = new SerializedObject(gameObjects);
            goSo.FindProperty("m_Layer").intValue = layer;
            goSo.ApplyModifiedProperties();
        }

        public static GameObject[] GetGOsIncludingChildrenRespectingNesting(IEnumerable<Transform> targets)
        {
            List<GameObject> result = new();
            void Walk(Transform current)
            {
                if (current.TryGetComponent<CustomInteractableBase>(out _))
                    return;
                result.Add(current.gameObject);
                foreach (Transform child in current)
                    Walk(child);
            }
            foreach (Transform root in targets)
            {
                result.Add(root.gameObject);
                foreach (Transform child in root)
                    Walk(child);
            }
            return result.ToArray();
        }

        public static void DrawLayerHelpBoxAndButtons(string scriptDisplayName, string layerName, IEnumerable<Transform> targets)
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                GUILayout.Label($"Interaction with this {scriptDisplayName} gets detected using colliders "
                    + $"specifically on the '{layerName}' layer. Colliders can be on this object and its "
                    + $"children, and they can be colliders and triggers.", EditorStyles.wordWrappedLabel);

            int layerIndex = LayerMask.NameToLayer(layerName);
            using (new GUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(disabled: targets.All(t => t.gameObject.layer == layerIndex)))
                    if (GUILayout.Button("Set Layer"))
                        SetLayer(
                            targets.Select(t => t.gameObject).ToArray(),
                            layerIndex);
                if (GUILayout.Button(new GUIContent(
                    "Set Layer Including Children",
                    "If there are any other Custom Interact or Custom Pickup scripts as children of this "
                        + "object, their and their children's layers will remain untouched.")))
                {
                    SetLayer(
                        GetGOsIncludingChildrenRespectingNesting(targets),
                        layerIndex);
                }
            }
        }
    }
}
