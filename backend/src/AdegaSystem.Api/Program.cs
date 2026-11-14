using System.Text.Json.Serialization;
using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.Data.DemoData;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Middleware;
using AdegaSystem.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "Frontend";

// A connection string nunca fica no repositório. Em desenvolvimento ela vem dos User Secrets
// (dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."); em outros ambientes,
// da variável de ambiente ConnectionStrings__DefaultConnection.
// Com "seed-demo" ou "use-demo" na linha de comando, a API usa o banco separado da demonstração
// (ver Data/DemoData/DemoDatabase.cs), e só no ambiente Development.
var demoMode = DemoDatabase.ParseMode(args);
var connectionString = demoMode == DemoMode.None
    ? builder.Configuration.GetConnectionString("DefaultConnection")
    : DemoDatabase.GetConnectionString(builder.Configuration, builder.Environment);
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' não configurada. Em desenvolvimento, rode na raiz do projeto: " +
        "dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" " +
        "\"Server=localhost;Port=3307;Database=adega;User=adega;Password=SUA_SENHA;\" --project backend/src/AdegaSystem.Api");
}

builder.Services.AddDbContext<AdegaDbContext>(options => options.UseMySQL(connectionString));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AdegaDbContext>("mysql");

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddControllers(options =>
    {
        // Sem isto, o ASP.NET valida sozinho as propriedades string não anuláveis e devolve
        // "The Name field is required." em inglês, antes do FluentValidation. Deixamos toda
        // a validação de campos com o FluentValidation, que tem as mensagens em português.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    // Enums trafegam como texto no JSON ("Low" em vez de 0), mais legível no Swagger e no frontend.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Quando o JSON enviado nem consegue ser lido (ex.: texto num campo numérico), o [ApiController]
// responde 400 sozinho. Aqui trocamos o formato padrão dele pelo formato da nossa API.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        // As chaves de erro de JSON vêm como "$.categoryId". Devolvemos só o nome do campo
        // e uma mensagem amigável, sem expor nomes de classes internas.
        // Filtros da URL (ex.: ?page=abc) vêm com o nome da propriedade ("Page"). Os dois viram o nome
        // usado pelo frontend ("categoryId", "page"). O erro genérico "o corpo é obrigatório" que o ASP.NET
        // acrescenta quando o JSON não pôde ser lido fica de fora (o nome dele é o do parâmetro, ex.: "dto").
        var bodyParameters = context.ActionDescriptor.Parameters
            .Where(p => p.BindingInfo?.BindingSource == Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Body)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0 && !bodyParameters.Contains(entry.Key))
            .Select(entry => new ApiError(ToFieldName(entry.Key), "Valor em formato inválido."))
            .ToList();
        return new BadRequestObjectResult(ApiResponse.Fail("Dados inválidos. Verifique o formato dos campos enviados.", errors));
    };
});

// Proteção contra tentativas de senha no login (ver Auth/LoginRateLimit.cs).
builder.Services.AddLoginRateLimit(builder.Configuration);

// Tratamento global de exceções (ver Middleware/GlobalExceptionHandler.cs).
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Validadores (FluentValidation): registra todas as classes AbstractValidator<T> do projeto.
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Services: Scoped = uma instância por requisição HTTP, a mesma vida útil do DbContext.
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<ICostHistoryService, CostHistoryService>();
builder.Services.AddScoped<IBatchService, BatchService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ISaleService, SaleService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IComboService, ComboService>();
builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();

// Login e permissões (ver Auth/AuthenticationSetup.cs).
builder.Services.AddJwtAuthentication(builder.Configuration);
// Dá acesso ao usuário da requisição atual fora dos controllers (usado pelo CurrentUserService).
builder.Services.AddHttpContextAccessor();
// Hash de senha do próprio ASP.NET Core (PBKDF2 com salt). A senha em si nunca é gravada.
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Adega System API";
        document.Info.Description = "API do sistema de gestão da adega.";

        // Adiciona o botão "Authorize" no Swagger: cole o token do login e as requisições
        // passam a enviar o cabeçalho "Authorization: Bearer <token>".
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Faça login em POST /api/auth/login e cole aqui o token recebido.",
        };
        document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// "seed-demo": prepara o banco adega_demo, carrega os dados de demonstração e encerra, sem subir a API.
if (demoMode == DemoMode.Seed)
{
    Environment.ExitCode = await DemoDataCommand.RunAsync(app);
    return;
}

// Em produção (Docker), a API pode aplicar as migrations sozinha ao subir: Database__MigrateOnStartup=true.
// Desligado por padrão; no desenvolvimento continua o "dotnet ef database update".
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AdegaDbContext>().Database.MigrateAsync();
    app.Logger.LogInformation("Migrations aplicadas.");
}

// Define a senha do admin na primeira subida (ver Services/AdminPasswordInitializer.cs).
// Se o banco ainda não estiver pronto (ex.: migrations não aplicadas), a API sobe mesmo assim.
try
{
    await AdminPasswordInitializer.RunAsync(app.Services, app.Configuration, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Não foi possível conferir a senha inicial do admin.");
}

// Primeiro middleware do pipeline, para capturar exceções de tudo o que vem depois.
app.UseExceptionHandler(_ => { });

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Adega System API v1");
        options.DocumentTitle = "Adega System API";
    });
}

if (!app.Environment.IsDevelopment())
{
    // HSTS: depois do primeiro acesso por HTTPS, o navegador não aceita mais abrir o site por HTTP.
    // O certificado HTTPS fica no proxy da frente (ver "Deploy" no README); a API em si só fala HTTP
    // dentro do Docker, por isso não há UseHttpsRedirection aqui.
    app.UseHsts();
}

app.UseCors(FrontendCorsPolicy);
app.UseRateLimiter();

// Primeiro descobre QUEM é o usuário (lê o token), depois decide se ele PODE acessar.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

/// <summary>"$.categoryId" vira "categoryId"; "Items[0].UnitCost" vira "items[0].unitCost".</summary>
static string ToFieldName(string key) =>
    string.Join('.', key.TrimStart('$', '.').Split('.').Select(System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName));

// Torna a classe Program visível para os testes de integração (WebApplicationFactory<Program>).
public partial class Program;
