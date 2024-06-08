using System;
using System.Linq;
using Ardalis.GuardClauses;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Intertech.MtmAutomation.Context;
using Intertech.MtmAutomationNuget.Enums;
using Intertech.MtmAutomationNuget.Entity;

namespace Intertech.MtmAutomationNuget.Repository
{
    /// <summary>
    /// Repository base class
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MtmRepository<T> : IMtmRepository<T> where T : class
    {
        /// <summary>
        /// context
        /// </summary>
        /// <value></value>
        public MtmDbContext Context { get; }

        /// <summary>
        /// DbContext Injector ctor
        /// </summary>
        /// <param name="_context"></param>
        public MtmRepository(MtmDbContext _context)
        {
            Guard.Against.Null(_context, nameof(DbContext));
            Context = _context;
        }

        /// <summary>
        /// Implemenets insert item async
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public async Task<bool> TryInsertItemAsync(T item)
        {
            await Context.Set<T>().AddAsync(item);
            return await Context.SaveChangesAsync() > 0;
        }

        /// <summary>
        /// Inserts list
        /// </summary>
        /// <param name="itemList"></param>
        /// <returns></returns>
        public async Task<bool> TryInsertListAsync(ICollection<T> itemList)
        {
            await Context.Set<T>().AddRangeAsync(itemList);
            return await Context.SaveChangesAsync() > 0;
        }

        /// <summary>
        /// Update item async
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public async Task<bool> TryUpdateItemAsync(T item)
        {
            Context.Set<T>().Update(item);
            return await Context.SaveChangesAsync() > 0;
        }

        /// <summary>
        /// Updates multiple item async
        /// </summary>
        /// <param name="itemList"></param>
        /// <returns></returns>
        public async Task<bool> TryUpdateListAsync(ICollection<T> itemList)
        {
            Context.Set<T>().UpdateRange(itemList);
            return await Context.SaveChangesAsync() > 0;
        }

        /// <summary>
        /// Delete item async
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public async Task<bool> TryDeleteItemAsync(T item)
        {
            Context.Set<T>().Remove(item);
            return await Context.SaveChangesAsync() > 0;
        }

        /// <summary>
        /// Delete list
        /// </summary>
        /// <param name="itemList"></param>
        /// <returns></returns>
        public async Task<bool> TryDeleteListAsync(ICollection<T> itemList)
        {
            Context.RemoveRange(itemList);
            return await Context.SaveChangesAsync() > 0;
        }

        /// <summary>
        /// get item with navigation props
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="readStyle"></param>
        /// <param name="includeProps"></param>
        /// <returns></returns>
        public async Task<T> GetItemAsync(Expression<Func<T, bool>> predicate, ReadStyle readStyle, params string[] includeProps)
        {
            IQueryable<T> query = GetEntitySet();

            if (readStyle == ReadStyle.ReadOnly)
            {
                query = query.AsNoTracking();
            }

            if (includeProps != null && includeProps.Length > 0)
            {
                foreach (var item in includeProps)
                {
                    query = query.Include(item);
                }
            }

            return await query.FirstOrDefaultAsync(predicate);
        }

        /// <summary>
        /// get item with navigation props
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="includeProps"></param>
        /// <returns></returns>
        public async Task<T> GetItemAsync(Expression<Func<T, bool>> predicate, params string[] includeProps)
        {
            return await GetItemAsync(predicate, ReadStyle.Executable, includeProps);
        }

        /// <summary>
        /// get filtered items with navigation props
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="readStyle"></param>
        /// <param name="includeProps"></param>
        /// <returns></returns>
        public async Task<IQueryable<T>> GetFilteredItemsAsync(Expression<Func<T, bool>> predicate, ReadStyle readStyle, params string[] includeProps)
        {
            IQueryable<T> query = GetEntitySet();

            if (readStyle == ReadStyle.ReadOnly)
            {
                query = query.AsNoTracking();
            }

            if (includeProps != null && includeProps.Length > 0)
            {
                foreach (var item in includeProps)
                {
                    query = query.Include(item);
                }
            }

            return await Task.FromResult(query.Where(predicate));
        }

        /// <summary>
        /// get filtered items with navigation props
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="includeProps"></param>
        /// <returns></returns>
        public async Task<IQueryable<T>> GetFilteredItemsAsync(Expression<Func<T, bool>> predicate, params string[] includeProps)
        {
            return await GetFilteredItemsAsync(predicate, ReadStyle.Executable, includeProps);
        }

        /// <summary>
        /// get all items with navigation props
        /// </summary>
        /// <param name="readStyle"></param>
        /// <param name="includeProps"></param>
        /// <returns></returns>
        public async Task<IQueryable<T>> GetAllItemsAsync(ReadStyle readStyle, params string[] includeProps)
        {
            IQueryable<T> query = GetEntitySet();

            if (readStyle == ReadStyle.ReadOnly)
            {
                query = query.AsNoTracking();
            }

            if (includeProps != null && includeProps.Length > 0)
            {
                foreach (var item in includeProps)
                {
                    query = query.Include(item);
                }
            }

            return await Task.FromResult(query);
        }

        /// <summary>
        /// get all items with navigation props
        /// </summary>
        /// <param name="includeProps"></param>
        /// <returns></returns>
        public async Task<IQueryable<T>> GetAllItemsAsync(params string[] includeProps)
        {
            return await GetAllItemsAsync(ReadStyle.Executable, includeProps);
        }

        /// <summary>
        /// Truncates partition
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="tableName"></param>
        /// <param name="partitionStatusOrder"></param>
        /// <returns></returns>
        public async Task<int> TruncatePartitionAsync(string schema, string tableName, int partitionStatusOrder)
        {
            return await ExecuteQueryAsync($"TRUNCATE TABLE {schema}.{tableName} WITH (PARTITIONS ({partitionStatusOrder}))");
        }

        /// <summary>
        /// Executes raw sql
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public async Task<IQueryable<T>> FromRawQueryAsync(string query)
        {
            return await Task.FromResult(Context.Set<T>().FromSqlRaw(query));
        }

        /// <summary>
        /// exec query
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public async Task<int> ExecuteQueryAsync(string query)
        {
            return await Task.FromResult(Context.Database.ExecQuery(query));
        }

        /// <summary>
        /// Sets record status
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        public void SetSearchQueryStatus(string status)
        {
            Context.RecordStatus = status;
        }

        private IQueryable<T> GetEntitySet()
        {
            if (string.IsNullOrEmpty(Context.RecordStatus))
            {   
                return Context.Set<T>().IgnoreQueryFilters();
            }

            return Context.Set<T>();
        }
    }
}
