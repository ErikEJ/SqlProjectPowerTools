using System.ComponentModel.Composition;
using MarkdownLintVS.Linting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownLintVS.Tagging
{
    /// <summary>
    /// Provides the tagger for sql files.
    /// </summary>
    [Export(typeof(ITaggerProvider))]
    [ContentType("text")]
    [TagType(typeof(IErrorTag))]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
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

            if (buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document)
                && (!document.FilePath?.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) ?? true))
            {
                return null;
            }

            return buffer.Properties.GetOrCreateSingletonProperty(
                typeof(MarkdownLintTagger),
                () => new MarkdownLintTagger(buffer, AnalysisCache)) as ITagger<T>;
        }
    }
}
