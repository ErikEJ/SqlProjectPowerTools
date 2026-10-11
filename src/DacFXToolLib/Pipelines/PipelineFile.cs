namespace DacFXToolLib.Pipelines
{
    /// <summary>
    /// A file produced by the <see cref="PipelineGenerator"/>.
    /// </summary>
    /// <param name="Path">Full path of the file.</param>
    /// <param name="Content">File content.</param>
    /// <param name="CanOverwrite">True if the file may be overwritten when it already exists and overwrite is requested.</param>
    public sealed record PipelineFile(string Path, string Content, bool CanOverwrite);
}
