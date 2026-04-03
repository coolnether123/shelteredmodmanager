using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Services.Navigation;

namespace Cortex.Services.Reference
{
    internal sealed class ReferenceBrowserItemPresentation
    {
        public string Title = string.Empty;
        public string Subtitle = string.Empty;
        public string Meta = string.Empty;
        public string Tooltip = string.Empty;
        public bool IsSelected;
        public ReferenceAssemblyDescriptor Assembly;
        public ReferenceTypeDescriptor Type;
        public ReferenceMemberDescriptor Member;
    }

    internal sealed class ReferenceBrowserSelectionPresentation
    {
        public string SelectionPath = string.Empty;
        public string SelectionCaption = string.Empty;
        public string AssemblyLabel = string.Empty;
        public string AssemblyPath = string.Empty;
        public string TypeLabel = string.Empty;
        public string MemberLabel = string.Empty;
    }

    internal sealed class ReferenceBrowserSessionService
    {
        private readonly List<ReferenceAssemblyDescriptor> _assemblies = new List<ReferenceAssemblyDescriptor>();
        private readonly List<ReferenceTypeDescriptor> _types = new List<ReferenceTypeDescriptor>();
        private readonly List<ReferenceMemberDescriptor> _members = new List<ReferenceMemberDescriptor>();

        public string AssemblyFilter = string.Empty;
        public string TypeFilter = string.Empty;
        public string MemberFilter = string.Empty;
        public bool IgnoreCache;

        public ReferenceAssemblyDescriptor SelectedAssembly { get; private set; }
        public ReferenceTypeDescriptor SelectedType { get; private set; }
        public ReferenceMemberDescriptor SelectedMember { get; private set; }

        public void EnsureAssembliesLoaded(IReferenceCatalogService referenceCatalogService, CortexShellState state)
        {
            if (_assemblies.Count == 0)
            {
                ReloadAssemblies(referenceCatalogService, state);
            }
        }

        public void ReloadAssemblies(IReferenceCatalogService referenceCatalogService, CortexShellState state)
        {
            _assemblies.Clear();
            _types.Clear();
            _members.Clear();
            SelectedAssembly = null;
            SelectedType = null;
            SelectedMember = null;

            var assemblies = referenceCatalogService != null
                ? referenceCatalogService.GetAssemblies(state != null && state.Settings != null ? state.Settings.ReferenceAssemblyRootPath : string.Empty)
                : new List<ReferenceAssemblyDescriptor>();
            for (var i = 0; i < assemblies.Count; i++)
            {
                _assemblies.Add(assemblies[i]);
            }
        }

        public IList<ReferenceBrowserItemPresentation> BuildAssemblyItems()
        {
            var items = new List<ReferenceBrowserItemPresentation>();
            for (var i = 0; i < _assemblies.Count; i++)
            {
                var assembly = _assemblies[i];
                if (!MatchesText(assembly.DisplayName + " " + assembly.AssemblyPath, AssemblyFilter))
                {
                    continue;
                }

                var location = Path.GetDirectoryName(assembly.AssemblyPath) ?? string.Empty;
                items.Add(new ReferenceBrowserItemPresentation
                {
                    Title = assembly.DisplayName ?? string.Empty,
                    Subtitle = CompactPath(location, 56),
                    Meta = Path.GetFileName(assembly.AssemblyPath) ?? string.Empty,
                    Tooltip = ComposeTooltip(assembly.DisplayName, assembly.AssemblyPath),
                    IsSelected = IsSelectedAssembly(assembly),
                    Assembly = assembly
                });
            }

            return items;
        }

        public IList<ReferenceBrowserItemPresentation> BuildTypeItems()
        {
            var items = new List<ReferenceBrowserItemPresentation>();
            for (var i = 0; i < _types.Count; i++)
            {
                var type = _types[i];
                if (!MatchesText(type.FullName, TypeFilter))
                {
                    continue;
                }

                string shortName;
                string namespaceName;
                SplitTypeName(type.FullName, out shortName, out namespaceName);
                items.Add(new ReferenceBrowserItemPresentation
                {
                    Title = shortName,
                    Subtitle = namespaceName,
                    Meta = FormatMetadataToken(type.MetadataToken),
                    Tooltip = ComposeTooltip(type.FullName, FormatMetadataToken(type.MetadataToken)),
                    IsSelected = IsSelectedType(type),
                    Type = type
                });
            }

            return items;
        }

