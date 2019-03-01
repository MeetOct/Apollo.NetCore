

using Apollo.NetCore.Internals;
using Apollo.NetCore.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Apollo.NetCoreDemo
{

    public class ConfigService
    {
        IOptionsMonitor<MonitorTest> _test;
        public ConfigService(IOptionsMonitor<MonitorTest> test)
        {
            Task.Factory.StartNew(() => 
            {
                while (true)
                {
                    Thread.Sleep(2000);
                    Console.WriteLine($"Current ConfigService MonitorTest is:{JsonConvert.SerializeObject(_test.CurrentValue)}");
                }
            });
            _test = test;
            _test.OnChange(OnChanged);
        }
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

        private void OnChanged(MonitorTest value)
        {
            Console.WriteLine("ConfigService MonitorTest has changed: " + JsonConvert.SerializeObject(value));
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

        private void OnChanged(MonitorTest value)
        {
            Console.WriteLine("MonitorTest has changed: " + JsonConvert.SerializeObject(value));
        }

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
                //注册通知事件
                var config = app.ApplicationServices.GetService<DefaultConfig>();
                var service = app.ApplicationServices.GetService<ConfigService>();
                config.ConfigChanged += service.OnChanged;

                var optionsMonitor = app.ApplicationServices.GetService<IOptionsMonitor<MonitorTest>>();

                optionsMonitor.OnChange(OnChanged);
            });
            app.Run(DisplayTimeAsync);
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Simple mockup of a simple per request controller.
            services.AddScoped<Controller>();

            services.AddSingleton(f => (ConfigServiceLocator)Activator.CreateInstance(typeof(ConfigServiceLocator), f.GetService<IOptions<ApolloSettings>>(), f.GetService<ILoggerFactory>()));

            services.AddSingleton(f => (RemoteConfigRepository)Activator.CreateInstance(typeof(RemoteConfigRepository), f.GetService<IOptions<ApolloSettings>>(), f.GetService<ILoggerFactory>(), f.GetService<ConfigServiceLocator>(), "application"));

            services.AddSingleton(f => (DefaultConfig)Activator.CreateInstance(typeof(DefaultConfig), f.GetService<RemoteConfigRepository>(), f.GetService<ILoggerFactory>(), "application"));
            services.AddSingleton(f=> (ConfigService)Activator.CreateInstance(typeof(ConfigService),f.GetService<IOptionsMonitor<MonitorTest>>()));
            // Binds config.json to the options and setups the change tracking.
            services.Configure<ApolloSettings>(Configuration.GetSection("Apollo"));
            services.Configure<MonitorTest>(Configuration.GetSection("MonitorTest"));
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
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .Build();
            host.Run();
        }
    }
}
