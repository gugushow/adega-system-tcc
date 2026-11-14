using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Auth;
using AdegaSystem.Api.DTOs.Users;
using AdegaSystem.Api.Entities;
using AdegaSystem.Tests.Infrastructure;

namespace AdegaSystem.Tests.Auth;

/// <summary>
/// Testes de ponta a ponta das permissões: fazem login de verdade e chamam os endpoints por HTTP.
/// Confirmam que cada perfil só alcança o que deve (401 sem login, 403 sem permissão).
/// </summary>
public class AuthorizationTests(ApiFactory factory) : ApiTestBase(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_IsPublic()
    {
        var response = await Factory.CreateClient().GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/products/barcode/7891234567890")]
    [InlineData("/api/sales")]
    [InlineData("/api/auth/me")]
    public async Task ProtectedEndpoint_WithoutToken_Returns401(string url)
    {
        var response = await Factory.CreateClient().GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Sua sessão expirou ou você não fez login. Entre novamente.", await MessageOf(response));
    }

    [Fact]
    public async Task ProtectedEndpoint_WithForgedToken_Returns401()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.assinatura-falsa");

        var response = await client.GetAsync("/api/products", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401WithGenericMessage()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginDto(ApiFactory.AdminEmail, "SenhaErrada1"), Json, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("E-mail ou senha inválidos.", await MessageOf(response));
    }

    [Fact]
    public async Task Me_ReturnsTheLoggedUser()
    {
        var client = await LoginAsAdminAsync();

        var me = await client.GetFromJsonAsync<ApiResponse<UserDto>>("/api/auth/me", Json, Ct);

        Assert.Equal(ApiFactory.AdminEmail, me!.Data!.Email);
        Assert.Equal(UserRole.ADMIN, me.Data.Role);
    }

    // Matriz de permissões de leitura: perfil, endpoint e o status esperado.
    [Theory]
    [InlineData(UserRole.VENDEDOR, "/api/products", HttpStatusCode.OK)]
    [InlineData(UserRole.VENDEDOR, "/api/categories", HttpStatusCode.OK)]
    [InlineData(UserRole.VENDEDOR, "/api/sales", HttpStatusCode.OK)]
    [InlineData(UserRole.VENDEDOR, "/api/customers", HttpStatusCode.OK)]
    [InlineData(UserRole.VENDEDOR, "/api/purchases", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.VENDEDOR, "/api/suppliers", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.VENDEDOR, "/api/batches", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.VENDEDOR, "/api/stock-movements", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.VENDEDOR, "/api/users", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.ESTOQUISTA, "/api/products", HttpStatusCode.OK)]
    [InlineData(UserRole.ESTOQUISTA, "/api/purchases", HttpStatusCode.OK)]
    [InlineData(UserRole.ESTOQUISTA, "/api/suppliers", HttpStatusCode.OK)]
    [InlineData(UserRole.ESTOQUISTA, "/api/batches", HttpStatusCode.OK)]
    [InlineData(UserRole.ESTOQUISTA, "/api/stock-movements", HttpStatusCode.OK)]
    [InlineData(UserRole.ESTOQUISTA, "/api/sales", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.ESTOQUISTA, "/api/customers", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.ESTOQUISTA, "/api/users", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.VENDEDOR, "/api/dashboard", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.VENDEDOR, "/api/reports/sales", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.VENDEDOR, "/api/settings/margin", HttpStatusCode.OK)]
    [InlineData(UserRole.ESTOQUISTA, "/api/dashboard", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.ESTOQUISTA, "/api/reports/stock", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.ESTOQUISTA, "/api/settings/margin", HttpStatusCode.OK)]
    [InlineData(UserRole.ADMIN, "/api/dashboard", HttpStatusCode.OK)]
    [InlineData(UserRole.ADMIN, "/api/reports/sales?groupBy=Month", HttpStatusCode.OK)]
    [InlineData(UserRole.ADMIN, "/api/reports/products?order=LeastSold", HttpStatusCode.OK)]
    [InlineData(UserRole.ADMIN, "/api/reports/categories", HttpStatusCode.OK)]
    [InlineData(UserRole.ADMIN, "/api/reports/idle-products?days=60", HttpStatusCode.OK)]
    [InlineData(UserRole.ADMIN, "/api/reports/margins?onlyBelowMinimum=true", HttpStatusCode.OK)]
    [InlineData(UserRole.ADMIN, "/api/reports/purchases-by-supplier", HttpStatusCode.OK)]
    [InlineData(UserRole.ADMIN, "/api/reports/stock", HttpStatusCode.OK)]
    [InlineData(UserRole.ADMIN, "/api/users", HttpStatusCode.OK)]
    [InlineData(UserRole.ADMIN, "/api/sales", HttpStatusCode.OK)]
    [InlineData(UserRole.ADMIN, "/api/purchases", HttpStatusCode.OK)]
    [InlineData(UserRole.VENDEDOR, "/api/combos?active=true", HttpStatusCode.OK)]
    [InlineData(UserRole.ESTOQUISTA, "/api/combos", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.VENDEDOR, "/api/settings/loyalty", HttpStatusCode.OK)]
    [InlineData(UserRole.VENDEDOR, "/api/audit-logs", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.ESTOQUISTA, "/api/audit-logs", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.ADMIN, "/api/combos", HttpStatusCode.OK)]
    [InlineData(UserRole.ADMIN, "/api/audit-logs?action=SALE_CANCELED&entity=Sale", HttpStatusCode.OK)]
    // Busca por código de barras (scanner do cadastro e do PDV): liberada para os três perfis.
    // 404 aqui prova que a permissão passou e a busca rodou (o código não existe).
    [InlineData(UserRole.ADMIN, "/api/products/barcode/7891234567890", HttpStatusCode.NotFound)]
    [InlineData(UserRole.VENDEDOR, "/api/products/barcode/7891234567890", HttpStatusCode.NotFound)]
    [InlineData(UserRole.ESTOQUISTA, "/api/products/barcode/7891234567890", HttpStatusCode.NotFound)]
    public async Task ReadPermissions_ByRole(UserRole role, string url, HttpStatusCode expected)
    {
        var (client, _) = await CreateAndLoginAsync(role);

        var response = await client.GetAsync(url, Ct);

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.Forbidden)
        {
            Assert.Equal("Você não tem permissão para acessar este recurso.", await MessageOf(response));
        }
    }

    // Ações que exigem um perfil específico. O 403 vem antes de qualquer validação do corpo.
    [Theory]
    [InlineData(UserRole.VENDEDOR, "POST", "/api/products")]
    [InlineData(UserRole.VENDEDOR, "DELETE", "/api/categories/1")]
    [InlineData(UserRole.VENDEDOR, "POST", "/api/sales/1/cancel")]
    [InlineData(UserRole.VENDEDOR, "DELETE", "/api/customers/1")]
    [InlineData(UserRole.VENDEDOR, "POST", "/api/auth/register")]
    [InlineData(UserRole.ESTOQUISTA, "POST", "/api/sales")]
    [InlineData(UserRole.ESTOQUISTA, "POST", "/api/sales/1/cancel")]
    [InlineData(UserRole.ESTOQUISTA, "POST", "/api/users")]
    [InlineData(UserRole.ESTOQUISTA, "PUT", "/api/settings/margin")]
    [InlineData(UserRole.VENDEDOR, "PUT", "/api/settings/margin")]
    [InlineData(UserRole.VENDEDOR, "PUT", "/api/settings/loyalty")]
    [InlineData(UserRole.VENDEDOR, "POST", "/api/combos")]
    [InlineData(UserRole.VENDEDOR, "PUT", "/api/combos/1")]
    [InlineData(UserRole.VENDEDOR, "DELETE", "/api/combos/1")]
    [InlineData(UserRole.VENDEDOR, "POST", "/api/customers/1/loyalty/adjust")]
    [InlineData(UserRole.ESTOQUISTA, "POST", "/api/customers/1/loyalty/redeem")]
    public async Task WriteActions_ForbiddenForRole(UserRole role, string method, string url)
    {
        var (client, _) = await CreateAndLoginAsync(role);

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = JsonContent.Create(new { }),
        }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeactivatedUser_OldTokenStopsWorkingImmediately()
    {
        var (client, user) = await CreateAndLoginAsync(UserRole.VENDEDOR);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me", Ct)).StatusCode);

        var admin = await LoginAsAdminAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/users/{user.Id}", Ct)).StatusCode);

        // O token ainda não venceu, mas o usuário foi desativado: acesso negado.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task ChangedRole_OldTokenStopsWorking()
    {
        var (client, user) = await CreateAndLoginAsync(UserRole.VENDEDOR);

        var admin = await LoginAsAdminAsync();
        var update = new UpdateUserDto(user.Name, user.Email, UserRole.ESTOQUISTA, Active: true);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/users/{user.Id}", update, Json, Ct)).StatusCode);

        // O token diz VENDEDOR, mas no banco agora é ESTOQUISTA: precisa entrar de novo.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/sales", Ct)).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns400AndKeepsTheSession()
    {
        var (client, _) = await CreateAndLoginAsync(UserRole.ESTOQUISTA);

        var response = await client.PutAsJsonAsync("/api/auth/me/password", new ChangePasswordDto("Errada123", "NovaSenha9"), Json, Ct);

        // 400 e não 401: o frontend trata 401 como "sessão expirada" e deslogaria o usuário.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("A senha atual está incorreta.", await MessageOf(response));
    }

    [Fact]
    public async Task Report_WithInvalidPeriod_Returns400WithFieldError()
    {
        var admin = await LoginAsAdminAsync();

        var response = await admin.GetAsync("/api/reports/sales?from=2026-09-30&to=2026-09-01", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(Json, Ct);
        Assert.Contains(body!.Errors, e => e.Field == "to" && e.Message == "A data final deve ser igual ou posterior à data inicial.");
    }

    [Fact]
    public async Task Register_ByAdmin_CreatesUserThatCanLogin()
    {
        var admin = await LoginAsAdminAsync();

        var response = await admin.PostAsJsonAsync("/api/auth/register",
            new CreateUserDto("Novo Vendedor", "novo.vendedor@adega.com", "Senha123", UserRole.VENDEDOR), Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await LoginAsync("novo.vendedor@adega.com", "Senha123");
    }

    [Fact]
    public async Task AuditLogs_ShowWhoCreatedAUser_WithoutThePassword()
    {
        var (_, user) = await CreateAndLoginAsync(UserRole.VENDEDOR);
        var admin = await LoginAsAdminAsync();

        var body = await admin.GetStringAsync($"/api/audit-logs?entity=User&entityId={user.Id}", Ct);

        Assert.Contains("\"action\":\"USER_CREATED\"", body);
        Assert.Contains(user.Email, body);
        Assert.DoesNotContain("Senha123", body);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
    }
}
