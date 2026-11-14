namespace AdegaSystem.Api.Common;

/// <summary>
/// Parâmetros de paginação comuns a todas as listagens (?page=1&amp;pageSize=20).
/// Os valores são corrigidos para limites seguros, para que ninguém peça 1 milhão de linhas de uma vez.
/// </summary>
public class PagedQuery
{
    public const int MaxPageSize = 100;

    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = Math.Max(1, value);
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, MaxPageSize);
    }

    public int Skip => (Page - 1) * PageSize;
}
