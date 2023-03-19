﻿using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using NSwag;
using NSwag.Generation.Processors.Security;
using DDD.NETCore.HostedServices;
using DDD.Application.Exceptions;
using DDD.Application.Settings;
using DDD.Application.Settings.Auth;
using DDD.Application.Settings.Email;
using DDD.Application.Settings.Monitoring;
using DDD.Application.Settings.Persistence;
using DDD.Application.Settings.PubSub;
using DDD.Domain.Model.Auth;
using DDD.Domain.Services.Auth;
using DDD.Infrastructure.Ports.Adapters.Auth.IAM.Negative;
using DDD.Infrastructure.Ports.Adapters.Auth.IAM.PowerIam;
using DDD.Infrastructure.Ports.Adapters.Email.Memory;
using DDD.Infrastructure.Ports.Adapters.Email.Smtp;
using DDD.Infrastructure.Ports.Adapters.Monitoring.AppInsights;
using DDD.Infrastructure.Ports.Adapters.Monitoring.Memory;
using DDD.Infrastructure.Ports.Adapters.PubSub.Memory;
using DDD.Infrastructure.Ports.Adapters.PubSub.Postgres;
using DDD.Infrastructure.Ports.Adapters.PubSub.Rabbit;
using DDD.Infrastructure.Ports.Adapters.PubSub.ServiceBus;
using DDD.Infrastructure.Ports.Auth;
using DDD.Infrastructure.Ports.Monitoring;
using DDD.Infrastructure.Ports.Email;
using DDD.Infrastructure.Ports.PubSub;
using DDD.Infrastructure.Services.Persistence;
using DDD.Infrastructure.Services.Persistence.Memory;
using DDD.Infrastructure.Services.Persistence.Postgres;
using DDD.Infrastructure.Services.Publisher;
using DDD.NETCore.Extensions.Swagger;
using Microsoft.IdentityModel.Tokens;

namespace DDD.NETCore.Extensions
{
	public static class ServiceCollectionExtensions
	{
		// Public API

		public static IServiceCollection AddListener<TImplementation>(this IServiceCollection services)
			where TImplementation : class, IEventListener
		{
			services.AddTransient(typeof(IEventListener), typeof(TImplementation));
			services.AddTransient<TImplementation>();
			return services;
		}
		
		public static IServiceCollection AddAccessControl(this IServiceCollection services, ISettings settings)
		{
			services.AddScoped<ICredentials, Credentials>();
			services.AddTransient<IAuthDomainService, AuthDomainService>();
			services.AddIamAdapter(settings);
			return services;
		}
		
		public static IServiceCollection AddIamAdapter(this IServiceCollection services, ISettings settings)
		{
			if (settings.Auth.Rbac.Provider == RbacProvider.Negative)
			{
				services.AddTransient<IIamPort, NegativeIamAdapter>();
			}
			else if (settings.Auth.Rbac.Provider == RbacProvider.PowerIAM)
			{
				services.AddTransient<IIamPort, PowerIamAdapter>();
			}
			else
			{
				throw new DddException(
					$"Can't add iam adapter for unsupported " +
					$"rbac provider: '{settings.Auth.Rbac.Provider}'.");
			}
			return services;
		}
		
		public static IServiceCollection AddEmailAdapter(this IServiceCollection services, ISettings settings)
		{
			if (settings.Email.Provider == EmailProvider.Smtp)
			{
				services.AddTransient<IEmailPort, SmtpEmailAdapter>();
			}
			else if (settings.Email.Provider == EmailProvider.Memory)
			{
				services.AddTransient<IEmailPort, MemoryEmailAdapter>();
			}
			else
			{
				throw new DddException(
					$"Can't add email for unsupported " +
					$"email provider: '{settings.Email.Provider}'.");
			}
			return services;
		}
		