        public IList<ReferenceBrowserItemPresentation> BuildMemberItems()
        {
            var items = new List<ReferenceBrowserItemPresentation>();
            for (var i = 0; i < _members.Count; i++)
            {
                var member = _members[i];
                if (!MatchesText(member.DisplayName + " " + member.DeclaringTypeName, MemberFilter))
                {
                    continue;
                }

                string title;
                string signature;
                SplitMemberDisplay(member.DisplayName, out title, out signature);
                var subtitle = ShortTypeName(member.DeclaringTypeName);
                if (!string.IsNullOrEmpty(signature))
                {
                    subtitle = subtitle + " " + signature;
                }

                items.Add(new ReferenceBrowserItemPresentation
                {
                    Title = title,
                    Subtitle = subtitle,
                    Meta = FormatMetadataToken(member.MetadataToken),
                    Tooltip = ComposeTooltip(member.DeclaringTypeName + "." + member.DisplayName, FormatMetadataToken(member.MetadataToken)),
                    IsSelected = IsSelectedMember(member),
                    Member = member
                });
            }

            return items;
        }

        public ReferenceBrowserSelectionPresentation BuildSelectionPresentation()
        {
            return new ReferenceBrowserSelectionPresentation
            {
                SelectionPath = BuildSelectionPath(),
                SelectionCaption = BuildSelectionCaption(),
                AssemblyLabel = SelectedAssembly != null ? SelectedAssembly.DisplayName : "None selected",
                AssemblyPath = SelectedAssembly != null ? SelectedAssembly.AssemblyPath : "Select an assembly to browse its types.",
                TypeLabel = SelectedType != null ? SelectedType.FullName : "No type selected",
                MemberLabel = SelectedMember != null ? SelectedMember.DisplayName : "No member selected"
            };
        }

        public void SelectAssembly(IReferenceCatalogService referenceCatalogService, ReferenceAssemblyDescriptor assembly, CortexShellState state)
        {
            if (assembly == null)
            {
                return;
            }

            SelectedAssembly = assembly;
            SelectedType = null;
            SelectedMember = null;
            LoadTypes(referenceCatalogService, assembly);
            if (state != null)
            {
                state.StatusMessage = "Selected assembly " + assembly.DisplayName;
            }
        }

        public void SelectType(IReferenceCatalogService referenceCatalogService, ReferenceTypeDescriptor type, CortexShellState state)
        {
            if (type == null)
            {
                return;
            }

            SelectedType = type;
            SelectedMember = null;
            LoadMembers(referenceCatalogService, type);
            if (state != null)
            {
                state.StatusMessage = "Selected type " + type.DisplayName;
            }
        }

        public void SelectMember(ReferenceMemberDescriptor member, CortexShellState state)
        {
            if (member == null)
            {
                return;
            }

            SelectedMember = member;
            if (state != null)
            {
                state.StatusMessage = "Selected member " + member.DeclaringTypeName + "." + member.DisplayName + ".";
            }
        }

        public void DecompileSelectedMember(ICortexNavigationService navigationService, CortexShellState state)
        {
            if (SelectedMember == null || navigationService == null)
            {
                return;
            }

            int highlightedLine;
            var response = navigationService.RequestDecompilerMethodView(
                state,
                SelectedMember.AssemblyPath,
                SelectedMember.MetadataToken,
                string.Empty,
                SelectedMember.DeclaringTypeName,
                string.Empty,
                IgnoreCache,
                out highlightedLine);
            if (response == null)
            {
                if (state != null)
                {
                    state.StatusMessage = "Could not decompile " + SelectedMember.DeclaringTypeName + "." + SelectedMember.DisplayName;
                }

                return;
            }

            if (state != null)
            {
                state.StatusMessage = "Decompiled " + SelectedMember.DeclaringTypeName + "." + SelectedMember.DisplayName;
            }
        }

        public void DecompileSelectedType(ICortexNavigationService navigationService, CortexShellState state)
        {
            if (SelectedType == null || navigationService == null)
            {
                return;
            }

            navigationService.RequestDecompilerSource(state, SelectedType.AssemblyPath, SelectedType.MetadataToken, DecompilerEntityKind.Type, IgnoreCache);
            if (state != null)
            {
                state.StatusMessage = "Decompiled type " + SelectedType.DisplayName;
            }
        }

        public void OpenDecompilerResult(ICortexNavigationService navigationService, CortexShellState state)
        {
            if (navigationService == null || state == null)
            {
                return;
            }

            if (SelectedMember != null)
            {
                navigationService.OpenDecompilerMethodTarget(
                    state,
                    SelectedMember.AssemblyPath,
                    SelectedMember.MetadataToken,
                    string.Empty,
                    SelectedMember.DeclaringTypeName,
                    string.Empty,
                    IgnoreCache,
                    "Opened decompiled source.",
                    "Could not open decompiled source.");
                return;
            }

            if (state.LastReferenceResult != null)
            {
                navigationService.OpenDecompilerResult(
                    state,
                    state.LastReferenceResult,
                    "Opened decompiled cache file.",
                    "Could not open decompiled cache file.");
            }
        }

        public void ClearDecompilerResult(CortexShellState state)
        {
            if (state != null)
            {
                state.LastReferenceResult = null;
            }
        }

