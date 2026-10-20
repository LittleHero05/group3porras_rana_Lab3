using group3porras_rana_Lab3.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
// AWS settings (profile + region) from appsettings
var awsOptions = builder.Configuration.GetAWSOptions();
builder.Services.AddDefaultAWSOptions(awsOptions);

// Read DB credentials from AWS Systems Manager Parameter Store (SecureString)
var parameterPath = builder.Configuration["ParameterPath"];
string dbUser, dbPassword;

using (var ssm = awsOptions.CreateServiceClient<IAmazonSimpleSystemsManagement>())
{
    var response = await ssm.GetParametersAsync(new GetParametersRequest
    {
        Names = new List<string> { $"{parameterPath}/DbUser", $"{parameterPath}/DbPassword" },
        WithDecryption = true   // decrypts the SecureString values
    });

    if (response.InvalidParameters?.Count > 0)
        throw new InvalidOperationException(
            "Missing parameters in Parameter Store: " + string.Join(", ", response.InvalidParameters));

    var values = response.Parameters.ToDictionary(p => p.Name, p => p.Value);
    dbUser = values[$"{parameterPath}/DbUser"];
    dbPassword = values[$"{parameterPath}/DbPassword"];
}

// Build the connection string from config + parameters (no password in any file)
var connectionString = new SqlConnectionStringBuilder
{
    DataSource = builder.Configuration["Database:Server"],
    InitialCatalog = builder.Configuration["Database:Name"],
    UserID = dbUser,
    Password = dbPassword,
    TrustServerCertificate = true
}.ConnectionString;

builder.Services.AddDbContext<LearningPortalContext>(options =>
    options.UseSqlServer(connectionString));


// NEW: cookie login
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";              // where to send users who aren't logged in
        options.AccessDeniedPath = "/Account/AccessDenied"; // where to send students who try instructor pages
        options.ExpireTimeSpan = TimeSpan.FromHours(8);    // stay logged in for 8 hours
    });

var app = builder.Build();


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
