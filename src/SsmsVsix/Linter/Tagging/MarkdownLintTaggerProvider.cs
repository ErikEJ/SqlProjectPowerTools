using System.ComponentModel.Composition;
using MarkdownLintVS.Linting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using SqlProjectsPowerTools;

namespace MarkdownLintVS.Tagging
{
    /// <summary>
    /// Provides the tagger for sql files.
    /// </summary>
    [Export(typeof(ITaggerProvider))]
    [ContentType("SQL")]
    [ContentType("SQL Server Tools")]
    [TagType(typeof(IErrorTag))]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    public class MarkdownLintTaggerProvider : ITaggerProvider
    {
        [Import]
        internal MarkdownAnalysisCache AnalysisCache { get; set; }

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer)
            where T : ITag
        {
            if (buffer == null)
            {
                return null;
            }

            if (ToolOptions.Instance.DisableCodeAnalysis)
            {
                return null;
            }

            var enabled = false;
            var sqlVersion = string.Empty;
            var rules = string.Empty;
            var project = string.Empty;

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var currentProject = await VS.Solutions.GetActiveProjectAsync();

                if (currentProject != null)
                {
                    project = currentProject.Name;
                    var runProperties = await currentProject.IsInSqlProjAsync();
                    enabled = runProperties.Run;
                    sqlVersion = runProperties.SqlVersion;
                    rules = runProperties.Rules;
                }
            });

            if (!enabled)
            {
                return null;
            }

            if (buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document)
                && (!document.FilePath?.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) ?? true))
            {
                return null;
            }

            return buffer.Properties.GetOrCreateSingletonProperty(
                typeof(MarkdownLintTagger),
                () => new MarkdownLintTagger(buffer, AnalysisCache, sqlVersion, rules, project)) as ITagger<T>;
        }
    }
}
