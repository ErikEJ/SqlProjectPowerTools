using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Utilities;

namespace SqlProjectsPowerTools.TreeViewer.MEF
{
    [Export(typeof(IAttachedCollectionSourceProvider))]
    [Name(nameof(DacpacItemSourceProvider))]
    [Order(Before = HierarchyItemsProviderNames.Contains)]
    [AppliesToUIContext(VsixPackage.UIContextGuid)]
    internal class DacpacItemSourceProvider : IAttachedCollectionSourceProvider
    {
        private readonly Dictionary<string, DacpacRootNode> rootNodes = new();

        public DacpacItemSourceProvider()
        {
            VS.Events.SolutionEvents.OnBeforeCloseSolution += OnBeforeCloseSolution;
        }

        private void OnBeforeCloseSolution()
        {
            foreach (var rootNode in rootNodes.Values)
            {
                rootNode?.Dispose();
            }

            rootNodes.Clear();
        }

        public IAttachedCollectionSource CreateCollectionSource(object item, string relationshipName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (relationshipName == KnownRelationships.Contains)
            {
                if (item is IVsHierarchyItem hierarchyItem && IsDacpacProject(hierarchyItem))
                {
                    string projectPath = GetProjectPath(hierarchyItem);
                    if (!string.IsNullOrEmpty(projectPath))
                    {
                        if (!rootNodes.TryGetValue(projectPath, out DacpacRootNode rootNode))
                        {
                            rootNode = new DacpacRootNode(hierarchyItem);
                            rootNodes[projectPath] = rootNode;
                        }

                        return rootNode;
                    }
                }
                else if (item is DacpacItemNode node)
                {
                    return node;
                }
            }

            return null;
        }

        public IEnumerable<IAttachedRelationship> GetRelationships(object item)
        {
            if (item is IVsHierarchyItem hierarchyItem && IsDacpacProject(hierarchyItem))
            {
                yield return Relationships.Contains;
            }
        }

        private static bool IsDacpacProject(IVsHierarchyItem hierarchyItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var isSqlProject = false;

            try
            {
                if (!HierarchyUtilities.IsProject(hierarchyItem.HierarchyIdentity))
                {
                    return false;
                }

                IVsHierarchy hierarchy = hierarchyItem.GetHierarchy();

                if (hierarchy.TryGetItemProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ProjectDir, out string projectDir))
                {
                    ThreadHelper.JoinableTaskFactory.Run(async () =>
                    {
                        var projects = await VS.Solutions.GetAllProjectsAsync();

                        var project = projects.FirstOrDefault(p => Path.GetDirectoryName(p.FullPath).Equals(projectDir, StringComparison.OrdinalIgnoreCase));

                        isSqlProject = project?.IsAnySqlDatabaseProject() ?? false;
                    });
                }
            }
            catch (Exception ex)
            {
                ex.Log();
            }

            return isSqlProject;
        }

        private static string GetProjectPath(IVsHierarchyItem hierarchyItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                EnvDTE.Project project = HierarchyUtilities.GetProject(hierarchyItem);
                return project?.FullName;
            }
            catch (Exception ex)
            {
                ex.Log();
                return null;
            }
        }
    }
}
