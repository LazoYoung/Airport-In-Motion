using UnityEngine;

namespace Layout
{
    public class Runway : Path
    {
        [SerializeField]
        private float width = 200f;

        protected override float GetWidth()
        {
            return width;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            tag = "Runway";
        }
    }
}
