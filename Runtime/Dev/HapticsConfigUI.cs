using JanSharp.Internal;
using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HapticsConfigUI : UdonSharpBehaviour
    {
        [HideInInspector][SerializeField][SingletonReference] private WidgetManager widgets;
        [HideInInspector][SingletonReference] public CustomInteractablesManager manager;
        public GenericValueEditor editor;

        private void Start()
        {
            editor.Draw(widgets.StdMoveWidgets(new WidgetData[]
            {
                NewHapticsEditor(nameof(manager.onSelectionGainedHaptics)),
                NewHapticsEditor(nameof(manager.onSelectionLostHaptics)),
                NewHapticsEditor(nameof(manager.onSelectionChangedHaptics)),
                NewHapticsEditor(nameof(manager.onPickupHaptics)),
                NewHapticsEditor(nameof(manager.onDropHaptics)),
                // NewHapticsEditor(nameof(manager.onInteractHaptics)),
                // NewHapticsEditor(nameof(manager.onUseHaptics)),
            }));
        }

        private WidgetData NewHapticsEditor(string variableName)
        {
            Vector3 hapticsConfig = (Vector3)manager.GetProgramVariable(variableName);
            return widgets.NewFoldOutScope(variableName, foldedOut: true).SetChildrenChained(widgets.StdMoveWidgets(new WidgetData[]
            {
                widgets.NewSliderField("duration", hapticsConfig.x, 0f, 1f)
                    .SetCustomData(nameof(HapticsConfigUI.variableName), variableName)
                    .SetListener(this, nameof(OnDurationChanged)),
                widgets.NewSliderField("amplitude", hapticsConfig.y, 0f, 1f)
                    .SetCustomData(nameof(HapticsConfigUI.variableName), variableName)
                    .SetListener(this, nameof(OnAmplitudeChanged)),
                widgets.NewSliderField("frequency", hapticsConfig.z, 0f, 1f)
                    .SetCustomData(nameof(HapticsConfigUI.variableName), variableName)
                    .SetListener(this, nameof(OnFrequencyChanged)),
            }));
        }

        public void OnDurationChanged() => OnConfigChanged(0);
        public void OnAmplitudeChanged() => OnConfigChanged(1);
        public void OnFrequencyChanged() => OnConfigChanged(2);

        [System.NonSerialized] public string variableName;

        private void OnConfigChanged(int vectorIndex)
        {
            Vector3 hapticsConfig = (Vector3)manager.GetProgramVariable(variableName);
            hapticsConfig[vectorIndex] = editor.GetSendingSliderField().Value;
            manager.SetProgramVariable(variableName, hapticsConfig);
        }
    }
}
