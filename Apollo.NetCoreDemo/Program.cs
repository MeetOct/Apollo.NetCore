

using Apollo.NetCore.Internals;
using Apollo.NetCore.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Apollo.NetCoreDemo
{

    public class ConfigService
    {
        public string Name { get; set; }
        public void OnChanged(object sender, ConfigChangeEventArgs changeEvent)
        {
            Console.WriteLine("Changes for namespace {0}", changeEvent.Namespace);
            foreach (string key in changeEvent.ChangedKeys)
            {
                ConfigChange change = changeEvent.GetChange(key);
                Console.WriteLine("Change - key: {0}, oldValue: {1}, newValue: {2}, changeType: {3}",
                    change.PropertyName, change.OldValue, change.NewValue, change.ChangeType);
            }
        }
    }

    public class Controller
    {
        public readonly DefaultConfig _config;

        public Controller(DefaultConfig config)
        {
            _config = config;
        }

        public async Task ShowApolloConfig(HttpContext context)
        {
            await context.Response.WriteAsync(_config.GetProperty("name","test"));
        }
    }

    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                // reloadOnChange: true is required for config changes to be detected.
                .AddJsonFile("config.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; set; }

        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory, IApplicationLifetime applicationLifetime)
        {
            loggerFactory.AddConsole(LogLevel.Error);
            //1. 轮询读取配置
            //2.长连接推送通知实现实时更新

            applicationLifetime.ApplicationStarted.Register(()=> 
            {
                var config = app.ApplicationServices.GetService<DefaultConfig>();
                var service = app.ApplicationServices.GetService<ConfigService>();
                config.ConfigChanged += service.OnChanged;
            });
            app.Run(DisplayTimeAsync);
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Simple mockup of a simple per request controller.
            services.AddScoped<Controller>();
            services.AddSingleton<RemoteConfigRepository>();
            services.AddSingleton<DefaultConfig>();
            services.AddSingleton<ConfigService>();
            // Binds config.json to the options and setups the change tracking.
            services.Configure<ApolloSettings>(Configuration.GetSection("Apollo"));
        }

        public Task DisplayTimeAsync(HttpContext context)
        {
            context.Response.ContentType = "text/plain";
            return context.RequestServices.GetRequiredService<Controller>().ShowApolloConfig(context);
        }

        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseStartup<Startup>()
                .Build();
            host.Run();
        }
    }
}
