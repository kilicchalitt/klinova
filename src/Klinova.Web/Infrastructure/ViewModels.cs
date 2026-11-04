namespace Klinova.Web.Infrastructure;

public sealed record PageHeaderModel(string Title, string? Subtitle = null);

public sealed record PagerModel(int Page, int TotalPages, int TotalCount, Func<int, string> UrlFor);
