using Azure.AI.DocumentIntelligence;
using IntelligentDocAnalyzer.Helpers;
using IntelligentDocAnalyzer.Models;

namespace IntelligentDocAnalyzer.Services;

public class StatementProcessor
{
    private readonly DocumentIntelligenceService _docService;

    // Try the model built specifically for US bank statements first; fall back to
    // progressively more generic models if the resource/tier doesn't support it
    // or the call fails outright.
    private static readonly string[] ModelCandidates =
    {
        "prebuilt-bankStatement.us",
        "prebuilt-invoice",
        "prebuilt-layout"
    };

    public StatementProcessor(DocumentIntelligenceService docService)
    {
        _docService = docService;
    }

    public async Task<(List<Transaction> transactions, string modelUsed)> ProcessAsync(BinaryData content)
    {
        AnalyzeResult? result = null;
        string used = string.Empty;

        foreach (var model in ModelCandidates)
        {
            try
            {
                result = await _docService.AnalyzeDocumentAsync(model, content);
                used = model;
                break;
            }
            catch
            {
                // try next model
            }
        }

        if (result == null)
        {
            throw new InvalidOperationException("Unable to analyze document with available prebuilt models.");
        }

        var transactions = ExtractTransactions(result, used);
        return (transactions, used);
    }

    public async Task<(List<Transaction> transactions, string modelUsed)> ProcessFromUrlAsync(Uri fileUri)
    {
        AnalyzeResult? result = null;
        string used = string.Empty;

        foreach (var model in ModelCandidates)
        {
            try
            {
                result = await _docService.AnalyzeDocumentAsync(model, fileUri);
                used = model;
                break;
            }
            catch
            {
                // try next model
            }
        }

        if (result == null)
        {
            throw new InvalidOperationException("Unable to analyze document with available prebuilt models.");
        }

        var transactions = ExtractTransactions(result, used);
        return (transactions, used);
    }

    private static List<Transaction> ExtractTransactions(AnalyzeResult analyzeResult, string modelUsed)
    {
        if (modelUsed == "prebuilt-bankStatement.us")
        {
            var fromFields = ExtractFromBankStatementFields(analyzeResult);
            if (fromFields.Count > 0)
                return fromFields;
        }

        return ExtractTransactionsFromAnalyzeResult(analyzeResult);
    }

    // Reads the structured Accounts -> Transactions fields that prebuilt-bankStatement.us
    // returns, instead of guessing at raw table columns.
    private static List<Transaction> ExtractFromBankStatementFields(AnalyzeResult analyzeResult)
    {
        var txns = new List<Transaction>();

        var document = analyzeResult.Documents?.FirstOrDefault();
        if (document == null) return txns;

        if (!document.Fields.TryGetValue("Accounts", out var accountsField) ||
            accountsField.FieldType != DocumentFieldType.List)
        {
            return txns;
        }

        foreach (var accountField in accountsField.ValueList)
        {
            if (accountField.FieldType != DocumentFieldType.Dictionary)
                continue;

            var account = accountField.ValueDictionary;

            if (!account.TryGetValue("Transactions", out var transactionsField) ||
                transactionsField.FieldType != DocumentFieldType.List)
            {
                continue;
            }

            foreach (var txnField in transactionsField.ValueList)
            {
                if (txnField.FieldType != DocumentFieldType.Dictionary)
                    continue;

                var fields = txnField.ValueDictionary;

                DateTime? date = fields.TryGetValue("Date", out var dateField) &&
                                  dateField.FieldType == DocumentFieldType.Date
                    ? dateField.ValueDate?.DateTime
                    : null;

                string? description = fields.TryGetValue("Description", out var descField) &&
                                       descField.FieldType == DocumentFieldType.String
                    ? descField.ValueString
                    : null;

                double? deposit = fields.TryGetValue("DepositAmount", out var depField) &&
                                   depField.FieldType == DocumentFieldType.Double
                    ? depField.ValueDouble
                    : null;

                double? withdrawal = fields.TryGetValue("WithdrawalAmount", out var wdField) &&
                                      wdField.FieldType == DocumentFieldType.Double
                    ? wdField.ValueDouble
                    : null;

                var txn = new Transaction
                {
                    Date = date,
                    Description = description,
                    Merchant = description,
                    // Withdrawals come back as a positive WithdrawalAmount; store them as
                    // negative so a single Amount column still nets deposits vs. spend.
                    Amount = deposit.HasValue ? (decimal)deposit.Value
                           : withdrawal.HasValue ? -(decimal)withdrawal.Value
                           : 0m
                };

                if (txn.Amount != 0 || txn.Date.HasValue || !string.IsNullOrEmpty(txn.Description))
                {
                    txns.Add(txn);
                }
            }
        }

        return txns;
    }

