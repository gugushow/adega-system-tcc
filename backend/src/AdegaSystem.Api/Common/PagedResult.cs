namespace AdegaSystem.Api.Common;

/// <summary>Uma página de resultados e as informações para montar a paginação na tela.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
}
