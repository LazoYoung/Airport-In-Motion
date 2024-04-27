using System;
using System.Collections.Generic;
using Dreamteck.Splines;
using JetBrains.Annotations;
using Layout;
using UnityEngine;

namespace Traffic
{
    public class TaxiInstruction
    {
        public Taxiway[] Taxiways;
        public Runway[] CrossRunways;
        public Runway DepartRunway;
        public Path HoldingPoint;

        public TaxiInstruction(Taxiway[] taxiways)
        {
            Taxiways = taxiways;
        }

        public static Taxiway[] GetTaxiways(string taxiways)
        {
            var taxiwayList = new List<Taxiway>();

            foreach (string identifier in taxiways.Split(' '))
            {
                var taxiway = Taxiway.Get(identifier);

                if (taxiway == null)
                {
                    taxiwayList.Clear();
                    break;
                }

                taxiwayList.Add(taxiway);
            }

            return taxiwayList.ToArray();
        }
    }
}