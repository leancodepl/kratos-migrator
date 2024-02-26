using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ory.Client.Api;
using Ory.Client.Client;

var sc = new ServiceCollection();
sc.AddDbContext<IdentityDbContext>(cfg =>
    cfg.UseSqlServer(Environment.GetEnvironmentVariable("CONNECTION_STRING"))
);
sc.AddIdentityCore<User>().AddEntityFrameworkStores<IdentityDbContext>();
sc.AddTransient(sp => new IdentityApi(
    new Configuration
    {
        BasePath = Environment.GetEnvironmentVariable("KRATOS_ADMIN_URL"),
        AccessToken = Environment.GetEnvironmentVariable("KRATOS_ACCESS_TOKEN")!,
    }
));
sc.AddLogging(cfg => cfg.AddConsole());
sc.AddTransient<Migrator>();
var sp = sc.BuildServiceProvider();

await sp.GetRequiredService<Migrator>().RunAsync();
