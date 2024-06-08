using System;
using System.Threading.Tasks;
using Intertech.MtmAutomationNuget.Services;
using Microsoft.Extensions.Caching.Distributed;

namespace Intertech.MtmAutomationNuget.LeaderElector
{
    /// <summary>
    /// LeaderElector
    /// </summary>
    public class LeaderElector : ILeaderElector
    {
        private readonly IMtmDistributedService service;

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="_service"></param>
        public LeaderElector(IMtmDistributedService _service)
        {
            service = _service;
        }

        /// <summary>
        /// IsLeader
        /// </summary>
        /// <returns></returns>
        public async Task<bool> IsLeaderAsync(string key)
        {
            var keyMachine = $"LeaderCacheMachine_{key}";
            var leaderMachineName = await service.GetCachedStringAsync(keyMachine);

            if (string.IsNullOrEmpty(leaderMachineName))
            {
                await TakeLeadAsync(key, Environment.MachineName);

                return true;
            }

            return leaderMachineName == Environment.MachineName;
        }

        /// <summary>
        /// IsLeaderMachine
        /// </summary>
        /// <returns></returns>
        public async Task<bool> IsLeaderMachineAsync(string applicationName)
        {
            var applicationMachineName = $"LeaderCacheMachine_{applicationName}";
            var leaderMachineName = await service.GetCachedStringAsync(applicationMachineName);

            if (string.IsNullOrEmpty(leaderMachineName))
            {
                await TakeLeadAsync(applicationName, Environment.MachineName);

                return true;
            }

            return leaderMachineName == Environment.MachineName;
        }

        /// <summary>
        /// Leader Machine Clean
        /// </summary>
        /// <returns></returns>
        public async Task TakeLeadAsync(string applicationName, string machineName)
        {
            var distributedCacheEntryOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTime.Now.AddMinutes(60)
            };

            await service.SetCacheStringAsync($"LeaderCacheMachine_{applicationName}", machineName, distributedCacheEntryOptions);
        }
    }
}
