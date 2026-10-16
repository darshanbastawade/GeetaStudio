using System.Text.Json;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Domain;
namespace ContentOS.Foundation.Tests;

public sealed class FactoryScriptRuleTests
{
    [Fact]
    public void Only_owned_script_schema_and_pinned_speakers_are_accepted()
    {
        var id = Guid.NewGuid(); var script = new FactoryGeneratedScript("factory-script.v1", "Hook", "Scene", [new(id, "A fictional choice.")], "Decision", "Reflection");
        var json = JsonSerializer.Serialize(script, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(id, FactoryScriptRules.Parse(json, [id]).Dialogue.Single().CharacterId);
        Assert.Throws<RuntimeValidationException>(() => FactoryScriptRules.Parse(json, [Guid.NewGuid()]));
        Assert.Throws<RuntimeValidationException>(() => FactoryScriptRules.Parse(json.Replace("factory-script.v1", "unknown"), [id]));
        Assert.Throws<RuntimeValidationException>(() => FactoryScriptRules.Parse(json[..^1] + ",\"tool\":\"arbitrary\"}", [id]));
        Assert.Throws<RuntimeValidationException>(() => FactoryScriptRules.Parse("{not JSON", [id]));
    }
    [Fact]
    public void Factory_child_lease_proof_cannot_be_supplied_or_persisted_as_http_json()
    {
        var json = "{\"operationId\":\"" + Guid.NewGuid() + "\",\"definitionId\":\"" + Guid.NewGuid() + "\",\"inputs\":{},\"factoryAdmission\":{\"workId\":\"" + Guid.NewGuid() + "\",\"leaseToken\":\"" + Guid.NewGuid() + "\"}}";
        var r = JsonSerializer.Deserialize<StartWorkflow>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Null(r.FactoryAdmission);
        Assert.DoesNotContain("factoryAdmission", JsonSerializer.Serialize(r with { FactoryAdmission = new(Guid.NewGuid(), Guid.NewGuid()) }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
