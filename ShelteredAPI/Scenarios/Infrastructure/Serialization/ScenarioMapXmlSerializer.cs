using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;
namespace ShelteredAPI.Scenarios.Infrastructure.Serialization{
    internal sealed class ScenarioMapXmlSerializer
    {
        public MapAuthoringDefinition Read(XmlElement element)
        {
            MapAuthoringDefinition result = new MapAuthoringDefinition();
            if (element == null)
                return result;

            result.StartLocationId = AttributeOrChild(element, "startLocationId", "StartLocationId");
            result.Width = ReadFloatAttribute(element, "width", 0f);
            result.Height = ReadFloatAttribute(element, "height", 0f);
            result.DefaultTerrainId = AttributeOrChild(element, "defaultTerrainId", "DefaultTerrainId");

            ReadLocations(element, result.Locations);
            ReadMarkers(Child(element, "Markers"), result.Markers);
            ReadBoundaries(Child(element, "Boundaries"), result.Boundaries);
            ReadTerrainPatches(Child(element, "TerrainPatches"), result.TerrainPatches);
            ReadLootTables(Child(element, "LootTables"), result.LootTables);
            ReadEncounterTables(Child(element, "EncounterTables"), result.EncounterTables);
            ReadRoutes(Child(element, "Routes"), result.Routes);
            return result;
        }

        public void Write(XmlWriter writer, MapAuthoringDefinition value)
        {
            if (value == null)
                value = new MapAuthoringDefinition();

            writer.WriteStartElement("Map");
            WriteAttribute(writer, "startLocationId", value.StartLocationId);
            WritePositiveFloatAttribute(writer, "width", value.Width);
            WritePositiveFloatAttribute(writer, "height", value.Height);
            WriteAttribute(writer, "defaultTerrainId", value.DefaultTerrainId);

            WriteLocations(writer, value.Locations);
            WriteMarkers(writer, value.Markers);
            WriteBoundaries(writer, value.Boundaries);
            WriteTerrainPatches(writer, value.TerrainPatches);
            WriteLootTables(writer, value.LootTables);
            WriteEncounterTables(writer, value.EncounterTables);
            WriteRoutes(writer, value.Routes);
            writer.WriteEndElement();
        }

        private static void ReadLocations(XmlElement mapElement, List<MapLocationDefinition> target)
        {
            XmlElement locationsElement = Child(mapElement, "Locations") ?? mapElement;
            XmlNodeList nodes = locationsElement.GetElementsByTagName("Location");
            for (int i = 0; nodes != null && i < nodes.Count; i++)
            {
                XmlElement node = nodes[i] as XmlElement;
                if (node == null || node.ParentNode != locationsElement)
                    continue;

                MapLocationDefinition location = new MapLocationDefinition();
                location.Id = AttributeOrChild(node, "id", "Id");
                location.DisplayName = AttributeOrChild(node, "displayName", "DisplayName");
                location.Kind = AttributeOrChild(node, "kind", "Kind");
                location.X = ReadFloatAttribute(node, "x", 0f);
                location.Y = ReadFloatAttribute(node, "y", 0f);
                location.GridX = ReadIntAttribute(node, "gridX", 0);
                location.GridY = ReadIntAttribute(node, "gridY", 0);
                location.Radius = ReadFloatAttribute(node, "radius", 0f);
                location.Searchable = ReadBoolAttribute(node, "searchable", true);
                location.DiscoveredAtStart = ReadBoolAttribute(node, "discoveredAtStart", true);
                location.VisibleAtStart = ReadBoolAttribute(node, "visibleAtStart", true);
                location.HiddenUntilDiscovered = ReadBoolAttribute(node, "hiddenUntilDiscovered", false);
                location.MarkerId = AttributeOrChild(node, "markerId", "MarkerId");
                location.BoundaryId = AttributeOrChild(node, "boundaryId", "BoundaryId");
                location.TerrainId = AttributeOrChild(node, "terrainId", "TerrainId");
                location.LootTableId = AttributeOrChild(node, "lootTableId", "LootTableId");
                location.EncounterTableId = AttributeOrChild(node, "encounterTableId", "EncounterTableId");
                location.RequiredGateId = AttributeOrChild(node, "requiredGateId", "RequiredGateId");
                location.Danger = ReadIntAttribute(node, "danger", 0);
                ReadProperties(Child(node, "Properties"), location.Properties);
                target.Add(location);
            }
        }

