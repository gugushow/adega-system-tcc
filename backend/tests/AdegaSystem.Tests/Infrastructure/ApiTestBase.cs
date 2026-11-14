using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Auth;
using AdegaSystem.Api.DTOs.Users;
using AdegaSystem.Api.Entities;

namespace AdegaSystem.Tests.Infrastructure;

/// <summary>
/// Base dos testes por HTTP: login de verdade e um HttpClient que já envia o token.
/// Usada pelos testes de permissões e pelos de comportamento da API.
/// </summary>
public abstract class ApiTestBase(ApiFactory factory)
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected ApiFactory Factory { get; } = factory;
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Faz login e devolve um HttpClient que já envia o token.</summary>
    protected async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto(email, password), Json, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>(Json, Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.Token);
        return client;
    }

    protected Task<HttpClient> LoginAsAdminAsync() => LoginAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword);

    /// <summary>O ADMIN cria um usuário do perfil pedido (e-mail único por chamada) e já faz login com ele.</summary>
    protected async Task<(HttpClient Client, UserDto User)> CreateAndLoginAsync(UserRole role)
    {
        var admin = await LoginAsAdminAsync();
        var email = $"{role.ToString().ToLower()}-{Guid.NewGuid():N}@adega.com";
        var response = await admin.PostAsJsonAsync("/api/users", new CreateUserDto("Usuário " + role, email, "Senha123", role), Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = (await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>(Json, Ct))!.Data!;
        return (await LoginAsync(email, "Senha123"), user);
    }

    protected static async Task<ApiResponse<object>> BodyOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ApiResponse<object>>(Json, Ct))!;

    protected static async Task<string?> MessageOf(HttpResponseMessage response) => (await BodyOf(response)).Message;
}
