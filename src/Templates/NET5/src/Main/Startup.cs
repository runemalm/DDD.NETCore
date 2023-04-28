﻿using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenDDD.Application.Settings;
using OpenDDD.Application.Settings.Persistence;
using OpenDDD.NET.Extensions;
using OpenDDD.NET.Hooks;
using Main.Extensions;
using Main.NET.Hooks;
using Application.Actions;
using Application.Actions.Commands;
using Domain.Model.Forecast;
using Domain.Model.Summary;
using Infrastructure.Ports.Adapters.Domain;
using Infrastructure.Ports.Adapters.Http.v1;
using Infrastructure.Ports.Adapters.Interchange.Translation;
using Infrastructure.Ports.Adapters.Repositories.Memory;
using Infrastructure.Ports.Adapters.Repositories.Migration;
using Infrastructure.Ports.Adapters.Repositories.Postgres;
using HttpCommonTranslation = Infrastructure.Ports.Adapters.Http.Common.Translation;

namespace Main
{
    public class Startup
    {
        private ISettings _settings;

        public Startup(
            ISettings settings)
        {
            _settings = settings;
        }
        
        public void ConfigureServices(IServiceCollection services)
        {
            // OpenDDD.NET
            services.AddAccessControl(_settings);
            services.AddMonitoring(_settings);
            services.AddPersistence(_settings);
            services.AddPubSub(_settings);
            services.AddTransactional(_settings);

            // App
            AddDomainServices(services);
            AddApplicationService(services);
            AddSecondaryAdapters(services);
            AddPrimaryAdapters(services);
            AddConversion(services);
            AddHooks(services);
        }

        public void Configure(
            IApplicationBuilder app, 
            IWebHostEnvironment env,
            IHostApplicationLifetime lifetime)
        {
            // OpenDDD.NET
            app.AddAccessControl(_settings);
            app.AddHttpAdapter(_settings);
            app.AddControl(lifetime);
        }

        // App
        
        private void AddDomainServices(IServiceCollection services)
        {
            services.AddDomainService<IForecastDomainService, ForecastDomainService>();
        }

        private void AddApplicationService(IServiceCollection services)
        {
            AddActions(services);
        }
        
        private void AddSecondaryAdapters(IServiceCollection services)
        {
            services.AddEmailAdapter(_settings);
            AddRepositories(services);
        }

        private void AddPrimaryAdapters(IServiceCollection services)
        {
            AddHttpAdapters(services);
            AddInterchangeEventAdapters(services);
            AddDomainEventAdapters(services);
        }

        private void AddHooks(IServiceCollection services)
        {
            services.AddTransient<IOnBeforePrimaryAdaptersStartedHook, OnBeforePrimaryAdaptersStartedHook>();
        }

        private void AddConversion(IServiceCollection services)
        {
            services.AddConversion(_settings);
        }

        private void AddActions(IServiceCollection services)
        {
            services.AddAction<GetAverageTemperatureAction, GetAverageTemperatureCommand>();
            services.AddAction<NotifyWeatherPredictedAction, NotifyWeatherPredictedCommand>();
            services.AddAction<PredictWeatherAction, PredictWeatherCommand>();
        }

        private void AddHttpAdapters(IServiceCollection services)
        {
            var mvcCoreBuilder = services.AddHttpAdapter(_settings);
            AddHttpAdapterCommon(services);
            AddHttpAdapterV1(services, mvcCoreBuilder);
        }

        private void AddHttpAdapterV1(IServiceCollection services, IMvcCoreBuilder mvcCoreBuilder)
        {
            mvcCoreBuilder.AddApplicationPart(Assembly.GetAssembly(typeof(HttpAdapter)));
            services.AddTransient<HttpCommonTranslation.Commands.PredictWeatherCommandTranslator>();
            services.AddTransient<HttpCommonTranslation.ForecastIdTranslator>();
            services.AddTransient<HttpCommonTranslation.ForecastTranslator>();
            services.AddTransient<HttpCommonTranslation.SummaryIdTranslator>();
            services.AddTransient<HttpCommonTranslation.SummaryTranslator>();
        }
        
        private void AddHttpAdapterCommon(IServiceCollection services)
        {
            services.AddHttpCommandTranslator<HttpCommonTranslation.Commands.PredictWeatherCommandTranslator>();

            services.AddHttpBuildingBlockTranslator<HttpCommonTranslation.ForecastIdTranslator>();
            services.AddHttpBuildingBlockTranslator<HttpCommonTranslation.ForecastTranslator>();
            services.AddHttpBuildingBlockTranslator<HttpCommonTranslation.SummaryIdTranslator>();
            services.AddHttpBuildingBlockTranslator<HttpCommonTranslation.SummaryTranslator>();
        }
        
        private void AddInterchangeEventAdapters(IServiceCollection services)
        {
            services.AddTransient<IIcForecastTranslator, IcForecastTranslator>();
        }
        
        private void AddDomainEventAdapters(IServiceCollection services)
        {
            services.AddListener<WeatherPredictedListener>();
        }
        
        private void AddRepositories(IServiceCollection services)
        {
            if (_settings.Persistence.Provider == PersistenceProvider.Memory)
            {
                services.AddRepository<IForecastRepository, MemoryForecastRepository>();
                services.AddRepository<ISummaryRepository, MemorySummaryRepository>();
            }
            else if (_settings.Persistence.Provider == PersistenceProvider.Postgres)
            {
                services.AddRepository<IForecastRepository, PostgresForecastRepository>();
                services.AddRepository<ISummaryRepository, PostgresSummaryRepository>();
            }
            services.AddMigrator<ForecastMigrator>();
            services.AddMigrator<SummaryMigrator>();
        }
    }
}
