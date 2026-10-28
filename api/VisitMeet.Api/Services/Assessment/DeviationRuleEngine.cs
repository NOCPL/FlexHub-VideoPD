using System.Globalization;
using System.Text.Json;
using VisitMeet.Api.Models;

namespace VisitMeet.Api.Services.Assessment;

public class DeviationRuleEngine
{
    public IReadOnlyList<(DeviationRule Rule, bool Fired, List<RuleEvidenceItem> Evidence)> EvaluateBureau(
        IEnumerable<DeviationRule> rules,
        NormalizedBureau bureau,
        Applicant applicant)
    {
        var results = new List<(DeviationRule, bool, List<RuleEvidenceItem>)>();
        foreach (var rule in rules.Where(r => r.Enabled && r.CheckKey.StartsWith("credit-report.", StringComparison.Ordinal)))
        {
            var parameters = Parse(rule.ParametersJson);
            var (fired, evidence) = rule.CheckKey switch
            {
                "credit-report.write-off-settled" => WriteOffSettled(bureau, parameters),
                "credit-report.score-floor" => ScoreFloor(bureau, parameters),
                "credit-report.recent-inquiries" => RecentInquiries(bureau, parameters),
                "credit-report.pan-consistency" => PanConsistency(bureau, applicant),
                "credit-report.phone-consistency" => PhoneConsistency(bureau, applicant),
                "credit-report.dob-consistency" => DobConsistency(bureau, applicant),
                _ => (false, new List<RuleEvidenceItem>())
            };
            results.Add((rule, fired, evidence));
        }
        return results;
    }

    public IReadOnlyList<(DeviationRule Rule, bool Fired, List<RuleEvidenceItem> Evidence)> EvaluateBank(
        IEnumerable<DeviationRule> rules,
        NormalizedBank bank,
        decimal? loanAmount)
    {
        var results = new List<(DeviationRule, bool, List<RuleEvidenceItem>)>();
        foreach (var rule in rules.Where(r => r.Enabled && r.CheckKey.StartsWith("bank-statement.", StringComparison.Ordinal)))
        {
            var parameters = Parse(rule.ParametersJson);
            var (fired, evidence) = rule.CheckKey switch
            {
                "bank-statement.risk-signals" => RiskSignals(bank, parameters),
                "bank-statement.repayment-account-activity" => RepaymentActivity(bank, parameters),
                "bank-statement.restricted-debit-modes" => RestrictedDebits(bank, parameters),
                "bank-statement.high-value-transactions" => HighValue(bank, parameters, loanAmount),
                "bank-statement.active-account-vintage" => AccountVintage(bank, parameters),
                _ => (false, new List<RuleEvidenceItem>())
            };
            results.Add((rule, fired, evidence));
        }
        return results;
    }

    private static (bool, List<RuleEvidenceItem>) WriteOffSettled(NormalizedBureau bureau, JsonElement p)
    {
        var amountGte = Num(p, "amountGte") ?? 0;
        var maxAge = (int)(Num(p, "maxAgeMonths") ?? 12);
        var cutoff = DateTimeOffset.UtcNow.AddMonths(-maxAge);
        var hits = bureau.Accounts.Where(a =>
            IsWriteOffOrSettled(a.Status) &&
            a.Amount >= amountGte &&
            (a.ReportedAt is null || a.ReportedAt >= cutoff)).ToList();
        return (hits.Count > 0, hits.Select(a => AccountEvidence("Written-off or settled account", a)).ToList());
    }

    private static (bool, List<RuleEvidenceItem>) ScoreFloor(NormalizedBureau bureau, JsonElement p)
    {
        var min = (int)(Num(p, "minScore") ?? 650);
        if (bureau.Score is null) return (false, []);
        if (bureau.Score >= min) return (false, []);
        return (true, [new RuleEvidenceItem
        {
            Kind = "score",
            Summary = $"Bureau score {bureau.Score} is below the floor of {min}.",
            Fields = { ["score"] = bureau.Score.ToString(), ["minScore"] = min.ToString() }
        }]);
    }

    private static (bool, List<RuleEvidenceItem>) RecentInquiries(NormalizedBureau bureau, JsonElement p)
    {
        var maxCount = (int)(Num(p, "maxCount") ?? 3);
        var maxAge = (int)(Num(p, "maxAgeMonths") ?? 6);
        var cutoff = DateTimeOffset.UtcNow.AddMonths(-maxAge);
        var hits = bureau.Enquiries.Where(e => e.Date is null || e.Date >= cutoff).ToList();
        if (hits.Count <= maxCount) return (false, []);
        return (true, hits.Select(e => new RuleEvidenceItem
        {
            Kind = "enquiry",
            Summary = $"Enquiry at {e.Institution ?? "unknown lender"} on {e.Date:yyyy-MM-dd}.",
            Fields =
            {
                ["institution"] = e.Institution,
                ["date"] = e.Date?.ToString("yyyy-MM-dd"),
                ["amount"] = e.Amount?.ToString(CultureInfo.InvariantCulture)
            }
        }).ToList());
    }

