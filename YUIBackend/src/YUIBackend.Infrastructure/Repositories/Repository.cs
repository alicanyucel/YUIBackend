using Microsoft.EntityFrameworkCore;
using MyProject.Domain.Abstractions;
using MyProject.Infrastructure.Persistence;

namespace MyProject.Infrastructure.Repositories;

internal abstract class Repository<T>(AppDbContext context) : IRepository<T>
    where T : Entity
{
    protected readonly AppDbContext Context = context;
    protected readonly DbSet<T> DbSet = context.Set<T>();

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await DbSet.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => await DbSet.ToListAsync(cancellationToken);

    public virtual async Task<PagedResult<T>> GetPageAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking();
        return await query.ToPagedResultAsync(
            source => source.OrderBy(entity => entity.CreatedAt).ThenBy(entity => entity.Id),
            source => source.OrderByDescending(entity => entity.CreatedAt).ThenByDescending(entity => entity.Id),
            pageNumber,
            pageSize,
            cancellationToken);
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        => await DbSet.AddAsync(entity, cancellationToken);

    public virtual void Update(T entity)
        => DbSet.Update(entity);

    public virtual void Delete(T entity)
        => entity.SoftDelete();
}
