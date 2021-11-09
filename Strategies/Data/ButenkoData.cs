namespace Strategies.Data
{
    public class ButenkoData
    {
        public string Symbol { get; set; }
        public int EmaFast { get; set; }
        public int EmaSLow { get; set; }
        public decimal AtrMult { get; set; }
        public int AtrPeriod { get; set; }
        public int MacdSlowPeriod { get; set; }
        public int MacdFastPeriod { get; set; }
    }
}
