using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Bunker;

namespace ShelteredAPI.Scenarios.Infrastructure.Serialization
{
    internal static class ScenarioBackendWorldMaterializer
    {
        public static void MigrateLegacyCurrentWorld(ScenarioDefinition definition)
        {
            if (definition == null)
                return;

            EnsureBackendWorlds(definition);
            if (definition.BackendWorlds.Worlds.Count == 0)
                StoreCurrentWorld(definition);
        }

        public static void StoreCurrentWorld(ScenarioDefinition definition)
        {
            if (definition == null)
                return;

            EnsureBackendWorlds(definition);
            ScenarioBackendWorldDefinition world = definition.BackendWorlds.GetOrCreate(definition.BaseGameMode);
            world.BunkerEdits = CloneBunkerEdits(definition.BunkerEdits);
            world.BunkerGrid = CloneBunkerGrid(definition.BunkerGrid);
            world.SceneSpritePlacements.Clear();

            List<SceneSpritePlacement> placements = definition.AssetReferences != null
                ? definition.AssetReferences.SceneSpritePlacements
                : null;
            List<SceneSpritePlacement> clonedPlacements = CloneSceneSpritePlacements(placements);
            for (int i = 0; i < clonedPlacements.Count; i++)
                world.SceneSpritePlacements.Add(clonedPlacements[i]);
        }

        public static void MaterializeCurrentWorld(ScenarioDefinition definition, ScenarioBaseGameMode baseMode)
        {
            if (definition == null)
                return;

            EnsureBackendWorlds(definition);
            ScenarioBackendWorldDefinition world = definition.BackendWorlds.Find(baseMode);
            definition.BunkerEdits = CloneBunkerEdits(world != null ? world.BunkerEdits : null);
            definition.BunkerGrid = CloneBunkerGrid(world != null ? world.BunkerGrid : null);
            EnsureAssetReferences(definition);
            definition.AssetReferences.SceneSpritePlacements.Clear();

            List<SceneSpritePlacement> placements = world != null ? world.SceneSpritePlacements : null;
            List<SceneSpritePlacement> clonedPlacements = CloneSceneSpritePlacements(placements);
            for (int i = 0; i < clonedPlacements.Count; i++)
                definition.AssetReferences.SceneSpritePlacements.Add(clonedPlacements[i]);
        }

        private static void EnsureBackendWorlds(ScenarioDefinition definition)
        {
            if (definition.BackendWorlds == null)
                definition.BackendWorlds = new ScenarioBackendWorldsDefinition();
        }

        private static void EnsureAssetReferences(ScenarioDefinition definition)
        {
            if (definition.AssetReferences == null)
                definition.AssetReferences = new AssetReferencesDefinition();
        }

        private static BunkerEditsDefinition CloneBunkerEdits(BunkerEditsDefinition source)
        {
            XmlDocument document = WriteSection(delegate(XmlWriter writer)
            {
                ScenarioDefinitionSerializer.WriteBunkerEdits(writer, source);
            });
            return ScenarioDefinitionSerializer.ReadBunkerEdits(document.DocumentElement);
        }

        private static ScenarioBunkerGridDefinition CloneBunkerGrid(ScenarioBunkerGridDefinition source)
        {
            XmlDocument document = WriteSection(delegate(XmlWriter writer)
            {
                ScenarioDefinitionSerializer.WriteBunkerGrid(writer, source);
            });
            return ScenarioDefinitionSerializer.ReadBunkerGrid(document.DocumentElement);
        }

        private static List<SceneSpritePlacement> CloneSceneSpritePlacements(List<SceneSpritePlacement> source)
        {
            AssetReferencesDefinition assets = new AssetReferencesDefinition();
            for (int i = 0; source != null && i < source.Count; i++)
            {
                if (source[i] != null)
                    assets.SceneSpritePlacements.Add(source[i]);
            }

            XmlDocument document = WriteSection(delegate(XmlWriter writer)
            {
                ScenarioDefinitionSerializer.WriteAssetReferences(writer, assets);
            });
            AssetReferencesDefinition cloned = ScenarioDefinitionSerializer.ReadAssetReferences(document.DocumentElement);
            return cloned.SceneSpritePlacements;
        }

        private static XmlDocument WriteSection(Action<XmlWriter> write)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = false;
            settings.OmitXmlDeclaration = true;

            string xml;
            using (StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture))
            {
                using (XmlWriter writer = XmlWriter.Create(stringWriter, settings))
                {
                    write(writer);
                }
                xml = stringWriter.ToString();
            }

            XmlDocument document = new XmlDocument();
            document.XmlResolver = null;
            using (StringReader stringReader = new StringReader(xml))
            {
                using (XmlReader reader = XmlReader.Create(stringReader))
                {
                    document.Load(reader);
                }
            }

            return document;
        }
    }
}
