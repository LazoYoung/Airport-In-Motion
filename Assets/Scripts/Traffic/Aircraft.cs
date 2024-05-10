using Layout;
using UnityEngine;

namespace Traffic
{
    public class Aircraft : MonoBehaviour
    {
        [SerializeField] public float taxiSpeed = 10f;

        [SerializeField] public float turnRadius = 30f;

        [SerializeField] public float length = 50f;
        
        [HideInInspector] [SerializeField]
        private Pathfinder pathfinder;

        [HideInInspector] [SerializeField]
        private TaxiInstruction taxiInstruction;
        
        public void Taxi(string instruction)
        {
            if (!taxiInstruction.active)
            {
                taxiInstruction.Init(instruction);
            }
            else
            {
                taxiInstruction.Amend(instruction);
            }

            pathfinder.Taxi(this, taxiInstruction);
        }

        private void OnEnable()
        {
            tag = "Aircraft";
            taxiInstruction = new TaxiInstruction();
            pathfinder = gameObject.AddComponent<Pathfinder>();
            pathfinder.hideFlags = HideFlags.HideAndDontSave;
            pathfinder.follow = false;
        }
        
        private void OnDisable()
        {
            Destroy(pathfinder);
        }

        private void Update()
        {
            pathfinder.followSpeed = taxiSpeed;
        }
    }
}