        private static void WriteLocations(XmlWriter writer, List<MapLocationDefinition> locations)
        {
            writer.WriteStartElement("Locations");
            for (int i = 0; locations != null && i < locations.Count; i++)
            {
                MapLocationDefinition location = locations[i];
                if (location == null)
                    continue;

                writer.WriteStartElement("Location");
                WriteAttribute(writer, "id", location.Id);
                WriteAttribute(writer, "displayName", location.DisplayName);
                WriteAttribute(writer, "kind", location.Kind);
                writer.WriteAttributeString("x", location.X.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("y", location.Y.ToString(CultureInfo.InvariantCulture));
                WriteNonZeroIntAttribute(writer, "gridX", location.GridX);
                WriteNonZeroIntAttribute(writer, "gridY", location.GridY);
                WritePositiveFloatAttribute(writer, "radius", location.Radius);
                writer.WriteAttributeString("searchable", location.Searchable ? "true" : "false");
                writer.WriteAttributeString("discoveredAtStart", location.DiscoveredAtStart ? "true" : "false");
                writer.WriteAttributeString("visibleAtStart", location.VisibleAtStart ? "true" : "false");
                writer.WriteAttributeString("hiddenUntilDiscovered", location.HiddenUntilDiscovered ? "true" : "false");
                WriteAttribute(writer, "markerId", location.MarkerId);
                WriteAttribute(writer, "boundaryId", location.BoundaryId);
                WriteAttribute(writer, "terrainId", location.TerrainId);
                WriteAttribute(writer, "lootTableId", location.LootTableId);
                WriteAttribute(writer, "encounterTableId", location.EncounterTableId);
                WriteAttribute(writer, "requiredGateId", location.RequiredGateId);
                WriteNonZeroIntAttribute(writer, "danger", location.Danger);
                WriteProperties(writer, "Properties", location.Properties);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void ReadMarkers(XmlElement element, List<MapMarkerDefinition> target)
        {
            foreach (XmlElement node in DirectChildren(element, "Marker"))
            {
                MapMarkerDefinition marker = new MapMarkerDefinition();
                marker.Id = AttributeOrChild(node, "id", "Id");
                marker.DisplayName = AttributeOrChild(node, "displayName", "DisplayName");
                marker.Kind = ReadEnumAttribute(node, "kind", MapMarkerKind.PointOfInterest);
                marker.X = ReadFloatAttribute(node, "x", 0f);
                marker.Y = ReadFloatAttribute(node, "y", 0f);
                marker.IconId = AttributeOrChild(node, "iconId", "IconId");
                marker.LocationId = AttributeOrChild(node, "locationId", "LocationId");
                marker.BoundaryId = AttributeOrChild(node, "boundaryId", "BoundaryId");
                marker.VisibleAtStart = ReadBoolAttribute(node, "visibleAtStart", true);
                ReadStringList(Child(node, "Tags"), "Tag", marker.Tags);
                ReadProperties(Child(node, "Properties"), marker.Properties);
                target.Add(marker);
            }
        }

        private static void WriteMarkers(XmlWriter writer, List<MapMarkerDefinition> markers)
        {
            writer.WriteStartElement("Markers");
            for (int i = 0; markers != null && i < markers.Count; i++)
            {
                MapMarkerDefinition marker = markers[i];
                if (marker == null)
                    continue;

                writer.WriteStartElement("Marker");
                WriteAttribute(writer, "id", marker.Id);
                WriteAttribute(writer, "displayName", marker.DisplayName);
                writer.WriteAttributeString("kind", marker.Kind.ToString());
                writer.WriteAttributeString("x", marker.X.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("y", marker.Y.ToString(CultureInfo.InvariantCulture));
                WriteAttribute(writer, "iconId", marker.IconId);
                WriteAttribute(writer, "locationId", marker.LocationId);
                WriteAttribute(writer, "boundaryId", marker.BoundaryId);
                writer.WriteAttributeString("visibleAtStart", marker.VisibleAtStart ? "true" : "false");
                WriteStringList(writer, "Tags", "Tag", marker.Tags);
                WriteProperties(writer, "Properties", marker.Properties);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void ReadBoundaries(XmlElement element, List<MapBoundaryDefinition> target)
        {
            foreach (XmlElement node in DirectChildren(element, "Boundary"))
            {
                MapBoundaryDefinition boundary = new MapBoundaryDefinition();
                boundary.Id = AttributeOrChild(node, "id", "Id");
                boundary.DisplayName = AttributeOrChild(node, "displayName", "DisplayName");
                boundary.Kind = ReadEnumAttribute(node, "kind", MapBoundaryKind.Region);
                boundary.MinX = ReadNullableFloatAttribute(node, "minX");
                boundary.MinY = ReadNullableFloatAttribute(node, "minY");
                boundary.MaxX = ReadNullableFloatAttribute(node, "maxX");
                boundary.MaxY = ReadNullableFloatAttribute(node, "maxY");
                boundary.TerrainId = AttributeOrChild(node, "terrainId", "TerrainId");
                boundary.LootTableId = AttributeOrChild(node, "lootTableId", "LootTableId");
                boundary.EncounterTableId = AttributeOrChild(node, "encounterTableId", "EncounterTableId");
                boundary.RequiredGateId = AttributeOrChild(node, "requiredGateId", "RequiredGateId");
                ReadMapPoints(Child(node, "Points"), "Point", boundary.Points);
                ReadProperties(Child(node, "Properties"), boundary.Properties);
                target.Add(boundary);
            }
        }

        private static void WriteBoundaries(XmlWriter writer, List<MapBoundaryDefinition> boundaries)
        {
            writer.WriteStartElement("Boundaries");
            for (int i = 0; boundaries != null && i < boundaries.Count; i++)
            {
                MapBoundaryDefinition boundary = boundaries[i];
                if (boundary == null)
                    continue;

                writer.WriteStartElement("Boundary");
                WriteAttribute(writer, "id", boundary.Id);
                WriteAttribute(writer, "displayName", boundary.DisplayName);
                writer.WriteAttributeString("kind", boundary.Kind.ToString());
                WriteNullableFloatAttribute(writer, "minX", boundary.MinX);
                WriteNullableFloatAttribute(writer, "minY", boundary.MinY);
                WriteNullableFloatAttribute(writer, "maxX", boundary.MaxX);
                WriteNullableFloatAttribute(writer, "maxY", boundary.MaxY);
                WriteAttribute(writer, "terrainId", boundary.TerrainId);
                WriteAttribute(writer, "lootTableId", boundary.LootTableId);
                WriteAttribute(writer, "encounterTableId", boundary.EncounterTableId);
                WriteAttribute(writer, "requiredGateId", boundary.RequiredGateId);
                WriteMapPoints(writer, "Points", "Point", boundary.Points);
                WriteProperties(writer, "Properties", boundary.Properties);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void ReadTerrainPatches(XmlElement element, List<MapTerrainPatchDefinition> target)
        {
            foreach (XmlElement node in DirectChildren(element, "Patch"))
            {
                MapTerrainPatchDefinition patch = new MapTerrainPatchDefinition();
                patch.Id = AttributeOrChild(node, "id", "Id");
                patch.TerrainId = AttributeOrChild(node, "terrainId", "TerrainId");
                patch.Shape = ReadEnumAttribute(node, "shape", MapTerrainBrushShape.Rectangle);
                patch.X = ReadFloatAttribute(node, "x", 0f);
                patch.Y = ReadFloatAttribute(node, "y", 0f);
                patch.Width = ReadFloatAttribute(node, "width", 0f);
                patch.Height = ReadFloatAttribute(node, "height", 0f);
                patch.Radius = ReadFloatAttribute(node, "radius", 0f);
                patch.Priority = ReadIntAttribute(node, "priority", 0);
                patch.BoundaryId = AttributeOrChild(node, "boundaryId", "BoundaryId");
                ReadMapPoints(Child(node, "Points"), "Point", patch.Points);
                ReadProperties(Child(node, "Properties"), patch.Properties);
                target.Add(patch);
            }
        }

        private static void WriteTerrainPatches(XmlWriter writer, List<MapTerrainPatchDefinition> patches)
        {
            writer.WriteStartElement("TerrainPatches");
            for (int i = 0; patches != null && i < patches.Count; i++)
            {
                MapTerrainPatchDefinition patch = patches[i];
                if (patch == null)
                    continue;

                writer.WriteStartElement("Patch");
                WriteAttribute(writer, "id", patch.Id);
                WriteAttribute(writer, "terrainId", patch.TerrainId);
                writer.WriteAttributeString("shape", patch.Shape.ToString());
                writer.WriteAttributeString("x", patch.X.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("y", patch.Y.ToString(CultureInfo.InvariantCulture));
                WritePositiveFloatAttribute(writer, "width", patch.Width);
                WritePositiveFloatAttribute(writer, "height", patch.Height);
                WritePositiveFloatAttribute(writer, "radius", patch.Radius);
                WriteNonZeroIntAttribute(writer, "priority", patch.Priority);
                WriteAttribute(writer, "boundaryId", patch.BoundaryId);
                WriteMapPoints(writer, "Points", "Point", patch.Points);
                WriteProperties(writer, "Properties", patch.Properties);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void ReadLootTables(XmlElement element, List<MapLootTableDefinition> target)
        {
            foreach (XmlElement node in DirectChildren(element, "LootTable"))
            {
                MapLootTableDefinition table = new MapLootTableDefinition();
                table.Id = AttributeOrChild(node, "id", "Id");
                table.DisplayName = AttributeOrChild(node, "displayName", "DisplayName");
                foreach (XmlElement entryNode in DirectChildren(Child(node, "Entries") ?? node, "Entry"))
                {
                    table.Entries.Add(new MapLootEntryDefinition
                    {
                        ItemId = AttributeOrChild(entryNode, "itemId", "ItemId"),
                        MinQuantity = ReadIntAttribute(entryNode, "min", 1),
                        MaxQuantity = ReadIntAttribute(entryNode, "max", 1),
                        Weight = ReadIntAttribute(entryNode, "weight", 1),
                        Chance = ReadFloatAttribute(entryNode, "chance", 1f)
                    });
                }
                ReadProperties(Child(node, "Properties"), table.Properties);
                target.Add(table);
            }
        }

        private static void WriteLootTables(XmlWriter writer, List<MapLootTableDefinition> tables)
        {
            writer.WriteStartElement("LootTables");
            for (int i = 0; tables != null && i < tables.Count; i++)
            {
                MapLootTableDefinition table = tables[i];
                if (table == null)
                    continue;

                writer.WriteStartElement("LootTable");
                WriteAttribute(writer, "id", table.Id);
                WriteAttribute(writer, "displayName", table.DisplayName);
                writer.WriteStartElement("Entries");
                for (int e = 0; table.Entries != null && e < table.Entries.Count; e++)
                {
                    MapLootEntryDefinition entry = table.Entries[e];
                    if (entry == null)
                        continue;

                    writer.WriteStartElement("Entry");
                    WriteAttribute(writer, "itemId", entry.ItemId);
                    writer.WriteAttributeString("min", entry.MinQuantity.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("max", entry.MaxQuantity.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("weight", entry.Weight.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("chance", entry.Chance.ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                WriteProperties(writer, "Properties", table.Properties);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void ReadEncounterTables(XmlElement element, List<MapEncounterTableDefinition> target)
        {
            foreach (XmlElement node in DirectChildren(element, "EncounterTable"))
            {
                MapEncounterTableDefinition table = new MapEncounterTableDefinition();
                table.Id = AttributeOrChild(node, "id", "Id");
                table.DisplayName = AttributeOrChild(node, "displayName", "DisplayName");
                foreach (XmlElement entryNode in DirectChildren(Child(node, "Entries") ?? node, "Entry"))
                {
                    MapEncounterEntryDefinition entry = new MapEncounterEntryDefinition();
                    entry.Id = AttributeOrChild(entryNode, "id", "Id");
                    entry.EncounterType = AttributeOrChild(entryNode, "type", "EncounterType");
                    entry.FactionId = AttributeOrChild(entryNode, "factionId", "FactionId");
                    entry.PersonalityId = AttributeOrChild(entryNode, "personalityId", "PersonalityId");
                    entry.MinCount = ReadIntAttribute(entryNode, "min", 1);
                    entry.MaxCount = ReadIntAttribute(entryNode, "max", 1);
                    entry.Weight = ReadIntAttribute(entryNode, "weight", 1);
                    entry.Chance = ReadFloatAttribute(entryNode, "chance", 1f);
                    entry.LootTableId = AttributeOrChild(entryNode, "lootTableId", "LootTableId");
                    entry.QuestId = AttributeOrChild(entryNode, "questId", "QuestId");
                    ReadProperties(Child(entryNode, "Properties"), entry.Properties);
                    table.Entries.Add(entry);
                }
                ReadProperties(Child(node, "Properties"), table.Properties);
                target.Add(table);
            }
        }

        private static void WriteEncounterTables(XmlWriter writer, List<MapEncounterTableDefinition> tables)
        {
            writer.WriteStartElement("EncounterTables");
            for (int i = 0; tables != null && i < tables.Count; i++)
            {
                MapEncounterTableDefinition table = tables[i];
                if (table == null)
                    continue;

                writer.WriteStartElement("EncounterTable");
                WriteAttribute(writer, "id", table.Id);
                WriteAttribute(writer, "displayName", table.DisplayName);
                writer.WriteStartElement("Entries");
                for (int e = 0; table.Entries != null && e < table.Entries.Count; e++)
                {
                    MapEncounterEntryDefinition entry = table.Entries[e];
                    if (entry == null)
                        continue;

                    writer.WriteStartElement("Entry");
                    WriteAttribute(writer, "id", entry.Id);
                    WriteAttribute(writer, "type", entry.EncounterType);
                    WriteAttribute(writer, "factionId", entry.FactionId);
                    WriteAttribute(writer, "personalityId", entry.PersonalityId);
                    writer.WriteAttributeString("min", entry.MinCount.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("max", entry.MaxCount.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("weight", entry.Weight.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("chance", entry.Chance.ToString(CultureInfo.InvariantCulture));
                    WriteAttribute(writer, "lootTableId", entry.LootTableId);
                    WriteAttribute(writer, "questId", entry.QuestId);
                    WriteProperties(writer, "Properties", entry.Properties);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                WriteProperties(writer, "Properties", table.Properties);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void ReadRoutes(XmlElement element, List<ExpeditionRouteDefinition> target)
        {
            foreach (XmlElement node in DirectChildren(element, "Route"))
            {
                ExpeditionRouteDefinition route = new ExpeditionRouteDefinition();
                route.Id = AttributeOrChild(node, "id", "Id");
                route.FromLocationId = AttributeOrChild(node, "from", "FromLocationId");
                route.ToLocationId = AttributeOrChild(node, "to", "ToLocationId");
                route.OneWay = ReadBoolAttribute(node, "oneWay", false);
                route.Distance = ReadFloatAttribute(node, "distance", 0f);
                route.Risk = ReadIntAttribute(node, "risk", 0);
                route.RequiredGateId = AttributeOrChild(node, "requiredGateId", "RequiredGateId");
                ReadMapPoints(Child(node, "Waypoints"), "Point", route.Waypoints);
                ReadProperties(Child(node, "Properties"), route.Properties);
                target.Add(route);
            }
        }

        private static void WriteRoutes(XmlWriter writer, List<ExpeditionRouteDefinition> routes)
        {
            writer.WriteStartElement("Routes");
            for (int i = 0; routes != null && i < routes.Count; i++)
            {
                ExpeditionRouteDefinition route = routes[i];
                if (route == null)
                    continue;

                writer.WriteStartElement("Route");
                WriteAttribute(writer, "id", route.Id);
                WriteAttribute(writer, "from", route.FromLocationId);
                WriteAttribute(writer, "to", route.ToLocationId);
                writer.WriteAttributeString("oneWay", route.OneWay ? "true" : "false");
                WritePositiveFloatAttribute(writer, "distance", route.Distance);
                WriteNonZeroIntAttribute(writer, "risk", route.Risk);
                WriteAttribute(writer, "requiredGateId", route.RequiredGateId);
                WriteMapPoints(writer, "Waypoints", "Point", route.Waypoints);
                WriteProperties(writer, "Properties", route.Properties);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void ReadMapPoints(XmlElement element, string pointName, List<MapPointDefinition> target)
        {
            foreach (XmlElement node in DirectChildren(element, pointName))
            {
                target.Add(new MapPointDefinition
                {
                    X = ReadFloatAttribute(node, "x", 0f),
                    Y = ReadFloatAttribute(node, "y", 0f)
                });
            }
        }

        private static void WriteMapPoints(XmlWriter writer, string parentName, string pointName, List<MapPointDefinition> points)
        {
            writer.WriteStartElement(parentName);
            for (int i = 0; points != null && i < points.Count; i++)
            {
                MapPointDefinition point = points[i];
                if (point == null)
                    continue;

                writer.WriteStartElement(pointName);
                writer.WriteAttributeString("x", point.X.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("y", point.Y.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void ReadProperties(XmlElement parent, List<ScenarioProperty> target)
        {
            foreach (XmlElement node in DirectChildren(parent, "Property"))
            {
                target.Add(new ScenarioProperty
                {
                    Key = AttributeOrChild(node, "key", "Key"),
                    Value = AttributeOrChild(node, "value", "Value")
                });
            }
        }

        private static void WriteProperties(XmlWriter writer, string parentName, List<ScenarioProperty> properties)
        {
            writer.WriteStartElement(parentName);
            for (int i = 0; properties != null && i < properties.Count; i++)
            {
                ScenarioProperty property = properties[i];
                if (property == null)
                    continue;

                writer.WriteStartElement("Property");
                WriteAttribute(writer, "key", property.Key);
                WriteAttribute(writer, "value", property.Value);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void ReadStringList(XmlElement parent, string itemName, List<string> target)
        {
            foreach (XmlElement node in DirectChildren(parent, itemName))
                target.Add(node.InnerText ?? string.Empty);
        }

        private static void WriteStringList(XmlWriter writer, string parentName, string itemName, List<string> values)
        {
            writer.WriteStartElement(parentName);
            for (int i = 0; values != null && i < values.Count; i++)
            {
                writer.WriteStartElement(itemName);
                writer.WriteString(values[i] ?? string.Empty);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static IEnumerable<XmlElement> DirectChildren(XmlElement parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name))
                yield break;

            XmlNodeList nodes = parent.GetElementsByTagName(name);
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement node = nodes[i] as XmlElement;
                if (node != null && node.ParentNode == parent)
                    yield return node;
            }
        }

        private static XmlElement Child(XmlElement parent, string name)
        {
            if (parent == null)
                return null;

            XmlNodeList nodes = parent.GetElementsByTagName(name);
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement element = nodes[i] as XmlElement;
                if (element != null && element.ParentNode == parent)
                    return element;
            }

            return null;
        }

        private static string AttributeOrChild(XmlElement element, string attributeName, string childName)
        {
            if (element == null)
                return null;
            if (!string.IsNullOrEmpty(attributeName) && element.HasAttribute(attributeName))
                return element.GetAttribute(attributeName);

            XmlElement child = Child(element, childName);
            return child != null ? child.InnerText : null;
        }

        private static T ReadEnumAttribute<T>(XmlElement element, string attributeName, T fallback)
        {
            if (element == null || !element.HasAttribute(attributeName))
                return fallback;

            try { return (T)Enum.Parse(typeof(T), element.GetAttribute(attributeName), true); }
            catch { return fallback; }
        }

        private static int ReadIntAttribute(XmlElement element, string attributeName, int fallback)
        {
            if (element == null || !element.HasAttribute(attributeName))
                return fallback;

            int parsed;
            return int.TryParse(element.GetAttribute(attributeName), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static bool ReadBoolAttribute(XmlElement element, string attributeName, bool fallback)
        {
            if (element == null || !element.HasAttribute(attributeName))
                return fallback;

            bool parsed;
            return bool.TryParse(element.GetAttribute(attributeName), out parsed) ? parsed : fallback;
        }

        private static float ReadFloatAttribute(XmlElement element, string attributeName, float fallback)
        {
            float? parsed = ReadNullableFloatAttribute(element, attributeName);
            return parsed.HasValue ? parsed.Value : fallback;
        }

        private static float? ReadNullableFloatAttribute(XmlElement element, string attributeName)
        {
            if (element == null || !element.HasAttribute(attributeName))
                return null;

            float parsed;
            return float.TryParse(element.GetAttribute(attributeName), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                ? (float?)parsed
                : null;
        }

        private static void WriteAttribute(XmlWriter writer, string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
                writer.WriteAttributeString(name, value);
        }

        private static void WritePositiveFloatAttribute(XmlWriter writer, string name, float value)
        {
            if (value > 0f)
                writer.WriteAttributeString(name, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteNullableFloatAttribute(XmlWriter writer, string name, float? value)
        {
            if (value.HasValue)
                writer.WriteAttributeString(name, value.Value.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteNonZeroIntAttribute(XmlWriter writer, string name, int value)
        {
            if (value != 0)
                writer.WriteAttributeString(name, value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