		public static IMvcCoreBuilder AddHttpAdapter(this IServiceCollection services, ISettings settings)
		{
			var builder = services
				.AddMvcCore(config => { })
				.AddJsonOptions(opts =>
				{
					opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
				});
			
			services.AddHttpClient();
			services.AddHttpAdapterDocs(settings);
			services.AddCorsPolicy(settings);
			
			return builder;
		}

		public static IServiceCollection AddMonitoring(this IServiceCollection services, ISettings settings)
		{
			if (settings.Monitoring.Provider == MonitoringProvider.Memory)
				services.AddSingleton<IMonitoringPort, MemoryMonitoringAdapter>();
			else if (settings.Monitoring.Provider == MonitoringProvider.AppInsights)
				services.AddSingleton<IMonitoringPort, AppInsightsMonitoringAdapter>();
			else
				throw new DddException(
					$"Can't add monitoring for unsupported provider: '{settings.Monitoring.Provider}'.");

			return services;
		}
		
		public static IServiceCollection AddPersistence(this IServiceCollection services, ISettings settings)
		{
			if (settings.Persistence.Provider == PersistenceProvider.Memory)
			{
				services.AddSingleton<IPersistenceService, MemoryPersistenceService>();
			}
			else if (settings.Persistence.Provider == PersistenceProvider.Postgres)
			{
				services.AddSingleton<IPersistenceService, PostgresPersistenceService>();
			}
			else
			{
				throw new DddException(
					$"Can't add persistence for unsupported " +
					$"persistence provider: '{settings.Persistence.Provider}'.");
			}
			return services;
		}

		public static IServiceCollection AddPubSub(this IServiceCollection services, ISettings settings)
		{
			services.AddPublishers(settings);
			services.AddEventAdapters(settings);
			services.AddOutbox(settings);
			services.AddDeadLetterQueue(settings);
			if (settings.PubSub.PublisherEnabled)
			{
				services.AddTransient<IPublisherService, PublisherService>();
				services.AddHostedService<PublisherHostedService>();
			}
			return services;
		}

		public static IServiceCollection AddAction<TAction, TCommand>(this IServiceCollection services)
		{
			services.AddTransient(typeof(TAction));
			services.AddTransient(typeof(TCommand));
			return services;
		}

		public static IServiceCollection AddHttpBuildingBlockTranslator<TTranslator>(this IServiceCollection services)
		{
			services.AddTransient(typeof(TTranslator));
			return services;
		}
		
		public static IServiceCollection AddHttpCommandTranslator<TTranslator>(this IServiceCollection services)
		{
			services.AddTransient(typeof(TTranslator));
			return services;
		}
		
		public static IServiceCollection AddMigrator<TMigrator>(this IServiceCollection services)
		{
			services.AddTransient(typeof(TMigrator));
			return services;
		}
		
		public static IServiceCollection AddRepository<TPort, TAdapter>(this IServiceCollection services)
			where TAdapter : class, IStartableRepository
		{
			services.AddSingleton<IStartableRepository, TAdapter>();
			services.AddSingleton(typeof(TPort), typeof(TAdapter));
			return services;
		}
		
		// Private API

		private static IServiceCollection AddCorsPolicy(this IServiceCollection services, ISettings settings)
		{
			services.AddCors(options =>
			{
				options.AddDefaultPolicy(
					policy => { policy
						.WithOrigins(settings.Http.Cors.AllowedOrigins.ToArray())
						.AllowAnyHeader()
						.WithMethods("GET", "POST", "PUT", "DELETE");
					});
			});
			return services;
		}

		private static IServiceCollection AddHttpAdapterDocs(this IServiceCollection services, ISettings settings)
		{
			if (settings.Http.Docs.Enabled)
			{
				services.AddMicrosoftApiExplorer();
				services.AddSwaggerDocuments(settings);
			}
			return services;
		}

		private static IServiceCollection AddPublishers(this IServiceCollection services, ISettings settings)
		{
			services.AddSingleton<IInterchangePublisher, InterchangePublisher>();
			services.AddSingleton<IDomainPublisher, DomainPublisher>();
			return services;
		}

