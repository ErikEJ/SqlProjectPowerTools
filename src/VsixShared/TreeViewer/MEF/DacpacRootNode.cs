using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Threading;

namespace SqlProjectsPowerTools.TreeViewer
{
    internal sealed class DacpacRootNode : IAttachedCollectionSource, INotifyPropertyChanged, IDisposable
    {
        private readonly DacpacItemNode item;
        private readonly IEnumerable items;
        private readonly string projectPath;
        private readonly string projectDirectory;
        private readonly DTE dte;
        private readonly string defaultName;
        private readonly object watcherLock = new();
        private EnvDTE.Project dteProject;
        private FileSystemWatcher dacpacWatcher;
        private string watchedDirectory;

        public DacpacRootNode(IVsHierarchyItem hierarchyItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            EnvDTE.Project project = HierarchyUtilities.GetProject(hierarchyItem);
            defaultName = project.Name + ".dacpac";
            item = new(this, defaultName, "root");
            items = new[] { item };
            dte = project.DTE;
            projectPath = project.FullName;
            projectDirectory = Path.GetDirectoryName(projectPath);
            dteProject = project;

            Rebuild(false);
            dte.Events.BuildEvents.OnBuildProjConfigDone += BuildEvents_OnBuildProjConfigDone;
        }

        private void BuildEvents_OnBuildProjConfigDone(string project, string projectConfig, string platform, string solutionConfig, bool success)
        {
            if (success && IsMatchingProject(project))
            {
                ScheduleRebuild(force: true);
            }
        }

        private void ScheduleRebuild(bool force)
        {
            Debouncer.Debounce(projectPath, () => Rebuild(force), 500);
        }

        private bool IsMatchingProject(string projectFromEvent)
        {
            if (string.IsNullOrWhiteSpace(projectFromEvent))
            {
                return false;
            }

            string trackedProject = NormalizePath(projectPath);
            string eventProject = NormalizePath(projectFromEvent);

            if (!string.IsNullOrEmpty(trackedProject) && !string.IsNullOrEmpty(eventProject))
            {
                return string.Equals(trackedProject, eventProject, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(Path.GetFileName(projectPath), Path.GetFileName(projectFromEvent), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return null;
            }
        }

        private void Rebuild(bool force)
        {
            ThreadHelper.JoinableTaskFactory.StartOnIdle(
                async () =>
            {
                try
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    string outputDirectory = GetOutputDirectory();
                    UpdateDacpacWatcher(outputDirectory);

                    await TaskScheduler.Default;

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    string dacpacPath = GetDacpacPath(outputDirectory);

                    if (!string.IsNullOrEmpty(dacpacPath))
                    {
                        string unpackedPath = UnpackDacpac(dacpacPath, force);
                        string tooltip = BuildTooltip(dacpacPath, unpackedPath);

                        if (!string.IsNullOrEmpty(unpackedPath))
                        {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            item.Rebuild(unpackedPath, dacpacPath, tooltip);
                            return;
                        }

                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        item.Rebuild(defaultName, "root", tooltip);
                        return;
                    }

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    item.Rebuild(defaultName, "root", BuildMissingDacpacTooltip(outputDirectory));
                }
                catch (Exception ex)
                {
                    ex.Log();
                }
            },
                VsTaskRunContext.UIThreadIdlePriority).FireAndForget();
        }

        private string GetOutputDirectory()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string outputPath = GetOutputPathFromProject();
            if (string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrWhiteSpace(projectDirectory))
            {
                return null;
            }

            return Path.GetFullPath(Path.Combine(projectDirectory, outputPath));
        }

        private string GetDacpacPath(string outputDirectory)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
            {
                return null;
            }

            string[] candidates = Directory.GetFiles(outputDirectory, "*.dacpac", SearchOption.TopDirectoryOnly);
            if (candidates.Length == 0)
            {
                return null;
            }

            HashSet<string> preferredNames = GetPreferredDacpacFileNames();

            return candidates
                .OrderByDescending(path => preferredNames.Contains(Path.GetFileName(path)))
                .ThenByDescending(path => File.GetLastWriteTimeUtc(path))
                .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private HashSet<string> GetPreferredDacpacFileNames()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var preferredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                defaultName,
                Path.GetFileNameWithoutExtension(projectPath) + ".dacpac",
            };

            EnvDTE.Project project = dteProject ?? FindProjectRecursive(dte.Solution.Projects);

