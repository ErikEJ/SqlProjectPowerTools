using System.Runtime.InteropServices;

namespace SqlProjectsPowerTools
{
    internal sealed class OptionsProvider
    {
        [ComVisible(true)]
        public sealed class VsixOptions : BaseOptionPage<ToolOptions>
        {
        }
    }
}