using System.Globalization;
using System.Text.Json;

namespace VisitMeet.Api.Services.Assessment;

public static class InputAdapters
{
    public static NormalizedBureau ToBureau(JsonElement doc)
    {
        var root = Unwrap(doc);
        var consumer = Obj(root, "consumer", "Customer", "personalDetails", "PersonalDetails") ?? root;
        var bureau = new NormalizedBureau
        {
            Provider = Str(root, "provider") ?? "equifax",
            Name = Str(consumer, "name", "Name", "fullName", "FullName"),
            Score = Int(consumer, "score", "Score", "creditScore") ?? Int(root, "score", "Score")
        };

        bureau.Pans.AddRange(Strings(consumer, "pans", "Pans", "PANs"));
        AddIf(bureau.Pans, Str(consumer, "pan", "PAN", "Pan", "PermanentAccountNumber"));
        bureau.Phones.AddRange(Strings(consumer, "phones", "Phones", "mobileNumbers"));
        AddIf(bureau.Phones, Str(consumer, "phone", "Phone", "mobile", "Mobile"));
        bureau.Dobs.AddRange(Strings(consumer, "dobs", "Dobs", "datesOfBirth"));
        AddIf(bureau.Dobs, Str(consumer, "dob", "DOB", "dateOfBirth", "DateOfBirth"));

        foreach (var item in Arr(root, "accounts", "Accounts", "tradelines", "Tradelines"))
        {
            bureau.Accounts.Add(new BureauAccount
            {
                AccountNumber = Str(item, "accountNumber", "AccountNumber", "acctNum"),
                Institution = Str(item, "institution", "Institution", "lender", "Lender"),
                Status = Str(item, "status", "Status", "accountStatus") ?? "",
                Amount = Dec(item, "amount", "Amount", "currentBalance", "writeOffAmount") ?? 0,
                ReportedAt = Date(item, "reportedAt", "ReportedAt", "dateReported", "DateReported"),
                OpenedAt = Date(item, "openedAt", "OpenedAt", "openDate", "DateOpened")
            });
        }

        foreach (var item in Arr(root, "enquiries", "Enquiries", "inquiries", "Inquiries"))
        {
            bureau.Enquiries.Add(new BureauEnquiry
            {
                Institution = Str(item, "institution", "Institution", "lender"),
                Date = Date(item, "date", "Date", "enquiryDate"),
                Amount = Dec(item, "amount", "Amount")
            });
        }

        return bureau;
    }

    public static NormalizedBank ToBank(JsonElement doc)
    {
        var root = Unwrap(doc);
        var bank = new NormalizedBank
        {
            Provider = Str(root, "provider") ?? "account-aggregator",
            LoanAmount = Dec(root, "loanAmount", "LoanAmount")
        };

        foreach (var item in Arr(root, "accounts", "Accounts", "accountDetails"))
        {
            bank.Accounts.Add(new BankAccountInfo
            {
                AccountNumber = Str(item, "accountNumber", "AccountNumber", "maskedAccNumber"),
                Type = Str(item, "type", "Type", "accountType"),
                OpenedAt = Date(item, "openedAt", "OpenedAt", "openDate", "startDate")
            });
        }

        foreach (var item in Arr(root, "transactions", "Transactions"))
        {
            var amount = Dec(item, "amount", "Amount", "value") ?? 0;
            bank.Transactions.Add(new BankTransaction
            {
                Date = Date(item, "date", "Date", "transactionTimestamp", "valueDate") ?? DateTimeOffset.UtcNow,
                Amount = Math.Abs(amount),
                Direction = (Str(item, "direction", "Direction", "type", "txnType") ?? (amount < 0 ? "debit" : "debit")).ToLowerInvariant(),
                Mode = Str(item, "mode", "Mode", "paymentMode"),
                Narration = Str(item, "narration", "Narration", "remarks", "narrative"),
                Risk = Str(item, "risk", "Risk", "category", "signal")
            });
        }

        return bank;
    }

    public static string DetectProvider(JsonElement doc, string kind, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested)) return requested.Trim();
        var root = Unwrap(doc);
        var named = Str(root, "provider");
        if (!string.IsNullOrWhiteSpace(named)) return named;
        return kind == Models.InputKinds.BankStatement ? "account-aggregator" : "equifax";
    }

    private static JsonElement Unwrap(JsonElement doc)
    {
        if (doc.ValueKind == JsonValueKind.String)
        {
            return JsonSerializer.Deserialize<JsonElement>(doc.GetString() ?? "{}");
        }
        return doc;
    }

    private static JsonElement? Obj(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.Object)
            {
                return value;
            }
        }
        return null;
    }

    private static IEnumerable<JsonElement> Arr(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray()) yield return item;
                yield break;
            }
        }
    }

    private static List<string> Strings(JsonElement root, params string[] names)
    {
        var list = new List<string>();
        foreach (var name in names)
        {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                {
                    AddIf(list, item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString());
                }
            }
            else
            {
                AddIf(list, value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString());
            }
        }
        return list;
    }

    private static string? Str(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.String) return value.GetString();
                if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                    return value.ToString();
            }
        }
        return null;
    }

    private static int? Int(JsonElement root, params string[] names)
    {
        var text = Str(root, names);
        return int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static decimal? Dec(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var d)) return d;
                if (value.ValueKind == JsonValueKind.String &&
                    decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
            }
        }
        return null;
    }

    private static DateTimeOffset? Date(JsonElement root, params string[] names)
    {
        var text = Str(root, names);
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)
            ? d
            : null;
    }

    private static void AddIf(List<string> list, string? value)
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length == 0) return;
        if (!list.Any(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(trimmed);
        }
    }
}
