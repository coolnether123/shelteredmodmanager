using System;
using System.Web.Script.Serialization;

namespace ShelteredModManager.ContentPacks
{
    public sealed class ContentPackSerializationResult
    {
        public bool Success;
        public ContentPackDocument Document;
        public string Json;
        public string ErrorMessage;
    }

    /// <summary>Bounded JSON serialization for untrusted downloaded content packs.</summary>
    public static class ContentPackJsonSerializer
    {
        public const int MaximumJsonLength = 1024 * 1024;
        public const int MaximumRecursionLimit = 32;

        public static ContentPackSerializationResult Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
                return Failed("Content-pack JSON is empty.");
            if (json.Length > MaximumJsonLength)
                return Failed("Content-pack JSON exceeds the 1 MiB limit.");

            try
            {
                JavaScriptSerializer serializer = CreateSerializer();
                ContentPackDocument document = serializer.Deserialize<ContentPackDocument>(json);
                if (document == null)
                    return Failed("Content-pack JSON did not contain a document.");

                return new ContentPackSerializationResult
                {
                    Success = true,
                    Document = document
                };
            }
            catch (Exception ex)
            {
                return Failed("Content-pack JSON is invalid: " + ex.Message);
            }
        }

        public static ContentPackSerializationResult Serialize(ContentPackDocument document)
        {
            if (document == null)
                return Failed("Content-pack document is required.");

            try
            {
                JavaScriptSerializer serializer = CreateSerializer();
                string json = serializer.Serialize(document);
                if (json.Length > MaximumJsonLength)
                    return Failed("Serialized content-pack JSON exceeds the 1 MiB limit.");

                return new ContentPackSerializationResult
                {
                    Success = true,
                    Document = document,
                    Json = json
                };
            }
            catch (Exception ex)
            {
                return Failed("Content-pack serialization failed: " + ex.Message);
            }
        }

        private static JavaScriptSerializer CreateSerializer()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = MaximumJsonLength;
            serializer.RecursionLimit = MaximumRecursionLimit;
            return serializer;
        }

        private static ContentPackSerializationResult Failed(string error)
        {
            return new ContentPackSerializationResult
            {
                Success = false,
                ErrorMessage = error
            };
        }
    }
}
