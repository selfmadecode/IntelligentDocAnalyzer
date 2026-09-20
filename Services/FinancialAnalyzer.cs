using IntelligentDocAnalyzer.Models;

namespace IntelligentDocAnalyzer.Services;

public class FinancialAnalyzer
{
    // Keyword rules are checked in this order, BEFORE any amount-based guess.
    // (Fixes a bug in the original: a $1,500 mortgage payment used to get
    // miscategorized as DebtRepayment just because it was over $1,000 — the
    // amount check ran before the "mortgage" keyword ever got a look.)
    private static readonly string[] DebtRepaymentKeywords =
    {
        "loan payment", "student loan", "auto loan", "car loan",
        "credit card payment", "loan pmt", "installment", "debt payment"
    };

    private static readonly string[] SubscriptionKeywords =
    {
        "netflix", "spotify", "subscription", "hulu", "disney+",
        "apple.com/bill", "amazon prime", "youtube premium",
        "gym membership", "membership fee"
    };

    private static readonly string[] FixedExpenseKeywords =
    {
        "rent", "mortgage", "insurance", "utility", "utilities",
        "electric", "water bill", "gas bill", "internet", "phone bill",
        "hoa fee", "property tax"
    };

    private static readonly string[] TransferKeywords =
    {
        "transfer to", "transfer from", "zelle", "venmo", "wire transfer", "ach transfer"
    };

    private static readonly string[] FeeKeywords =
    {
        "overdraft fee", "service fee", "maintenance fee", "nsf fee",
        "atm fee", "monthly fee", "late fee"
    };

    public StatementAnalysisResult Analyze(IEnumerable<Transaction> transactions)
    {
        var txList = transactions?.ToList() ?? new List<Transaction>();

        foreach (var t in txList)
        {
            t.Category = Categorize(t);
        }

        var result = new StatementAnalysisResult { Transactions = txList };

        var inflow = txList.Where(t => t.Amount > 0).ToList();
        var outflow = txList.Where(t => t.Amount < 0).ToList();

        result.Summary.TotalInflow = inflow.Sum(t => t.Amount);
        result.Summary.TotalOutflow = outflow.Sum(t => Math.Abs(t.Amount));
        result.Summary.NetCashFlow = result.Summary.TotalInflow - result.Summary.TotalOutflow;
        result.Summary.SavingsRate = result.Summary.TotalInflow > 0
            ? result.Summary.NetCashFlow / result.Summary.TotalInflow
            : 0m;
        result.Summary.TransactionCount = txList.Count;
        result.Summary.AverageTransactionAmount = txList.Count > 0
            ? txList.Average(t => Math.Abs(t.Amount))
            : 0m;

        // Spending-only breakdown (Income excluded) so "top spending categories"
        // doesn't get crowded out by your paycheck.
        var spendingByCategory = outflow
            .GroupBy(t => t.Category)
            .Select(g => new CategoryBreakdown
            {
                Category = g.Key.ToString(),
                Total = g.Sum(t => Math.Abs(t.Amount)),
                TransactionCount = g.Count(),
                PercentOfSpending = result.Summary.TotalOutflow > 0
                    ? g.Sum(t => Math.Abs(t.Amount)) / result.Summary.TotalOutflow
                    : 0m
            })
            .OrderByDescending(c => c.Total)
            .ToList();

        result.Summary.SpendingBreakdown = spendingByCategory;
        result.Summary.TopSpendingCategories = spendingByCategory
            .Take(5)
            .Select(c => new KeyValuePair<string, decimal>(c.Category, c.Total))
            .ToList();

        result.Summary.LargestExpenses = outflow
            .OrderByDescending(t => Math.Abs(t.Amount))
            .Take(5)
            .ToList();
        result.Summary.LargestDeposits = inflow
            .OrderByDescending(t => t.Amount)
            .Take(5)
            .ToList();
        // Kept for any existing callers of the original combined list.
        result.Summary.LargestTransactions = txList
            .OrderByDescending(t => Math.Abs(t.Amount))
            .Take(5)
            .ToList();

        var recurring = DetectRecurring(outflow);
        result.Summary.DetectedRecurringSubscriptions = recurring
            .Select(r => r.AverageIntervalDays > 0
                ? $"{r.Merchant} (~{r.TypicalAmount:C}, every ~{r.AverageIntervalDays:0} days) x{r.Count}"
                : $"{r.Merchant} (~{r.TypicalAmount:C}) x{r.Count}")
            .ToList();

        if (txList.Any(t => t.Balance.HasValue))
        {
            result.Summary.BalanceTrend = BuildBalanceTrend(txList);
        }

        result.Summary.Insights = BuildInsights(result.Summary, spendingByCategory);

        return result;
    }

