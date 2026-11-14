namespace AdegaSystem.Api.Common.Exceptions;

/// <summary>O registro pedido não existe. Vira HTTP 404.</summary>
public class NotFoundException(string message) : Exception(message);
