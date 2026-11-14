using System.Reflection;
using System.Text.Json;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

namespace AdegaSystem.Tests.Middleware;

/// <summary>
/// O handler global decide o status HTTP de cada exceção. Aqui ele é chamado direto, sem subir a API,
/// para cobrir casos difíceis de provocar de verdade (erros do MySQL, cliente que desistiu).
/// </summary>
public class GlobalExceptionHandlerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ListLogger _logger = new();

    private async Task<(int Status, ApiResponse<object>? Body)> HandleAsync(Exception exception, bool requestAborted = false)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        if (requestAborted)
        {
            var aborted = new CancellationTokenSource();
            aborted.Cancel();
            context.RequestAborted = aborted.Token;
        }

        var handled = await new GlobalExceptionHandler(_logger).TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        context.Response.Body.Position = 0;
        var body = context.Response.Body.Length == 0
            ? null
            : await JsonSerializer.DeserializeAsync<ApiResponse<object>>(context.Response.Body, Json);
        return (context.Response.StatusCode, body);
    }

    /// <summary>
    /// O MySqlException não tem construtor público (só o driver cria). Para o teste, criamos um por
    /// reflexão com o código de erro desejado, embrulhado como o EF faz ao salvar.
    /// </summary>
    private static DbUpdateException MySqlError(int number)
    {
        var mySql = (MySqlException)Activator.CreateInstance(typeof(MySqlException),
            BindingFlags.NonPublic | BindingFlags.Instance, null, ["erro do banco", number], null)!;
        Assert.Equal(number, mySql.Number);
        return new DbUpdateException("Erro ao salvar.", mySql);
    }

    [Fact]
    public async Task DuplicateEntry_Returns409()
    {
        var (status, body) = await HandleAsync(MySqlError(1062));

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("Já existe um registro com esses dados (outra pessoa acabou de salvar). Atualize a tela e confira.", body!.Message);
        Assert.Empty(_logger.Errors);
    }

    [Theory]
    [InlineData(1213)] // deadlock
    [InlineData(1205)] // tempo de espera por trava esgotado
    public async Task LockConflict_Returns409AskingToTryAgain(int number)
    {
        var (status, body) = await HandleAsync(MySqlError(number));

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.StartsWith("Os dados foram alterados por outra operação ao mesmo tempo.", body!.Message);
    }

    [Fact]
    public async Task OtherDatabaseError_Returns500AndIsLogged()
    {
        var (status, body) = await HandleAsync(MySqlError(1146)); // tabela não existe

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal("Ocorreu um erro inesperado. Tente novamente.", body!.Message);
        Assert.Single(_logger.Errors);
    }

    [Fact]
    public async Task RequestCanceledByTheClient_IsNotLoggedAsError()
    {
        var (_, body) = await HandleAsync(new OperationCanceledException(), requestAborted: true);

        Assert.Null(body);
        Assert.Empty(_logger.Errors);
    }

    [Fact]
    public async Task CanceledOperation_WithoutTheClientGivingUp_IsStillAnError()
    {
        // Um cancelamento que não veio do cliente (ex.: timeout interno) continua sendo um erro de verdade.
        var (status, _) = await HandleAsync(new OperationCanceledException());

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Single(_logger.Errors);
    }

    /// <summary>Logger de teste que só guarda as mensagens de nível Error.</summary>
    private sealed class ListLogger : ILogger<GlobalExceptionHandler>
    {
        public List<string> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
            }
        }
    }
}
