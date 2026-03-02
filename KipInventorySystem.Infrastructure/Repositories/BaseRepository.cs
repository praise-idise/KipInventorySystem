using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Infrastructure.Persistence;
using KipInventorySystem.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KipInventorySystem.Infrastructure.Repositories;

internal class BaseRepository<T>(ApplicationDbContext context) : IBaseRepository<T> where T : class
{
    protected readonly ApplicationDbContext _context = context;
    protected readonly DbSet<T> _dbSet = context.Set<T>();

    private static readonly string KeyPropertyName = GetKeyPropertyName();

    private static string GetKeyPropertyName()
    {
        var keyProperty = typeof(T).GetProperties()
            .FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null);

        if (keyProperty == null)
        {
            throw new InvalidOperationException($"No property marked with [Key] attribute found on type {typeof(T).Name}");
        }

        return keyProperty.Name;
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default) =>
        await _dbSet.AddAsync(entity, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddRangeAsync(entities, cancellationToken);
    }

    public void Update(T entity) => _dbSet.Update(entity);

    public void Remove(T entity) => _dbSet.Remove(entity);

    public void RemoveRange(IEnumerable<T> entities)
    {
        _dbSet.RemoveRange(entities);
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(e => EF.Property<Guid>(e, KeyPropertyName) == id, cancellationToken);
    }

    public async Task<T?> GetByIdAsync(Guid id, Func<IQueryable<T>, IQueryable<T>> include, CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet;
        if (include != null)
        {
            query = include(query);
        }
        return await query.FirstOrDefaultAsync(
            e => EF.Property<Guid>(e, KeyPropertyName) == id,
            cancellationToken);
    }

    public async Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _dbSet.ToListAsync(cancellationToken);

    public async Task<PaginationResult<T>> GetPagedItemsAsync(
        RequestParameters parameters,
        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        int page = Math.Max(1, parameters.PageNumber);
        int size = Math.Clamp(parameters.PageSize, 1, 100);

        IQueryable<T> query = _context.Set<T>();

        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        int totalRecords = await query.CountAsync(cancellationToken);

        if (totalRecords == 0)
        {
            return new PaginationResult<T>
            {
                Records = [],
                TotalRecords = 0,
                PageSize = size,
                CurrentPage = page
            };
        }

        var orderedQuery = orderBy(query);

        var pagedData = await orderedQuery
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return new PaginationResult<T>
        {
            Records = pagedData,
            TotalRecords = totalRecords,
            PageSize = size,
            CurrentPage = page
        };
    }

    public async Task<List<T>> WhereAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        await _dbSet.Where(predicate).ToListAsync(cancellationToken);

    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        await _dbSet.AnyAsync(predicate, cancellationToken);

    public async Task<T?> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return _dbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        if (predicate is not null)
        {
            return _dbSet.CountAsync(predicate, cancellationToken);
        }
        return _dbSet.CountAsync(cancellationToken);
    }

    public Task<List<T>> GetPagedListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        return _dbSet.Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public async Task<List<T>> GetAllIncludingDeletedAsync(CancellationToken cancellationToken = default) =>
        await _dbSet.IgnoreQueryFilters().ToListAsync(cancellationToken);

    public async Task<T?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => EF.Property<Guid>(e, KeyPropertyName) == id, cancellationToken);
    }

    public async Task<List<T>> WhereIncludingDeletedAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        await _dbSet.IgnoreQueryFilters().Where(predicate).ToListAsync(cancellationToken);
}
