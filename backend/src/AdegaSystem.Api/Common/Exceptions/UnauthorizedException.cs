namespace AdegaSystem.Api.Common.Exceptions;

/// <summary>
/// Credenciais inválidas (login ou senha atual errada). O GlobalExceptionHandler responde HTTP 401.
/// A mensagem é sempre genérica, para não revelar se o e-mail existe.
/// </summary>
public class UnauthorizedException(string message) : Exception(message);
