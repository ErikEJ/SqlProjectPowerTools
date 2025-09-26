using Microsoft.SqlServer.Dac;

namespace DacFXToolLib
{
    public class DacPackageUnpacker
    {
        public void Unpack(string dacpacPath, string outputPath)
        {
            // Ensure output directory exists
            Directory.CreateDirectory(outputPath);
            
            var package = DacPackage.Load(dacpacPath);
            DacServices.Unpack(package, outputPath);
        }
    }
}