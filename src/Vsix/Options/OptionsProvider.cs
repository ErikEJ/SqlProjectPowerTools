using System.Runtime.InteropServices;

namespace SqlProjectsPowerTools
{
    internal class OptionsProvider
    {
        [ComVisible(true)]
        public class VsixOptions : BaseOptionPage<ToolOptions>
        {
        }
    }
}