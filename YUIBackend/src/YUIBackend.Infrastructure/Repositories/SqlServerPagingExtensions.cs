using Microsoft.EntityFrameworkCore;
using MyProject.Domain.Abstractions;

namespace MyProject.Infrastructure.Repositories;

internal static class SqlServerPagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
        Func<IQueryable<T>, IOrderedQueryable<T>> reverseOrderBy,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var offset = checked((pageNumber - 1) * pageSize);
        var orderedQuery = orderBy(query);
        List<T> items;

        if (offset == 0)
        {
            items = await orderedQuery.Take(pageSize).ToListAsync(cancellationToken);
        }
        else
        {
            items = await reverseOrderBy(orderedQuery.Take(checked(offset + pageSize)))
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            items.Reverse();
        }

        return new PagedResult<T>(items, totalCount, pageNumber, pageSize);
    }
}