    private static (bool, List<RuleEvidenceItem>) PanConsistency(NormalizedBureau bureau, Applicant applicant)
    {
        var declared = NormId(applicant.Pan);
        var distinct = bureau.Pans.Select(NormId).Where(x => x.Length > 0).Distinct().ToList();
        if (distinct.Count == 0) return (false, []);
        var mismatch = distinct.Count > 1 || (declared.Length > 0 && distinct.Any(p => p != declared));
        if (!mismatch) return (false, []);
        return (true, [new RuleEvidenceItem
        {
            Kind = "identity",
            Summary = "PAN on the bureau does not match the application, or multiple PANs are present.",
            Fields =
            {
                ["declared"] = applicant.Pan,
                ["bureau"] = string.Join(", ", bureau.Pans)
            }
        }]);
    }

    private static (bool, List<RuleEvidenceItem>) PhoneConsistency(NormalizedBureau bureau, Applicant applicant)
    {
        var declared = Digits(applicant.Phone);
        var distinct = bureau.Phones.Select(Digits).Where(x => x.Length >= 8).Distinct().ToList();
        if (distinct.Count == 0) return (false, []);
        var mismatch = distinct.Count > 1 || (declared.Length >= 8 && distinct.All(p => !p.EndsWith(declared[^Math.Min(10, declared.Length)..])));
        if (!mismatch && distinct.Count == 1) return (false, []);
        if (!mismatch) return (false, []);
        return (true, [new RuleEvidenceItem
        {
            Kind = "identity",
            Summary = "Phone numbers on the bureau vary, or do not match the application.",
            Fields =
            {
                ["declared"] = applicant.Phone,
                ["bureau"] = string.Join(", ", bureau.Phones)
            }
        }]);
    }

    private static (bool, List<RuleEvidenceItem>) DobConsistency(NormalizedBureau bureau, Applicant applicant)
    {
        var declared = NormId(applicant.DateOfBirth);
        var distinct = bureau.Dobs.Select(NormId).Where(x => x.Length > 0).Distinct().ToList();
        if (distinct.Count == 0) return (false, []);
        var mismatch = distinct.Count > 1 || (declared.Length > 0 && distinct.Any(d => d != declared && !declared.Contains(d) && !d.Contains(declared)));
        if (!mismatch) return (false, []);
        return (true, [new RuleEvidenceItem
        {
            Kind = "identity",
            Summary = "Date of birth on the bureau varies, or does not match the application.",
            Fields =
            {
                ["declared"] = applicant.DateOfBirth,
                ["bureau"] = string.Join(", ", bureau.Dobs)
            }
        }]);
    }

    private static (bool, List<RuleEvidenceItem>) RiskSignals(NormalizedBank bank, JsonElement p)
    {
        var categories = Strings(p, "categories");
        if (categories.Count == 0) categories = ["gambling", "crypto", "money-mule"];
        var minAmount = Num(p, "minAmount") ?? 1;
        var hits = bank.Transactions.Where(t =>
            t.Amount >= minAmount &&
            categories.Any(c =>
                Contains(t.Risk, c) || Contains(t.Narration, c) || Contains(t.Mode, c))).ToList();
        return (hits.Count > 0, hits.Select(t => TxnEvidence(t)).ToList());
    }

    private static (bool, List<RuleEvidenceItem>) RepaymentActivity(NormalizedBank bank, JsonElement p)
    {
        var minCredits = (int)(Num(p, "minCredits") ?? 1);
        var maxAge = (int)(Num(p, "maxAgeMonths") ?? 3);
        var keywords = Strings(p, "keywords");
        if (keywords.Count == 0) keywords = ["emi", "loan", "repay", "nbfc", "instal"];
        var cutoff = DateTimeOffset.UtcNow.AddMonths(-maxAge);
        var hits = bank.Transactions.Where(t =>
            t.Date >= cutoff &&
            t.Direction.Contains("debit", StringComparison.OrdinalIgnoreCase) &&
            keywords.Any(k => Contains(t.Narration, k))).ToList();
        if (hits.Count >= minCredits)
        {
            return (true, hits.Select(t => TxnEvidence(t, "Loan repayment on statement")).ToList());
        }
        return (false, []);
    }

    private static (bool, List<RuleEvidenceItem>) RestrictedDebits(NormalizedBank bank, JsonElement p)
    {
        var modes = Strings(p, "modes");
        if (modes.Count == 0) modes = ["cash", "crypto", "atm"];
        var hits = bank.Transactions.Where(t =>
            t.Direction.Contains("debit", StringComparison.OrdinalIgnoreCase) &&
            modes.Any(m => Contains(t.Mode, m) || Contains(t.Narration, m))).ToList();
        return (hits.Count > 0, hits.Select(t => TxnEvidence(t, "Restricted debit mode")).ToList());
    }

