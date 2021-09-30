using System.Collections.Generic;

namespace TechnicalIndicator.Pivot.PivotTypes
{
    public class Traditional : Pivot, IPivotPoint
    {
        public override decimal CentralPivot { get; set; }
        public override PivotLevels Levels { get; set; }

        public Pivot Calculate(decimal high, decimal low, decimal close)
        {
            decimal pivot = (high + low + close) / 3.0m;
            decimal bc = (high + low) / 2.0m;

            return new Traditional()
            {
                CentralPivot = pivot,
                BC = bc,
                TC = (pivot - bc) + pivot,

                Levels = new PivotLevels()
                {
                    Supports = new List<decimal>() { (pivot * 2) - high, pivot - (high - low), low - 2 * (high - pivot) },
                    Resistances = new List<decimal>() { (pivot * 2) - low, pivot + (high - low), high + 2 * (pivot - low) }
                }
            };
        }
    }
}
