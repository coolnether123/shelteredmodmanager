using System.Collections.ObjectModel;
using Cortex.Shell.Shared.Models;

namespace Cortex.Host.Avalonia.Models
{
    public sealed class WorkspaceFileNodeViewModel
    {
        public WorkspaceFileNodeViewModel(WorkspaceFileNode node)
        {
            Name = node != null ? node.Name : string.Empty;
            FullPath = node != null ? node.FullPath : string.Empty;
            IsDirectory = node != null && node.IsDirectory;
            Children = new ObservableCollection<WorkspaceFileNodeViewModel>();
            if (node != null)
            {
                for (var i = 0; i < node.Children.Count; i++)
                {
                    Children.Add(new WorkspaceFileNodeViewModel(node.Children[i]));
                }
            }
        }

        public string Name { get; private set; }
        public string FullPath { get; private set; }
        public bool IsDirectory { get; private set; }
        public ObservableCollection<WorkspaceFileNodeViewModel> Children { get; private set; }
    }
}
