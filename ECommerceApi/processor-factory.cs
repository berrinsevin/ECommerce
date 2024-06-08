using System.Threading;
using System.Threading.Tasks;
using Intertech.MtmAutomationNuget.Services;
using Microsoft.Extensions.DependencyInjection;
using Intertech.MtmAutomationNuget.LeaderElector;

namespace Intertech.MtmAutomationNuget.Factory
{
    /// <summary>
    /// ProcessorFactory
    /// </summary>
    public class ProcessorFactory
    {
        private readonly IServiceScopeFactory factory;

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="_factory"></param>
        public ProcessorFactory(IServiceScopeFactory _factory)
        {
            factory = _factory;
        }

        /// <summary>
        /// ExecuteProcessorAsync
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task ExecuteProcessorAsync<T>(CancellationToken token) where T : IMtmProcessor
        {
            using (var scope = factory.CreateAsyncScope())
            {
                var processor = scope.ServiceProvider.GetRequiredService<T>();
                if (!Equals(processor, default(T)))
                {
                    var delayTime = processor.GetDelayTime();
                    if (delayTime > 0)
                    {
                        await Task.Delay(delayTime, token);
                    }

                    bool isLeaderMachine = true;
                    if (processor.IsLeaderElectorActive())
                    {
                        var leaderElector = scope.ServiceProvider.GetRequiredService<ILeaderElector>();
                        isLeaderMachine = await leaderElector.IsLeaderAsync(typeof(T).Name);
                    }

                    if (isLeaderMachine)
                    {
                        await processor.ProcessAsync();
                    }
                }
            }
        }
    }
}
