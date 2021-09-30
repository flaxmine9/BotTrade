namespace TechnicalIndicator.Pivot
{
    public abstract class Pivot
    {
        public abstract decimal CentralPivot { get; set; }
        public decimal TC { get; set; }
        public decimal BC { get; set; }

        public abstract PivotLevels Levels { get; set; }
    }
}
