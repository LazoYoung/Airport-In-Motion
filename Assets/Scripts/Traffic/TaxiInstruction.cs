using System.Collections.Generic;
using System.Text.RegularExpressions;
using Layout;
using UnityEngine;

namespace Traffic
{
    public class TaxiInstruction
    {
        public readonly Taxiway[] Taxiways;
        public readonly Runway[] CrossRunways;
        public readonly Runway DepartRunway;
        public readonly Path HoldingPoint;
        
        // Expected format: [(RWY) via] {(TWY...)} [.(TWY/RWY)] [x(RWY)]
        // Example 1: 01R via B M .01L
        public TaxiInstruction(string instruction)
        {
            var taxiwayList = new List<Taxiway>();
            var regex = new Regex("( via )", RegexOptions.IgnoreCase);
            string taxiInstruction = instruction;
            
            if (regex.IsMatch(instruction))
            {
                string identifier = regex.Split(instruction)[0];
                DepartRunway = Runway.Get(identifier);
                taxiInstruction = instruction.Substring(identifier.Length + 5);

                Debug.Log(taxiInstruction);
            }
            
            // todo: runway hold, cross instructions
            
            foreach (string identifier in taxiInstruction.Split(' '))
            {
                var taxiway = Taxiway.Get(identifier);

                if (taxiway == null)
                {
                    taxiwayList.Clear();
                    break;
                }

                taxiwayList.Add(taxiway);
            }

            Taxiways = taxiwayList.ToArray();
        }
    }
}
