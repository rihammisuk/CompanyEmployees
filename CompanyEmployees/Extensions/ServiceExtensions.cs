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
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using CompanyEmployees.Presentation.Controllers;
using System;

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

        public static void AddCustomMediaTypes(this IServiceCollection services)
        {
            services.Configure<MvcOptions>(config =>
            {
                var systemTextJsonOutputFormatter = config.OutputFormatters
                .OfType<SystemTextJsonOutputFormatter>()?
                .FirstOrDefault();
                if (systemTextJsonOutputFormatter != null)
                {
                    systemTextJsonOutputFormatter.SupportedMediaTypes
                    .Add("application/vnd.codemaze.hateoas+json");
                    systemTextJsonOutputFormatter.SupportedMediaTypes
                    .Add("application/vnd.codemaze.apiroot+json");
                }
                var xmlOutputFormatter = config.OutputFormatters
                .OfType<XmlDataContractSerializerOutputFormatter>()?
                .FirstOrDefault();
                if (xmlOutputFormatter != null)
                {
                    xmlOutputFormatter.SupportedMediaTypes
                    .Add("application/vnd.codemaze.hateoas+xml");
                    xmlOutputFormatter.SupportedMediaTypes
                    .Add("application/vnd.codemaze.apiroot+xml");
                }
            });
        }

        public static void ConfigureVersioning(this IServiceCollection services)
        {
            // Normal Versioning
            //services.AddApiVersioning(opt =>
            //{
            //    opt.ReportApiVersions = true;
            //    opt.AssumeDefaultVersionWhenUnspecified = true;
            //    opt.DefaultApiVersion = new ApiVersion(1, 0);
            //    //opt.ApiVersionReader = new HeaderApiVersionReader("api-version"); // HTTP Header Versioning
            //    opt.ApiVersionReader = new QueryStringApiVersionReader("api-version"); //QueryString
            //}).AddMvc();

            //Using Conventions
            //If we have a lot of versions of a single controller, we can assign these versions in the configuration instead
            //Now, we can remove the [ApiVersion] attribute from the controllers
            services.AddApiVersioning(opt =>
            {
                opt.ReportApiVersions = true;
                opt.AssumeDefaultVersionWhenUnspecified = true;
                opt.DefaultApiVersion = new ApiVersion(1, 0);
                opt.ApiVersionReader = new HeaderApiVersionReader("api-version");
            })
             .AddMvc(opt =>
             {
                 opt.Conventions.Controller<CompaniesController>()
                 .HasApiVersion(new ApiVersion(1, 0));
                 opt.Conventions.Controller<CompaniesV2Controller>()
                 .HasDeprecatedApiVersion(new ApiVersion(2, 0));
             });
        }

        //For Response Caching
        //public static void ConfigureResponseCaching(this IServiceCollection services) =>
        //    services.AddResponseCaching();

        //For Output Caching
        public static void ConfigureOutputCaching(this IServiceCollection services) =>
             services.AddOutputCache(opt =>
             {
                 //opt.AddBasePolicy(bp => bp.Expire(TimeSpan.FromSeconds(10)));
                 opt.AddPolicy("120SecondsDuration", p => p.Expire(TimeSpan.FromSeconds(120)));
                 opt.AddPolicy("QueryParamDuration", p => p.Expire(TimeSpan.FromSeconds(10)).SetVaryByQuery("firstKey"));

             });
    }
}
