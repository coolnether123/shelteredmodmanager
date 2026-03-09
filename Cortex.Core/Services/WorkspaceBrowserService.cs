using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class WorkspaceBrowserService : IWorkspaceBrowserService
    {
        private readonly ISourceLookupIndex _lookupIndex;

        public WorkspaceBrowserService(ISourceLookupIndex lookupIndex)
        {
            _lookupIndex = lookupIndex;
        }

        public WorkspaceTreeNode BuildTree(string rootPath, WorkspaceTreeKind kind)
        {
            return _lookupIndex != null
                ? _lookupIndex.BuildTree(rootPath, kind)
                : null;
        }

        public void Refresh(string rootPath, WorkspaceTreeKind kind)
        {
            if (_lookupIndex == null || string.IsNullOrEmpty(rootPath))
            {
                return;
            }

            _lookupIndex.RefreshRoot(rootPath);
        }
    }
}
