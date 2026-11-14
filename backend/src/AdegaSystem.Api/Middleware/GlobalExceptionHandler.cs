using System.Text.Json;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace AdegaSystem.Api.Middleware;

/// <summary>
/// Tratamento global de exceções. Os services apenas lançam exceções
/// (BusinessException, NotFoundException, UnauthorizedException, ValidationException) e este handler
/// decide o status HTTP e monta a resposta padrão. Assim, nenhum controller precisa de try/catch.
/// </summary>
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Códigos de erro do MySQL tratados aqui.</summary>
    private const int MySqlDuplicateEntry = 1062;
    private const int MySqlLockWaitTimeout = 1205;
    private const int MySqlDeadlock = 1213;

    private const string ConcurrencyMessage =
        "Os dados foram alterados por outra operação ao mesmo tempo. Atualize a tela e tente novamente.";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Quem chamou desistiu (fechou a aba, cancelou a requisição): não é erro do sistema
        // e não há para quem responder.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Requisição {Method} {Path} cancelada pelo cliente.", httpContext.Request.Method, httpContext.Request.Path);
            return true;
        }

        var mySqlError = FindMySqlErrorNumber(exception);
        var (statusCode, response) = exception switch
        {
            ValidationException validation => (StatusCodes.Status400BadRequest, ApiResponse.Fail(
                "Dados inválidos.",
                // "SalePrice" vira "salePrice", o mesmo nome do campo no JSON, para o frontend
                // conseguir marcar o campo certo do formulário.
                validation.Errors.Select(e => new ApiError(ToJsonFieldName(e.PropertyName), e.ErrorMessage)).ToList())),
            BusinessException => (StatusCodes.Status400BadRequest, ApiResponse.Fail(exception.Message)),
            NotFoundException => (StatusCodes.Status404NotFound, ApiResponse.Fail(exception.Message)),
            // Login com e-mail ou senha errados.
            UnauthorizedException => (StatusCodes.Status401Unauthorized, ApiResponse.Fail(exception.Message)),
            // Duas operações mexeram no estoque do mesmo produto ao mesmo tempo (ver o token de
            // concorrência em ProductConfiguration). A segunda é desfeita e o usuário tenta de novo.
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, ApiResponse.Fail(ConcurrencyMessage)),
            // Os services já conferem duplicidade antes de gravar, com mensagem própria. Isto cobre o caso raro
            // de duas pessoas salvarem o mesmo CPF/CNPJ/e-mail no mesmo instante: o índice único do banco barra a segunda.
            _ when mySqlError == MySqlDuplicateEntry => (StatusCodes.Status409Conflict, ApiResponse.Fail(
                "Já existe um registro com esses dados (outra pessoa acabou de salvar). Atualize a tela e confira.")),
            // Duas operações disputaram os mesmos registros (ex.: dois resgates de pontos do mesmo cliente)
            // e o banco desfez uma delas para manter os dados certos.
            _ when mySqlError is MySqlDeadlock or MySqlLockWaitTimeout => (StatusCodes.Status409Conflict, ApiResponse.Fail(ConcurrencyMessage)),
            _ => (StatusCodes.Status500InternalServerError, ApiResponse.Fail("Ocorreu um erro inesperado. Tente novamente.")),
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            // Erros inesperados são registrados no log com todos os detalhes,
            // mas o cliente recebe só uma mensagem genérica (sem stack trace nem dados internos).
            logger.LogError(exception, "Erro não tratado em {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }

    /// <summary>Procura um erro do MySQL na exceção ou nas internas (o EF embrulha em DbUpdateException).</summary>
    private static int? FindMySqlErrorNumber(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is MySqlException mySql)
            {
                return mySql.Number;
            }
        }

        return null;
    }

    /// <summary>Converte cada parte do caminho: "Items[0].UnitCost" vira "items[0].unitCost".</summary>
    private static string ToJsonFieldName(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
