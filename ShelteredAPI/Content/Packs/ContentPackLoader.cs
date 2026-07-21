using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;
using ShelteredModManager.ContentPacks;

namespace ShelteredAPI.Content.Packs
{
    internal sealed class ContentPackLoader
    {
        public const string RelativePackPath = "Content/content-pack.json";
        private const long MaximumPackBytes = 4L * 1024L * 1024L;
        private readonly ContentPackMapper _mapper;

        public ContentPackLoader()
            : this(new ContentPackMapper())
        {
        }

        internal ContentPackLoader(ContentPackMapper mapper)
        {
            _mapper = mapper ?? throw new ArgumentNullException("mapper");
        }

        public ContentPackLoadResult Load(ModEntry entry)
        {
            ContentPackLoadResult result = new ContentPackLoadResult
            {
                ModId = entry != null ? entry.Id : null
            };
            if (entry == null || string.IsNullOrEmpty(entry.RootPath))
            {
                result.ErrorMessage = "Activating mod has no root path.";
                return result;
            }

            string path = Path.Combine(
                entry.RootPath,
                RelativePackPath.Replace('/', Path.DirectorySeparatorChar));
            result.PackPath = path;
            if (!File.Exists(path))
                return result;

            result.Found = true;
            try
            {
                FileInfo info = new FileInfo(path);
                if (info.Length > MaximumPackBytes)
                    return Failed(result, "Content pack exceeds the 4 MiB file-size limit.");

                ContentPackSerializationResult parsed =
                    ContentPackJsonSerializer.Deserialize(File.ReadAllText(path));
                if (!parsed.Success)
                    return Failed(result, parsed.ErrorMessage);

                ContentPackValidationResult validation = ContentPackValidator.Validate(
                    parsed.Document,
                    new ContentPackValidationContext
                    {
                        ExpectedModId = entry.Id,
                        ModRootPath = entry.RootPath,
                        ValidateAssetFiles = true
                    });
                CopyIssues(validation, result.Issues);
                if (!validation.IsValid)
                    return Failed(result, "Content pack failed validation with " + validation.ErrorCount + " error(s).");

                ContentPackMappedBatch batch = _mapper.Map(entry, parsed.Document);
                ContentOperationResult registration = ContentRegistry.RegisterBatch(
                    entry.Id,
                    batch.Items,
                    batch.Recipes);
                if (!registration.Success)
                    return Failed(result, "Content pack registration failed: " + registration.ErrorMessage);

                result.Success = true;
                result.ItemCount = batch.Items.Count;
                result.RecipeCount = batch.Recipes.Count;
                return result;
            }
            catch (Exception ex)
            {
                return Failed(result, "Content pack load failed: " + ex.Message);
            }
        }

        private static void CopyIssues(
            ContentPackValidationResult validation,
            List<ContentPackValidationIssue> destination)
        {
            if (validation == null || destination == null)
                return;

            for (int i = 0; i < validation.Issues.Count; i++)
                destination.Add(validation.Issues[i]);
        }

        private static ContentPackLoadResult Failed(
            ContentPackLoadResult result,
            string error)
        {
            result.Success = false;
            result.ErrorMessage = error;
            return result;
        }
    }
}
