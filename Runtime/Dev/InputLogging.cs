using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class InputLogging : UdonSharpBehaviour
    {
        public GenericValueEditor valueEditor;
        private ButtonWidgetData clearButtonData;
        private GroupingWidgetData root;

        private void Start()
        {
            root = valueEditor.NewGrouping();
            clearButtonData = (ButtonWidgetData)valueEditor.NewButton("Clear")
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
            root.AddChild((LabelWidgetData)valueEditor.NewLabel($"InputUse, value: {value}, handType {(args.handType == HandType.RIGHT ? "right" : "left")}").StdMove());
            Redraw();
        }

        public override void InputGrab(bool value, UdonInputEventArgs args)
        {
            root.AddChild((LabelWidgetData)valueEditor.NewLabel($"InputGrab, value: {value}, handType {(args.handType == HandType.RIGHT ? "right" : "left")}").StdMove());
            Redraw();
        }

        public override void InputDrop(bool value, UdonInputEventArgs args)
        {
            root.AddChild((LabelWidgetData)valueEditor.NewLabel($"InputDrop, value: {value}, handType {(args.handType == HandType.RIGHT ? "right" : "left")}").StdMove());
            Redraw();
        }
    }
}
