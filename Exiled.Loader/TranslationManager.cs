// -----------------------------------------------------------------------
// <copyright file="TranslationManager.cs" company="Exiled Team">
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
    /// Used to handle plugin translations.
    /// </summary>
    public static class TranslationManager
    {
        /// <summary>
        /// Gets the translation serializer.
        /// </summary>
        public static ISerializer Serializer { get; } = new SerializerBuilder()
            .WithTypeInspector(inner => new CommentGatheringTypeInspector(inner))
            .WithEmissionPhaseObjectGraphVisitor(args => new CommentsObjectGraphVisitor(args.InnerVisitor))
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreFields()
            .Build();

        /// <summary>
        /// Gets the translation deserializer.
        /// </summary>
        public static IDeserializer Deserializer { get; } = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithNodeDeserializer(inner => new ValidatingNodeDeserializer(inner), deserializer => deserializer.InsteadOf<ObjectNodeDeserializer>())
            .IgnoreFields()
            .IgnoreUnmatchedProperties()
            .Build();

        /// <summary>
        /// Loads all plugin translations.
        /// </summary>
        /// <param name="rawTranslations">The raw translations to be loaded.</param>
        /// <returns>Returns a dictionary of loaded translations.</returns>
        public static SortedDictionary<string, ITranslations> Load(string rawTranslations)
        {
            try
            {
                Log.Info("Loading plugin translations...");

                Dictionary<string, object> rawDeserializedTranslations = Deserializer.Deserialize<Dictionary<string, object>>(rawTranslations) ?? new Dictionary<string, object>();
                SortedDictionary<string, ITranslations> deserializedTranslations = new SortedDictionary<string, ITranslations>(StringComparer.Ordinal);

                foreach (IPlugin<IConfig> plugin in Loader.Plugins)
                {
                    if (plugin.Translations == null)
                        continue;

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
        /// Reads, Loads and Saves plugin translations.
        /// </summary>
        /// <returns>Returns a value indicating if the reloading process has been completed successfully or not.</returns>
        public static bool Reload() => Save(Load(Read()));

        /// <summary>
        /// Saves plugin translations.
        /// </summary>
        /// <param name="translations">The translations to be saved, already serialized in yaml format.</param>
        /// <returns>Returns a value indicating whether the translations have been saved successfully or not.</returns>
        public static bool Save(string translations)
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

        /// <summary>
        /// Saves plugin translations.
        /// </summary>
        /// <param name="translations">The translations to be saved.</param>
        /// <returns>Returns a value indicating whether the translations have been saved successfully or not.</returns>
        public static bool Save(SortedDictionary<string, ITranslations> translations)
        {
            try
            {
                if (translations == null || translations.Count == 0)
                    return false;

                return Save(Serializer.Serialize(translations));
            }
            catch (YamlException yamlException)
            {
                Log.Error($"An error has occurred while serializing translations: {yamlException}");

                return false;
            }
        }

        /// <summary>
        /// Read all plugin translations.
        /// </summary>
        /// <returns>Returns the read translations.</returns>
        public static string Read()
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
    }
}