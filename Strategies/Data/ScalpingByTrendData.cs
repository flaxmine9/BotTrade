namespace Strategies.Data
{
    public class ScalpingByTrendData
    {
        public string Symbol { get; set; }

        public int SMAFastPeriod { get; set; }
        public int SMASlowPeriod { get; set; }

        public int SuperTrendPeriod { get; set; }
        public decimal SuperTrendMultiplier { get; set; }

        public int LinearRegressionPeriod { get; set; }
        public decimal LinearRegressionSlopeValue { get; set; }

        public int RocPeriod { get; set; }
        public int RocSmoothPeriod { get; set; }
        public decimal RocValue { get; set; }

    }
}
