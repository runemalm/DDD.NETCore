﻿using System;
using System.Threading.Tasks;
using DDD.Infrastructure.Ports.Adapters.Common.Exceptions;
using DDD.Infrastructure.Ports.Adapters.Common.Translation.Converters;
using DDD.Infrastructure.Ports.Monitoring;
using DDD.Infrastructure.Ports.PubSub;
using DDD.Logging;

namespace DDD.Infrastructure.Ports.Adapters.PubSub.Memory
{
	public class MemoryEventAdapter : EventAdapter<Subscription>
	{
		public MemoryEventAdapter(
			string topic,
			string client,
			int maxDeliveryRetries,
			ILogger logger,
			IMonitoringPort monitoringAdapter,
			SerializerSettings serializerSettings)
			: base(
				topic, 
				client, 
				maxDeliveryRetries,
				logger, 
				monitoringAdapter,
				serializerSettings)
		{
			
		}

		public override void Start()
		{
			IsStarted = true;
		}
		
		public override Task StartAsync()
		{
			IsStarted = true;
			return Task.CompletedTask;
		}

		public override void Stop()
		{
			IsStarted = false;
		}

		public override Task StopAsync()
		{
			Stop();
			return Task.CompletedTask;
		}

		public override Subscription Subscribe(IEventListener listener)
		{
			var subscription = new Subscription(listener);
			AddSubscription(subscription);
			return subscription;
		}

		public override Task<Subscription> SubscribeAsync(IEventListener listener)
			=> Task.FromResult(Subscribe(listener));

		public override void Unsubscribe(IEventListener listener)
		{
			var subscription = GetSubscription(listener);
			RemoveSubscription(subscription);
		}

		public override Task UnsubscribeAsync(IEventListener listener)
		{
			Unsubscribe(listener);
			return Task.CompletedTask;
		}

		public override Task AckAsync(IPubSubMessage message)
		{
			if (!(message is MemoryMessage))
			{
				throw new MemoryException(
					"Expected IPubSubMessage to be a MemoryMessage. " +
					"Something must be wrong with the implementation.");
			}

			var memoryMessage = (MemoryMessage)message;
			
			// Need no ack here since memory based event adapter will always succeed with delivery..

			return Task.CompletedTask;
		}

		public override async Task FlushAsync(OutboxEvent outboxEvent)
		{
			if (!IsStarted)
				throw new MemoryException("Can't flush event, memory event adapter is not started.");

			var message = new MemoryMessage(outboxEvent.JsonPayload);

			foreach (var sub in GetSubscriptions())
			{
				if (sub.EventName == outboxEvent.EventName)
				{
					if (sub.DomainModelVersion.ToStringWithWildcardBuild() ==
					    outboxEvent.DomainModelVersion.ToStringWithWildcardBuild())
					{
						await sub.Listener.Handle(message);
					}
				}
			}

			await base.FlushAsync(outboxEvent);
		}
	}
}
