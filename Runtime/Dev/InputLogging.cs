using UdonSharp;
using UnityEngine;
using VRC.Udon.Common;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class InputLogging : UdonSharpBehaviour
    {
        [HideInInspector][SerializeField][SingletonReference] private WidgetManager widgetManager;
        public GenericValueEditor valueEditor;
        private ButtonWidgetData clearButtonData;
        private GroupingWidgetData root;

        private void Start()
        {
            root = widgetManager.NewGrouping();
            clearButtonData = (ButtonWidgetData)widgetManager.NewButton("Clear")
                .SetListener(this, nameof(Clear));
            Clear();
            Redraw();
        }

        private void Redraw()
        {
            valueEditor.Draw(new WidgetData[] { root });
        }

        public void Clear()
        {
            root.ClearChildren();
            root.AddChild(clearButtonData);
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            root.AddChild(widgetManager.NewLabel($"InputUse, value: {value}, handType {(args.handType == HandType.RIGHT ? "right" : "left")}").StdMoveWidget());
            Redraw();
        }

        public override void InputGrab(bool value, UdonInputEventArgs args)
        {
            root.AddChild(widgetManager.NewLabel($"InputGrab, value: {value}, handType {(args.handType == HandType.RIGHT ? "right" : "left")}").StdMoveWidget());
            Redraw();
        }

        public override void InputDrop(bool value, UdonInputEventArgs args)
        {
            root.AddChild(widgetManager.NewLabel($"InputDrop, value: {value}, handType {(args.handType == HandType.RIGHT ? "right" : "left")}").StdMoveWidget());
            Redraw();
        }
    }
}
