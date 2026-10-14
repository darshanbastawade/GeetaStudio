using System.Text.Json;
using ContentOS.Domain;

namespace ContentOS.Foundation.Tests;

public sealed class HistoricalPackTests
{
    [Fact]
    public void Pinned_source_excerpts_are_exact_hashes_and_the_pack_keeps_uncertain_material_distinct()
    {
        using var sources = JsonDocument.Parse(File.ReadAllText(HistoricalTestFixture.Data("sources.json")));
        var quotes = sources.RootElement.GetProperty("quotes").EnumerateArray().ToArray();
        Assert.Equal(14, quotes.Length); Assert.Equal(14, quotes.Select(x => x.GetProperty("key").GetString()).Distinct().Count());
        foreach (var q in quotes)
        {
            var text = q.GetProperty("text").GetString()!; CorpusRules.Exact(text);
            Assert.Equal(HistoricalTestFixture.Hash(text), q.GetProperty("sha256").GetString());
            Assert.NotEmpty(q.GetProperty("locator").GetString()!);
        }
        using var items = JsonDocument.Parse(File.ReadAllText(HistoricalTestFixture.Data("read-items.json")));
        var entries = items.RootElement.GetProperty("entries").EnumerateArray().ToArray(); Assert.Equal(10, entries.Length);
        Assert.Equal(Enumerable.Range(1, 10), entries.Select(x => x.GetProperty("position").GetInt32()).Order());
        var keys = quotes.Select(x => x.GetProperty("key").GetString()).ToHashSet();
        foreach (var e in entries) Assert.All(e.GetProperty("quotes").EnumerateArray(), q => Assert.Contains(q.GetString(), keys));
        Assert.Equal(4, entries.Count(x => x.GetProperty("classification").GetString() == "DocumentedFact"));
        foreach (var e in entries.Where(x => x.GetProperty("classification").GetString() is "PopularLegend" or "DramatizedDialogue"))
        {
            Assert.Equal(0, e.GetProperty("startYear").GetInt32()); Assert.Equal(0, e.GetProperty("endYear").GetInt32());
            Assert.StartsWith("Undated", e.GetProperty("dating").GetString());
        }
    }

    [Fact]
    public void Exact_source_accounts_do_not_relax_scripture_or_fiction_classifications()
    {
        EvidenceRules.Classification("SourceQuotation", "SourceQuotation", "Historian", "Attributed", "SourceExact");
        Assert.Throws<RuntimeValidationException>(() => EvidenceRules.Classification("SourceQuotation", "HistoricalClaim", "", "", "SourceExact"));
        Assert.Throws<RuntimeValidationException>(() => EvidenceRules.Classification("SourceQuotation", "SourceQuotation", "", "", "Fictional"));
        Assert.Throws<RuntimeValidationException>(() => CorpusRules.QuotationLocator("Invented historical quotation", 1));
        Assert.True(EvidenceRules.RequiresClaims("Historical"));
    }
}
