using System.Net;
using System.Text;
using ContentOS.Application;
using ContentOS.Contracts;
namespace ContentOS.Integrations;

public sealed class CreationRenderer(string key = "create.local.a.v1") : ICreationRenderer
{
    public string Key => key;
    public string Qualification => "LocalZeroCost";
    public void Validate(CreationMaterial m)
    {
        CreationRules.Require(m.Content.Length is > 0 and <= 1048576 && m.Rights is "Owned" or "Licensed" && m.AltText.Length is > 0 and <= 200, "Bounded licensed material and description required.");
        if (m.MediaType == "image/png") CreationRules.Require(MediaFiles.Inspect(m.Content).Mime == "image/png", "Plain PNG required.");
        else { CreationRules.Require(m.MediaType == "text/plain" && m.Content.Length <= 4000, "PNG or small UTF-8 text only."); var text = new UTF8Encoding(false, true).GetString(m.Content); CreationRules.Input(new("Learner", "notes", text), ["notes"]); }
    }
    public Task<CreationRenderResult> RenderAsync(CreationRender r, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Func<string, string?> e = WebUtility.HtmlEncode; var b = new StringBuilder("<!doctype html><html lang=\"en-IN\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Private learning pack</title><style>@page{size:A4;margin:18mm}body{font:18px/1.6 system-ui;max-width:720px;margin:auto;color:#17202a}section{break-inside:avoid;border:1px solid #aaa;padding:16px;margin:18px 0}img{max-width:100%;max-height:260px}p,blockquote{overflow-wrap:anywhere;white-space:pre-wrap}h1{font-size:28px}footer{font-size:14px;break-inside:avoid}h2{break-after:avoid}@media print{body{font-size:16px;line-height:1.45}section{padding:12px;margin:12px 0}h1{font-size:24px}h2{font-size:20px}footer{font-size:12px}}</style><main>");
        b.Append("<h1>").Append(e(r.SourceTitle)).Append("</h1><p>Private personalized fiction · ").Append(e(r.Format)).Append(" · ").Append(e(r.Input.Theme)).Append("</p><section><h2>A fictional learner</h2><p>").Append(e(r.Input.Nickname)).Append(" pauses to reflect on ").Append(e(r.Input.Theme)).Append(" with ").Append(e(r.CharacterName)).Append(" as a reviewed fictional character. This original framing is fiction, not scripture.</p></section><section><h2>Reviewed source content</h2><blockquote>").Append(e(r.SourceBody)).Append("</blockquote></section><section><h2>Your reflection</h2><p>").Append(e(r.Input.Reflection)).Append("</p><p>What does the source say? What remains your interpretation?</p></section>");
        foreach (var m in r.Materials) { Validate(m); b.Append("<section><h2>").Append(e(m.AltText)).Append("</h2>"); if (m.MediaType == "image/png") b.Append("<img alt=\"").Append(e(m.AltText)).Append("\" src=\"data:image/png;base64,").Append(Convert.ToBase64String(m.Content)).Append("\">"); else b.Append("<p>").Append(e(Encoding.UTF8.GetString(m.Content))).Append("</p>"); b.Append("<p>Asset rights: ").Append(e(m.Rights)).Append(" · ").Append(e(m.Credits)).Append("</p></section>"); }
        b.Append("<footer><h2>Source credits and lineage</h2>"); foreach (var c in r.Citations) b.Append("<p>").Append(e(c.Citation)).Append(" · ").Append(e(c.RequiredAttribution)).Append(" · ").Append(e(c.Sha256)).Append("</p>"); b.Append("<p>Private use only. Sharing requires separate current review, consent and source permissions. Local template composition; no AI or external service received user data.</p></footer></main></html>"); return Task.FromResult(new CreationRenderResult(b.ToString(), 0, "INR"));
    }
    public Task CleanupAsync(Guid creationId, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.CompletedTask; }
}
