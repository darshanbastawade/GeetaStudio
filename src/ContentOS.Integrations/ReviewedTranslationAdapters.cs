using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Integrations;

// Local, exact reviewed-memory adapters. No machine translation quality or live vendor qualification is implied.
public sealed class ReviewedMemoryTranslationA : ITranslationAdapter
{
    public string Key => "reviewed-memory.a.v1";
    public TranslationResult Translate(TranslationRequest r)
    {
        if (r.Locale.Locale == r.Locale.SourceLocale) return new(r.Source, null, Key);
        var hash = JsonSerializer.Serialize(r.Source);
        var memory = r.ReviewedMemory.FirstOrDefault(x => x.ReviewStatus == "Approved" && JsonSerializer.Serialize(x.Entry.Source) == hash);
        if (memory == null) throw new RuntimeValidationException("No exact reviewed translation memory for this locale and audience treatment.", "UnsupportedLocale");
        return new(memory.Entry.Target, memory.Id, Key);
    }
}
public sealed class ReviewedMemoryTranslationB : ITranslationAdapter
{
    public string Key => "reviewed-memory.b.v1";
    public TranslationResult Translate(TranslationRequest r)
    {
        if (r.Locale.Locale == r.Locale.SourceLocale) return new(r.Source, null, Key);
        var matches = r.ReviewedMemory.Where(x => x.ReviewStatus == "Approved" && JsonSerializer.Serialize(x.Entry.Source) == JsonSerializer.Serialize(r.Source)).OrderBy(x => x.Id).ToArray();
        if (matches.Length == 0) throw new RuntimeValidationException("This exact source is not supported by reviewed memory.", "UnsupportedLocale");
        return new(matches[0].Entry.Target, matches[0].Id, Key);
    }
}
