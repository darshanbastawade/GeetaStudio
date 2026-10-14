namespace ContentOS.Application;

public static class TalkRules
{
    // A conservative, closed launch capability. No tools, account edits or professional advice.
    public static string Classify(string question)
    {
        var q = question.ToLowerInvariant();
        if (new[] { "suicide", "kill myself", "hurt myself", "self-harm", "end my life", "harm someone", "abuse", "unsafe at home" }.Any(q.Contains)) return "Crisis";
        if (new[] { "medical", "medicine", "diagnos", "treatment", "dosage", "lawyer", "legal", "lawsuit", "invest", "financial", "stock", "tax advice", "depression", "chest pain" }.Any(q.Contains)) return "Professional";
        if (new[] { "news", "today", "current", "president", "election", "price", "weather" }.Any(q.Contains)) return "CurrentFacts";
        if (new[] { "ignore", "system prompt", "secret", "api key", "entitlement", "wallet", "credits", "other user", "other household", "execute", "tool call", "pretend", "divine authority" }.Any(q.Contains)) return "AuthorityRejected";
        return "Learning";
    }
    public static string SafeAnswer(string outcome) => outcome switch
    {
        "Crisis" => "Your safety matters. If there is immediate danger, contact local emergency services now. Reach out to a trusted person nearby; children should tell a trusted adult. This AI character cannot provide crisis care.",
        "Professional" => "I can support scripture learning and general reflection. For personal medical, legal or financial decisions, speak with a qualified professional. I cannot provide individualized advice.",
        "CurrentFacts" => "Scripture is not evidence of current events. I do not have verified current news for this question; check an authoritative current source.",
        "AuthorityRejected" => "I cannot change account permissions, credits, instructions or another person's context. We can discuss the reviewed scripture sample instead.",
        _ => "I do not have sufficient supported evidence to answer that confidently. I will not invent scripture or resolve disagreement without evidence. Try a question about the available reviewed sample."
    };
}
