using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ILLiasu_Local_Mall.Infrastructure.Identity;
using ILLiasu_Local_Mall.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Integration smoke tests against the explicitly approved local Development database.
// Requires the app running at https://localhost:7116; fixtures are removed in finally.
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
    ApplicationName = typeof(ApplicationDbContext).Assembly.GetName().Name,
    ContentRootPath = root, EnvironmentName = "Development"
});
IdentityProvisioning.RequireLocalDevelopment(builder.Environment, builder.Configuration.GetConnectionString("DefaultConnection"));
builder.Logging.ClearProviders();
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
await using var app = builder.Build();
await using var scope = app.Services.CreateAsyncScope();
var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
var fixtureEmails = new List<string>();
string Password() => "Aa1!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
string Email() { var email = "identity-smoke-" + Guid.NewGuid().ToString("N") + "@example.invalid"; fixtureEmails.Add(email); return email; }
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
HttpClient Client() => new(new HttpClientHandler {
    AllowAutoRedirect = false, CookieContainer = new CookieContainer(),
    // Only this fixed loopback HTTPS endpoint is used; never a production endpoint.
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
}) { BaseAddress = new Uri("https://localhost:7116") };
async Task<HttpResponseMessage> Post(HttpClient client, string path, Dictionary<string,string> values)
{
    var html = await client.GetStringAsync(path);
    var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
    if (!match.Success) throw new Exception("Antiforgery token not found.");
    values["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value);
    return await client.PostAsync(path, new FormUrlEncodedContent(values));
}
try
{
    await IdentityProvisioning.SeedRolesAsync(roles);
    await IdentityProvisioning.SeedRolesAsync(roles);
    Check(await roles.Roles.CountAsync() == 4, "Idempotent four-role seeding");
    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();
    await using(var command = connection.CreateCommand()) {
        command.CommandText = "SELECT name FROM sys.tables WHERE name LIKE 'AspNet%' ORDER BY name";
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>(); while(await reader.ReadAsync()) names.Add(reader.GetString(0));
        Check(names.Order(StringComparer.Ordinal).SequenceEqual(new[]{"AspNetRoleClaims","AspNetRoles","AspNetUserClaims","AspNetUserLogins","AspNetUserRoles","AspNetUserTokens","AspNetUsers"}.Order(StringComparer.Ordinal)), "Seven standard Identity tables");
    }
    using var customer = Client();
    Check((await customer.GetAsync("/")).StatusCode == HttpStatusCode.OK, "Storefront accessible anonymously");
    Check((await customer.GetAsync("/Account/Login")).StatusCode == HttpStatusCode.OK, "Login page renders");
    var anonymous = await customer.GetAsync("/Admin");
    Check(anonymous.StatusCode == HttpStatusCode.Redirect && anonymous.Headers.Location!.ToString().Contains("/Account/Login"), "Anonymous Admin challenge");
    var missingCsrf = await customer.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string,string>()));
    Check(missingCsrf.StatusCode == HttpStatusCode.BadRequest, "Registration rejects missing antiforgery");
    var email = Email(); var password = Password();
    var registered = await Post(customer, "/Account/Register", new() {
        ["Email"] = email, ["Password"] = password, ["ConfirmPassword"] = password,
        ["Roles"] = "Admin", ["ReturnUrl"] = "https://example.invalid/escape"
    });
    Check(registered.StatusCode == HttpStatusCode.Redirect && registered.Headers.Location!.ToString() == "/Account", "Registration and safe return URL");
    var cookie = registered.Headers.GetValues("Set-Cookie").First(v => v.StartsWith("__Host-ILLiasu.Identity="));
    Check(cookie.Contains("secure",StringComparison.OrdinalIgnoreCase) && cookie.Contains("httponly",StringComparison.OrdinalIgnoreCase) && cookie.Contains("samesite=lax",StringComparison.OrdinalIgnoreCase), "Secure HttpOnly SameSite cookie");
    db.ChangeTracker.Clear();
    var newUser = (await users.FindByEmailAsync(email))!;
    Check((await users.GetRolesAsync(newUser)).SequenceEqual(new[]{AppRoles.Customer}), "Public registration gets Customer only despite role injection");
    foreach(var route in new[]{"/Admin","/Admin/Overview","/Admin/Overview/Index","/Overview/Index"}) {
        var denied = await customer.GetAsync(route);
        Check(denied.StatusCode == HttpStatusCode.NotFound || (denied.StatusCode == HttpStatusCode.Redirect && denied.Headers.Location!.ToString().Contains("/Account/AccessDenied")), "Customer denied " + route);
    }
    Check((await customer.GetAsync("/Account/AccessDenied")).StatusCode == HttpStatusCode.Forbidden, "Access denied page returns 403");
    await customer.GetAsync("/Account/Logout");
    Check((await customer.GetAsync("/Account")).StatusCode == HttpStatusCode.OK, "GET logout does not sign out");
    Check((await customer.PostAsync("/Account/Logout",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode == HttpStatusCode.BadRequest, "Logout rejects missing antiforgery");
    Check((await Post(customer,"/Account/Logout",new())).StatusCode == HttpStatusCode.Redirect, "POST logout succeeds");
    Check((await customer.GetAsync("/Account")).StatusCode == HttpStatusCode.Redirect, "Logged-out account challenged");
    Check((await Post(customer,"/Account/Login",new(){["Email"]=email,["Password"]=password})).StatusCode == HttpStatusCode.Redirect, "Customer login succeeds");
    await Post(customer,"/Account/Logout",new());
    for(var i=0;i<5;i++) await Post(customer,"/Account/Login",new(){["Email"]=email,["Password"]=Password()});
    db.ChangeTracker.Clear(); newUser=(await users.FindByEmailAsync(email))!;
    Check(await users.IsLockedOutAsync(newUser), "Five failed logins trigger lockout");
    Check((await Post(customer,"/Account/Login",new(){["Email"]=email,["Password"]=password})).StatusCode == HttpStatusCode.OK, "Locked account cannot sign in with correct password");
    using var weak = Client();
    var weakEmail = Email();
    await Post(weak,"/Account/Register",new(){["Email"]=weakEmail,["Password"]="weak",["ConfirmPassword"]="weak"});
    Check(await users.FindByEmailAsync(weakEmail) is null, "Weak password rejected");
    foreach(var role in new[]{AppRoles.InventoryManager,AppRoles.SalesStaff,AppRoles.Admin}) {
        var staffEmail=Email(); var staffPassword=Password();
        var staff=new ApplicationUser {UserName=staffEmail,Email=staffEmail};
        Check((await users.CreateAsync(staff,staffPassword)).Succeeded && (await users.AddToRoleAsync(staff,role)).Succeeded,"Create temporary " + role + " test fixture");
        using var staffClient=Client();
        await Post(staffClient,"/Account/Login",new(){["Email"]=staffEmail,["Password"]=staffPassword});
        var result=await staffClient.GetAsync("/Admin");
        Check(role==AppRoles.Admin ? result.StatusCode==HttpStatusCode.OK : result.StatusCode==HttpStatusCode.Redirect && result.Headers.Location!.ToString().Contains("AccessDenied"),role + " authorization");
        if(role==AppRoles.Admin) await users.DeleteAsync(staff);
    }
    builder.Configuration["BootstrapAdmin:Email"] = email;
    builder.Configuration["BootstrapAdmin:Password"] = Password();
    var blockedElevation = false;
    try { await IdentityProvisioning.RunAsync(app.Services,builder.Environment,builder.Configuration,true); }
    catch(InvalidOperationException) { blockedElevation = true; }
    Check(blockedElevation && !await users.IsInRoleAsync(newUser,AppRoles.Admin), "Bootstrap refuses to elevate a public customer");
    var existingAdmins=await users.GetUsersInRoleAsync(AppRoles.Admin);
    if(existingAdmins.Count==0)
    {
        var bootstrapEmail=Email();
        builder.Configuration["BootstrapAdmin:Email"] = bootstrapEmail;
        builder.Configuration["BootstrapAdmin:Password"] = Password();
        await IdentityProvisioning.RunAsync(app.Services,builder.Environment,builder.Configuration,true);
        await IdentityProvisioning.RunAsync(app.Services,builder.Environment,builder.Configuration,true);
        Check((await users.GetUsersInRoleAsync(AppRoles.Admin)).Count==1, "First-admin provisioning is idempotent");
    }
    else
    {
        var existing=existingAdmins[0];
        var existingHash=existing.PasswordHash;
        builder.Configuration["BootstrapAdmin:Email"]=existing.Email;
        builder.Configuration["BootstrapAdmin:Password"]=Password();
        await IdentityProvisioning.RunAsync(app.Services,builder.Environment,builder.Configuration,true);
        db.ChangeTracker.Clear();
        Check((await users.FindByIdAsync(existing.Id))!.PasswordHash==existingHash,"Existing Admin credentials preserved on repeated bootstrap");
        builder.Configuration["BootstrapAdmin:Email"]=Email();
        var blockedSecond=false;
        try{await IdentityProvisioning.RunAsync(app.Services,builder.Environment,builder.Configuration,true);}
        catch(InvalidOperationException){blockedSecond=true;}
        Check(blockedSecond && (await users.GetUsersInRoleAsync(AppRoles.Admin)).Count==existingAdmins.Count,"Existing administrators preserved; second bootstrap refused");
    }
    var guarded = false;
    var priorEnvironment=builder.Environment.EnvironmentName;
    builder.Environment.EnvironmentName="Production";
    try { await IdentityProvisioning.RunAsync(app.Services,builder.Environment,builder.Configuration,true); }
    catch(InvalidOperationException) { guarded=true; }
    finally { builder.Environment.EnvironmentName=priorEnvironment; }
    Check(guarded,"Admin provisioning refuses Production before database access");
    Console.WriteLine("All authentication smoke tests passed.");
}
finally
{
    db.ChangeTracker.Clear();
    foreach(var email in fixtureEmails) {
        var user=await users.FindByEmailAsync(email);
        if(user is not null && !(await users.DeleteAsync(user)).Succeeded) throw new Exception("Test fixture cleanup failed.");
    }
    Console.WriteLine("Temporary authentication accounts removed.");
}
