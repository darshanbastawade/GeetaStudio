using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Integrations;

public abstract class LocalDraftTemplate : IFactoryTemplate
{
    public abstract string Key { get; }
    public FactoryRenderResult Render(FactoryRenderRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var s = request.Snapshot.Lesson.Sections ?? throw new RuntimeValidationException("Reviewed lesson sections required."); var b = request.Snapshot;
        var cast = string.Join("; ", b.Cast.Cast.Select(c => c.Character.Profile.Rules.Name + " (" + c.Role + ")"));
        var source = "Reviewed source anchor (quoted, not generated):\n" + s.SourceAnchor + "\nAttributed interpretation:\n" + s.Interpretation + "\n";
        var fiction = "Original fiction — not scripture/history. Cast: " + cast + "\n" + s.Situation + "\n" + s.Example + "\nChoice/action: " + s.PracticalAction;
        var treatment = request.Format.Kind switch
        {
            "Explanation" => "Context:\n" + s.Situation + "\nExplanation treatment:\n" + s.Example + "\nPractice: " + s.PracticalAction,
            "Story" => fiction + "\nReflection: " + s.Reflection,
            "ComicScript" => "Panel 1 — setting: " + s.Situation + "\nPanel 2 — original fictional example: " + s.Example + "\nPanel 3 — agency: " + s.PracticalAction + "\nPanel 4 — reflection: " + s.Reflection + "\nCast guide: " + cast,
            "ShortVideoScript" => "Original fictional short; no generated audio/video.\nScene: " + s.Situation + "\nBeat: " + s.Example + "\nDecision: " + s.PracticalAction + "\nClosing prompt: " + s.Reflection + "\nCast: " + cast,
            "Carousel" => "Card 1: " + s.Situation + "\nCard 2 (original fictional example): " + s.Example + "\nCard 3: " + s.PracticalAction + "\nCard 4: " + s.Reflection,
            "Reflection" => "Reflect: " + s.Reflection + "\nPractice: " + s.PracticalAction + "\nLearning goal: " + b.Brief.LearningGoal,
            _ => throw new RuntimeValidationException("Unsupported format.")
        };
        var body = source + "\n" + treatment + "\nEditorial direction: " + request.Format.Instruction + "\nTemplate: " + Key;
        if (body.Length > request.Format.MaxCharacters) throw new RuntimeValidationException("Generated draft exceeds format character limit.");
        return new(body, treatment, Key);
    }
}
public sealed class LocalDraftTemplateA : LocalDraftTemplate { public override string Key => "local-template.a.v1"; }
public sealed class LocalDraftTemplateB : LocalDraftTemplate { public override string Key => "local-template.b.v1"; }
