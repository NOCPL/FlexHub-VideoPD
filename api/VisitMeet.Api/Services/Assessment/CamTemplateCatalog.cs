namespace VisitMeet.Api.Services.Assessment;

public sealed record CamFieldDef(
    int Number,
    string Section,
    string Label,
    string Question,
    string Datatype,
    string Source,
    string[]? Options = null,
    bool AllowMultiple = false,
    int? WhenField = null,
    string[]? WhenValues = null,
    int? TaskNumber = null,
    string? RuleLink = null,
    string? PhotoType = null,
    string? JsonShape = null);

public static class CamTemplateCatalog
{
    public static readonly Guid BusinessLapTemplateId = Guid.Parse("f1111111-1111-1111-1111-111111111111");

    public static readonly (string Name, string Source, int Order)[] Sections =
    [
        ("1-Household", "Transcript", 1),
        ("2-Income", "Transcript", 2),
        ("Expenses", "Transcript", 3),
        ("3-Loan Details", "Transcript", 4),
        ("Credit Report Details", "Transcript", 5),
        ("4-Property & Mortgage", "Transcript", 6),
        ("References", "Transcript", 7),
        ("5-Household photos", "Photos", 8),
        ("Business Site Analysis", "Photos", 9),
        ("6-Dairy Assets", "Photos", 10),
        ("7-Kirana Photos", "Photos", 11),
        ("8-Tailoring Photos", "Photos", 12),
        ("9-Barber Photos", "Photos", 13),
        ("10-Clothing Store Photos", "Photos", 14),
    ];

    public static readonly string[] FileCategories =
    [
        "house", "business site", "dairy", "kirana", "tailoring", "barber", "clothing store"
    ];

    private static readonly string[] IncomeSources = ["Salaried", "Agriculture", "Dairy", "Business", "Rental"];
    private static readonly string[] BusinessTypes = ["Kirana", "Tailoring", "Barber", "Clothing store", "Manufacturing", "Other"];
    private static readonly string[] YesNo = ["Yes", "No"];

