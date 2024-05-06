using Dreamteck.Splines;
using UnityEngine;

namespace Layout
{
    [RequireComponent(typeof(SplineComputer))]
    public class Taxiway : Path
    {
        [SerializeField]
        private float width = 50f;

        protected override float GetWidth()
        {
            return width;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            tag = "Taxiway";
        }
    }
}