    // Fallback path for prebuilt-invoice / prebuilt-layout results, which don't have a
    // bank-statement-specific schema and have to be parsed from raw tables/lines instead.
    private static List<Transaction> ExtractTransactionsFromAnalyzeResult(AnalyzeResult analyzeResult)
    {
        var txns = new List<Transaction>();

        if (analyzeResult.Tables != null && analyzeResult.Tables.Count > 0)
        {
            foreach (var table in analyzeResult.Tables)
            {
                if (table.RowCount == 0)
                    continue;

                var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < table.ColumnCount; c++)
                {
                    var cell = table.Cells.FirstOrDefault(x => x.RowIndex == 0 && x.ColumnIndex == c);

                    var txt = cell?.Content?.Trim() ?? string.Empty;

                    if (string.IsNullOrEmpty(txt))
                        continue;

                    headers[txt.NormalizeHeader()] = c;
                }

                for (int r = 1; r < table.RowCount; r++)
                {
                    var txn = new Transaction();

                    if (headers.TryGetValue("date", out var dateIdx))
                    {
                        var cell = table.Cells.FirstOrDefault(x => x.RowIndex == r && x.ColumnIndex == dateIdx);
                        txn.Date = cell?.Content.TryParseDate();
                    }

                    if (headers.TryGetValue("description", out var descIdx))
                    {
                        var cell = table.Cells.FirstOrDefault(x => x.RowIndex == r && x.ColumnIndex == descIdx);
                        txn.Description = cell?.Content?.Trim();
                        txn.Merchant = txn.Description;
                    }

                    if (headers.TryGetValue("amount", out var amtIdx))
                    {
                        var cell = table.Cells.FirstOrDefault(x => x.RowIndex == r && x.ColumnIndex == amtIdx);
                        txn.Amount = cell?.Content.TryParseDecimal() ?? 0m;
                    }

                    if (headers.TryGetValue("balance", out var balIdx))
                    {
                        var cell = table.Cells.FirstOrDefault(x => x.RowIndex == r && x.ColumnIndex == balIdx);
                        txn.Balance = cell?.Content.TryParseNullableDecimal();
                    }

                    if (txn.Amount == 0)
                    {
                        for (int c = table.ColumnCount - 1; c >= 0; c--)
                        {
                            var cell = table.Cells.FirstOrDefault(x => x.RowIndex == r && x.ColumnIndex == c);
                            var val = cell?.Content.TryParseNullableDecimal();
                            if (val.HasValue)
                            {
                                txn.Amount = val.Value;
                                break;
                            }
                        }
                    }

                    if (txn.Amount != 0 || txn.Date.HasValue || !string.IsNullOrEmpty(txn.Description))
                    {
                        txns.Add(txn);
                    }
                }
            }
        }

        if (!txns.Any())
        {
            if (analyzeResult.Pages != null)
            {
                foreach (var page in analyzeResult.Pages)
                {
                    var lines = page.Lines;
                    foreach (var line in lines)
                    {
                        var text = line.Content?.Trim();
                        if (string.IsNullOrEmpty(text))
                            continue;

                        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        DateTime? date = null;
                        
                        if (parts.Length > 0) date = parts[0].TryParseDate();
                        
                        decimal amt = 0;
                        if (parts.Length > 0) amt = parts[^1].TryParseDecimal();

                        if (amt != 0 || date.HasValue)
                        {
                            txns.Add(new Transaction { Date = date, Description = text, Merchant = text, Amount = amt });
                        }
                    }
                }
            }
        }

        return txns;
    }    
}
