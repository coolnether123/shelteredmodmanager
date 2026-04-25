using System.Globalization;
using System.Xml;

namespace ModAPI.Scenarios
{
    internal static class SpritePatchSerializer
    {
        public static SpritePatchDefinition ReadPatch(XmlElement element, System.Func<XmlElement, string, string, string> attributeOrChild, System.Func<XmlElement, string, XmlElement> child, System.Func<XmlElement, string, int, int> readIntAttribute)
        {
            if (element == null)
                return null;

            SpritePatchDefinition patch = new SpritePatchDefinition();
            patch.Id = attributeOrChild(element, "id", "Id");
            patch.DisplayName = attributeOrChild(element, "displayName", "DisplayName");
            patch.BaseSpriteId = attributeOrChild(element, "baseSpriteId", "BaseSpriteId");
            patch.BaseRelativePath = attributeOrChild(element, "basePath", "BasePath");
            patch.BaseRuntimeSpriteKey = attributeOrChild(element, "baseRuntimeSpriteKey", "BaseRuntimeSpriteKey");
            patch.Width = readIntAttribute(element, "width", 0);
            patch.Height = readIntAttribute(element, "height", 0);

            XmlElement operations = child(element, "Operations");
            if (operations == null)
                return patch;

            XmlNodeList operationNodes = operations.GetElementsByTagName("Operation");
            for (int i = 0; i < operationNodes.Count; i++)
            {
                XmlElement operationElement = operationNodes[i] as XmlElement;
                if (operationElement == null)
                    continue;

                SpritePatchOperation operation = new SpritePatchOperation();
                operation.Id = attributeOrChild(operationElement, "id", "Id");
                operation.Order = readIntAttribute(operationElement, "order", i);
                operation.Kind = ReadOperationKind(operationElement, attributeOrChild);

                XmlElement runs = child(operationElement, "Runs");
                if (runs != null)
                {
                    XmlNodeList runNodes = runs.GetElementsByTagName("Run");
                    for (int runIndex = 0; runIndex < runNodes.Count; runIndex++)
                    {
                        XmlElement runElement = runNodes[runIndex] as XmlElement;
                        if (runElement == null)
                            continue;

                        operation.Runs.Add(new SpritePatchDeltaRun
                        {
                            X = readIntAttribute(runElement, "x", 0),
                            Y = readIntAttribute(runElement, "y", 0),
                            Length = readIntAttribute(runElement, "length", 1),
                            ColorHex = attributeOrChild(runElement, "color", "ColorHex")
                        });
                    }
                }

                patch.Operations.Add(operation);
            }

            return patch;
        }

        public static void WritePatch(XmlWriter writer, SpritePatchDefinition patch)
        {
            if (writer == null || patch == null)
                return;

            writer.WriteStartElement("Patch");
            WriteAttribute(writer, "id", patch.Id);
            WriteAttribute(writer, "displayName", patch.DisplayName);
            WriteAttribute(writer, "baseSpriteId", patch.BaseSpriteId);
            WriteAttribute(writer, "basePath", patch.BaseRelativePath);
            WriteAttribute(writer, "baseRuntimeSpriteKey", patch.BaseRuntimeSpriteKey);
            if (patch.Width > 0)
                writer.WriteAttributeString("width", patch.Width.ToString(CultureInfo.InvariantCulture));
            if (patch.Height > 0)
                writer.WriteAttributeString("height", patch.Height.ToString(CultureInfo.InvariantCulture));

            writer.WriteStartElement("Operations");
            for (int i = 0; i < patch.Operations.Count; i++)
            {
                SpritePatchOperation operation = patch.Operations[i];
                if (operation == null)
                    continue;

                writer.WriteStartElement("Operation");
                WriteAttribute(writer, "id", operation.Id);
                writer.WriteAttributeString("order", operation.Order.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("kind", operation.Kind.ToString());
                writer.WriteStartElement("Runs");
                for (int runIndex = 0; runIndex < operation.Runs.Count; runIndex++)
                {
                    SpritePatchDeltaRun run = operation.Runs[runIndex];
                    if (run == null)
                        continue;

                    writer.WriteStartElement("Run");
                    writer.WriteAttributeString("x", run.X.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("y", run.Y.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("length", run.Length.ToString(CultureInfo.InvariantCulture));
                    WriteAttribute(writer, "color", run.ColorHex);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static SpritePatchOperationKind ReadOperationKind(XmlElement element, System.Func<XmlElement, string, string, string> attributeOrChild)
        {
            string raw = attributeOrChild(element, "kind", "Kind");
            if (string.IsNullOrEmpty(raw))
                return SpritePatchOperationKind.Pixels;

            try
            {
                return (SpritePatchOperationKind)System.Enum.Parse(typeof(SpritePatchOperationKind), raw, true);
            }
            catch
            {
                return SpritePatchOperationKind.Pixels;
            }
        }

        private static void WriteAttribute(XmlWriter writer, string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
                writer.WriteAttributeString(name, value);
        }
    }
}
