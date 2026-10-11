using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ContentOS.Infrastructure;

public sealed class RuntimeDesignFactory : IDesignTimeDbContextFactory<RuntimeDbContext>
{
    public RuntimeDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<RuntimeDbContext>()
        .UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__ContentOS") ??
            "Server=localhost;Database=GeetaStudio_Dev;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;").Options);
}
