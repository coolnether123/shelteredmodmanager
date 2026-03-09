using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class RuntimeSourceNavigationService : IRuntimeSourceNavigationService
    {
        private readonly IRuntimeSymbolResolver _symbolResolver;
        private readonly ISourcePathResolver _sourcePathResolver;

        public RuntimeSourceNavigationService(IRuntimeSymbolResolver symbolResolver, ISourcePathResolver sourcePathResolver)
        {
            _symbolResolver = symbolResolver;
            _sourcePathResolver = sourcePathResolver;
        }

        public SourceNavigationTarget Resolve(RuntimeLogEntry entry, int frameIndex, CortexProjectDefinition project, CortexSettings settings)
        {
            if (entry == null || entry.StackFrames == null || entry.StackFrames.Count == 0)
            {
                return Failure("No runtime stack frames are available for this log entry.");
            }

            if (frameIndex < 0 || frameIndex >= entry.StackFrames.Count)
            {
                return Failure("The selected stack frame is out of range.");
            }

            var frame = entry.StackFrames[frameIndex];
            var directPath = _sourcePathResolver != null
                ? _sourcePathResolver.ResolveCandidatePath(project, settings, frame != null ? frame.FilePath : null)
                : string.Empty;
            if (!string.IsNullOrEmpty(directPath))
            {
                var lineNumber = frame != null && frame.LineNumber > 0 ? frame.LineNumber : 1;
                return new SourceNavigationTarget
                {
                    Success = true,
                    FilePath = directPath,
                    LineNumber = lineNumber,
                    ColumnNumber = frame != null ? frame.ColumnNumber : 0,
                    StatusMessage = "Resolved runtime frame to source."
                };
            }

            if (_symbolResolver == null)
            {
                return Failure("No runtime symbol resolver is available.");
            }

            return _symbolResolver.Resolve(frame, project, settings) ?? Failure("Runtime symbol resolution failed.");
        }

        private static SourceNavigationTarget Failure(string message)
        {
            return new SourceNavigationTarget
            {
                Success = false,
                FilePath = string.Empty,
                LineNumber = 0,
                ColumnNumber = 0,
                StatusMessage = message ?? string.Empty
            };
        }
    }
}