        private void LoadTypes(IReferenceCatalogService referenceCatalogService, ReferenceAssemblyDescriptor assembly)
        {
            _types.Clear();
            _members.Clear();
            SelectedType = null;
            SelectedMember = null;
            if (assembly == null || referenceCatalogService == null)
            {
                return;
            }

            var types = referenceCatalogService.GetTypes(assembly.AssemblyPath);
            for (var i = 0; i < types.Count; i++)
            {
                _types.Add(types[i]);
            }
        }

        private void LoadMembers(IReferenceCatalogService referenceCatalogService, ReferenceTypeDescriptor type)
        {
            _members.Clear();
            SelectedMember = null;
            if (type == null || referenceCatalogService == null)
            {
                return;
            }

            var members = referenceCatalogService.GetMembers(type.AssemblyPath, type.FullName);
            for (var i = 0; i < members.Count; i++)
            {
                _members.Add(members[i]);
            }
        }

        private bool IsSelectedAssembly(ReferenceAssemblyDescriptor assembly)
        {
            return SelectedAssembly != null &&
                assembly != null &&
                string.Equals(SelectedAssembly.AssemblyPath, assembly.AssemblyPath, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsSelectedType(ReferenceTypeDescriptor type)
        {
            return SelectedType != null &&
                type != null &&
                string.Equals(SelectedType.FullName, type.FullName, StringComparison.Ordinal);
        }

        private bool IsSelectedMember(ReferenceMemberDescriptor member)
        {
            return SelectedMember != null &&
                member != null &&
                SelectedMember.MetadataToken == member.MetadataToken &&
                string.Equals(SelectedMember.DeclaringTypeName, member.DeclaringTypeName, StringComparison.Ordinal);
        }

        private string BuildSelectionPath()
        {
            var assemblyName = SelectedAssembly != null ? SelectedAssembly.DisplayName : "Assembly";
            var typeName = SelectedType != null ? ShortTypeName(SelectedType.FullName) : "Type";
            var memberName = SelectedMember != null ? SelectedMember.DisplayName : "Member";
            return assemblyName + "  >  " + typeName + "  >  " + memberName;
        }

        private string BuildSelectionCaption()
        {
            if (SelectedAssembly == null)
            {
                return "Browse reference assemblies, then narrow down to a type and member before decompiling.";
            }

            if (SelectedType == null)
            {
                return "Assembly selected. Pick a type to inspect its members and decompile a focused target.";
            }

            if (SelectedMember == null)
            {
                return "Type selected. Pick a member to inspect its signature or decompile just that method.";
            }

            return "Selected " + SelectedMember.DeclaringTypeName + "." + SelectedMember.DisplayName + ".";
        }

        private static string CompactPath(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            var tailLength = Math.Max(18, maxLength / 2);
            var headLength = Math.Max(12, maxLength - tailLength - 3);
            if (value.Length <= headLength + tailLength + 3)
            {
                return value;
            }

            return value.Substring(0, headLength) + "..." + value.Substring(value.Length - tailLength);
        }

        private static string ShortTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return "Unknown";
            }

            var separatorIndex = fullName.LastIndexOf('.');
            return separatorIndex >= 0 && separatorIndex < fullName.Length - 1
                ? fullName.Substring(separatorIndex + 1)
                : fullName;
        }

        private static void SplitTypeName(string fullName, out string shortName, out string namespaceName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                shortName = "Unknown";
                namespaceName = "(global namespace)";
                return;
            }

            var separatorIndex = fullName.LastIndexOf('.');
            if (separatorIndex <= 0 || separatorIndex >= fullName.Length - 1)
            {
                shortName = fullName;
                namespaceName = "(global namespace)";
                return;
            }

            shortName = fullName.Substring(separatorIndex + 1);
            namespaceName = fullName.Substring(0, separatorIndex);
        }

        private static void SplitMemberDisplay(string displayName, out string title, out string signature)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                title = "Unknown";
                signature = string.Empty;
                return;
            }

            var openParen = displayName.IndexOf('(');
            if (openParen <= 0)
            {
                title = displayName;
                signature = string.Empty;
                return;
            }

            title = displayName.Substring(0, openParen);
            signature = displayName.Substring(openParen);
        }

        private static string FormatMetadataToken(int metadataToken)
        {
            return "0x" + metadataToken.ToString("X8");
        }

        private static bool MatchesText(string value, string filter)
        {
            return string.IsNullOrEmpty(filter) || (value ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ComposeTooltip(string primary, string secondary)
        {
            if (string.IsNullOrEmpty(primary))
            {
                return secondary ?? string.Empty;
            }

            if (string.IsNullOrEmpty(secondary))
            {
                return primary;
            }

            return primary + "\n" + secondary;
        }
    }
}
