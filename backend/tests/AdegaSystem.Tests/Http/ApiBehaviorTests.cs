using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.Data.Seed;
using AdegaSystem.Api.DTOs.Auth;
using AdegaSystem.Api.DTOs.Categories;
using AdegaSystem.Api.DTOs.Customers;
using AdegaSystem.Api.Entities;
using AdegaSystem.Tests.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AdegaSystem.Tests.Http;

/// <summary>
/// Comportamentos da API vistos de fora, por HTTP: códigos de status, formato padrão das respostas
/// (ApiResponse), nomes dos campos nos erros e limites (paginação, tentativas de login).
/// </summary>
public class ApiBehaviorTests(ApiFactory factory) : ApiTestBase(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Create_Returns201WithTheAddressOfTheNewRecord()
    {
        var admin = await LoginAsAdminAsync();

        var response = await admin.PostAsJsonAsync("/api/categories", new CreateCategoryDto($"Licores {Guid.NewGuid():N}", null), Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ApiResponse<CategoryDto>>(Json, Ct))!.Data!;
        Assert.Equal($"/api/categories/{created.Id}", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task ValidationError_Returns400WithTheJsonFieldName()
    {
        var admin = await LoginAsAdminAsync();

        var response = await admin.PostAsJsonAsync("/api/categories", new CreateCategoryDto("", null), Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await BodyOf(response);
        Assert.False(body.Success);
        // "name" (como no JSON), não "Name" (como na classe C#): o frontend marca o campo certo.
        Assert.Contains(body.Errors, e => e.Field == "name" && e.Message == "O nome é obrigatório.");
    }

    [Fact]
    public async Task MalformedJson_Returns400WithFriendlyMessage()
    {
        var admin = await LoginAsAdminAsync();

        var response = await admin.PostAsync("/api/categories",
            new StringContent("{ \"name\": \"Vinhos\", ", Encoding.UTF8, "application/json"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await BodyOf(response);
        Assert.Equal("Dados inválidos. Verifique o formato dos campos enviados.", body.Message);
        // Sem o erro genérico do ASP.NET com o nome interno do parâmetro ("dto").
        Assert.DoesNotContain(body.Errors, e => e.Field == "dto");
    }

    [Fact]
    public async Task InvalidQueryParameter_Returns400NamingTheField()
    {
        var admin = await LoginAsAdminAsync();

        var response = await admin.GetAsync("/api/products?page=abc", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await BodyOf(response);
        Assert.Contains(body.Errors, e => e.Field == "page" && e.Message == "Valor em formato inválido.");
    }

    [Fact]
    public async Task PageSize_IsLimitedTo100()
    {
        var admin = await LoginAsAdminAsync();

        var result = await admin.GetFromJsonAsync<ApiResponse<PagedResult<CategoryDto>>>("/api/categories?pageSize=1000", Json, Ct);

        Assert.Equal(PagedQuery.MaxPageSize, result!.Data!.PageSize);
    }

    [Fact]
    public async Task UnknownRecord_Returns404InTheStandardFormat()
    {
        var admin = await LoginAsAdminAsync();

        var response = await admin.GetAsync("/api/products/999999", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await BodyOf(response);
        Assert.False(body.Success);
        Assert.Equal("Produto não encontrado.", body.Message);
    }

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        // Token assinado com a chave certa, mas que venceu há 1 minuto.
        var now = DateTime.UtcNow;
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "AdegaSystem",
            Audience = "AdegaSystem.Frontend",
            IssuedAt = now.AddHours(-8),
            NotBefore = now.AddHours(-8),
            Expires = now.AddMinutes(-1),
            Subject = new ClaimsIdentity(
            [
                new Claim(AppClaims.UserId, UserSeed.AdminId.ToString()),
                new Claim(AppClaims.Role, nameof(UserRole.ADMIN)),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.JwtSecret)), SecurityAlgorithms.HmacSha256),
        });
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/auth/me", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Vendedor_EditingCustomer_CannotDeactivateIt()
    {
        var admin = await LoginAsAdminAsync();
        var created = await admin.PostAsJsonAsync("/api/customers", new CreateCustomerDto("Cliente do Balcão"), Json, Ct);
        var customer = (await created.Content.ReadFromJsonAsync<ApiResponse<CustomerDto>>(Json, Ct))!.Data!;
        var (vendedor, _) = await CreateAndLoginAsync(UserRole.VENDEDOR);

        // O vendedor edita o nome e tenta mandar Active = false junto.
        var response = await vendedor.PutAsJsonAsync($"/api/customers/{customer.Id}",
            new UpdateCustomerDto("Cliente do Balcão (editado)", null, null, null, null, Active: false), Json, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ApiResponse<CustomerDto>>(Json, Ct))!.Data!;
        Assert.Equal("Cliente do Balcão (editado)", updated.Name);
        Assert.True(updated.Active);
    }

    [Fact]
    public async Task Admin_EditingCustomer_CanDeactivateIt()
    {
        var admin = await LoginAsAdminAsync();
        var created = await admin.PostAsJsonAsync("/api/customers", new CreateCustomerDto("Cliente Antigo"), Json, Ct);
        var customer = (await created.Content.ReadFromJsonAsync<ApiResponse<CustomerDto>>(Json, Ct))!.Data!;

        var response = await admin.PutAsJsonAsync($"/api/customers/{customer.Id}",
            new UpdateCustomerDto("Cliente Antigo", null, null, null, null, Active: false), Json, Ct);

        var updated = (await response.Content.ReadFromJsonAsync<ApiResponse<CustomerDto>>(Json, Ct))!.Data!;
        Assert.False(updated.Active);
    }

    [Fact]
    public async Task Login_TooManyAttempts_Returns429()
    {
        // Fábrica própria com limite baixo (a compartilhada tem limite alto para os outros testes).
        await using var factory = new ApiFactory { LoginPermitLimit = 3 };
        var client = factory.CreateClient();
        var wrong = new LoginDto(ApiFactory.AdminEmail, "SenhaErrada1");

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var allowed = await client.PostAsJsonAsync("/api/auth/login", wrong, Json, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        // A 4ª tentativa no mesmo minuto é barrada, mesmo com a senha certa.
        var blocked = await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto(ApiFactory.AdminEmail, ApiFactory.AdminPassword), Json, Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal(LoginRateLimit.Message, await MessageOf(blocked));
    }

    [Fact]
    public async Task RateLimit_AppliesOnlyToLogin()
    {
        await using var factory = new ApiFactory { LoginPermitLimit = 1 };
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginDto(ApiFactory.AdminEmail, ApiFactory.AdminPassword), Json, Ct);
        var token = (await login.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>(Json, Ct))!.Data!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Depois de gastar a única tentativa de login, o resto da API continua respondendo normalmente.
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me", Ct)).StatusCode);
        }
    }
}
