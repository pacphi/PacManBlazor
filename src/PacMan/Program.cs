﻿// ReSharper disable HeapView.ObjectAllocation.Evident

using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MediatR.Pipeline;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using PacMan.GameComponents;
using PacMan.GameComponents.Audio;
using PacMan.GameComponents.Events;
using PacMan.GameComponents.GameActs;
using PacMan.GameComponents.Ghosts;
using PacMan.GameComponents.Requests;

namespace PacMan
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("app");

            IServiceCollection services = builder.Services;

            services.AddSingleton<IGame, Game>();
            services.AddSingleton<IGameStorage, GameStorage>();
            services.AddSingleton<IHumanInterfaceParser, HumanInterfaceParser>();
            services.AddSingleton<ISoundLoader, SoundLoader>();
            services.AddSingleton<IGameSoundPlayer, GameSoundPlayer>();

            services.AddSingleton<IAct, AttractAct>();
            services.AddSingleton<IAct, GameAct>();

            services.AddSingleton<IAct, BigPacChaseAct>();
            services.AddSingleton<IAct, GameOverAct>();
            services.AddSingleton<IAct, GhostTearAct>();
            services.AddSingleton<IAct, LevelFinishedAct>();
            services.AddSingleton<IAct, NullAct>();
            services.AddSingleton<IAct, PacManDyingAct>();
            services.AddSingleton<IAct, PlayerIntroAct>();
            services.AddSingleton<IAct, DemoPlayerIntroAct>();
            services.AddSingleton<IAct, StartButtonAct>();
            services.AddSingleton<IAct, TornGhostChaseAct>();

            services.AddSingleton<IAct, PlayerGameOverAct>();

            services.AddSingleton<IActs, Acts>();

            services.AddSingleton<IGameStats, GameStats>();

            services.AddSingleton<IHaveTheMazeCanvases, MazeCanvases>();

            services.AddSingleton<IGhost, Blinky>();
            services.AddSingleton<IGhost, Pinky>();
            services.AddSingleton<IGhost, Inky>();
            services.AddSingleton<IGhost, Clyde>();

            services.AddSingleton<IPacMan, PacMan.GameComponents.PacMan>();

            services.AddSingleton<ICoinBox, CoinBox>();

            services.AddSingleton<IFruit, Fruit>();

            services.AddSingleton<IGhostCollection, GhostCollection>();

            services.AddSingleton<IStatusPanel, StatusPanel>();
            services.AddSingleton<IScorePanel, ScorePanel>();

            services.AddSingleton<IMaze, Maze>();

            services.AddLogging();

            services.Add(new ServiceDescriptor(
                typeof(IExceptionNotificationService),
                typeof(ExceptionNotificationService),
                ServiceLifetime.Singleton));

            Assembly thisAssembly = Assembly.GetExecutingAssembly();

            Assembly componentsAssembly = typeof(ClassThatLivesInGameComponentsActsAsAMarkerForThisAssemblyForReflection).Assembly;

            services.AddMediatR(
                c =>
                    c.AsSingleton(),
                thisAssembly,
                componentsAssembly);

            services.AddSingleton(new HttpClient
            {
                BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
            });

            var host = builder.Build();

            await host.RunAsync();
        }

        private static void RegisterPolymorphicNotificationHandler<TNotification, THandler>(IServiceCollection services, params Assembly[] assemblies)
            where TNotification : INotification
            where THandler : INotificationHandler<TNotification>
        {
            var notificationTypes = assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(TNotification).IsAssignableFrom(type));

            foreach (var notificationType in notificationTypes)
            {
                var type = typeof(INotificationHandler<>).MakeGenericType(notificationType);
                services.AddScoped(type, typeof(THandler));
            }
        }
    }
}