            AddDacpacFileName(preferredNames, GetProjectPropertyValue(project, "SqlTargetPath"));
            AddDacpacFileName(preferredNames, GetProjectPropertyValue(project, "TargetPath"));
            AddDacpacFileName(preferredNames, GetProjectPropertyValue(project, "TargetFileName"));
            AddDacpacFileName(preferredNames, GetProjectPropertyValue(project, "TargetName"));
            AddDacpacFileName(preferredNames, GetProjectPropertyValue(project, "OutputFileName"));
            AddDacpacFileName(preferredNames, GetProjectPropertyValue(project, "AssemblyName"));

            return preferredNames;
        }

        private static void AddDacpacFileName(ISet<string> fileNames, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string candidate = Path.GetFileName(value);

            if (string.IsNullOrEmpty(candidate))
            {
                return;
            }

            string fileName = candidate.EndsWith(".dacpac", StringComparison.OrdinalIgnoreCase)
                ? candidate
                : candidate + ".dacpac";

            fileNames.Add(fileName);
        }

        private static string GetProjectPropertyValue(EnvDTE.Project project, string propertyName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                return project?.Properties?.Item(propertyName)?.Value?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private void UpdateDacpacWatcher(string outputDirectory)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string normalizedOutputDirectory = NormalizePath(outputDirectory);
            bool watcherMatches = !string.IsNullOrEmpty(normalizedOutputDirectory)
                && string.Equals(watchedDirectory, normalizedOutputDirectory, StringComparison.OrdinalIgnoreCase);

            if (watcherMatches)
            {
                return;
            }

            DisposeWatcher();

            if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
            {
                return;
            }

            var watcher = new FileSystemWatcher(outputDirectory, "*.dacpac")
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            };

            watcher.Changed += DacpacWatcher_Changed;
            watcher.Created += DacpacWatcher_Changed;
            watcher.Deleted += DacpacWatcher_Changed;
            watcher.Renamed += DacpacWatcher_Renamed;
            watcher.EnableRaisingEvents = true;

            lock (watcherLock)
            {
                dacpacWatcher = watcher;
                watchedDirectory = normalizedOutputDirectory;
            }
        }

        private void DacpacWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            ScheduleRebuild(force: false);
        }

        private void DacpacWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            ScheduleRebuild(force: false);
        }

        private void DisposeWatcher()
        {
            FileSystemWatcher watcherToDispose;

            lock (watcherLock)
            {
                watcherToDispose = dacpacWatcher;
                dacpacWatcher = null;
                watchedDirectory = null;
            }

            if (watcherToDispose == null)
            {
                return;
            }

            watcherToDispose.EnableRaisingEvents = false;
            watcherToDispose.Changed -= DacpacWatcher_Changed;
            watcherToDispose.Created -= DacpacWatcher_Changed;
            watcherToDispose.Deleted -= DacpacWatcher_Changed;
            watcherToDispose.Renamed -= DacpacWatcher_Renamed;
            watcherToDispose.Dispose();
        }

        private string GetOutputPathFromProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                EnvDTE.Project project = dteProject;

                if (project == null || !string.Equals(project.FullName, projectPath, StringComparison.OrdinalIgnoreCase))
                {
                    project = FindProjectRecursive(dte.Solution.Projects);
                    dteProject = project;
                }

                return project?.ConfigurationManager?.ActiveConfiguration?.Properties?.Item("OutputPath")?.Value?.ToString();
            }
            catch (Exception ex)
            {
                ex.Log();
                return null;
            }
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

        public IEnumerable Items => items;

        private static string UnpackDacpac(string dacpacPath, bool force)
        {
            if (!File.Exists(dacpacPath))
            {
                return null;
            }

            string path = GetExtractionPath(dacpacPath);
            string currentStamp = GetDacpacStamp(dacpacPath);

            if (Directory.Exists(path))
            {
                if (!force && IsExtractionCurrent(path, currentStamp))
                {
                    return path;
                }

                try
                {
                    Directory.Delete(path, true);
                }
                catch (IOException ex)
                {
                    ex.Log();
                    return null;
                }
                catch (UnauthorizedAccessException ex)
                {
                    ex.Log();
                    return null;
                }
            }

            try
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(dacpacPath, path);
                WriteExtractionStamp(path, currentStamp);
                return path;
            }
            catch (IOException ex)
            {
                ex.Log();
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                ex.Log();
                return null;
            }
        }

        private static string GetExtractionPath(string dacpacPath)
        {
            return Path.Combine(Path.GetTempPath(), "SQL Database Project Power Tools", GetPathKey(dacpacPath));
        }

        private static string GetPathKey(string path)
        {
            byte[] pathBytes = Encoding.UTF8.GetBytes(path);
            byte[] hashBytes;

            using (SHA256 sha256 = SHA256.Create())
            {
                hashBytes = sha256.ComputeHash(pathBytes);
            }

            var builder = new StringBuilder(16);
            for (int i = 0; i < 8; i++)
            {
                builder.Append(hashBytes[i].ToString("x2"));
            }

            return builder.ToString();
        }

        private static string GetStampPath(string extractionPath)
        {
            return Path.Combine(extractionPath, ".dacpacstamp");
        }

        private static string GetDacpacStamp(string dacpacPath)
        {
            FileInfo fileInfo = new(dacpacPath);
            return $"{fileInfo.Length}:{fileInfo.LastWriteTimeUtc.Ticks}";
        }

        private static bool IsExtractionCurrent(string extractionPath, string currentStamp)
        {
            string stampPath = GetStampPath(extractionPath);
            if (!File.Exists(stampPath))
            {
                return false;
            }

            try
            {
                string existingStamp = File.ReadAllText(stampPath);
                return string.Equals(existingStamp, currentStamp, StringComparison.Ordinal);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static void WriteExtractionStamp(string extractionPath, string currentStamp)
        {
            try
            {
                File.WriteAllText(GetStampPath(extractionPath), currentStamp);
            }
            catch (UnauthorizedAccessException ex)
            {
                // Writing the stamp is a best-effort optimization; failures should not break extraction.
                Debug.WriteLine("Failed to write extraction stamp due to unauthorized access: " + ex);
            }
            catch (IOException ex)
            {
                // Writing the stamp is a best-effort optimization; failures should not break extraction.
                Debug.WriteLine("Failed to write extraction stamp due to I/O error: " + ex);
            }
            catch (Exception ex)
            {
                // Swallow any unexpected errors to avoid failing otherwise successful extraction.
                Debug.WriteLine("Failed to write extraction stamp due to unexpected error: " + ex);
            }
        }

        private static string BuildMissingDacpacTooltip(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                return "Build the project to browse its generated DACPAC package.";
            }

            return $"Build the project to browse its generated DACPAC package.\r\nExpected output folder: {outputDirectory}";
        }

        private static string BuildTooltip(string dacpacPath, string extractedPath)
        {
            if (string.IsNullOrWhiteSpace(dacpacPath) || !File.Exists(dacpacPath))
            {
                return BuildMissingDacpacTooltip(outputDirectory: null);
            }

            FileInfo fileInfo = new(dacpacPath);
            var tooltip = new StringBuilder();

            AppendTooltipLine(tooltip, "DACPAC file", fileInfo.Name);
            AppendTooltipLine(tooltip, "Size", fileInfo.Length.ToString("N0") + " bytes");
            AppendTooltipLine(tooltip, "Last updated", fileInfo.LastWriteTime.ToString());

            AddOriginMetadata(tooltip, extractedPath);

            return tooltip.ToString().TrimEnd();
        }

        private static void AddOriginMetadata(StringBuilder tooltip, string extractedPath)
        {
            string originPath = GetOriginPath(extractedPath);
            if (string.IsNullOrWhiteSpace(originPath))
            {
                return;
            }

            try
            {
                string originContent = File.ReadAllText(originPath);

                AppendTooltipLine(tooltip, "DacFX Version", GetOriginElementValue(originContent, "ProductVersion"));
            }
            catch (Exception ex)
            {
                ex.Log();
            }
        }

        private static string GetOriginPath(string extractedPath)
        {
            if (string.IsNullOrWhiteSpace(extractedPath) || !Directory.Exists(extractedPath))
            {
                return null;
            }

            return Directory.GetFiles(extractedPath, "origin.xml", SearchOption.TopDirectoryOnly).FirstOrDefault();
        }

        private static string GetOriginElementValue(string originContent, string elementName)
        {
            Match match = Regex.Match(
                originContent,
                $@"<(?:(?:\w+):)?{Regex.Escape(elementName)}\b[^>]*>(?<value>.*?)</(?:(?:\w+):)?{Regex.Escape(elementName)}>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline,
                TimeSpan.FromSeconds(1));

            return match.Success ? CleanOriginValue(match.Groups["value"].Value) : null;
        }

        private static string CleanOriginValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value
                .Replace("\r", string.Empty)
                .Replace("\n", " ")
                .Replace("\t", " ")
                .Trim();
        }

        private static void AppendTooltipLine(StringBuilder tooltip, string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            tooltip.Append(label);
            tooltip.Append(": ");
            tooltip.AppendLine(value.Trim());
        }

        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            dte.Events.BuildEvents.OnBuildProjConfigDone -= BuildEvents_OnBuildProjConfigDone;
            DisposeWatcher();
            item?.Dispose();
        }
    }
}
