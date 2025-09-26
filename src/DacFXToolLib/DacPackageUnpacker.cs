using Microsoft.SqlServer.Dac;
using System.IO;

namespace DacFXToolLib
{
    public class DacPackageUnpacker
    {
        public void Unpack(string dacpacPath, string outputPath)
        {
            // Ensure output directory exists
            Directory.CreateDirectory(outputPath);
            
            using (var package = DacPackage.Load(dacpacPath))
            {
                DacServices.Unpack(package, outputPath);
            }
        }
    }
}