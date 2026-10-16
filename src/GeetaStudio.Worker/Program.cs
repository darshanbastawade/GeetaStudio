using ContentOS.Application;
using ContentOS.Domain;
using ContentOS.Infrastructure;
using ContentOS.Integrations;
using GeetaStudio.Worker;

var builder = Host.CreateApplicationBuilder(args);
if (!builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Runtime:Enabled"))
    throw new InvalidOperationException("The proposed READ/WATCH manual-export pilot does not enable the execution worker. Keep Runtime:Enabled=false outside Development.");
if (builder.Configuration.GetValue<bool>("Runtime:Enabled"))
{
    builder.Services.AddContentRuntime(builder.Configuration, builder.Environment.IsDevelopment());
    foreach (var key in RuntimeRules.Adapters)
        builder.Services.AddScoped<ITestOperationProvider>(s => new TestOperationProvider(key, s.GetRequiredService<ITestEffectLedger>()));
}
builder.Services.AddHostedService<Worker>();
builder.Build().Run();
