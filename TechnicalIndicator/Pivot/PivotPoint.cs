using TechnicalIndicator.Models;

namespace TechnicalIndicator.Pivot
{
    public class PivotPoint
    {
        private IPivotPoint _pivotPoint { get; set; }

        /// <summary>
        /// 1) Traditional Pivot
        /// 2) DeMark Pivot
        /// 3) Pivot Camarilla
        /// </summary>
        /// <param name="pivotPoint">1)Traditional 2) DeMark 3) Camarilla</param>
        public PivotPoint(IPivotPoint pivotPoint)
        {
            _pivotPoint = pivotPoint;
        }

        public Pivot GetPivotPoint(Kline price)
        {
            return _pivotPoint.Calculate(price.High, price.Low, price.Close);
        }
    }
}
