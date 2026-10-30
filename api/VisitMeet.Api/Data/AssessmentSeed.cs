using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VisitMeet.Api.Models;
using VisitMeet.Api.Services.Assessment;

namespace VisitMeet.Api.Data;

public static class AssessmentSeed
{
    public static async Task EnsureSeededAsync(AppDbContext db, Guid adminId)
    {
        if (await db.LoanCategories.AnyAsync())
        {
            return;
        }

        var lap = new LoanCategory
        {
            Id = Guid.Parse("c1111111-1111-1111-1111-111111111111"),
            Code = "business-lap",
            Name = "Business LAP",
            Description = "Loan against property for business households.",
            CreatedAt = DateTimeOffset.UtcNow
        };
        var jlg = new LoanCategory
        {
            Id = Guid.Parse("c2222222-2222-2222-2222-222222222222"),
            Code = "jlg",
            Name = "JLG",
            Description = "Joint liability group microfinance.",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.LoanCategories.AddRange(lap, jlg);

        var now = DateTimeOffset.UtcNow;
        var categoryJson = AssessmentMapper.ToJson(new[] { lap.Id });
        SeedRules(db, adminId, categoryJson, now);
        SeedBusinessLapGuide(db, adminId, lap.Id, now);
        CamTemplateSeed.Seed(db, adminId, lap.Id, now);
        SeedSampleApplication(db, adminId, lap.Id, now);
        await db.SaveChangesAsync();
    }

    private static void SeedRules(AppDbContext db, Guid adminId, string categoryJson, DateTimeOffset now)
    {
        void Rule(string id, string check, string name, string description, string severity, object parameters, int task)
        {
            db.DeviationRules.Add(new DeviationRule
            {
                Id = Guid.Parse(id),
                CheckKey = check,
                Name = name,
                Description = description,
                Severity = severity,
                ParametersJson = JsonSerializer.Serialize(parameters, AssessmentMapper.Json),
                LoanCategoryIdsJson = categoryJson,
                Enabled = true,
                FeedsTaskNumber = task,
                UpdatedByUserId = adminId,
                UpdatedAt = now
            });
        }

        Rule("d1111111-1111-1111-1111-111111111111", "credit-report.write-off-settled", "credit-report.write-off-settled",
            "Flag written-off or settled accounts at or above the amount, reported within the lookback window.",
            "High", new { amountGte = 0, maxAgeMonths = 12 }, 93);
        Rule("d1111111-1111-1111-1111-111111111112", "credit-report.score-floor", "credit-report.score-floor",
            "Flag when the bureau score is below the floor.",
            "High", new { minScore = 650 }, 93);
        Rule("d1111111-1111-1111-1111-111111111113", "credit-report.recent-inquiries", "credit-report.recent-inquiries",
            "Flag when bureau enquiries in the lookback window exceed the count.",
            "Medium", new { maxCount = 3, maxAgeMonths = 6 }, 92);
        Rule("d1111111-1111-1111-1111-111111111114", "credit-report.pan-consistency", "credit-report.pan-consistency",
            "Flag PAN variants on the bureau versus the application.",
            "High", new { }, 88);
        Rule("d1111111-1111-1111-1111-111111111115", "credit-report.phone-consistency", "credit-report.phone-consistency",
            "Flag phone number variants on the bureau versus the application.",
            "Medium", new { }, 89);
        Rule("d1111111-1111-1111-1111-111111111116", "credit-report.dob-consistency", "credit-report.dob-consistency",
            "Flag date-of-birth variants on the bureau versus the application.",
            "High", new { }, 90);
        Rule("d1111111-1111-1111-1111-111111111117", "bank-statement.risk-signals", "bank-statement.risk-signals",
            "High-severity risk narrations such as gambling, crypto or mule activity.",
            "High", new { categories = new[] { "gambling", "crypto", "money-mule" }, minAmount = 1 }, 96);
        Rule("d1111111-1111-1111-1111-111111111118", "bank-statement.risk-signals", "bank-statement.risk-signals",
            "Medium-severity cash-heavy or unusual flows.",
            "Medium", new { categories = new[] { "cash-heavy" }, minAmount = 50000 }, 96);
        Rule("d1111111-1111-1111-1111-111111111119", "bank-statement.repayment-account-activity", "bank-statement.repayment-account-activity",
            "Flag EMI or loan-repayment debits on the statement.",
            "Medium", new { minCredits = 1, maxAgeMonths = 3, keywords = new[] { "emi", "loan", "repay", "nbfc" } }, 97);
        Rule("d1111111-1111-1111-1111-11111111111a", "bank-statement.restricted-debit-modes", "bank-statement.restricted-debit-modes",
            "Flag restricted debit modes such as cash, ATM or crypto.",
            "Medium", new { modes = new[] { "cash", "crypto", "atm" } }, 98);
        Rule("d1111111-1111-1111-1111-11111111111b", "bank-statement.high-value-transactions", "bank-statement.high-value-transactions",
            "Flag transactions at or above the band for the loan amount.",
            "Medium", new
            {
                bands = new[]
                {
                    new { loanAmountGte = 800000, txnAmountGte = 10000 },
                    new { loanAmountGte = 1000000, txnAmountGte = 15000 },
                    new { loanAmountGte = 1200000, txnAmountGte = 20000 }
                }
            }, 99);
        Rule("d1111111-1111-1111-1111-11111111111c", "bank-statement.active-account-vintage", "bank-statement.active-account-vintage",
            "Flag accounts opened more recently than the minimum vintage.",
            "Medium", new { minAgeMonths = 6 }, 100);
    }

    private static void SeedBusinessLapGuide(AppDbContext db, Guid adminId, Guid lapId, DateTimeOffset now)
    {
        var guide = new EngagementGuide
        {
            Id = Guid.Parse("e1111111-1111-1111-1111-111111111111"),
            Code = "business-lap",
            Name = "Business LAP interview",
            EventType = "Interview",
            Version = 1,
            Status = GuideStatuses.Active,
            LoanCategoryIdsJson = AssessmentMapper.ToJson(new[] { lapId }),
            CreatedByUserId = adminId,
            UpdatedByUserId = adminId,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.EngagementGuides.Add(guide);

        Guid Group(string name, int order)
        {
            var id = Guid.NewGuid();
            db.GuideGroups.Add(new GuideGroup { Id = id, GuideId = guide.Id, Name = name, SortOrder = order });
            return id;
        }

        var intro = Group("Introduction", 1);
        var intent = Group("Loan Intent", 2);
        var household = Group("Household", 3);
        var residence = Group("Residence", 4);
        var mortgage = Group("Mortgage", 5);
        var income = Group("Income", 6);
        var salary = Group("Income: Salary", 7);
        var agri = Group("Income: Agriculture", 8);
        var dairy = Group("Income: Dairy", 9);
        var business = Group("Income: Business", 10);
        var rental = Group("Income: Rental", 11);
        var expenses = Group("Expenses", 12);
        var assets = Group("Assets", 13);
        var credit = Group("Credit details", 14);
        var bureau = Group("Credit Bureau", 15);
        var banking = Group("Banking", 16);

        void Task(int n, Guid group, string title, string description, string[]? choices = null,
            bool multi = false, int? whenTask = null, string[]? whenChoices = null, string? rule = null)
        {
            db.GuideTasks.Add(new GuideTask
            {
                Id = Guid.NewGuid(),
                GuideId = guide.Id,
                GroupId = group,
                Number = n,
                Title = title,
                Description = description,
                ChoicesJson = AssessmentMapper.ToJson(choices),
                AllowMultiple = multi,
                ConditionTaskNumber = whenTask,
                ConditionChoicesJson = AssessmentMapper.ToJson(whenChoices),
                RuleLink = rule,
                SortOrder = n
            });
        }

        Task(1, intro, "Consent to proceed", "Introduce yourself, state the purpose of Video PD, and ask permission to continue.", ["Yes", "No"]);
        Task(2, intent, "Loan purpose", "What will this loan be used for?");
        Task(3, intent, "Requested amount", "What amount is the household applying for?");
        Task(4, intent, "Requested tenure", "What tenure is the household asking for?");
        Task(5, intent, "Fresh or balance transfer", "Is this a fresh loan or a balance transfer?", ["Fresh", "Balance Transfer"]);
        Task(6, intent, "BT details", "Which lender, outstanding amount, and reason for the balance transfer?", whenTask: 5, whenChoices: ["Balance Transfer"]);
        Task(7, intent, "End use of funds", "Walk through how the funds will be used in the next 90 days.");
        Task(8, intent, "Other lenders considered", "Have they applied to or been declined by any other lender recently?", ["Yes", "No"]);
        Task(9, household, "Household members", "How many people live in the household, including dependants?");
        Task(10, household, "Earning members", "Who in the household earns, and what do they do?");
        Task(11, household, "Dependants", "How many dependants, and who supports them?");
        Task(12, household, "Family structure", "Nuclear, joint, or extended family? Any recent split?");
        Task(13, residence, "Current address", "Confirm the full residential address and how long they have lived there.");
        Task(14, residence, "Years at residence", "How many years at this house?");
        Task(15, residence, "Ownership status", "Is your residence (a) self-owned (b) family-owned/ancestral (c) rented?", ["Owned", "Family Owned", "Rented"]);
        Task(16, residence, "Family-owned details", "Who holds title, and what is the applicant's right to stay?", whenTask: 15, whenChoices: ["Family Owned"]);
        Task(17, residence, "Monthly rent", "What is the monthly rent, to whom, and is there a written agreement?", whenTask: 15, whenChoices: ["Rented"]);
        Task(18, mortgage, "Mortgaged property is the residence", "Is the property offered as mortgage the same as the residence just discussed?", ["Yes", "No"]);
        Task(19, mortgage, "Mortgaged property address", "If not the residence, record the mortgaged property address and distance.", whenTask: 18, whenChoices: ["No"]);
        Task(20, mortgage, "Property type", "Plot, house, shop, or mixed use?");
        Task(21, mortgage, "Ownership of mortgaged property", "Whose name is on the title?");
        Task(22, mortgage, "Occupancy", "Self-occupied, family, tenant, or vacant?");
        Task(23, mortgage, "Stated market value", "What value does the household quote, and what is it based on?");
        Task(24, mortgage, "Encumbrances", "Any existing mortgage, attachment, or family claim?");
        Task(25, mortgage, "Documents seen", "Which title, tax, and identity papers were shown on camera?");
        Task(26, mortgage, "Access and boundaries", "Road access, neighbours, and any boundary dispute.");
        Task(27, mortgage, "Current use", "How is the property used today?");
        Task(28, mortgage, "Disputes", "Any court case, family dispute, or municipal notice?");
        Task(29, income, "Income sources", "Which income sources does the household have? Select every source that applies.", ["Salaried", "Agriculture", "Dairy", "Business", "Rental"], multi: true);
        Task(30, income, "Stated combined income", "What is the household's combined monthly income, in their words?");
        Task(31, salary, "Employer", "Who is the employer, and is it formal or informal?", whenTask: 29, whenChoices: ["Salaried"]);
        Task(32, salary, "Role and tenure", "Designation and years with this employer.", whenTask: 29, whenChoices: ["Salaried"]);
        Task(33, salary, "Take-home pay", "Monthly take-home after deductions.", whenTask: 29, whenChoices: ["Salaried"]);
        Task(34, salary, "Pay proof", "Salary slip, passbook credit, or cash? How often?", whenTask: 29, whenChoices: ["Salaried"]);
        Task(35, salary, "Variable pay", "Overtime, incentive, or bonus in a typical month.", whenTask: 29, whenChoices: ["Salaried"]);
        Task(36, salary, "Other earning members' salary", "Anyone else salaried in the household?", whenTask: 29, whenChoices: ["Salaried"]);
        Task(37, salary, "Job stability", "Any layoff, seasonal gap, or planned change?", whenTask: 29, whenChoices: ["Salaried"]);
        Task(38, salary, "Workplace visit", "Can the workplace be shown or described clearly?", whenTask: 29, whenChoices: ["Salaried"]);
        Task(39, salary, "PF or ESI", "Any statutory deductions that confirm employment?", whenTask: 29, whenChoices: ["Salaried"]);
        Task(40, salary, "Salary cash vs bank", "How much of salary lands in the bank statement?", whenTask: 29, whenChoices: ["Salaried"]);
        Task(41, agri, "Crops", "Which crops, acres, and owned vs leased land?", whenTask: 29, whenChoices: ["Agriculture"]);
        Task(42, agri, "Seasonal cycle", "How many harvests a year, and which months bring cash?", whenTask: 29, whenChoices: ["Agriculture"]);
        Task(43, agri, "Gross agricultural income", "Last 12 months' gross from crop sales.", whenTask: 29, whenChoices: ["Agriculture"]);
        Task(44, agri, "Input costs", "Seed, labour, diesel, and rent for land.", whenTask: 29, whenChoices: ["Agriculture"]);
        Task(45, agri, "Buyers", "Mandi, trader, or contract? Proof of sale?", whenTask: 29, whenChoices: ["Agriculture"]);
        Task(46, agri, "Irrigation", "Rainfed, borewell, or canal — any risk this season?", whenTask: 29, whenChoices: ["Agriculture"]);
        Task(47, dairy, "Herd", "How many milch animals, and who owns them?", whenTask: 29, whenChoices: ["Dairy"]);
        Task(48, dairy, "Daily yield", "Litres per day now versus six months ago.", whenTask: 29, whenChoices: ["Dairy"]);
        Task(49, dairy, "Buyer", "Co-op, private dairy, or doorstep? Rate per litre.", whenTask: 29, whenChoices: ["Dairy"]);
        Task(50, dairy, "Monthly dairy income", "Typical monthly cash from milk.", whenTask: 29, whenChoices: ["Dairy"]);
        Task(51, dairy, "Feed and vet cost", "Monthly cost to keep the herd.", whenTask: 29, whenChoices: ["Dairy"]);
        Task(52, dairy, "Dairy photos", "Show shed, animals, and cans if on site.", whenTask: 29, whenChoices: ["Dairy"]);
        Task(53, dairy, "Seasonality", "Does yield drop in summer? By how much?", whenTask: 29, whenChoices: ["Dairy"]);
        Task(54, business, "Business type", "Kirana, tailoring, barber, clothing, manufacturing, or other?", whenTask: 29, whenChoices: ["Business"]);
        Task(55, business, "Years in business", "When did this activity start, and under whose name?", whenTask: 29, whenChoices: ["Business"]);
        Task(56, business, "Premises", "Home, rented shop, or market stall?", whenTask: 29, whenChoices: ["Business"]);
        Task(57, business, "Stock and tools", "What stock or machines are visible?", whenTask: 29, whenChoices: ["Business"]);
        Task(58, business, "Daily sales", "Typical weekday and weekend sales.", whenTask: 29, whenChoices: ["Business"]);
        Task(59, business, "Gross monthly revenue", "Calculated monthly sales / turnover for the primary business.", whenTask: 29, whenChoices: ["Business"]);
        Task(60, business, "Purchases", "Who supplies, and how often is stock replenished?", whenTask: 29, whenChoices: ["Business"]);
        Task(61, business, "Margins", "Rough gross margin they quote, with an example SKU.", whenTask: 29, whenChoices: ["Business"]);
        Task(62, business, "Employees", "Family only, or paid staff? Cost?", whenTask: 29, whenChoices: ["Business"]);
        Task(63, business, "Licences", "GST, shop act, or none?", whenTask: 29, whenChoices: ["Business"]);
        Task(64, business, "Seasonality", "Peak months and lean months.", whenTask: 29, whenChoices: ["Business"]);
        Task(65, business, "Cash vs UPI", "How much of sales is digital?", whenTask: 29, whenChoices: ["Business"]);
        Task(66, business, "Business site photos", "Capture the shop or workplace.", whenTask: 29, whenChoices: ["Business"]);
        Task(67, business, "Customers", "Walk-in, regulars, or B2B?", whenTask: 29, whenChoices: ["Business"]);
        Task(68, business, "Competition", "How many similar shops nearby?", whenTask: 29, whenChoices: ["Business"]);
        Task(69, business, "Working capital cycle", "How many days of stock, and any credit given?", whenTask: 29, whenChoices: ["Business"]);
        Task(70, business, "Net business income", "After purchases, rent, and wages, what is left each month?", whenTask: 29, whenChoices: ["Business"]);
        Task(71, rental, "Rental income", "Which property is let, to whom, and monthly rent received?", whenTask: 29, whenChoices: ["Rental"]);
        Task(72, expenses, "Food and utilities", "Monthly kitchen, LPG, electricity, and water.");
        Task(73, expenses, "Education and health", "School fees and regular medical spend.");
        Task(74, expenses, "Rent or housing", "If they pay rent or society charges, how much?");
        Task(75, expenses, "Travel and phone", "Commuting and mobile spend.");
        Task(76, expenses, "Festivals and other", "Typical extra spend in a year, monthly equivalent.");
        Task(77, expenses, "Existing EMIs", "All EMIs currently paid, including informal.");
        Task(78, expenses, "Stated surplus", "After expenses, what do they say remains?");
        Task(79, assets, "House and land", "Other land or house besides the mortgaged property?");
        Task(80, assets, "Vehicles", "Two-wheeler, four-wheeler, or commercial vehicle?");
        Task(81, assets, "Gold", "Approximate gold held, pledged or free?");
        Task(82, assets, "Livestock beyond dairy", "Goats, poultry, or draught animals?");
        Task(83, assets, "Financial assets", "Recurring deposit, chit, or insurance?");
        Task(84, assets, "Business assets", "Machinery, freezer, sewing machines, or shop fittings?");
        Task(85, credit, "Existing loans", "List live loans: lender, amount, EMI, remaining tenure.");
        Task(86, credit, "Informal credit", "Gold loan, money lender, or relatives?");
        Task(87, credit, "Past delay", "Any bounce, restructuring, or collection visit they admit?");
        Task(88, bureau, "PAN variations", "Probe PAN mismatches shown by the bureau flag.", rule: "credit-report.pan-consistency");
        Task(89, bureau, "Phone number variations", "Probe phone mismatches shown by the bureau flag.", rule: "credit-report.phone-consistency");
        Task(90, bureau, "DOB variations", "Probe date-of-birth mismatches shown by the bureau flag.", rule: "credit-report.dob-consistency");
        Task(91, bureau, "Name variations", "Any other name or alias on the bureau?");
        Task(92, bureau, "Bureau inquiries", "Discuss recent enquiries flagged from the bureau.", rule: "credit-report.recent-inquiries");
        Task(93, bureau, "Defaults and bounces", "Discuss write-offs, settled accounts, and low score.", rule: "credit-report.write-off-settled");
        Task(94, bureau, "Active tradelines", "Walk through open accounts the bureau shows.");
        Task(95, bureau, "Closed tradelines", "Any closed account that still shows a balance?");
        Task(96, banking, "Bank statement risk signals", "Probe gambling, crypto, mule, or cash-heavy flags.", rule: "bank-statement.risk-signals");
        Task(97, banking, "Loan repayments", "Match EMI debits on the statement to declared loans.", rule: "bank-statement.repayment-account-activity");
        Task(98, banking, "Restricted debits", "Probe cash / ATM / restricted debit modes.", rule: "bank-statement.restricted-debit-modes");
        Task(99, banking, "High value transactions", "Ask about each high-value credit or debit flagged.", rule: "bank-statement.high-value-transactions");
        Task(100, banking, "Account vintage", "Why is the account newer than the vintage rule?", rule: "bank-statement.active-account-vintage");
    }

    private static void SeedSampleApplication(AppDbContext db, Guid adminId, Guid lapId, DateTimeOffset now)
    {
        var app = new AssessmentApplication
        {
            Id = Guid.Parse("a1111111-1111-1111-1111-111111111111"),
            Code = "CAM31",
            LoanCategoryId = lapId,
            Bank = "SBI",
            Branch = "Mumbai Central",
            GroupId = "G-22",
            MemberId = "M-10482",
            DisplayName = "Meena Devi",
            LoanAmount = 900000,
            CreatedByUserId = adminId,
            CreatedAt = now
        };
        var applicant = new Applicant
        {
            Id = Guid.Parse("a2222222-2222-2222-2222-222222222222"),
            ApplicationId = app.Id,
            Role = ApplicantRoles.Primary,
            Name = "Meena Devi",
            Pan = "ABCDE1234F",
            Phone = "9876543210",
            DateOfBirth = "1988-03-12"
        };
        db.AssessmentApplications.Add(app);
        db.Applicants.Add(applicant);

        var engine = new DeviationRuleEngine();
        var rules = db.DeviationRules.Local.ToList();
        SeedRun(db, applicant, adminId, now, InputKinds.Bureau, "equifax", "equifax-meena.json", SampleBureauJson, engine, rules, applicant);
        SeedRun(db, applicant, adminId, now.AddMinutes(1), InputKinds.BankStatement, "account-aggregator", "aa-meena.json", SampleBankJson, engine, rules, applicant, 900000);

        var meeting = db.Meetings.Local.FirstOrDefault(m => m.Code == "VK7M2Q")
                      ?? db.Meetings.FirstOrDefault(m => m.Code == "VK7M2Q");
        db.EngagementSessions.Add(new EngagementSession
        {
            Id = Guid.Parse("51111111-1111-1111-1111-111111111111"),
            ApplicationId = app.Id,
            MeetingId = meeting?.Id,
            GuideId = Guid.Parse("e1111111-1111-1111-1111-111111111111"),
            GuideVersion = 1,
            Mode = SessionModes.Live,
            Status = SessionStatuses.InProgress,
            CreatedByUserId = adminId,
            StartedAt = now
        });
        db.AssessmentMemos.Add(new AssessmentMemo
        {
            Id = Guid.Parse("61111111-1111-1111-1111-111111111111"),
            ApplicationId = app.Id,
            SessionId = Guid.Parse("51111111-1111-1111-1111-111111111111"),
            TemplateId = CamTemplateCatalog.BusinessLapTemplateId,
            TemplateVersion = 2,
            Status = MemoStatuses.Pending,
            TranscriptStatus = "Missing",
            CreatedByUserId = adminId,
            CreatedAt = now
        });
    }

    private static void SeedRun(
        AppDbContext db,
        Applicant applicant,
        Guid adminId,
        DateTimeOffset now,
        string kind,
        string provider,
        string fileName,
        string json,
        DeviationRuleEngine engine,
        List<DeviationRule> rules,
        Applicant person,
        decimal? loanAmount = null)
    {
        var input = new ApplicantInput
        {
            Id = Guid.NewGuid(),
            ApplicantId = applicant.Id,
            Kind = kind,
            Provider = provider,
            FileName = fileName,
            RawJson = json,
            UploadedByUserId = adminId,
            UploadedAt = now
        };
        db.ApplicantInputs.Add(input);
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        var run = new RuleRun
        {
            Id = Guid.NewGuid(),
            ApplicantId = applicant.Id,
            InputId = input.Id,
            Kind = kind,
            RanAt = now
        };
        db.RuleRuns.Add(run);
        var evaluated = kind == InputKinds.Bureau
            ? engine.EvaluateBureau(rules, InputAdapters.ToBureau(doc), person)
            : engine.EvaluateBank(rules, InputAdapters.ToBank(doc), loanAmount);
        foreach (var (rule, fired, evidence) in evaluated)
        {
            db.RuleRunResults.Add(new RuleRunResult
            {
                Id = Guid.NewGuid(),
                RuleRunId = run.Id,
                RuleId = rule.Id,
                CheckKey = rule.CheckKey,
                Name = rule.Name,
                Severity = rule.Severity,
                Fired = fired,
                EvidenceJson = JsonSerializer.Serialize(evidence, AssessmentMapper.Json),
                FeedsTaskNumber = rule.FeedsTaskNumber
            });
        }
    }

    private const string SampleBureauJson = """
        {
          "provider": "equifax",
          "consumer": {
            "name": "Meena Devi",
            "pan": "ABCDE1234F",
            "pans": ["ABCDE1234F", "ABCDE9999Z"],
            "phone": "9876543210",
            "phones": ["9876543210", "9123456789"],
            "dob": "1988-03-12",
            "dobs": ["1988-03-12", "1988-03-13"],
            "score": 612
          },
          "accounts": [
            { "accountNumber": "XX4412", "institution": "Bajaj Finance", "status": "Written-Off", "amount": 18500, "reportedAt": "2026-02-10" }
          ],
          "enquiries": [
            { "institution": "HDFC Bank", "date": "2026-08-02", "amount": 300000 },
            { "institution": "ICICI Bank", "date": "2026-07-18", "amount": 250000 },
            { "institution": "Axis Bank", "date": "2026-06-21", "amount": 200000 },
            { "institution": "IDFC First", "date": "2026-05-09", "amount": 150000 }
          ]
        }
        """;

    private const string SampleBankJson = """
        {
          "provider": "account-aggregator",
          "loanAmount": 900000,
          "accounts": [
            { "accountNumber": "XXXX4521", "type": "SAVINGS", "openedAt": "2026-06-01" }
          ],
          "transactions": [
            { "date": "2026-09-12", "amount": 15000, "direction": "debit", "mode": "UPI", "narration": "BETTING APP WALLET", "risk": "gambling" },
            { "date": "2026-09-08", "amount": 12000, "direction": "debit", "mode": "cash", "narration": "ATM CASH WDL", "risk": "cash-heavy" },
            { "date": "2026-08-28", "amount": 6400, "direction": "debit", "mode": "NACH", "narration": "BAJAJ FINANCE EMI" },
            { "date": "2026-08-05", "amount": 18000, "direction": "debit", "mode": "IMPS", "narration": "STOCK PURCHASE KIRANA" }
          ]
        }
        """;
}