    public static readonly CamFieldDef[] Fields =
    [
        F(1, "1-Household", "household_members", "How many people live in the household, including dependants?", "Number", task: 9),
        F(2, "1-Household", "earning_members", "Who in the household earns, and what does each person do?", task: 10),
        F(3, "1-Household", "dependants", "How many dependants are there, and who supports them?", task: 11),
        F(4, "1-Household", "family_structure", "Is the family nuclear, joint, or extended, and was there a recent split?", task: 12),
        F(5, "1-Household", "residential_address", "What is the full residential address, and how long has the household lived there?", task: 13),
        F(6, "1-Household", "years_at_residence", "How many years has the household lived at this residence?", "Number", task: 14),
        F(7, "1-Household", "residence_ownership", "Is the residence self-owned, family-owned or ancestral, or rented?", "Options", options: ["Owned", "Family Owned", "Rented"], task: 15),
        F(8, "1-Household", "monthly_rent", "What is the monthly rent, who is it paid to, and is there a written agreement?", "Number", when: 7, whenValues: ["Rented"], task: 17),
        F(9, "1-Household", "family_owned_stay", "If the residence is family-owned, who holds title and what is the applicant's right to stay?", when: 7, whenValues: ["Family Owned"], task: 16),
        F(10, "1-Household", "household_support", "Who currently provides for the dependants if the earning pattern has changed?"),

        F(11, "2-Income", "income_sources", "Which income sources does the household have? Record every source that applies.", "Options", options: IncomeSources, multi: true, task: 29),
        F(12, "2-Income", "stated_combined_monthly_income", "What combined monthly income does the household state, in their own words?", "Number", task: 30),
        F(13, "2-Income", "employer", "Who employs the salaried member, and is the job formal or informal?", when: 11, whenValues: ["Salaried"], task: 31),
        F(14, "2-Income", "role_and_tenure", "What is the designation, and how many years have they been with this employer?", when: 11, whenValues: ["Salaried"], task: 32),
        F(15, "2-Income", "take_home_pay", "What is the monthly take-home pay after deductions?", "Number", when: 11, whenValues: ["Salaried"], task: 33),
        F(16, "2-Income", "salary_in_bank", "How much of the salary is credited to the bank, and how much is paid in cash?", when: 11, whenValues: ["Salaried"], task: 40),
        F(17, "2-Income", "crops_and_land", "Which crops are grown, on how many acres, and is the land owned or leased?", when: 11, whenValues: ["Agriculture"], task: 41),
        F(18, "2-Income", "gross_agricultural_income", "What was the gross income from crop sales over the last 12 months?", "Number", when: 11, whenValues: ["Agriculture"], task: 43),
        F(19, "2-Income", "dairy_herd", "How many milch animals does the household keep, and who owns them?", when: 11, whenValues: ["Dairy"], task: 47),
        F(20, "2-Income", "monthly_dairy_income", "What is the typical monthly cash income from milk?", "Number", when: 11, whenValues: ["Dairy"], task: 50),
        F(21, "2-Income", "business_type", "What is the primary business: kirana, tailoring, barber, clothing store, manufacturing, or other?", "Options", options: BusinessTypes, when: 11, whenValues: ["Business"], task: 54),
        F(22, "2-Income", "years_in_business", "When did this business start, and in whose name does it run?", when: 11, whenValues: ["Business"], task: 55),
        F(23, "2-Income", "business_premises", "Is the business run from home, a rented shop, or a market stall?", when: 11, whenValues: ["Business"], task: 56),
        F(24, "2-Income", "daily_sales", "What are typical weekday and weekend sales?", "Number", when: 11, whenValues: ["Business"], task: 58),
        F(25, "2-Income", "purchases", "Who supplies the business, and how often is stock replenished?", when: 11, whenValues: ["Business"], task: 60),
        F(26, "2-Income", "gross_monthly_business_revenue", "What is the calculated Gross Monthly Business Revenue (Sales/Turnover) for the applicant's primary business?", "Number", when: 11, whenValues: ["Business"], task: 59),
        F(27, "2-Income", "gross_margin", "What gross margin does the household quote, with one example item?", when: 11, whenValues: ["Business"], task: 61),
        F(28, "2-Income", "employees_and_wages", "Is the work done by family only or by paid staff, and what do wages cost each month?", when: 11, whenValues: ["Business"], task: 62),
        F(29, "2-Income", "cash_vs_upi", "How much of sales is collected in cash, and how much by UPI or bank transfer?", when: 11, whenValues: ["Business"], task: 65),
        F(30, "2-Income", "net_business_income", "After purchases, rent, and wages, what is left from the business each month?", "Number", when: 11, whenValues: ["Business"], task: 70),
        F(31, "2-Income", "rental_income", "Which property is let, to whom, and what monthly rent is received?", "Number", when: 11, whenValues: ["Rental"], task: 71),
        F(32, "2-Income", "business_seasonality", "Which months are peak for the business, and which are lean?", when: 11, whenValues: ["Business"], task: 64),
        F(33, "2-Income", "licences", "Does the business have GST, a shop act licence, or neither?", when: 11, whenValues: ["Business"], task: 63),
        F(34, "2-Income", "working_capital_cycle", "How many days of stock are held, and is credit given to customers?", when: 11, whenValues: ["Business"], task: 69),
        F(35, "2-Income", "customers", "Are customers walk-in, regulars, or other businesses?", when: 11, whenValues: ["Business"], task: 67),
        F(36, "2-Income", "competition", "How many similar shops operate nearby?", when: 11, whenValues: ["Business"], task: 68),

        F(37, "Expenses", "food_and_utilities", "What is the monthly spend on kitchen, LPG, electricity, and water?", "Number", task: 72),
        F(38, "Expenses", "education_and_health", "What is the monthly spend on school fees and regular medical costs?", "Number", task: 73),
        F(39, "Expenses", "housing_expense", "What rent or society charge does the household pay each month?", "Number", task: 74),
        F(40, "Expenses", "travel_and_phone", "What is the monthly spend on commuting and mobile phones?", "Number", task: 75),
        F(41, "Expenses", "existing_emis", "What EMIs are paid today, including informal loans?", "Number", task: 77),
        F(42, "Expenses", "stated_surplus", "After expenses, what does the household say remains each month?", "Number", task: 78),

        F(43, "3-Loan Details", "loan_purpose", "What will this loan be used for?", task: 2),
        F(44, "3-Loan Details", "requested_amount", "What loan amount is the household applying for?", "Number", task: 3),
        F(45, "3-Loan Details", "requested_tenure", "What tenure is the household asking for?", task: 4),
        F(46, "3-Loan Details", "fresh_or_balance_transfer", "Is this a fresh loan or a balance transfer?", "Options", options: ["Fresh", "Balance Transfer"], task: 5),
        F(47, "3-Loan Details", "balance_transfer_details", "For a balance transfer, which lender, what outstanding, and why move the loan?", when: 46, whenValues: ["Balance Transfer"], task: 6),
        F(48, "3-Loan Details", "end_use_of_funds", "How will the funds be used in the next 90 days?", task: 7),

        F(49, "Credit Report Details", "bureau_defaults_and_score", "What write-offs, settled accounts, or bureau score below the floor must the memo record?", rule: "credit-report.write-off-settled|credit-report.score-floor"),
        F(50, "Credit Report Details", "existing_loans", "List each live loan as one row: lender, amount, EMI, remaining tenure, and status.", "Json", task: 85, rule: "credit-report.write-off-settled", jsonShape: "Array of { lender, amount, emi, remainingTenure, status, source }"),
        F(51, "Credit Report Details", "pan_variations", "Which PAN values on the bureau differ from the application PAN?", rule: "credit-report.pan-consistency"),
        F(52, "Credit Report Details", "phone_variations", "Which phone numbers on the bureau differ from the application phone?", rule: "credit-report.phone-consistency"),
        F(53, "Credit Report Details", "dob_variations", "Which dates of birth on the bureau differ from the application?", rule: "credit-report.dob-consistency"),
        F(54, "Credit Report Details", "bureau_inquiries", "Which bureau enquiries in the lookback window must the memo record?", rule: "credit-report.recent-inquiries"),

        F(55, "4-Property & Mortgage", "mortgage_is_residence", "Is the property offered as mortgage the same as the residence?", "Options", options: YesNo, task: 18),
        F(56, "4-Property & Mortgage", "mortgaged_property_address", "If the mortgage is not the residence, what is the property address and how far is it?", when: 55, whenValues: ["No"], task: 19),
        F(57, "4-Property & Mortgage", "property_type", "Is the mortgaged property a plot, house, shop, or mixed use?", task: 20),
        F(58, "4-Property & Mortgage", "title_holder", "Whose name is on the title of the mortgaged property?", task: 21),
        F(59, "4-Property & Mortgage", "occupancy", "Is the property self-occupied, used by family, tenanted, or vacant?", task: 22),
        F(60, "4-Property & Mortgage", "stated_market_value", "What market value does the household quote, and what is that figure based on?", "Number", task: 23),
        F(61, "4-Property & Mortgage", "encumbrances", "Is there an existing mortgage, attachment, or family claim on the property?", task: 24),
        F(62, "4-Property & Mortgage", "documents_seen", "Which title, tax, and identity papers were shown on camera?", task: 25),

        F(63, "References", "reference_contact", "What is the name and phone number of a reference who does not live in the household?"),

        F(64, "5-Household photos", "house_photo", "Which house photos were captured on this visit?", source: "Photos", photo: "house"),
        F(65, "Business Site Analysis", "business_site_photo", "Which business-site photos were captured on this visit?", source: "Photos", photo: "business site"),

        F(66, "6-Dairy Assets", "dairy_unit_present", "Do the visit photos show a dairy unit?", "Options", "Photos", options: YesNo, photo: "dairy"),
        F(67, "6-Dairy Assets", "dairy_shed", "What does the cattle shed show: roof, walls, and space per animal?", source: "Photos", when: 66, whenValues: YesNo.Take(1).ToArray(), photo: "dairy"),
        F(68, "6-Dairy Assets", "dairy_animals", "How many animals are visible, and do they match the stated herd?", source: "Photos", when: 66, whenValues: ["Yes"], photo: "dairy"),
        F(69, "6-Dairy Assets", "dairy_cans", "Are milk cans visible, and do they show a buyer or co-op mark?", source: "Photos", when: 66, whenValues: ["Yes"], photo: "dairy"),
        F(70, "6-Dairy Assets", "dairy_feed_storage", "Is feed stored on site, and does the quantity match the stated herd?", source: "Photos", when: 66, whenValues: ["Yes"], photo: "dairy"),
        F(71, "6-Dairy Assets", "dairy_water", "Is a water source visible for the animals?", source: "Photos", when: 66, whenValues: ["Yes"], photo: "dairy"),
        F(72, "6-Dairy Assets", "dairy_floor", "What is the floor and drainage condition of the shed?", source: "Photos", when: 66, whenValues: ["Yes"], photo: "dairy"),
        F(73, "6-Dairy Assets", "dairy_animal_count", "Count the milch animals visible in the photos.", "Number", "Photos", when: 66, whenValues: ["Yes"], photo: "dairy"),
        F(74, "6-Dairy Assets", "dairy_ownership_marking", "Is there any marking or record that ties the animals to this household?", source: "Photos", when: 66, whenValues: ["Yes"], photo: "dairy"),
        F(75, "6-Dairy Assets", "dairy_buyer_branding", "Does any can, board, or slip name the milk buyer?", source: "Photos", when: 66, whenValues: ["Yes"], photo: "dairy"),
        F(76, "6-Dairy Assets", "dairy_site_condition", "What is the overall condition of the dairy site?", source: "Photos", when: 66, whenValues: ["Yes"], photo: "dairy"),

        F(77, "7-Kirana Photos", "kirana_present", "Do the visit photos show a kirana shop?", "Options", "Photos", options: YesNo, photo: "kirana"),
        F(78, "7-Kirana Photos", "kirana_signboard", "What does the shop signboard say?", source: "Photos", when: 77, whenValues: ["Yes"], photo: "kirana"),
        F(79, "7-Kirana Photos", "kirana_stock", "How full are the shelves, and what categories of stock are visible?", source: "Photos", when: 77, whenValues: ["Yes"], photo: "kirana"),
        F(80, "7-Kirana Photos", "kirana_billing", "Is a billing counter, register, or UPI stand visible?", source: "Photos", when: 77, whenValues: ["Yes"], photo: "kirana"),
        F(81, "7-Kirana Photos", "kirana_cold_storage", "Is a fridge or other cold storage visible?", source: "Photos", when: 77, whenValues: ["Yes"], photo: "kirana"),
        F(82, "7-Kirana Photos", "kirana_weights", "Are weights, a scale, or measures visible?", source: "Photos", when: 77, whenValues: ["Yes"], photo: "kirana"),
        F(83, "7-Kirana Photos", "kirana_frontage", "What does the shop front and access from the road show?", source: "Photos", when: 77, whenValues: ["Yes"], photo: "kirana"),

        F(84, "8-Tailoring Photos", "tailoring_machines", "How many sewing machines are visible?", source: "Photos", when: 21, whenValues: ["Tailoring"], photo: "tailoring"),
        F(85, "8-Tailoring Photos", "tailoring_work_in_progress", "Is there cloth or a garment in progress on a machine?", source: "Photos", when: 21, whenValues: ["Tailoring"], photo: "tailoring"),
        F(86, "8-Tailoring Photos", "tailoring_finished_garments", "Are finished garments visible, and roughly how many?", source: "Photos", when: 21, whenValues: ["Tailoring"], photo: "tailoring"),
        F(87, "8-Tailoring Photos", "tailoring_staff", "How many people are working at the machines?", source: "Photos", when: 21, whenValues: ["Tailoring"], photo: "tailoring"),
        F(88, "8-Tailoring Photos", "tailoring_power", "Is power and lighting sufficient for the machines shown?", source: "Photos", when: 21, whenValues: ["Tailoring"], photo: "tailoring"),
        F(89, "8-Tailoring Photos", "tailoring_orders", "Are order slips, measurements, or customer cloth visible?", source: "Photos", when: 21, whenValues: ["Tailoring"], photo: "tailoring"),
        F(90, "8-Tailoring Photos", "tailoring_setup", "Is the tailoring set up as a shop or inside the home?", source: "Photos", when: 21, whenValues: ["Tailoring"], photo: "tailoring"),

        F(91, "9-Barber Photos", "barber_present", "Do the visit photos show a barber shop?", "Options", "Photos", options: YesNo, photo: "barber"),
        F(92, "9-Barber Photos", "barber_chairs", "How many chairs are visible?", source: "Photos", when: 91, whenValues: ["Yes"], photo: "barber"),
        F(93, "9-Barber Photos", "barber_tools", "Are mirrors, clippers, and other tools visible and in use?", source: "Photos", when: 91, whenValues: ["Yes"], photo: "barber"),
        F(94, "9-Barber Photos", "barber_products", "Are retail products displayed, and roughly how much stock?", source: "Photos", when: 91, whenValues: ["Yes"], photo: "barber"),
        F(95, "9-Barber Photos", "barber_staff", "How many people are working in the shop?", source: "Photos", when: 91, whenValues: ["Yes"], photo: "barber"),
        F(96, "9-Barber Photos", "barber_frontage", "What does the shop front show?", source: "Photos", when: 91, whenValues: ["Yes"], photo: "barber"),

        F(97, "10-Clothing Store Photos", "clothing_store_present", "Do the visit photos show a clothing store?", "Options", "Photos", options: YesNo, photo: "clothing store"),
        F(98, "10-Clothing Store Photos", "clothing_racks", "Are display racks present, and how full are they?", source: "Photos", when: 97, whenValues: ["Yes"], photo: "clothing store"),
        F(99, "10-Clothing Store Photos", "clothing_inventory", "What depth of inventory is visible by type of garment?", source: "Photos", when: 97, whenValues: ["Yes"], photo: "clothing store"),
        F(100, "10-Clothing Store Photos", "clothing_trial_room", "Is a trial room visible?", source: "Photos", when: 97, whenValues: ["Yes"], photo: "clothing store"),
        F(101, "10-Clothing Store Photos", "clothing_billing", "Is a billing counter or UPI display visible?", source: "Photos", when: 97, whenValues: ["Yes"], photo: "clothing store"),
        F(102, "10-Clothing Store Photos", "clothing_brands", "Which brands or unbranded stock are on display?", source: "Photos", when: 97, whenValues: ["Yes"], photo: "clothing store"),
        F(103, "10-Clothing Store Photos", "clothing_frontage", "What does the store front and access show?", source: "Photos", when: 97, whenValues: ["Yes"], photo: "clothing store"),
    ];