    private static TransactionCategory Categorize(Transaction t)
    {
        if (t.Amount > 0)
            return TransactionCategory.Income;

        if (t.Amount == 0)
            return TransactionCategory.Unknown;

        var desc = t.Description?.ToLowerInvariant() ?? string.Empty;
        var abs = Math.Abs(t.Amount);

        if (DebtRepaymentKeywords.Any(desc.Contains))
            return TransactionCategory.DebtRepayment;

        if (SubscriptionKeywords.Any(desc.Contains))
            return TransactionCategory.Subscription;

        if (FixedExpenseKeywords.Any(desc.Contains))
            return TransactionCategory.FixedExpense;

        if (TransferKeywords.Any(desc.Contains))
            return TransactionCategory.Transfer;

        if (FeeKeywords.Any(desc.Contains))
            return TransactionCategory.Fee;

        // No keyword matched — fall back to a coarse amount-based guess.
        if (abs > 1000)
            return TransactionCategory.DebtRepayment;

        return TransactionCategory.VariableExpense;
    }

    // Groups by merchant/description, then requires the amount to be within a small
    // tolerance (rather than an exact match) so a subscription that ticked up a dollar
    // still gets recognized. Also estimates the average gap between charges when dates
    // are available, since a ~30-day cadence is stronger recurrence evidence than count alone.
    private static List<RecurringCandidate> DetectRecurring(List<Transaction> outflow)
    {
        var candidates = new List<RecurringCandidate>();

        var groups = outflow.GroupBy(t => (t.Merchant ?? t.Description ?? string.Empty).ToLowerInvariant().Trim());

        foreach (var g in groups)
        {
            if (g.Count() < 2)
                continue;

            var amounts = g.Select(x => Math.Abs(x.Amount)).OrderBy(x => x).ToList();
            var median = amounts[amounts.Count / 2];
            var tolerance = Math.Max(2m, median * 0.1m);
            var withinTolerance = g.Where(x => Math.Abs(Math.Abs(x.Amount) - median) <= tolerance).ToList();

            if (withinTolerance.Count < 2)
                continue;

            double avgIntervalDays = 0;
            var dated = withinTolerance.Where(x => x.Date.HasValue).OrderBy(x => x.Date).ToList();
            if (dated.Count >= 2)
            {
                var gaps = new List<double>();
                for (int i = 1; i < dated.Count; i++)
                {
                    gaps.Add((dated[i].Date!.Value - dated[i - 1].Date!.Value).TotalDays);
                }
                avgIntervalDays = gaps.Average();
            }

            candidates.Add(new RecurringCandidate
            {
                Merchant = g.Key,
                Count = withinTolerance.Count,
                TypicalAmount = median,
                AverageIntervalDays = avgIntervalDays
            });
        }

        return candidates.OrderByDescending(c => c.Count).Take(10).ToList();
    }

    private static BalanceTrend BuildBalanceTrend(List<Transaction> txList)
    {
        var dated = txList.Where(t => t.Balance.HasValue).ToList();
        var ordered = dated.Any(t => t.Date.HasValue)
            ? dated.OrderBy(t => t.Date).ToList()
            : dated;

        return new BalanceTrend
        {
            StartingBalance = ordered.First().Balance!.Value,
            EndingBalance = ordered.Last().Balance!.Value,
            LowestBalance = ordered.Min(t => t.Balance!.Value),
            HighestBalance = ordered.Max(t => t.Balance!.Value),
            DaysNegative = ordered.Count(t => t.Balance!.Value < 0)
        };
    }

    // Turns the numbers above into a short list of plain-English observations.
    private static List<string> BuildInsights(AnalysisSummary summary, List<CategoryBreakdown> spendingByCategory)
    {
        var insights = new List<string>();

        if (summary.NetCashFlow < 0)
        {
            insights.Add($"You spent {Math.Abs(summary.NetCashFlow):C} more than you took in this period.");
        }
        else if (summary.TotalInflow > 0)
        {
            insights.Add($"You kept {summary.SavingsRate:P0} of what came in this period ({summary.NetCashFlow:C} net).");
        }

        var topCategory = spendingByCategory.FirstOrDefault();
        if (topCategory != null)
        {
            insights.Add($"'{topCategory.Category}' was your biggest spending category at {topCategory.Total:C} ({topCategory.PercentOfSpending:P0} of total spending).");
        }

        if (summary.DetectedRecurringSubscriptions.Any())
        {
            insights.Add($"Found {summary.DetectedRecurringSubscriptions.Count} likely recurring charge(s) — worth checking for anything you no longer use.");
        }

        if (summary.LargestExpenses.Any())
        {
            var biggest = summary.LargestExpenses.First();
            insights.Add($"Your single largest expense was {Math.Abs(biggest.Amount):C} ({biggest.Description}).");
        }

        if (summary.BalanceTrend != null)
        {
            var bt = summary.BalanceTrend;
            if (bt.DaysNegative > 0)
            {
                insights.Add($"Your balance went negative on {bt.DaysNegative} transaction(s) this period — lowest point was {bt.LowestBalance:C}.");
            }

            var change = bt.EndingBalance - bt.StartingBalance;
            insights.Add(change >= 0
                ? $"Your balance grew by {change:C} over the statement period."
                : $"Your balance dropped by {Math.Abs(change):C} over the statement period.");
        }

        return insights;
    }

    private class RecurringCandidate
    {
        public string Merchant { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TypicalAmount { get; set; }
        public double AverageIntervalDays { get; set; }
    }
}
