using Asp.Versioning;
using CompanyEmployees.Presentation.Controllers;
using Contracts;
using Entities.ConfigurationModels;
using Entities.Models;
using LoggerService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Repository;
using Service;
using Service.Contracts;
using System.Text;
using System.Threading.RateLimiting;

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
            services.AddScoped<IServiceManager, ServiceManager>();

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


        public static void ConfigureRateLimitingOptions(this IServiceCollection services)
        {
            services.AddRateLimiter(opt =>
            {

                opt.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter("GlobalLimiter",
                partition => new FixedWindowRateLimiterOptions
                {
                    //At this point, we can start the app and send five requests from Postman.
                    //All of those should return the expected result. But, if we send the sixth
                    //request before one minute period expires
                    //----------------------------------------------------
                    AutoReplenishment = true,
                    PermitLimit = 30,

                    //==========Rate Limiter Queues=========
                    QueueLimit = 2,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    //======================================
                    Window = TimeSpan.FromMinutes(1)
                    //----------------------------------------------------

                }));

                // Policies With Rate Limiting  ( create specific limit rules of specific endpoints)
                opt.AddPolicy("SpecificPolicy", context =>
                 RateLimitPartition.GetFixedWindowLimiter("SpecificLimiter",
                 partition => new FixedWindowRateLimiterOptions
                 {
                     AutoReplenishment = true,
                     PermitLimit = 30,
                     Window = TimeSpan.FromSeconds(10)
                 }));

                //Rejection Configuration -   rate limiter denies the request should be 429
                //opt.RejectionStatusCode = 429;

                // we can use the OnRejected Func delegate instead of the RejectionStatusCode property to additionally modify the response after
                // a user exceeds the number of requests
                opt.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.StatusCode = 429;
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                        await context.HttpContext.Response.WriteAsync($"Too many requests. Please try again after {retryAfter.TotalSeconds} second(s).", token);
                    else
                        await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", token);
                };
            });
        }


        public static void ConfigureIdentity(this IServiceCollection services)
        {
            var builder = services.AddIdentity<User, IdentityRole>(o =>
            {
                o.Password.RequireDigit = true;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredLength = 10;
                o.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<RepositoryContext>()
            .AddDefaultTokenProviders();
        }

        public static void ConfigureJWT(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtConfiguration = new JwtConfiguration();
            configuration.Bind(jwtConfiguration.Section, jwtConfiguration);

            var secretKey = Environment.GetEnvironmentVariable("SECRET");
            services.AddAuthentication(opt =>
            {
                opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtConfiguration.ValidIssuer,
                    ValidAudience = jwtConfiguration.ValidAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
                };
            });
        }

        public static void AddJwtConfiguration(this IServiceCollection services, IConfiguration configuration) =>
            services.Configure<JwtConfiguration>(configuration.GetSection("JwtSettings"));

        public static void ConfigureSwagger(this IServiceCollection services)
        {
            services.AddSwaggerGen(s =>
            {
                s.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Code Maze API",
                    Version = "v1",
                    Description = "CompanyEmployees API by CodeMaze",
                    TermsOfService = new Uri("https://example.com/terms"),
                    Contact = new OpenApiContact
                    {
                        Name = "John Doe",
                        Email = "John.Doe@gmail.com",
                        Url = new Uri("https://twitter.com/johndoe"),
                    },
                    License = new OpenApiLicense
                    {
                        Name = "CompanyEmployees API LICX",
                        Url = new Uri("https://example.com/license"),
                    }
                });
                s.SwaggerDoc("v2", new OpenApiInfo
                {
                    Title = "Code Maze API",
                    Version = "v2"
                });

                var xmlFile = $"{typeof(Presentation.AssemblyReference).Assembly.GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                s.IncludeXmlComments(xmlPath);

                s.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Description = "Place to add JWT with Bearer",
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
                s.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                            Name = "Bearer",
                        },
                        new List<string>()
                    }
                });
            });
        }











    }
}
