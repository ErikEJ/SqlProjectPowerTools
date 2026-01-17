using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Threading;
using SqlProjectsPowerTools.TreeViewer.MEF;

namespace SqlProjectsPowerTools.TreeViewer
{
#pragma warning disable S3881 // "IDisposable" should be implemented correctly
    internal class DacpacRootNode : IAttachedCollectionSource, INotifyPropertyChanged, IDisposable
#pragma warning restore S3881 // "IDisposable" should be implemented correctly
    {
        private readonly DacpacItemNode item;
        private readonly string projectPath;
        private readonly DTE dte;
        private readonly string defaultName;

        public DacpacRootNode(IVsHierarchyItem hierarchyItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            EnvDTE.Project project = HierarchyUtilities.GetProject(hierarchyItem);
            defaultName = project.Name + ".dacpac";
            item = new(this, defaultName, "root");
            dte = project.DTE;
            projectPath = project.FullName;

            Rebuild(false);
            dte.Events.BuildEvents.OnBuildProjConfigDone += BuildEvents_OnBuildProjConfigDone;
        }

        private void BuildEvents_OnBuildProjConfigDone(string project, string projectConfig, string platform, string solutionConfig, bool success)
        {
            if (success && projectPath.EndsWith(project))
            {
                Debouncer.Debounce(projectPath, () => Rebuild(true), 500);
            }
        }

        private void Rebuild(bool force)
        {
            ThreadHelper.JoinableTaskFactory.StartOnIdle(
                async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var dacpacPath = GetDacpacPath();

                    await TaskScheduler.Default;

                    if (!string.IsNullOrEmpty(dacpacPath))
                    {
                        var unpackedPath = UnpackDacpac(dacpacPath, force);

                        if (!string.IsNullOrEmpty(unpackedPath))
                        {
                            item.Rebuild(unpackedPath, dacpacPath);
                        }
                    }
                    else
                    {
                        item.Rebuild(defaultName, "root");
                    }
                },
                VsTaskRunContext.UIThreadIdlePriority).FireAndForget();
        }

        private string GetDacpacPath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            EnvDTE.Project project = FindProjectRecursive(dte.Solution.Projects);

            if (project != null)
            {
                // TODO : use correct way to get output path for a .dacpac
                var outputPath = project.ConfigurationManager.ActiveConfiguration.Properties.Item("SqlTargetPath").Value.ToString();

                if (string.IsNullOrEmpty(outputPath))
                {
                    outputPath = project.ConfigurationManager.ActiveConfiguration.Properties.Item("TargetPath").Value.ToString();
                }

                var binDir = Path.Combine(Path.GetDirectoryName(project.FullName), outputPath);

                return Directory.Exists(binDir) ? Directory.GetFiles(binDir, "*.dacpac", SearchOption.TopDirectoryOnly).FirstOrDefault() : null;
            }

            return null;
        }

        /// <summary>
        /// Recursively searches for a project by path, including projects nested in solution folders.
        /// </summary>
        private EnvDTE.Project FindProjectRecursive(Projects projects)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (EnvDTE.Project project in projects)
            {
                EnvDTE.Project found = FindProjectRecursive(project);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Recursively searches within a project (which may be a solution folder) for the target project.
        /// </summary>
        private EnvDTE.Project FindProjectRecursive(EnvDTE.Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (project == null)
            {
                return null;
            }

            // Check if this is our target project
            if (string.Equals(project.FullName, projectPath, StringComparison.OrdinalIgnoreCase))
            {
                return project;
            }

            // If this is a solution folder, search its nested projects
            if (project.Kind == EnvDTE.Constants.vsProjectKindSolutionItems)
            {
                foreach (ProjectItem projectItem in project.ProjectItems)
                {
                    EnvDTE.Project subProject = projectItem.SubProject;
                    if (subProject != null)
                    {
                        EnvDTE.Project found = FindProjectRecursive(subProject);
                        if (found != null)
                        {
                            return found;
                        }
                    }
                }
            }

            return null;
        }

        public object SourceItem => this;

        public bool HasItems => item != null;

        public IEnumerable Items => new[] { item };

        private static string UnpackDacpac(string dacpacPath, bool force)
        {
            if (!File.Exists(dacpacPath))
            {
                return null;
            }

            var path = Path.Combine(Path.GetTempPath(), Vsix.Name, Path.GetFileName(dacpacPath));

            if (Directory.Exists(path))
            {
                if (!force && Directory.GetLastWriteTime(path) > File.GetLastWriteTime(dacpacPath))
                {
                    return path;
                }

                Directory.Delete(path, true);
            }

            try
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(dacpacPath, path);
                return path;
            }
            catch (IOException ex)
            {
                ex.Log();
                return null;
            }
        }

        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Dispose()
        {
        }
    }
}
