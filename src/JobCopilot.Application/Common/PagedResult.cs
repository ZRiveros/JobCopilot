namespace JobCopilot.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount)
{
    public bool HasMore(int alreadyLoaded) => alreadyLoaded < TotalCount;
}
