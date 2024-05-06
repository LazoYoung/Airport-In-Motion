using System.Collections.Generic;
using Dreamteck.Splines;
using Traffic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Misc
{
    [CustomEditor(typeof(Aircraft))]
    public class AircraftEditor : Editor
    {

        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            
            var root = GetRootElement();
            var box = new Box()
            {
                style =
                {
                    marginTop = 10f,
                    paddingTop = 10f,
                    paddingBottom = 10f,
                    paddingLeft = 10f,
                    paddingRight = 10f
                }
            };
            var label = new Label("Actions")
            {
                style =
                {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    paddingBottom = 20f
                }
            };
            var taxi = GetTaxiAction();
            box.Add(label);
            box.Add(taxi);
            root.Add(box);
            return root;
        }

        private VisualElement GetTaxiAction()
        {
            var action = new VisualElement()
            {
                style = { flexDirection = FlexDirection.Row }
            };
            var textField = new TextField()
            {
                style = { flexGrow = 1f, marginRight = 20f },
            };
            var button = new Button()
            {
                text = "Taxi",
                style = { width = 100f }
            };
            var aircraft = target as Aircraft;
            
            textField.RegisterValueChangedCallback(e =>
            {
                var rawValue = e.newValue.ToUpper();
                var value = TaxiInstruction.tokenRegex.Replace(rawValue, eval => eval.Value.ToLower());
                textField.SetValueWithoutNotify(value);
            });
            button.clicked += () =>
            {
                aircraft!.Taxi(textField.value);
                textField.value = "";
            };
            action.Add(textField);
            action.Add(button);
            action.SetEnabled(EditorApplication.isPlaying);
            return action;
        }

        private VisualElement GetRootElement()
        {
            var root = new VisualElement();
            var iter = serializedObject.GetIterator();

            for (var enterChildren = true; iter.NextVisible(enterChildren); enterChildren = false)
            {
                var propertyField = new PropertyField(iter);
                propertyField.Bind(serializedObject);
                root.Add(propertyField);
            }

            return root;
        }

        private DropdownField GetTaxiwayDropdown(SplineComputer[] taxiways)
        {
            var choices = new List<string>(taxiways.Length);
            foreach (var spline in taxiways)
            {
                choices.Add(spline.name);
            }
            
            return new DropdownField(choices, 0)
            {
                tooltip = "Select a taxiway.",
                style =
                {
                    marginRight = 20f,
                    width = 60f
                }
            };
        }
    }
}