		private static IServiceCollection AddEventAdapters(this IServiceCollection services, ISettings settings)
		{
			services.AddInterchangeEventAdapter(settings);
			services.AddDomainEventAdapter(settings);
			return services;
		}
		
		private static IServiceCollection AddOutbox(this IServiceCollection services, ISettings settings)
		{
			if (settings.Persistence.Provider == PersistenceProvider.Memory)
			{
				services.AddSingleton<IOutbox, MemoryOutbox>();
			}
			else if (settings.Persistence.Provider == PersistenceProvider.Postgres)
			{
				services.AddSingleton<IOutbox, PostgresOutbox>();
			}
			else
			{
				throw new DddException(
					$"Can't add outbox, unsupported persistence provider " +
					$"in config: '{settings.Persistence.Provider}'.");
			}
			return services;
		}
		
		private static IServiceCollection AddDeadLetterQueue(this IServiceCollection services, ISettings settings)
		{
			if (settings.Persistence.Provider == PersistenceProvider.Memory)
			{
				services.AddSingleton<IDeadLetterQueue, MemoryDeadLetterQueue>();
			}
			else if (settings.Persistence.Provider == PersistenceProvider.Postgres)
			{
				services.AddSingleton<IDeadLetterQueue, PostgresDeadLetterQueue>();
			}
			else
			{
				throw new DddException(
					$"Can't add dead letter queue, unsupported persistence provider " +
					$"in config: '{settings.Persistence.Provider}'.");
			}
			return services;
		}

		private static IServiceCollection AddInterchangeEventAdapter(this IServiceCollection services, ISettings settings)
		{
			if (settings.PubSub.Provider == PubSubProvider.ServiceBus)
				services.AddSingleton<IInterchangeEventAdapter, ServiceBusInterchangeEventAdapter>();
			else if (settings.PubSub.Provider == PubSubProvider.Rabbit)
				services.AddSingleton<IInterchangeEventAdapter, RabbitInterchangeEventAdapter>();
			else if (settings.PubSub.Provider == PubSubProvider.Memory)
				services.AddSingleton<IInterchangeEventAdapter, MemoryInterchangeEventAdapter>();
			return services;
		}

		private static IServiceCollection AddDomainEventAdapter(this IServiceCollection services, ISettings settings)
		{
			if (settings.PubSub.Provider == PubSubProvider.ServiceBus)
				services.AddSingleton<IDomainEventAdapter, ServiceBusDomainEventAdapter>();
			else if (settings.PubSub.Provider == PubSubProvider.Rabbit)
				services.AddSingleton<IDomainEventAdapter, RabbitDomainEventAdapter>();
			else if (settings.PubSub.Provider == PubSubProvider.Memory)
				services.AddSingleton<IDomainEventAdapter, MemoryDomainEventAdapter>();
			return services;
		}

		private static IServiceCollection AddMicrosoftApiExplorer(this IServiceCollection services)
		{
			var builder = services.AddMvcCore();
			builder.AddApiExplorer();
			return services;
		}

		private static IServiceCollection AddSwaggerDocuments(this IServiceCollection services, ISettings settings)
		{
			/*
			 * Each 'document' corresponds to a specific api version definition in the UI.
			 * Documents are added by the NSwag 'generator'.
			 * 
			 * The generator is defined below, it adds 'processors'.
			 * The 'document processor' is used to add the 'security definitions'.
			 * The 'operation processor' is used to add the endpoints.
			 *
			 * So NSwag will used the configured generator below to create the openapi yml file.
			 */
			foreach (var majorVersion in settings.Http.Docs.MajorVersions)
			{
				if (!settings.Http.Docs.Definitions.Any())
				{
					services.AddSwaggerDocument(settings, majorVersion, "", "");
				}
				else
				{
					foreach (var defSelector in settings.Http.Docs.Definitions)
					{
						services.AddSwaggerDocument(settings, majorVersion, defSelector.Name, defSelector.BasePath);
					}
				}
			}
			return services;
		}

