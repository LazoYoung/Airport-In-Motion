using System.Collections.Generic;
using System.Text.RegularExpressions;
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
            var taxiways = FindTaxiways();
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
            var joinTwy = GetJoinTaxiwayAction(taxiways);
            var taxi = GetTaxiAction();
            box.Add(label);
            box.Add(joinTwy);
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
                string rawValue = e.newValue.ToUpper();
                var regex = new Regex("(via)|(short)|(cross)", RegexOptions.IgnoreCase);
                string value = regex.Replace(rawValue, eval => eval.Value.ToLower());
                textField.SetValueWithoutNotify(value);
            });
            button.clicked += () =>
            {
                aircraft!.Taxi(new TaxiInstruction(textField.text));
            };
            action.Add(textField);
            action.Add(button);
            action.SetEnabled(EditorApplication.isPlaying);
            return action;
        }

        private VisualElement GetJoinTaxiwayAction(SplineComputer[] taxiways)
        {
            var action = new VisualElement()
            {
                style = { flexDirection = FlexDirection.Row }
            };
            var button = new Button
            {
                text = "Join Taxiway",
                style =
                {
                    flexGrow = 1f
                }
            };
            var dropdown = GetTaxiwayDropdown(taxiways);
            var aircraft = target as Aircraft;
            
            button.clicked += () =>
            {
                var taxiway = taxiways[dropdown.index];
                var direction = Spline.Direction.Forward;
                aircraft!.JoinTaxiway(taxiway, direction);
            };
            action.Add(dropdown);
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

        private SplineComputer[] FindTaxiways()
        {
            var splines = FindObjectsOfType<SplineComputer>();
            var result = new List<SplineComputer>();
            
            foreach (var spline in splines)
            {
                if (!spline.CompareTag("Aircraft"))
                {
                    result.Add(spline);
                }
            }

            return result.ToArray();
        }
    }
}
