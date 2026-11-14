namespace AdegaSystem.Api.Common.Exceptions;

/// <summary>
/// Uma regra de negócio foi violada (ex.: código de barras repetido, estoque insuficiente).
/// O GlobalExceptionHandler transforma essa exceção em HTTP 400 com a mensagem para o usuário.
/// </summary>
public class BusinessException(string message) : Exception(message);
