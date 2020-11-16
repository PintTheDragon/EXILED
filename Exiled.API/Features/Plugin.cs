// -----------------------------------------------------------------------
// <copyright file="Plugin.cs" company="Exiled Team">
// Copyright (c) Exiled Team. All rights reserved.
// Licensed under the CC BY-SA 3.0 license.
// </copyright>
// -----------------------------------------------------------------------

namespace Exiled.API.Features
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    using CommandSystem;

    using Exiled.API.Enums;
    using Exiled.API.Extensions;
    using Exiled.API.Interfaces;

    using RemoteAdmin;

    /// <summary>
    /// Expose how a plugin has to be made.
    /// </summary>
    /// <typeparam name="TConfig">The config type.</typeparam>
    public abstract class Plugin<TConfig> : IPlugin<TConfig>
        where TConfig : IConfig, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin{TConfig}"/> class.
        /// </summary>
        public Plugin()
        {
            Name = Assembly.GetName().Name;
            Prefix = Name.ToSnakeCase();
            Author = ((AssemblyCompanyAttribute)Attribute.GetCustomAttribute(Assembly, typeof(AssemblyCompanyAttribute), false))?.Company;
            Version = Assembly.GetName().Version;
        }

        /// <inheritdoc/>
        public string Location() => Exiled.Loader.Loader.PluginLocations[this];

        /// <inheritdoc/>
        public Assembly Assembly { get; } = Assembly.GetCallingAssembly();

        /// <inheritdoc/>
        public virtual string Name { get; }

        /// <inheritdoc/>
        public virtual string Prefix { get; }

        /// <inheritdoc/>
        public virtual string Author { get; }

        /// <inheritdoc/>
        public virtual PluginPriority Priority { get; }

        /// <inheritdoc/>
        public virtual Version Version { get; }

        /// <inheritdoc/>
        public virtual Version RequiredExiledVersion { get; } = new Version(2, 1, 0);

        /// <inheritdoc/>
        public Dictionary<Type, Dictionary<Type, ICommand>> Commands { get; } = new Dictionary<Type, Dictionary<Type, ICommand>>()
        {
            { typeof(RemoteAdminCommandHandler), new Dictionary<Type, ICommand>() },
            { typeof(GameConsoleCommandHandler), new Dictionary<Type, ICommand>() },
            { typeof(ClientCommandHandler), new Dictionary<Type, ICommand>() },
        };

        /// <inheritdoc/>
        public TConfig Config { get; } = new TConfig();

        /// <inheritdoc/>
        public virtual void OnEnabled() => Log.Info($"{Name} v{Version.Major}.{Version.Minor}.{Version.Build}, made by {Author}, has been enabled!");

        /// <inheritdoc/>
        public virtual void OnDisabled() => Log.Info($"{Name} has been disabled!");

        /// <inheritdoc/>
        public virtual void OnReloaded() => Log.Info($"{Name} has been reloaded!");

        /// <inheritdoc/>
        public virtual void OnRegisteringCommands()
        {
            foreach (Type type in Assembly.GetTypes())
            {
                if (type.GetInterface("ICommand") != typeof(ICommand))
                    continue;

                if (!Attribute.IsDefined(type, typeof(CommandHandlerAttribute)))
                    continue;

                foreach (CustomAttributeData customAttributeData in type.CustomAttributes)
                {
                    try
                    {
                        if (customAttributeData.AttributeType != typeof(CommandHandlerAttribute))
                            continue;

                        Type commandType = (Type)customAttributeData.ConstructorArguments?[0].Value;

                        if (!Commands.TryGetValue(commandType, out Dictionary<Type, ICommand> typeCommands))
                            continue;

                        if (!typeCommands.TryGetValue(type, out ICommand command))
                            command = (ICommand)Activator.CreateInstance(type);

                        if (commandType == typeof(RemoteAdminCommandHandler))
                            CommandProcessor.RemoteAdminCommandHandler.RegisterCommand(command);
                        else if (commandType == typeof(GameConsoleCommandHandler))
                            GameCore.Console.singleton.ConsoleCommandHandler.RegisterCommand(command);
                        else if (commandType == typeof(ClientCommandHandler))
                            QueryProcessor.DotCommandHandler.RegisterCommand(command);

                        Commands[commandType][type] = command;
                    }
                    catch (Exception exception)
                    {
                        Log.Error($"An error has occurred while registering a command: {exception}");
                    }
                }
            }
        }

        /// <inheritdoc/>
        public virtual void OnUnregisteringCommands()
        {
            foreach (KeyValuePair<Type, Dictionary<Type, ICommand>> types in Commands)
            {
                foreach (ICommand command in types.Value.Values)
                {
                    if (types.Key == typeof(RemoteAdminCommandHandler))
                        CommandProcessor.RemoteAdminCommandHandler.UnregisterCommand(command);
                    else if (types.Key == typeof(GameConsoleCommandHandler))
                        GameCore.Console.singleton.ConsoleCommandHandler.UnregisterCommand(command);
                    else if (types.Key == typeof(ClientCommandHandler))
                        QueryProcessor.DotCommandHandler.UnregisterCommand(command);
                }
            }
        }

        /// <inheritdoc/>
        public int CompareTo(IPlugin<IConfig> other) => -Priority.CompareTo(other.Priority);
    }
}
