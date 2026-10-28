namespace VisitMeet.Api.Services.Assessment;

public class NormalizedBureau
{
    public string Provider { get; set; } = "equifax";
    public string? Name { get; set; }
    public int? Score { get; set; }
    public List<string> Pans { get; set; } = [];
    public List<string> Phones { get; set; } = [];
    public List<string> Dobs { get; set; } = [];
    public List<BureauAccount> Accounts { get; set; } = [];
    public List<BureauEnquiry> Enquiries { get; set; } = [];
}

public class BureauAccount
{
    public string? AccountNumber { get; set; }
    public string? Institution { get; set; }
    public string Status { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTimeOffset? ReportedAt { get; set; }
    public DateTimeOffset? OpenedAt { get; set; }
}

public class BureauEnquiry
{
    public string? Institution { get; set; }
    public DateTimeOffset? Date { get; set; }
    public decimal? Amount { get; set; }
}

public class NormalizedBank
{
    public string Provider { get; set; } = "account-aggregator";
    public decimal? LoanAmount { get; set; }
    public List<BankAccountInfo> Accounts { get; set; } = [];
    public List<BankTransaction> Transactions { get; set; } = [];
}

public class BankAccountInfo
{
    public string? AccountNumber { get; set; }
    public string? Type { get; set; }
    public DateTimeOffset? OpenedAt { get; set; }
}

public class BankTransaction
{
    public DateTimeOffset Date { get; set; }
    public decimal Amount { get; set; }
    public string Direction { get; set; } = "debit";
    public string? Mode { get; set; }
    public string? Narration { get; set; }
    public string? Risk { get; set; }
}

public class RuleEvidenceItem
{
    public string Kind { get; set; } = "";
    public string Summary { get; set; } = "";
    public Dictionary<string, string?> Fields { get; set; } = new();
}
