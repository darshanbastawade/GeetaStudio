using System.Text.RegularExpressions;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Application;

public static partial class AnalyticsRules
{
    public static readonly string[] RevenueStreams = ["PlatformPayout", "Sponsorship", "GeetaPlusSubscription", "TalkCredits", "CreateCredits", "BooksComicsPacks", "InstitutionalContract", "ContentLicensing", "ContentOSLicensing", "Merchandise", "AffiliateCommerce"];
    public static readonly string[] CostCategories = ["MediaSpend", "AiTool", "Production", "Review", "Rework", "OperatingOverhead"];
    public static readonly string[] Activities = ["Acquired", "Converted", "Returned", "Learning"];
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9:_./-]{0,159}$")] private static partial Regex ReferencePattern();
    [GeneratedRegex("^[A-Z]{3}$")] private static partial Regex CurrencyPattern();
    public static void Require(bool ok, string message) { if (!ok) throw new RuntimeValidationException(message); }
    public static void Reference(string s) => Require(s != null && ReferencePattern().IsMatch(s), "Bounded evidence/key/reference required; no personal identifiers or arbitrary URLs.");
    public static void Currency(string s) => Require(s != null && CurrencyPattern().IsMatch(s), "Three uppercase currency letters required.");
    public static void Version(string key, int version) { Reference(key); Require(version is >= 1 and <= 100000, "Positive bounded version required."); }
    public static decimal Net(AnalyticsEntryInput e) => e.Gross - e.Tax - e.PassThrough - e.Refunds - e.Fees;
    public static decimal FinancialAmount(AnalyticsEntryInput e) => e.Kind == "Revenue" ? Net(e) : e.Kind == "Refund" ? -e.Amount : e.Amount;
    public static void Entry(AnalyticsEntryInput e)
    {
        Reference(e.EventId); Reference(e.EconomicKey); Reference(e.EvidenceReference); Reference(e.AcquisitionChannel); Reference(e.DiscoverySource);
        if (e.Campaign != "") Reference(e.Campaign);
        Require(e.Kind is "Revenue" or "Refund" or "Cost" or "Settlement" or "Metric" or "Activity", "Unsupported owned analytics event kind.");
        Require(e.OccurredUtc <= DateTimeOffset.UtcNow.AddMinutes(5) && e.OccurredUtc >= new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), "Actual events need plausible event time.");
        Require(e.FinancialState is "Earned" or "Estimated", "Earned/Estimated required.");
        Currency(e.Currency);
        Require(e.ProductionId == null || e.Kind == "Cost" && e.RevisionId != null, "Explicit production cost binding requires its core revision.");
        foreach (var value in new[] { e.Gross, e.Tax, e.PassThrough, e.Refunds, e.Fees, e.Amount }) Require(value >= 0 && value <= 1_000_000_000m && decimal.Round(value, 8) == value, "Nonnegative financial values, at most eight decimals, required.");
        if (e.Kind == "Revenue")
        {
            Require(RevenueStreams.Contains(e.RevenueStream) && e.CostCategory == "" && e.Amount == 0, "Revenue stream required independently of acquisition.");
            Require(e.Tax + e.PassThrough <= e.Gross && e.Refunds <= e.Gross, "Tax/pass-through or refunds exceed gross; net may be negative when fees survive refunds.");
        }
        else Require((e.Kind == "Refund" ? RevenueStreams.Contains(e.RevenueStream) : e.RevenueStream == "") && e.Gross + e.Tax + e.PassThrough + e.Refunds + e.Fees == 0, "Only revenue carries gross/deductions.");
        if (e.Kind == "Cost") Require(CostCategories.Contains(e.CostCategory), "Explicit cost category required.");
        else Require(e.CostCategory == "", "Cost category only applies to costs.");
        if (e.Kind is "Settlement" or "Refund") Require(e.Amount > 0, "Positive cost/settlement required.");
        if (e.Kind == "Refund") { Require(e.RelatedEntryId != null && e.FinancialState == "Earned" && e.SettlementReference == "", "Refund needs original earned revenue."); }
        else if (e.Kind == "Settlement") { Require(e.RelatedEntryId != null && e.FinancialState == "Earned", "Settlement must reference earned revenue."); Reference(e.SettlementReference); }
        else Require(e.RelatedEntryId == null && e.SettlementReference == "", "Settlement binding only applies to settlements.");
        if (e.Kind is "Activity" or "Metric") Require(e.Amount == 0 && e.FxId == null, "Nonfinancial events cannot carry cost/FX.");
        if (e.Kind == "Metric")
        {
            Require(e.DefinitionId != null && e.WindowStartUtc != null && e.WindowEndUtc > e.WindowStartUtc && e.ObservedUtc >= e.WindowEndUtc && e.ObservedUtc <= DateTimeOffset.UtcNow.AddMinutes(5), "Versioned metric and observed coverage window required.");
            Require(e.WindowEndUtc <= e.OccurredUtc && e.WindowEndUtc - e.WindowStartUtc <= TimeSpan.FromDays(366), "Metric window must finish by event time and be bounded.");
            Require(e.Coverage is "Available" or "Unavailable" or "Partial", "Metric coverage required.");
            Require(e.Coverage == "Unavailable" ? e.MetricValue == null : e.MetricValue is >= 0 and <= 1_000_000_000_000m, "Unavailable metrics must be null, never zero.");
        }
        else Require(e.DefinitionId == null && e.MetricValue == null && e.WindowStartUtc == null && e.WindowEndUtc == null && e.ObservedUtc == null && e.Coverage == "Available", "Metric fields only apply to metrics.");
        if (e.Kind == "Activity")
        {
            Require(e.ConsentId != null && Activities.Contains(e.Activity), "Consented activity required.");
            Require(e.Activity == "Learning" ? e.LearningScore is >= 0 and <= 1 : e.LearningScore == null, "Learning score only applies to learning activity (0..1).");
            if (e.SessionReference != "") Reference(e.SessionReference);
        }
        else if (e.Kind == "Cost" && e.SessionReference != "") { Reference(e.SessionReference); Require(e.ConsentId != null && e.Activity == "" && e.LearningScore == null, "Session costs need consent and a pseudonymous session reference."); }
        else Require(e.ConsentId == null && e.Activity == "" && e.LearningScore == null && e.SessionReference == "", "Identity/activity fields only apply to consented activity or session costs.");
    }
    public static void Forecast(AnalyticsForecastRequest r)
    {
        Version(r.Key, r.Version); Currency(r.Currency); Reference(r.AssumptionEvidence); Reference(r.AcquisitionChannel);
        Require(RevenueStreams.Contains(r.RevenueStream), "Forecast stream required.");
        Require(r.Audience is >= 0 and <= 1_000_000_000m && r.Conversion is >= 0 and <= 1 && r.Retention is >= 0 and <= 1 && r.UnitPrice is >= 0 and <= 1_000_000m && r.Periods is >= 1 and <= 120, "Bounded forecast assumptions required.");
        foreach (var cost in new[] { r.MediaSpend, r.AiToolCost, r.OperatingOverhead, r.ReviewCost, r.PayoutPerThousand }) Require(cost is >= 0 and <= 1_000_000_000m, "Bounded nonnegative assumptions required.");
        Require(r.LowMultiplier is >= 0 and <= 1 && r.HighMultiplier is >= 1 and <= 10 && r.EndsUtc > r.StartsUtc && r.EndsUtc - r.StartsUtc <= TimeSpan.FromDays(3660), "Inspectible uncertainty and bounded period required.");
        Require(r.RevenueStream == "PlatformPayout" || !r.PlatformPayoutEligible && r.PayoutPerThousand == 0, "Platform eligibility applies only to platform payout projections.");
    }
    public static decimal ForecastGross(AnalyticsForecastRequest r)
    {
        if (r.RevenueStream == "PlatformPayout") return r.PlatformPayoutEligible ? r.Audience / 1000m * r.PayoutPerThousand * r.Periods : 0;
        decimal periods = 0, remaining = 1; for (var i = 0; i < r.Periods; i++) { periods += remaining; remaining *= r.Retention; }
        return decimal.Round(r.Audience * r.Conversion * r.UnitPrice * periods, 8);
    }
}
