using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;

namespace Intertech.CallCenterPlusNuget.Cache
{
    /// <summary>
    /// memory Cache base
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    public class CcpMemoryCache<TEntity> : IDisposable, ICcpCache<TEntity> where TEntity : class
    {
        private MemoryCacheEntryOptions MemoryCacheEntryOptions { get; set; }
        private readonly object cacheLock = new();
        private readonly string key = typeof(TEntity).Name + "Cache";
        private readonly IMemoryCache cache;

        /// <summary>
        /// Service scope factory resolve for repos
        /// </summary>
        protected readonly IServiceScopeFactory Factory;
        /// <summary>
        /// FxCache ctor
        /// </summary>
        /// <param name="_factory"></param>
        /// <param name="_cache"></param>
        public CcpMemoryCache(IServiceScopeFactory _factory, IMemoryCache _cache)
        {
            Factory = _factory;
            cache = _cache;
        }

        /// <summary>
        /// Gets single item by predicate
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public async Task<TEntity> TryGetItemAsync(Func<TEntity, bool> predicate)
        {
            var valueList = await TryGetListAsync();
            return await Task.FromResult(valueList?.FirstOrDefault(predicate));
        }

        /// <summary>
        /// Gets single item by predicate
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public async Task<IEnumerable<TEntity>> TryGetItemListAsync(Func<TEntity, bool> predicate)
        {
            var valueList = await TryGetListAsync();
            return await Task.FromResult(valueList?.Where(predicate));
        }

        /// <summary>
        /// Gets single item
        /// </summary>
        /// <returns></returns>
        public async Task<TEntity> TryGetSingleItemAsync()
        {
            var valueList = await TryGetListAsync();

            return await Task.FromResult(valueList?.FirstOrDefault());
        }

        /// <summary>
        /// Gets list from cache
        /// </summary>
        /// <returns></returns>
        public async Task<List<TEntity>> TryGetListAsync()
        {
            var valueList = await GetListAsync();

            if (valueList == null || (MemoryCacheEntryOptions != null && MemoryCacheEntryOptions.AbsoluteExpiration < DateTime.Now))
            {
                await TryReloadCacheAsync();
                valueList = await GetListAsync(true); // last try, if null we can't do anything more.
            }

            return valueList;
        }

        /// <summary>
        /// Gets list items
        /// </summary>
        /// <param name="isRecursed"></param>
        /// <returns></returns>
        public Task<List<TEntity>> GetListAsync(bool isRecursed)
        {
            var result = (List<TEntity>)cache.Get(key);
            if (result == null && isRecursed)
            {
                result = new List<TEntity>();
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets list items
        /// </summary>
        /// <returns></returns>
        public Task<List<TEntity>> GetListAsync() {
            return GetListAsync(false);
        }

        /// <summary>
        /// Bind entity list to memcache
        /// </summary>
        /// <param name="valueList"></param>
        protected void SetCacheList(List<TEntity> valueList)
        {
            lock (cacheLock)
            {
                cache.Set(key, valueList, MemoryCacheEntryOptions);
            }
        }

        /// <summary>
        /// Cache reload action
        /// </summary>
        public async Task TryReloadCacheAsync()
        {
            IEnumerable<TEntity> tempList = await GetEntityListAsync();
            MemoryCacheEntryOptions = SetPolicy();

            if (!tempList.Any())
            {
                return; // exception handler
            }

            SetCacheList(tempList.ToList());
        }

        /// <summary>
        /// Push Items to cached List
        /// </summary>
        public async Task TryPushItemsAsync(List<TEntity> itemList)
        {
            var valueList = await TryGetListAsync();

            valueList ??= new List<TEntity>();

            valueList.AddRange(itemList);

            SetCacheList(valueList);
        }

        /// <summary>
        /// Push Single item to cached List
        /// </summary>
        public async Task TryPushItemAsync(TEntity item)
        {
            var listItem = new List<TEntity> { item };

            await TryPushItemsAsync(listItem);
        }

        /// <summary>
        /// Set policy
        /// </summary>
        /// <returns></returns>
        protected virtual MemoryCacheEntryOptions SetPolicy()
        {
            return null;
        }

        /// <summary>
        /// Gets policy
        /// </summary>
        /// <returns></returns>
        public object GetPolicy()
        {
            return MemoryCacheEntryOptions;
        }

        /// <summary>
        /// Not implemented for memorycache
        /// </summary>
        /// <returns></returns>
        public int GetItemCount()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// GetCacheEntity
        /// </summary>
        /// <returns></returns>
        public Type GetCacheEntity()
        {
            return typeof(TEntity);
        }

        /// <summary>
        /// Get entities
        /// </summary>
        /// <returns></returns>
        public virtual Task<List<TEntity>> GetEntityListAsync()
        {
            return Task.FromResult(new List<TEntity>());
        }

        /// <summary>
        /// Dispose operation
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {

        }

        /// <summary>
        /// Dispose operation
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
