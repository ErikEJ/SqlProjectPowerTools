using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SqlProjectsPowerTools
{
    public static class SqlProjHelper
    {
        public static async Task<string> BuildSqlProjectAsync(string sqlprojPath, bool mustBuild = true)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (sqlprojPath.EndsWith(".dacpac", StringComparison.OrdinalIgnoreCase))
            {
                return sqlprojPath;
            }

            var project = await GetProjectAsync(sqlprojPath);
            if (project == null)
            {
                return null;
            }

            if (mustBuild && !await VS.Build.ProjectIsUpToDateAsync(project))
            {
                var ok = await VS.Build.BuildProjectAsync(project, BuildAction.Build);

                if (!ok)
                {
                    throw new InvalidOperationException("Dacpac build failed");
                }
            }

            var dacpacPath = await project.GetDacpacPathAsync();

            if (!string.IsNullOrEmpty(dacpacPath))
            {
                return dacpacPath;
            }

            throw new InvalidOperationException("Dacpac build failed, please pick the file manually");
        }

        private static async System.Threading.Tasks.Task LinkedFilesSearchAsync(IEnumerable<SolutionItem> projectItems, HashSet<string> files)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            foreach (var item in projectItems)
            {
                if (item.Children.Any())
                {
                    await LinkedFilesSearchAsync(item.Children, files);
                }

                if (item.Type == SolutionItemType.PhysicalFile)
                {
                    var file = item as PhysicalFile;
                    var fullPath = file.Parent?.FullPath;

                    if (file.Extension == ".dacpac"
                        && !string.IsNullOrEmpty(fullPath)
                        && !string.IsNullOrEmpty(file.FullPath)
                        && !fullPath.StartsWith(Path.GetDirectoryName(file.FullPath), StringComparison.OrdinalIgnoreCase))
                    {
                        files.Add(file.FullPath);
                    }
                }
            }
        }

        private static async Task<Project> GetProjectAsync(string projectItemPath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            return (await VS.Solutions.GetAllProjectsAsync())
                .SingleOrDefault(p => p.FullPath == projectItemPath);
        }
    }
}