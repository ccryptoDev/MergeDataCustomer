using MergeDataCustomer.Helpers.Configuration;
using MergeDataImporter.Application.Interfaces;
using MergeDataImporter.Application.Services;
using MergeDataImporter.Helpers.Enums;
using MergeDataImporter.Helpers.Generic;
using MergeDataImporter.Repositories.Context;
using MergeDataImporter.Repositories.Contracts;
using MergeDataEntities.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using MergeDataCustomer.Application.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("Layer3", new OpenApiInfo()
    {
        Version = "Layer3",
        Title = "API MergeDataCustomer - Layer 3",
        Description = "The layer 3 of MergeData manages his versioning starting with 3 and following with the API version. For example: if 3.1 is the first version, 3.12 could be the next one."
    });

    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
});

//to avoid AmbiguousMatchException:
builder.Services.AddApiVersioning(options => {
    options.DefaultApiVersion = Microsoft.AspNetCore.Mvc.ApiVersion.Parse(ApiVersioning.CurrentVersion);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});


builder.Services.AddCors();

//reutilize dbContext from MDI layer 1
builder.Services.AddDbContext<RawContext>();

builder.Services.AddIdentity<User, Role>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<RawContext>()
    .AddDefaultTokenProviders();


//authorization / authentication section
builder.Services.AddAuthentication(authentication =>
{
    authentication.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    authentication.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(bearer =>
    {
        bearer.RequireHttpsMetadata = false;
        bearer.SaveToken = true;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("S0M3RAN0MS3CR3T!1!MAG1C!1!")),
            ValidateIssuer = false,
            ValidateAudience = false,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var prop in typeof(Permissions).GetNestedTypes().SelectMany(c => c.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)))
    {
        var propertyValue = prop.GetValue(null);
        if (propertyValue is not null)
        {
            options.AddPolicy(propertyValue.ToString(), policy => policy.RequireClaim(ApplicationClaimTypes.Permission, propertyValue.ToString()));
        }
    }
});

//reimported services
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddTransient<ITokenService, IdentityService>();
builder.Services.AddTransient<IAccountService, AccountService>();
builder.Services.AddScoped<ReportService>();

//own services
builder.Services.AddScoped<StoreService>();
builder.Services.AddScoped<GeneralService>();

builder.Services.AddTransient<IMailService, SMTPMailService>();

//add infrastructure mappings
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

//swagger auth definitions
builder.Services.AddSwaggerGen((c) =>
{
    c.SchemaFilter<EnumSchemaFilter>();
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
                Scheme = "Bearer",
                Name = "Bearer",
                In = ParameterLocation.Header,
            }, new List<string>()
        },
    });
});

var app = builder.Build();

app.UseCors(opts =>
        opts.AllowAnyMethod()
            .AllowAnyHeader()
            .SetIsOriginAllowed(origin => true)
            .AllowCredentials());

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
app.UseHttpsRedirection();
app.UseMiddleware<ErrorHandlerMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/Layer3/swagger.json", typeof(Program).Assembly.GetName().Name);
    options.RoutePrefix = "swagger";
    options.DisplayRequestDuration();
});

app.UseStaticFiles();

app.Run();
