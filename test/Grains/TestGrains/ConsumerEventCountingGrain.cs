using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using UnitTests.GrainInterfaces;

namespace UnitTests.Grains
{
    public class ConsumerEventCountingGrain : Grain, IConsumerEventCountingGrain
    {
        private int _numConsumedItems;
        private ILogger _logger;
        private IAsyncObservable<int> _consumer;
        private StreamSubscriptionHandle<int> _subscriptionHandle;
        internal const string StreamNamespace = "HaloStreamingNamespace";

        public ConsumerEventCountingGrain(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger($"{this.GetType().Name}-{this.IdentityString}");
        }

        private class AsyncObserver<T> : IAsyncObserver<T>
        {
            private readonly Func<T, Task> _onNext;

            public AsyncObserver(Func<T, Task> onNext)
            {
                _onNext = onNext;
            }

            public Task OnNextAsync(T item, StreamSequenceToken token = null)
            {
                return _onNext(item);
            }

            public Task OnCompletedAsync()
            {
                return Task.CompletedTask;
            }

            public Task OnErrorAsync(Exception ex)
            {
                return Task.CompletedTask;
            }
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            _logger.Info("Consumer.OnActivateAsync");
            _numConsumedItems = 0;
            _subscriptionHandle = null;
            return base.OnActivateAsync(cancellationToken);
        }

        public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
        {
            _logger.Info("Consumer.OnDeactivateAsync");
            await StopConsuming();
            _numConsumedItems = 0;
            await base.OnDeactivateAsync(reason, cancellationToken);
        }

        public async Task BecomeConsumer(Guid streamId, string providerToUse)
        {
            _logger.Info("Consumer.BecomeConsumer");
            if (String.IsNullOrEmpty(providerToUse))
            {
                throw new ArgumentNullException("providerToUse");
            }
            IStreamProvider streamProvider = this.GetStreamProvider(providerToUse);
            IAsyncStream<int> stream = streamProvider.GetStream<int>(streamId, StreamNamespace);
            _consumer = stream;
            _subscriptionHandle = await _consumer.SubscribeAsync(new AsyncObserver<int>(EventArrived));
        }

        private Task EventArrived(int evt)
        {
            _numConsumedItems++;
            _logger.Info("Consumer.EventArrived. NumConsumed so far: " + _numConsumedItems);
            return Task.CompletedTask;
        }

        public async Task StopConsuming()
        {
            _logger.Info("Consumer.StopConsuming");
            if (_subscriptionHandle != null && _consumer != null)
            {
                await _subscriptionHandle.UnsubscribeAsync();
                _subscriptionHandle = null;
                _consumer = null;
            }
        }

        public Task<int> GetNumberConsumed()
        {
            return Task.FromResult(_numConsumedItems); 
        }
    }
}