namespace AdegaSystem.Api.Common;

/// <summary>
/// Formato único de resposta da API. Toda resposta, de sucesso ou de erro,
/// tem os mesmos campos, então o frontend sempre sabe onde procurar a mensagem e os dados.
/// </summary>
public record ApiResponse<T>(bool Success, string? Message, T? Data, IReadOnlyList<ApiError> Errors)
{
    public static ApiResponse<T> Ok(T data, string? message = null) => new(true, message, data, []);
}

/// <summary>Resposta sem dados, usada em erros e em operações que não devolvem nada.</summary>
public static class ApiResponse
{
    public static ApiResponse<object> Ok(string message) => new(true, message, null, []);

    public static ApiResponse<object> Fail(string message, IReadOnlyList<ApiError>? errors = null) =>
        new(false, message, null, errors ?? []);
}

/// <summary>Um erro de validação: qual campo (quando houver) e o que está errado.</summary>
public record ApiError(string? Field, string Message);
