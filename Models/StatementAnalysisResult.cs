namespace IntelligentDocAnalyzer.Models;

public class AnalysisSummary
{
    public decimal TotalInflow { get; set; }
    public decimal TotalOutflow { get; set; }
    public decimal NetCashFlow { get; set; }
    public List<KeyValuePair<string, decimal>> TopSpendingCategories { get; set; } = new();
    public List<Transaction> LargestTransactions { get; set; } = new();
    public List<string> DetectedRecurringSubscriptions { get; set; } = new();
    public decimal SavingsRate { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageTransactionAmount { get; set; }
    public List<CategoryBreakdown> SpendingBreakdown { get; set; } = new();
    public List<Transaction> LargestExpenses { get; set; } = new();
    public List<Transaction> LargestDeposits { get; set; } = new();
    public BalanceTrend? BalanceTrend { get; set; }
    public List<string> Insights { get; set; } = new();
}

public class StatementAnalysisResult
{
    public List<Transaction> Transactions { get; set; } = new();
    public AnalysisSummary Summary { get; set; } = new();
    public string? RawModelUsed { get; set; }
}
