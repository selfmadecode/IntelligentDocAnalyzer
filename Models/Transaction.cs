namespace IntelligentDocAnalyzer.Models;

public enum TransactionCategory
{
    Unknown,
    Income,
    FixedExpense,
    VariableExpense,
    DebtRepayment,
    Transfer,
    Subscription,
    Fee
}

public class Transaction
{
    public DateTime? Date { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public decimal? Balance { get; set; }
    public string? Merchant { get; set; }
    public TransactionCategory Category { get; set; } = TransactionCategory.Unknown;
}
