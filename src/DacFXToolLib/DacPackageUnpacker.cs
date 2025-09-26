using Microsoft.SqlServer.Dac;

namespace DacFXToolLib
{
    public static class DacPackageUnpacker
    {
        public static void Unpack(string dacpacPath, string outputPath)
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