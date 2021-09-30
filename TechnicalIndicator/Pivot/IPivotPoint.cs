namespace TechnicalIndicator.Pivot
{
    public interface IPivotPoint
    {
        public Pivot Calculate(decimal high, decimal low, decimal close);
    }
}
