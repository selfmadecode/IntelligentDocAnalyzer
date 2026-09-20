namespace IntelligentDocAnalyzer.Models;

// Per-category rollup shown in the spending breakdown and used to build insights.
public class CategoryBreakdown
{
    public string Category { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int TransactionCount { get; set; }
    public decimal PercentOfSpending { get; set; }
}

// Opening/closing/low-water-mark view of the running balance across the statement.
// Only populated when at least one transaction has a Balance value.
public class BalanceTrend
{
    public decimal StartingBalance { get; set; }
    public decimal EndingBalance { get; set; }
    public decimal LowestBalance { get; set; }
    public decimal HighestBalance { get; set; }
    public int DaysNegative { get; set; }
}
