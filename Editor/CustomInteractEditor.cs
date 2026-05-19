using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    [InitializeOnLoad]
    public static class CustomInteractOnBuild
    {
        static CustomInteractOnBuild()
        {
            OnBuildUtil.RegisterType<CustomInteract>(OnBuild);
        }

        private static bool hasInvalidListeners;

        private static bool OnBuild(CustomInteract interact)
        {
            if (interact.TryGetComponent<CustomPickup>(out _))
            {
                Debug.LogError($"[CustomInteractsAndPickups] CustomInteract and CustomPickup scripts must "
                    + $"not both be on the same object.", interact.gameObject);
                return false;
            }

            hasInvalidListeners = false;
            SerializedObject so = new SerializedObject(interact);
            var listenerTuples = EditorUtil.EnumerateArrayProperty(so.FindProperty("listeners"))
                .SelectMany(Selector)
                .ToList();
            if (hasInvalidListeners)
            {
                Debug.LogError($"[CustomInteractsAndPickups] Invalid CustomInteract listeners "
                    + $"for '{interact.name}', see above errors.", interact);
                return false;
            }

            EditorUtil.SetArrayProperty(
                so.FindProperty("actualListeners"),
                listenerTuples.Select(t => t.listener).ToList(),
                (p, v) => p.objectReferenceValue = v);

            EditorUtil.SetArrayProperty(
                so.FindProperty("listenerEventNames"),
                listenerTuples.Select(t => t.eventName).ToList(),
                (p, v) => p.stringValue = v);

            so.ApplyModifiedProperties();

            return true;
        }

        private static IEnumerable<(UdonSharpBehaviour listener, string eventName)> Selector(SerializedProperty property)
        {
            UdonSharpBehaviour listener = (UdonSharpBehaviour)property.objectReferenceValue;
            if (listener == null)
                yield break;
            bool hasAnEvent = false;
            MethodInfo method = EditorUtil.GetMethodIncludingBase(listener.GetType(), "Interact", BindingFlags.Instance | BindingFlags.Public, typeof(UdonSharpBehaviour));
            if (method.DeclaringType != typeof(UdonSharpBehaviour))
            {
                hasAnEvent = true;
                yield return (listener, "_interact");
            }
            method = EditorUtil.GetMethodIncludingBase(listener.GetType(), "OnInteractDown", BindingFlags.Instance | BindingFlags.Public, typeof(UdonSharpBehaviour));
            if (method != null)
            {
                hasAnEvent = true;
                yield return (listener, "OnInteractDown");
            }
            if (hasAnEvent)
                yield break;
            hasInvalidListeners = true;
            Debug.LogError($"[CustomInteractsAndPickups] Invalid CustomInteract listener: Missing methods "
                + $"Interact() and or OnInteractDown(). Must be public non static (aka instance) methods. "
                + $"Listener class name: '{listener.GetType().Name}'.", listener);
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(CustomInteract))]
    public class CustomInteractEditor : Editor
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
                "Interact",
                CustomInteractablesManagerAPI.InteractLayerName,
                targets.Cast<CustomInteract>().Select(i => i.transform));
            EditorGUILayout.Space();
            so.Update();
            DrawPropertiesExcluding(so, "m_Script");
            so.ApplyModifiedProperties();
        }
    }
}
