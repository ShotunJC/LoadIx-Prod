using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using LoadIx.Api.Endpoints;
using LoadIx.Api.Security;
using LoadIx.Application.Security;
using LoadIx.Domain.Common;
using LoadIx.Domain.Tenancy;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Seed;
using LoadIx.IntegrationTests.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace LoadIx.IntegrationTests.Api;

[Collection(MsSqlDatabaseGroup.Name)]
public sealed class AuthenticationEndpointTests(
    MsSqlDatabaseFixture database) : IAsyncLifetime
{
    private const string AdministratorEmail =
        DevelopmentIdentitySeeder.AdministratorEmail;
    private const string AdministratorPassword = "LoadIx Demo Admin 2026!";
    private readonly List<WebApplicationFactory<Program>> _factories = [];
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            database.ConnectionString)
        {
            InitialCatalog = $"LoadIx_Auth_{Guid.NewGuid():N}",
        };
        _connectionString = builder.ConnectionString;
        var (factory, _) = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        await dbContext.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>()
            .SeedAsync(CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<DevelopmentIdentitySeeder>()
            .SeedAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        foreach (var factory in _factories)
        {
            factory.Dispose();
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ValidLoginReturnsAccessTokenAndSecureRefreshCookie()
    {
        var (factory, _) = CreateFactory();
        var client = factory.CreateClient();

        var response = await LoginAsync(
            client,
            AdministratorEmail,
            AdministratorPassword);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        payload.Should().NotBeNull();
        payload!.AccessToken.Should().NotBeNullOrWhiteSpace();
        payload.User.Permissions.Should().Contain(LoadIxPermissions.UserManage);
        var cookie = response.Headers.GetValues("Set-Cookie").Single();
        cookie.Should().ContainEquivalentOf("HttpOnly");
        cookie.Should().ContainEquivalentOf("SameSite=Strict");
        cookie.Should().NotContain(payload.AccessToken);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload.AccessToken);
        (await client.GetAsync("/api/v1/auth/me")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        var sessions = await client.GetFromJsonAsync<SessionResponse[]>(
            "/api/v1/auth/sessions");
        sessions.Should().ContainSingle(session => session.IsCurrent);
    }

    [Fact]
    public async Task InvalidLoginIsRejectedWithoutAccountDisclosure()
    {
        var (factory, _) = CreateFactory();
        var client = factory.CreateClient();

        var known = await LoginAsync(client, AdministratorEmail, "wrong-password");
        var unknown = await LoginAsync(
            client,
            "missing@loadix.example.invalid",
            "wrong-password");

        known.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var knownProblem = await known.Content.ReadFromJsonAsync<ProblemResponse>();
        var unknownProblem =
            await unknown.Content.ReadFromJsonAsync<ProblemResponse>();
        knownProblem.Should().BeEquivalentTo(unknownProblem);
    }

    [Fact]
    public async Task RepeatedInvalidLoginLocksTheAccount()
    {
        var email = $"lockout-{Guid.NewGuid():N}@loadix.example.invalid";
        await CreateTenantUserAsync(email, "Correct Password 2026!");
        var (factory, _) = CreateFactory();
        var client = factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await LoginAsync(client, email, "wrong-password")).StatusCode
                .Should().Be(HttpStatusCode.Unauthorized);
        }

        (await LoginAsync(client, email, "Correct Password 2026!")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        (await userManager.IsLockedOutAsync(user!)).Should().BeTrue();
    }

    [Fact]
    public async Task RefreshRotatesTokensAndReuseRevokesTheFamily()
    {
        var (factory, _) = CreateFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await LoginAsync(
            client,
            AdministratorEmail,
            AdministratorPassword);
        var oldCookie = CookieFrom(login);

        var refresh = await RefreshAsync(client, oldCookie);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var newCookie = CookieFrom(refresh);
        newCookie.Should().NotBe(oldCookie);

        (await RefreshAsync(client, oldCookie)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await RefreshAsync(client, newCookie)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokedSessionCannotBeRefreshed()
    {
        var (factory, _) = CreateFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await LoginAsync(
            client,
            AdministratorEmail,
            AdministratorPassword);
        var payload = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        var cookie = CookieFrom(login);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload.AccessToken);

        (await client.DeleteAsync($"/api/v1/auth/sessions/{payload.SessionId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await RefreshAsync(client, cookie)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InvitationAssignsRoleAndPolicyRejectsGreaterAuthority()
    {
        var clientSuppliedTenantId = TenantId.New();
        await CreateTenantAsync(clientSuppliedTenantId);
        var (factory, emailSender) = CreateFactory();
        var adminClient = factory.CreateClient();
        var login = await LoginAsync(
            adminClient,
            AdministratorEmail,
            AdministratorPassword);
        var admin = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        var roles = (await adminClient.GetFromJsonAsync<RoleResponse[]>(
            "/api/v1/users/roles"))!;
        var readOnlyRole = roles.Single(role => role.Name == "Read only");
        var invitedEmail = $"invite-{Guid.NewGuid():N}@loadix.example.invalid";

        var invitation = await adminClient.PostAsJsonAsync(
            "/api/v1/users/invitations",
            new
            {
                Email = invitedEmail,
                DisplayName = "Invited Test User",
                RoleIds = new[] { readOnlyRole.Id },
                TenantId = clientSuppliedTenantId.Value,
            });
        invitation.StatusCode.Should().Be(HttpStatusCode.Created);
        var invitationPayload =
            (await invitation.Content.ReadFromJsonAsync<InvitationResponse>())!;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext =
                scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
            (await dbContext.UserInvitations.SingleAsync(candidate =>
                    candidate.Id == invitationPayload.Id))
                .TenantId.Should().Be(DevelopmentSeedIds.TenantId);
        }
        emailSender.LastInvitationUri.Should().NotBeNull();
        var token = QueryValue(emailSender.LastInvitationUri!, "token");

        var anonymousClient = factory.CreateClient();
        (await anonymousClient.PostAsJsonAsync(
            "/api/v1/auth/accept-invitation",
            new AcceptInvitationRequest(token, "Invited User 2026!")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var invitedLogin = await LoginAsync(
            anonymousClient,
            invitedEmail,
            "Invited User 2026!");
        invitedLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var invited =
            (await invitedLogin.Content.ReadFromJsonAsync<AuthResponse>())!;
        anonymousClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", invited.AccessToken);

        (await anonymousClient.GetAsync("/api/v1/users")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await anonymousClient.PostAsJsonAsync(
            "/api/v1/users/invitations",
            new InviteUserRequest(
                "forbidden@loadix.example.invalid",
                "Forbidden",
                [readOnlyRole.Id]))).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DelegatedUserCannotModifyRoleWithGreaterAuthority()
    {
        var email = $"delegated-{Guid.NewGuid():N}@loadix.example.invalid";
        await CreateTenantUserWithPermissionsAsync(
            email,
            "Delegated User 2026!",
            [LoadIxPermissions.UserView, LoadIxPermissions.UserManage]);
        var (factory, _) = CreateFactory();
        Guid administratorRoleId;
        string[] originalPermissions;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
            var administratorRole = await dbContext.Roles
                .IgnoreQueryFilters()
                .SingleAsync(role => role.DisplayName == "Tenant Administrator");
            administratorRoleId = administratorRole.Id;
            originalPermissions = administratorRole.GetPermissions().ToArray();
        }

        var client = factory.CreateClient();
        var login = await LoginAsync(client, email, "Delegated User 2026!");
        var payload = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload.AccessToken);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/users/roles/{administratorRoleId}/permissions",
            new UpdateRolePermissionsRequest(
                [LoadIxPermissions.UserView, LoadIxPermissions.UserManage],
                "Attempt to reduce administrator authority"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<LoadIxDbContext>();
        var unchangedRole = await verificationDb.Roles
            .IgnoreQueryFilters()
            .SingleAsync(role => role.Id == administratorRoleId);
        unchangedRole.GetPermissions().Should().BeEquivalentTo(originalPermissions);
    }

    [Fact]
    public async Task NormalTenantUserCannotEnterAnotherTenantContext()
    {
        var secondTenantId = TenantId.New();
        await CreateTenantAsync(secondTenantId);
        var (factory, _) = CreateFactory();
        var client = factory.CreateClient();
        var login = await LoginAsync(
            client,
            AdministratorEmail,
            AdministratorPassword);
        var payload = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload.AccessToken);
        client.DefaultRequestHeaders.Add(
            "X-LoadIx-Support-Tenant",
            secondTenantId.Value.ToString());
        client.DefaultRequestHeaders.Add(
            "X-LoadIx-Support-Reason",
            "Pilot support investigation");

        (await client.GetAsync("/api/v1/users")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PlatformAdministratorSupportContextIsVisibleAndAudited()
    {
        var secondTenantId = TenantId.New();
        await CreateTenantAsync(secondTenantId);
        var email = $"platform-{Guid.NewGuid():N}@loadix.example.invalid";
        await CreatePlatformAdministratorAsync(email, "Platform Admin 2026!");
        var (factory, _) = CreateFactory();
        var client = factory.CreateClient();
        var login = await LoginAsync(client, email, "Platform Admin 2026!");
        var payload = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload.AccessToken);
        client.DefaultRequestHeaders.Add(
            "X-LoadIx-Support-Tenant",
            secondTenantId.Value.ToString());
        client.DefaultRequestHeaders.Add(
            "X-LoadIx-Support-Reason",
            "Pilot support investigation");

        var response = await client.GetAsync("/api/v1/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-LoadIx-Support-Context").Single()
            .Should().Be(secondTenantId.Value.ToString());
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        (await dbContext.SecurityAuditEvents.AnyAsync(auditEvent =>
            auditEvent.TenantId == secondTenantId
            && auditEvent.EventType == "SupportContext.Entered"
            && auditEvent.IsSupportContext)).Should().BeTrue();
    }

    [Fact]
    public async Task DeactivatedUserCannotLoginOrUseExistingAccessToken()
    {
        var email = $"inactive-{Guid.NewGuid():N}@loadix.example.invalid";
        var userId = await CreateTenantUserAsync(email, "Active User 2026!");
        var (factory, _) = CreateFactory();
        var userClient = factory.CreateClient();
        var userLogin = await LoginAsync(userClient, email, "Active User 2026!");
        var userPayload =
            (await userLogin.Content.ReadFromJsonAsync<AuthResponse>())!;
        userClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", userPayload.AccessToken);

        var adminClient = factory.CreateClient();
        var adminLogin = await LoginAsync(
            adminClient,
            AdministratorEmail,
            AdministratorPassword);
        var adminPayload =
            (await adminLogin.Content.ReadFromJsonAsync<AuthResponse>())!;
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminPayload.AccessToken);
        (await adminClient.PostAsync(
            $"/api/v1/users/{userId}/deactivate",
            null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await userClient.GetAsync("/api/v1/auth/me")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAsync(userClient, email, "Active User 2026!")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredAccessTokenIsRejectedAndAudited()
    {
        var (factory, _) = CreateFactory();
        var client = factory.CreateClient();
        var now = TimeProvider.System.GetUtcNow();
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            "QqlwWau6fgTcPhzXxeKclRbIb7YJUNfC0L58wURHwDK"));
        var expiredToken = new JwtSecurityToken(
            issuer: "LoadIx.Api",
            audience: "LoadIx.Web",
            claims:
            [
                new Claim(
                    JwtRegisteredClaimNames.Sub,
                    DevelopmentSeedIds.ActorId.ToString()),
            ],
            notBefore: now.AddMinutes(-10).UtcDateTime,
            expires: now.AddMinutes(-5).UtcDateTime,
            signingCredentials: new SigningCredentials(
                signingKey,
                SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                new JwtSecurityTokenHandler().WriteToken(expiredToken));

        (await client.GetAsync("/api/v1/auth/me")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        (await dbContext.SecurityAuditEvents.AnyAsync(auditEvent =>
            auditEvent.EventType == "Authentication.TokenRejected"
            && auditEvent.FailureCode == "expired_token"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task PasswordResetUsesGenericRequestAndRevokesOldPassword()
    {
        var email = $"reset-{Guid.NewGuid():N}@loadix.example.invalid";
        await CreateTenantUserAsync(email, "Old Password 2026!");
        var (factory, emailSender) = CreateFactory();
        var client = factory.CreateClient();

        var request = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new ForgotPasswordRequest(email));
        var unknown = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new ForgotPasswordRequest("unknown@loadix.example.invalid"));
        request.StatusCode.Should().Be(HttpStatusCode.Accepted);
        unknown.StatusCode.Should().Be(HttpStatusCode.Accepted);
        emailSender.LastPasswordResetUri.Should().NotBeNull();

        var token = QueryValue(emailSender.LastPasswordResetUri!, "token");
        (await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest(email, token, "New Password 2026!")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LoginAsync(client, email, "Old Password 2026!")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAsync(client, email, "New Password 2026!")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    private (WebApplicationFactory<Program> Factory, CapturingEmailSender Email)
        CreateFactory()
    {
        var emailSender = new CapturingEmailSender();
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:Database", _connectionString);
                builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IAccountEmailSender>();
                    services.AddSingleton<IAccountEmailSender>(emailSender);
                });
            });
        _factories.Add(factory);
        return (factory, emailSender);
    }

    private async Task<Guid> CreateTenantUserAsync(
        string email,
        string password)
    {
        var (factory, _) = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        var now = TimeProvider.System.GetUtcNow();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = DevelopmentSeedIds.TenantId,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Integration Test User",
            IsActive = true,
            CreatedAtUtc = now,
            ModifiedAtUtc = now,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        (await userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
        var role = await dbContext.Roles.SingleAsync(
            candidate => candidate.DisplayName == "Read only");
        (await userManager.AddToRoleAsync(user, role.Name!)).Succeeded
            .Should().BeTrue();
        return user.Id;
    }

    private async Task CreateTenantUserWithPermissionsAsync(
        string email,
        string password,
        IReadOnlyCollection<string> permissions)
    {
        var (factory, _) = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<ApplicationRole>>();
        var now = TimeProvider.System.GetUtcNow();
        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            TenantId = DevelopmentSeedIds.TenantId,
            Name = $"{DevelopmentSeedIds.TenantId.Value:N}:delegated-{Guid.NewGuid():N}",
            DisplayName = $"Delegated {Guid.NewGuid():N}",
        };
        role.SetPermissions(permissions);
        (await roleManager.CreateAsync(role)).Succeeded.Should().BeTrue();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = DevelopmentSeedIds.TenantId,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Delegated Integration Test User",
            IsActive = true,
            CreatedAtUtc = now,
            ModifiedAtUtc = now,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        (await userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
        (await userManager.AddToRoleAsync(user, role.Name!)).Succeeded.Should().BeTrue();
    }

    private async Task CreateTenantAsync(TenantId tenantId)
    {
        var (factory, _) = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        dbContext.Tenants.Add(Tenant.Create(
            tenantId,
            "Isolation Test Tenant",
            "Isolation Test Tenant (Pty) Ltd",
            $"ISO-{tenantId.Value:N}",
            null,
            "ZAR",
            "Africa/Johannesburg").Value);
        await dbContext.SaveChangesAsync();
    }

    private async Task CreatePlatformAdministratorAsync(
        string email,
        string password)
    {
        var (factory, _) = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var now = TimeProvider.System.GetUtcNow();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Platform Test Administrator",
            IsActive = true,
            IsPlatformAdministrator = true,
            CreatedAtUtc = now,
            ModifiedAtUtc = now,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        (await userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password) =>
        client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password));

    private static Task<HttpResponseMessage> RefreshAsync(
        HttpClient client,
        string cookie)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", cookie);
        request.Headers.Add("X-LoadIx-Client", "integration-test");
        return client.SendAsync(request);
    }

    private static string CookieFrom(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

    private static string QueryValue(Uri uri, string key)
    {
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers
            .ParseQuery(uri.Query);
        return query[key].Single()!;
    }

    private sealed class CapturingEmailSender : IAccountEmailSender
    {
        public Uri? LastInvitationUri { get; private set; }

        public Uri? LastPasswordResetUri { get; private set; }

        public Task SendAsync(
            string email,
            string subject,
            string body,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SendInvitationAsync(
            string email,
            string displayName,
            Uri acceptanceUri,
            CancellationToken cancellationToken)
        {
            LastInvitationUri = acceptanceUri;
            return Task.CompletedTask;
        }

        public Task SendPasswordResetAsync(
            string email,
            Uri resetUri,
            CancellationToken cancellationToken)
        {
            LastPasswordResetUri = resetUri;
            return Task.CompletedTask;
        }

        public Task SendEmailConfirmationAsync(
            string email,
            Uri confirmationUri,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed record ProblemResponse(
        string Title,
        int Status,
        string Code);
}
