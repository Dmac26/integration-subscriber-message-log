using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wachter.Enterprise.Configuration;
using Serilog;
using Wachter.IntegrationSubscriberMessageLog.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Wachter.IntegrationSubscriberMessageLog.Models;
using Wachter.IntegrationSubscriberMessageLog.Resolvers;

namespace Wachter.IntegrationSubscriberMessageLog
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(@"C:\Logs\IntegrationSubscriberMessageLog\log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            KeeperResolver.Initialize();

            try
            {
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    options.ServiceName = "Wachter.IntegrationSubscriberMessageLog";
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<MessageLogService>();

                    // Your real config resolver, e.g.:
                    //services.AddSingleton<IConfigurationResolver, ActualConfigurationResolverClass>();

                    // DbContext with connection string from config
                    //services.AddDbContext<IntegrationMessageLogDbContext>(options =>
                    //    options.UseSqlServer(hostContext.Configuration.GetConnectionString("YourConnectionStringName")));

                    services.AddDbContext<IntegrationMessageLogDbContext>(options =>
                        options.UseSqlServer(hostContext.Configuration.GetConnectionString("IntegrationMessageLog")));
                });
        //.UseSerilog();
    }
}