		private static IServiceCollection AddSwaggerDocument(this IServiceCollection services, ISettings settings, int majorVersion, string definitionName, string basePath)
		{
			services.AddOpenApiDocument(c =>
			{
				var title = $"{settings.General.Context} API";

				if (settings.Http.Docs.Title != "")
					title = settings.Http.Docs.Title;

				c.Title = title;
				c.DocumentName = $"Version {majorVersion}{(!definitionName.IsNullOrEmpty() ? " ("+definitionName+")" : "")}";
				c.DocumentProcessors.Add(new DocumentProcessor(settings, majorVersion));

				// Security definitions
				var securityNames = new List<string>();

				if (settings.Auth.Enabled)
				{
					services.ValidateJwtSettings(settings.Auth.JwtToken);

					c.DocumentProcessors.Add(
						new SecurityDefinitionAppender(
							"JWT Token",
							new OpenApiSecurityScheme
							{
								Type = OpenApiSecuritySchemeType.ApiKey,
								Name = settings.Auth.JwtToken.Name,
								In = ApiKeyLocationFromString(settings.Auth.JwtToken.Location),
								Description =
									$"Type into the textbox: " +
									$"{settings.Auth.JwtToken.Scheme} " +
									$"{{your jwt token}}."
							}));

					securityNames.Add("JWT Token");

					foreach (var extraToken in settings.Http.Docs.AuthExtraTokens)
					{
						c.DocumentProcessors.Add(
							new SecurityDefinitionAppender(
								extraToken.Name,
								new OpenApiSecurityScheme
								{
									Type = OpenApiSecuritySchemeType.ApiKey,
									Name = extraToken.KeyName,
									In = ApiKeyLocationFromString(extraToken.Location),
									Description = extraToken.Description
								}));

						securityNames.Add(extraToken.Name);
					}
				}

				// Security requirements
				c.OperationProcessors.Insert(
					0,
					new OperationProcessor(
						majorVersion,
						definitionName,
						securityNames,
						basePath,
						settings.Http.Docs.Hostname,
						settings.Http.Docs.HttpEnabled,
						settings.Http.Docs.HttpsEnabled,
						settings.Http.Docs.HttpPort,
						settings.Http.Docs.HttpsPort));
			});
			
			return services;
		}
		
		private static IServiceCollection ValidateJwtSettings(this IServiceCollection services,
			IAuthJwtTokenSettings settings)
		{
			var errors = new List<string>();
			
			var allowedLocations = new List<string> { "header" };
			if (string.IsNullOrEmpty(settings.Location) || !allowedLocations.Contains(settings.Location.ToLower()))
				errors.Add($"'Location' must be one of: ('{string.Join("'|'", allowedLocations)}').");
			
			var allowedSchemes = new List<string> { "Bearer" };
			if (string.IsNullOrEmpty(settings.Scheme) || !allowedSchemes.Contains(settings.Scheme))
				errors.Add($"'Scheme' must be one of: ('{string.Join("'|'", allowedSchemes)}').");
			
			if (string.IsNullOrEmpty(settings.Name))
				errors.Add($"'Name' must be set.");

			if (errors.Count > 0)
				throw new SettingsException(
					$"Auth is enabled in the settings, but there are a/some invalid JWT auth setting(s). {string.Join(" ", errors)}");

			return services;
		}

		private static OpenApiSecurityApiKeyLocation ApiKeyLocationFromString(string value)
		{
			switch (value.ToLower())
			{
				case "cookie":
					return OpenApiSecurityApiKeyLocation.Cookie;
				case "header":
					return OpenApiSecurityApiKeyLocation.Header;
				case "query":
					return OpenApiSecurityApiKeyLocation.Query;
				default:
					throw new SettingsException(
						$"Unsupported 'location' in http docs auth def " +
						$"api key: {value}");
			}
		}
	}
}
