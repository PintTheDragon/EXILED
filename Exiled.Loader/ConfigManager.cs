// -----------------------------------------------------------------------
// <copyright file="ConfigManager.cs" company="Exiled Team">
// Copyright (c) Exiled Team. All rights reserved.
// Licensed under the CC BY-SA 3.0 license.
// </copyright>
// -----------------------------------------------------------------------

namespace Exiled.Loader
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Exiled.API.Extensions;
    using Exiled.API.Features;
    using Exiled.API.Interfaces;
    using Exiled.Loader.Features.Configs;

    using YamlDotNet.Core;
    using YamlDotNet.Serialization;
    using YamlDotNet.Serialization.NamingConventions;
    using YamlDotNet.Serialization.NodeDeserializers;

    /// <summary>
    /// Used to handle plugin configs.
    /// </summary>
    public static class ConfigManager
    {
        /// <summary>
        /// Gets the config serializer.
        /// </summary>
        public static ISerializer Serializer { get; } = new SerializerBuilder()
            .WithTypeInspector(inner => new CommentGatheringTypeInspector(inner))
            .WithEmissionPhaseObjectGraphVisitor(args => new CommentsObjectGraphVisitor(args.InnerVisitor))
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreFields()
            .Build();

        /// <summary>
        /// Gets the config serializer.
        /// </summary>
        public static IDeserializer Deserializer { get; } = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithNodeDeserializer(inner => new ValidatingNodeDeserializer(inner), deserializer => deserializer.InsteadOf<ObjectNodeDeserializer>())
            .IgnoreFields()
            .IgnoreUnmatchedProperties()
            .Build();

        /// <inheritdoc cref="LoadSorted(string)"/>
        [Obsolete("Replaced with LoadSorted(string)", true)]
        public static Dictionary<string, IConfig> Load(string rawConfigs) => new Dictionary<string, IConfig>(LoadSorted(rawConfigs));

        /// <summary>
        /// Loads all plugin configs.
        /// </summary>
        /// <param name="rawConfigs">The raw configs to be loaded.</param>
        /// <returns>Returns a dictionary of loaded configs.</returns>
        public static SortedDictionary<string, IConfig> LoadSorted(string rawConfigs)
        {
            try
            {
                Log.Info("Loading plugin configs...");

                Dictionary<string, object> rawDeserializedConfigs = Deserializer.Deserialize<Dictionary<string, object>>(rawConfigs) ?? new Dictionary<string, object>();
                SortedDictionary<string, IConfig> deserializedConfigs = new SortedDictionary<string, IConfig>(StringComparer.Ordinal);

                if (!rawDeserializedConfigs.TryGetValue("exiled_loader", out object rawDeserializedConfig))
                {
                    Log.Warn($"Exiled.Loader doesn't have default configs, generating...");

                    deserializedConfigs.Add("exiled_loader", Loader.Config);
                }
                else
                {
                    deserializedConfigs.Add("exiled_loader", Deserializer.Deserialize<Config>(Serializer.Serialize(rawDeserializedConfig)));

                    Loader.Config.CopyProperties(deserializedConfigs["exiled_loader"]);
                }

                foreach (IPlugin<IConfig> plugin in Loader.Plugins)
                {
                    if (!rawDeserializedConfigs.TryGetValue(plugin.Prefix, out rawDeserializedConfig))
                    {
                        Log.Warn($"{plugin.Name} doesn't have default configs, generating...");

                        deserializedConfigs.Add(plugin.Prefix, plugin.Config);
                    }
                    else
                    {
                        try
                        {
                            deserializedConfigs.Add(plugin.Prefix, (IConfig)Deserializer.Deserialize(Serializer.Serialize(rawDeserializedConfig), plugin.Config.GetType()));

                            plugin.Config.CopyProperties(deserializedConfigs[plugin.Prefix]);
                        }
                        catch (YamlException yamlException)
                        {
                            Log.Error($"{plugin.Name} configs could not be loaded, some of them are in a wrong format, default configs will be loaded instead! {yamlException}");

                            deserializedConfigs.Add(plugin.Prefix, plugin.Config);
                        }
                    }
                }

                Log.Info("Plugin configs loaded successfully!");

                return deserializedConfigs;
            }
            catch (Exception exception)
            {
                Log.Error($"An error has occurred while loading configs! {exception}");

                return null;
            }
        }

        /// <summary>
        /// Loads all plugin translations.
        /// </summary>
        /// <param name="rawTranslations">The raw translations to be loaded.</param>
        /// <returns>Returns a dictionary of loaded translations.</returns>
        public static SortedDictionary<string, ITranslations> LoadSortedTranslations(string rawTranslations)
        {
            try
            {
                Log.Info("Loading plugin translations...");

                Dictionary<string, object> rawDeserializedTranslations = Deserializer.Deserialize<Dictionary<string, object>>(rawTranslations) ?? new Dictionary<string, object>();
                SortedDictionary<string, ITranslations> deserializedTranslations = new SortedDictionary<string, ITranslations>(StringComparer.Ordinal);

                foreach (IPlugin<IConfig> plugin in Loader.Plugins)
                {
                    if (!rawDeserializedTranslations.TryGetValue(plugin.Prefix, out object rawDeserializedTranslation))
                    {
                        Log.Warn($"{plugin.Name} doesn't have default translations, generating...");

                        deserializedTranslations.Add(plugin.Prefix, plugin.Translations);
                    }
                    else
                    {
                        try
                        {
                            deserializedTranslations.Add(plugin.Prefix, (ITranslations)Deserializer.Deserialize(Serializer.Serialize(rawDeserializedTranslation), plugin.Translations.GetType()));

                            plugin.Translations.CopyProperties(deserializedTranslations[plugin.Prefix]);
                        }
                        catch (YamlException yamlException)
                        {
                            Log.Error($"{plugin.Name} translations could not be loaded, some of them are in a wrong format, default translations will be loaded instead! {yamlException}");

                            deserializedTranslations.Add(plugin.Prefix, plugin.Translations);
                        }
                    }
                }

                Log.Info("Plugin translations loaded successfully!");

                return deserializedTranslations;
            }
            catch (Exception exception)
            {
                Log.Error($"An error has occurred while loading translations! {exception}");

                return null;
            }
        }

        /// <summary>
        /// Reads, Loads and Saves plugin configs.
        /// </summary>
        /// <returns>Returns a value indicating if the reloading process has been completed successfully or not.</returns>
        public static bool Reload() => Save(LoadSorted(Read()));

        /// <summary>
        /// Reads, Loads and Saves plugin translations.
        /// </summary>
        /// <returns>Returns a value indicating if the reloading process has been completed successfully or not.</returns>
        public static bool ReloadTranslations() => SaveTranslations(LoadSortedTranslations(ReadTranslations()));

        /// <summary>
        /// Reads, Loads and Saves plugin configs and translations.
        /// </summary>
        /// <returns>Returns a value indicating if the reloading process has been completed successfully or not.</returns>
        public static bool ReloadAll() => Reload() && ReloadTranslations();

        /// <summary>
        /// Saves plugin configs.
        /// </summary>
        /// <param name="configs">The configs to be saved, already serialized in yaml format.</param>
        /// <returns>Returns a value indicating whether the configs have been saved successfully or not.</returns>
        public static bool Save(string configs)
        {
            try
            {
                File.WriteAllText(Paths.Config, configs ?? string.Empty);

                return true;
            }
            catch (Exception exception)
            {
                Log.Error($"An error has occurred while saving configs to {Paths.Config} path: {exception}");

                return false;
            }
        }

        /// <summary>
        /// Saves plugin translations.
        /// </summary>
        /// <param name="translations">The translations to be saved, already serialized in yaml format.</param>
        /// <returns>Returns a value indicating whether the translations have been saved successfully or not.</returns>
        public static bool SaveTranslations(string translations)
        {
            try
            {
                File.WriteAllText(Paths.Translations, translations ?? string.Empty);

                return true;
            }
            catch (Exception exception)
            {
                Log.Error($"An error has occurred while saving translations to {Paths.Translations} path: {exception}");

                return false;
            }
        }

        /// <inheritdoc cref="Save(SortedDictionary{string, IConfig})"/>
        [Obsolete("Replaced with Save(SortedDictionary{string, IConfig})", true)]
        public static bool Save(Dictionary<string, IConfig> configs) => Save(new SortedDictionary<string, IConfig>(configs));

        /// <summary>
        /// Saves plugin configs.
        /// </summary>
        /// <param name="configs">The configs to be saved.</param>
        /// <returns>Returns a value indicating whether the configs have been saved successfully or not.</returns>
        public static bool Save(SortedDictionary<string, IConfig> configs)
        {
            try
            {
                if (configs == null || configs.Count == 0)
                    return false;

                return Save(Serializer.Serialize(configs));
            }
            catch (YamlException yamlException)
            {
                Log.Error($"An error has occurred while serializing configs: {yamlException}");

                return false;
            }
        }

        /// <summary>
        /// Saves plugin translations.
        /// </summary>
        /// <param name="translations">The translations to be saved.</param>
        /// <returns>Returns a value indicating whether the translations have been saved successfully or not.</returns>
        public static bool SaveTranslations(SortedDictionary<string, ITranslations> translations)
        {
            try
            {
                if (translations == null || translations.Count == 0)
                    return false;

                return SaveTranslations(Serializer.Serialize(translations));
            }
            catch (YamlException yamlException)
            {
                Log.Error($"An error has occurred while serializing translations: {yamlException}");

                return false;
            }
        }

        /// <summary>
        /// Read all plugin configs.
        /// </summary>
        /// <returns>Returns the read configs.</returns>
        public static string Read()
        {
            try
            {
                if (File.Exists(Paths.Config))
                    return File.ReadAllText(Paths.Config);
            }
            catch (Exception exception)
            {
                Log.Error($"An error has occurred while reading configs from {Paths.Config} path: {exception}");
            }

            return string.Empty;
        }

        /// <summary>
        /// Read all plugin translations.
        /// </summary>
        /// <returns>Returns the read translations.</returns>
        public static string ReadTranslations()
        {
            try
            {
                if (File.Exists(Paths.Translations))
                    return File.ReadAllText(Paths.Translations);
            }
            catch (Exception exception)
            {
                Log.Error($"An error has occurred while reading translations from {Paths.Translations} path: {exception}");
            }

            return string.Empty;
        }

        /// <summary>
        /// Clears the configs.
        /// </summary>
        /// <returns>Returns a value indicating whether configs have been cleared successfully or not.</returns>
        public static bool Clear() => Save(string.Empty);

        /// <summary>
        /// Reloads RemoteAdmin configs.
        /// </summary>
        public static void ReloadRemoteAdmin()
        {
            ServerStatic.RolesConfig = new YamlConfig(ServerStatic.RolesConfigPath);
            ServerStatic.SharedGroupsConfig = (GameCore.ConfigSharing.Paths[4] == null) ? null : new YamlConfig(GameCore.ConfigSharing.Paths[4] + "shared_groups.txt");
            ServerStatic.SharedGroupsMembersConfig = (GameCore.ConfigSharing.Paths[5] == null) ? null : new YamlConfig(GameCore.ConfigSharing.Paths[5] + "shared_groups_members.txt");
            ServerStatic.PermissionsHandler = new PermissionsHandler(ref ServerStatic.RolesConfig, ref ServerStatic.SharedGroupsConfig, ref ServerStatic.SharedGroupsMembersConfig);
            ServerStatic.GetPermissionsHandler().RefreshPermissions();

            foreach (Player p in Player.List)
            {
                p.ReferenceHub.serverRoles.SetGroup(null, false, false, false);
                p.ReferenceHub.serverRoles.RefreshPermissions();
            }
        }
    }
}