    public static void Validate()
    {
        if (Sections.Length != 14)
            throw new InvalidOperationException("Business LAP CAM must have 14 sections.");
        if (Fields.Length != 103)
            throw new InvalidOperationException($"Business LAP CAM must have 103 fields, found {Fields.Length}.");
        var numbers = Fields.Select(f => f.Number).OrderBy(n => n).ToArray();
        if (numbers.Distinct().Count() != 103 || numbers[0] != 1 || numbers[^1] != 103)
            throw new InvalidOperationException("CAM field numbers must be 1 through 103.");
        if (Fields.Select(f => f.Label).Distinct(StringComparer.Ordinal).Count() != 103)
            throw new InvalidOperationException("CAM field labels must be unique.");
        var known = Sections.Select(s => s.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var field in Fields)
        {
            if (!known.Contains(field.Section))
                throw new InvalidOperationException($"Field {field.Number} uses an unknown section.");
            if (field.WhenField is int gate && (gate < 1 || gate >= field.Number || Fields.All(f => f.Number != gate)))
                throw new InvalidOperationException($"Field {field.Number} has an invalid condition.");
        }
    }

    private static CamFieldDef F(
        int number,
        string section,
        string label,
        string question,
        string datatype = "String",
        string source = "Transcript",
        string[]? options = null,
        bool multi = false,
        int? when = null,
        string[]? whenValues = null,
        int? task = null,
        string? rule = null,
        string? photo = null,
        string? jsonShape = null) =>
        new(number, section, label, question, datatype, source, options, multi, when, whenValues, task, rule, photo, jsonShape);
}
