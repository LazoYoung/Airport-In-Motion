using System.Collections.Generic;
using Dreamteck.Splines;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor
{
    [CustomEditor(typeof(Aircraft))]
    public class JoinTaxiwayButton : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
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
            var actions = new VisualElement()
            {
                style = { flexDirection = FlexDirection.Row }
            };
            var dropdown = GetTaxiwayDropdown(taxiways);
            var button = GetJoinTaxiwayButton();
            var aircraft = target as Aircraft;
            
            button.clicked += () =>
            {
                if (!Application.isPlaying)
                {
                    Debug.LogWarning("Game is not running!");
                    return;
                }
                
                var taxiway = taxiways[dropdown.index];
                var direction = Spline.Direction.Forward;
                aircraft!.JoinTaxiway(taxiway, direction);
            };
            actions.Add(dropdown);
            actions.Add(button);
            box.Add(label);
            box.Add(actions);
            root.Add(box);
            return root;
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

        private Button GetJoinTaxiwayButton()
        {
            return new Button
            {
                text = "Join Taxiway",
                style =
                {
                    flexGrow = 1f
                }
            };
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