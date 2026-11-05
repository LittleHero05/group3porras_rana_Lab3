using Amazon.DynamoDBv2;
using Amazon.S3;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using group3porras_rana_Lab3.Models;
using group3porras_rana_Lab3.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();

// Keep the existing configuration-driven AWS authentication (local profile or EC2 role).
var awsOptions = builder.Configuration.GetAWSOptions();
builder.Services.AddDefaultAWSOptions(awsOptions);
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddAWSService<IAmazonDynamoDB>();
builder.Services.AddScoped<S3Service>();
builder.Services.AddScoped<CommentService>();

var parameterPath = builder.Configuration["ParameterPath"];
if (string.IsNullOrWhiteSpace(parameterPath))
    throw new InvalidOperationException("Set ParameterPath in configuration.");

using (var ssm = awsOptions.CreateServiceClient<IAmazonSimpleSystemsManagement>())
{
    var names = new List<string> { $"{parameterPath}/DbUser", $"{parameterPath}/DbPassword" };
    var response = await ssm.GetParametersAsync(new GetParametersRequest
    {
        Names = names,
        WithDecryption = true
    });

    if (response.InvalidParameters is { Count: > 0 })
        throw new InvalidOperationException("Missing SSM parameters: " + string.Join(", ", response.InvalidParameters));

    var values = response.Parameters.ToDictionary(p => p.Name, p => p.Value);
    if (!values.TryGetValue(names[0], out var dbUser) ||
        !values.TryGetValue(names[1], out var dbPassword))
        throw new InvalidOperationException("SSM did not return both database credentials.");

    var server = builder.Configuration["Database:Server"];
    var database = builder.Configuration["Database:Name"];
    if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
        throw new InvalidOperationException("Set Database:Server and Database:Name.");

    var connectionString = new SqlConnectionStringBuilder
    {
        DataSource = server,
        InitialCatalog = database,
        UserID = dbUser,
        Password = dbPassword,
        Encrypt = true,
        // Beanstalk's Linux server doesn't have the Amazon RDS CA installed,
        // so certificate-chain validation fails. Traffic is still encrypted (Encrypt = true).
        TrustServerCertificate = true
    }.ConnectionString;

    builder.Services.AddDbContext<LearningPortalContext>(options =>
        options.UseSqlServer(connectionString));
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    // Elastic Beanstalk's reverse proxy supplies X-Forwarded-Proto.
    // Configure known proxies/networks for production as appropriate.
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });
}

// Beanstalk single-instance may start with HTTP only. Only redirect when HTTPS exists.
if (app.Environment.IsDevelopment()) app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();