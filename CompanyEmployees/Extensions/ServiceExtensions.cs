using Contracts;
using LoggerService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Repository;
using Service;
using Service.Contracts;
using static NLog.LayoutRenderers.Wrappers.ReplaceLayoutRendererWrapper;
using System.Runtime.Intrinsics.X86;

namespace CompanyEmployees.Extensions
{
    public static class ServiceExtensions
    {
        public static void ConfigureCors(this IServiceCollection services) =>
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", builder =>
                    builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .WithExposedHeaders("X-Pagination"));
            });

        public static void ConfigureIISIntegration(this IServiceCollection services) =>
            services.Configure<IISOptions>(options =>
            {

            });

        public static void ConfigureLoggerService(this IServiceCollection services) =>
            services.AddSingleton<ILoggerManager, LoggerManager>();

        public static void ConfigureRepositoryManager(this IServiceCollection services) => 
            services.AddScoped<IRepositoryManager, RepositoryManager>();

        public static void ConfigureServiceManager(this IServiceCollection services) =>
            services.AddScoped <IServiceManager, ServiceManager>();

        public static void ConfigureSqlContext(this IServiceCollection services, IConfiguration configuration) =>
            services.AddDbContext<RepositoryContext>(opts => opts.UseSqlServer(configuration.GetConnectionString("sqlConnection")));

        //From.NET 6 RC2, there is a shortcut method AddSqlServer, which can be used like this:
        //public static void ConfigureSqlContex(this IServiceCollection services, IConfiguration configuration) =>
        //    services.AddSqlServer<RepositoryContext>((configuration.GetConnectionString("sqlConnection")));

        //This method replaces both AddDbContext and UseSqlServer methods and allows an easier configuration.But it doesn’t provide all of the
        //features the AddDbContext method provides.So for more advanced options, it is recommended to use AddDbContext.

        public static IMvcBuilder AddCustomCSVFormatter(this IMvcBuilder builder) =>
            builder.AddMvcOptions(config => config.OutputFormatters.Add(new
            CsvOutputFormatter()));


    }
}
