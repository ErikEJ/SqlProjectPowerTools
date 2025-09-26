using Microsoft.SqlServer.Dac;

namespace DacFXToolLib
{
    public class DacPackageUnpacker
    {
        public void Unpack(string dacpacPath, string outputPath)
        {
            var package = DacPackage.Load(dacpacPath);
            DacServices.Unpack(package, outputPath);
        }
    }
}