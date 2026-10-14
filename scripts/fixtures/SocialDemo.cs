using System.Text.Json;
using System.Security.Principal;
using ContentOS.Application;
using ContentOS.Contracts;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using ContentOS.Foundation.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("The publishing Windows demo requires Windows.");
var repo=Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!;var directory=Path.Combine(repo,".local","m18-demo");Directory.CreateDirectory(directory);
using var input=JsonDocument.Parse(File.ReadAllText(Path.Combine(repo,".local","m17-demo","fixture.json")));var tenant=input.RootElement.GetProperty("tenant").GetGuid();var universe=input.RootElement.GetProperty("universe").GetGuid();var production=input.RootElement.GetProperty("production").GetGuid();var owner=new CoreScope(tenant,WindowsIdentity.GetCurrent().Name);
using var root=SocialTestFixture.Services();using var scope=root.CreateScope();var s=scope.ServiceProvider;var db=s.GetRequiredService<CoreDbContext>();var w=await s.GetRequiredService<IWatchFactory>().ExportAsync(owner,universe,production,"Export",default);var editorial=s.GetRequiredService<IEditorialWorkbench>();var script=w.Production.Snapshot.Script;var social=s.GetRequiredService<ISocialPublishing>();var before=await db.Revisions.AsNoTracking().SingleAsync(x=>x.Id==script.RevisionId);var image=w.Production.Timeline!.Scenes[0].Image;var context=new EditorialTestFixture.Fixture(owner,universe,w.Production.Timeline.AudienceId,Guid.Empty,Guid.Empty,Guid.Empty);var group=Guid.NewGuid().ToString("N")[..8];var packages=new List<SocialPackageView>();Guid unknownAccount=default;
foreach(var platform in new[]{"YouTube","Instagram","Facebook","X"})
{
    var capability=SocialChannelProfiles.Catalog.Single(x=>x.Platform==platform);var key="m18-"+platform.ToLowerInvariant()+"-"+group;var adapter=platform=="X"?"social.unknown.v1":"social.fixture.a.v1";var account=await social.RegisterAccountAsync(owner,universe,new(Guid.NewGuid(),key,1,platform,"fixture:"+platform.ToLowerInvariant(),adapter,"Fixture",capability.Key,capability.RequiredScopes,null,"fixture:explicit-no-live-account"),default);if(platform=="X")unknownAccount=account;
    // All four use the same approved scripture-free original fixture and exact spoken master.
    var format=platform=="Facebook"?"Article":platform=="X"?"Link":"Short";var package=await social.PrepareAsync(owner,universe,new(Guid.NewGuid(),key,script.Id,account,format,"m18-private-"+group,"",platform=="X"?"https://example.invalid/fixture":"",platform is "YouTube" or "Instagram"?[image.Id]:[],platform is "YouTube" or "Instagram"?production:null,MadeForKids:true),default);await social.ReviewAsync(owner,universe,package.Id,new(Guid.NewGuid(),package.RowVersion,package.PayloadHash,"Approve","Fixture","fixture:technical-package-not-human-review"),default);packages.Add((await social.PackagesAsync(owner,universe,default)).Single(x=>x.Id==package.Id));var export=await social.ExportAsync(owner,universe,package.Id,default);File.WriteAllText(Path.Combine(directory,platform+".json"),JsonSerializer.Serialize(export,new JsonSerializerOptions{WriteIndented=true}));
}
var x=packages.Single(p=>p.Snapshot!.Payload.Platform=="X");var publication=await social.ScheduleAsync(owner,universe,new(Guid.NewGuid(),x.Id,DateTimeOffset.UtcNow,"India Standard Time"),default);publication=await SocialTestFixture.Drain(s,context,publication.Id);if(publication.State!="Unknown")throw new Exception("Controlled timeout must retain Unknown.");
var binding=(await social.AccountsAsync(owner,universe,default)).Single(a=>a.Id==unknownAccount).Binding;await social.RegisterAccountAsync(owner,universe,binding with{OperationId=Guid.NewGuid(),Version=2,Adapter="social.fixture.b.v1"},default);
if(before.Body!=(await db.Revisions.AsNoTracking().SingleAsync(r=>r.Id==before.Id)).Body)throw new Exception("Channel packaging changed core text.");
var payload=new {tenant,universe,production,publication=publication.Id,package=x.Id,payloadHash=x.PayloadHash,packages=packages.Select(p=>new{p.Id,p.PayloadHash,platform=p.Snapshot!.Payload.Platform}),scheduledAdapter=publication.Adapter,accountReference=publication.AccountReference,coreRevision=before.Id,coreBodyPreserved=true,fixtureOnly=true,liveConnected=false};
File.WriteAllText(Path.Combine(directory,"fixture.json"),JsonSerializer.Serialize(payload,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Four reviewed private packages and original unknown publication prepared; no social platform contacted.");
namespace ContentOS.Foundation.Tests{internal static class ArchitectureTests{public static string RepositoryRoot=>Environment.GetEnvironmentVariable("GEETA_REPOSITORY")!;}}