    private static (bool, List<RuleEvidenceItem>) HighValue(NormalizedBank bank, JsonElement p, decimal? loanAmount)
    {
        var threshold = ThresholdForLoan(p, loanAmount ?? bank.LoanAmount);
        if (threshold is null) return (false, []);
        var hits = bank.Transactions.Where(t => t.Amount >= threshold.Value).ToList();
        return (hits.Count > 0, hits.Select(t =>
        {
            var item = TxnEvidence(t, $"High-value transaction (≥ {threshold:0})");
            item.Fields["threshold"] = threshold.Value.ToString(CultureInfo.InvariantCulture);
            return item;
        }).ToList());
    }

    private static (bool, List<RuleEvidenceItem>) AccountVintage(NormalizedBank bank, JsonElement p)
    {
        var minAge = (int)(Num(p, "minAgeMonths") ?? 6);
        var cutoff = DateTimeOffset.UtcNow.AddMonths(-minAge);
        var young = bank.Accounts.Where(a => a.OpenedAt is DateTimeOffset opened && opened > cutoff).ToList();
        if (young.Count == 0) return (false, []);
        return (true, young.Select(a => new RuleEvidenceItem
        {
            Kind = "account",
            Summary = $"Account {a.AccountNumber ?? "unknown"} opened {a.OpenedAt:yyyy-MM-dd}, younger than {minAge} months.",
            Fields =
            {
                ["accountNumber"] = a.AccountNumber,
                ["openedAt"] = a.OpenedAt?.ToString("yyyy-MM-dd"),
                ["type"] = a.Type
            }
        }).ToList());
    }

    private static decimal? ThresholdForLoan(JsonElement p, decimal? loanAmount)
    {
        if (loanAmount is null) return Num(p, "txnAmountGte");
        if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("bands", out var bands) &&
            bands.ValueKind == JsonValueKind.Array)
        {
            decimal? best = null;
            decimal bestLoan = -1;
            foreach (var band in bands.EnumerateArray())
            {
                var loanGte = Num(band, "loanAmountGte") ?? 0;
                var txnGte = Num(band, "txnAmountGte");
                if (txnGte is null) continue;
                if (loanAmount >= loanGte && loanGte >= bestLoan)
                {
                    bestLoan = loanGte;
                    best = txnGte;
                }
            }
            return best;
        }
        return Num(p, "txnAmountGte");
    }

    private static bool IsWriteOffOrSettled(string status)
    {
        var s = status.ToLowerInvariant();
        return s.Contains("write") || s.Contains("written") || s.Contains("settled") || s.Contains("woff");
    }

    private static RuleEvidenceItem AccountEvidence(string summary, BureauAccount account) => new()
    {
        Kind = "account",
        Summary = $"{summary}: {account.Institution ?? "lender"} {account.AccountNumber ?? ""} ({account.Status}, {account.Amount:0}).",
        Fields =
        {
            ["accountNumber"] = account.AccountNumber,
            ["institution"] = account.Institution,
            ["status"] = account.Status,
            ["amount"] = account.Amount.ToString(CultureInfo.InvariantCulture),
            ["reportedAt"] = account.ReportedAt?.ToString("yyyy-MM-dd")
        }
    };

    private static RuleEvidenceItem TxnEvidence(BankTransaction txn, string? summary = null) => new()
    {
        Kind = "transaction",
        Summary = summary ?? $"{txn.Direction} {txn.Amount:0} via {txn.Mode ?? "unknown"} on {txn.Date:yyyy-MM-dd}",
        Fields =
        {
            ["date"] = txn.Date.ToString("yyyy-MM-dd"),
            ["amount"] = txn.Amount.ToString(CultureInfo.InvariantCulture),
            ["direction"] = txn.Direction,
            ["mode"] = txn.Mode,
            ["narration"] = txn.Narration,
            ["risk"] = txn.Risk
        }
    };

    private static JsonElement Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        }
        catch (JsonException)
        {
            return JsonSerializer.Deserialize<JsonElement>("{}");
        }
    }

    private static decimal? Num(JsonElement p, string name)
    {
        if (p.ValueKind != JsonValueKind.Object || !p.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var d)) return d;
        if (value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    private static List<string> Strings(JsonElement p, string name)
    {
        var list = new List<string>();
        if (p.ValueKind != JsonValueKind.Object || !p.TryGetProperty(name, out var value)) return list;
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    list.Add(item.GetString()!);
            }
        }
        return list;
    }

    private static bool Contains(string? haystack, string needle) =>
        !string.IsNullOrWhiteSpace(haystack) &&
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static string NormId(string? value) =>
        (value ?? "").Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");

    private static string Digits(string? value) =>
        new string((value ?? "").Where(char.IsDigit).ToArray());
}
