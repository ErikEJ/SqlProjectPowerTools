using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools
{
    public static class ResultDeserializer
    {
        public static VisualCompareResult BuildVisualCompareResult(string jsonFilePath)
        {
            var json = File.ReadAllText(jsonFilePath, Encoding.UTF8);
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var ser = new DataContractJsonSerializer(typeof(VisualCompareResult));
            var result = ser.ReadObject(ms) as VisualCompareResult;
            ms.Close();
            return result ?? new VisualCompareResult { Differences = [], DeploymentScript = string.Empty };
        }

        public static List<TableModel> BuildTableResult(string output)
        {
            var resultParts = output.Split(new[] { "Result:" + Environment.NewLine }, StringSplitOptions.None);
            if (resultParts.Length == 2 && TryRead(resultParts[1], out List<TableModel> deserialized))
            {
                return deserialized;
            }

            var errorParts = output.Split(new[] { "Error:" + Environment.NewLine }, StringSplitOptions.None);
            if (errorParts.Length == 2)
            {
                throw new InvalidOperationException("Table list error: " + Environment.NewLine + errorParts[1]);
            }

            throw new InvalidOperationException($"Table list error: Unable to launch external process: {Environment.NewLine + output}");
        }

        public static string BuildDiagramResult(string output)
        {
            var resultParts = output.Split(new[] { "Result:" + Environment.NewLine }, StringSplitOptions.None);
            if (resultParts.Length == 2)
            {
                return resultParts[1].Trim();
            }

            var errorParts = output.Split(new[] { "Error:" + Environment.NewLine }, StringSplitOptions.None);
            if (errorParts.Length == 2)
            {
                throw new InvalidOperationException("Result error: " + Environment.NewLine + errorParts[1]);
            }

            throw new InvalidOperationException($"Launch error: Unable to launch external process: {Environment.NewLine + output}");
        }

        private static bool TryRead<T>(string options, out T deserialized)
            where T : class, new()
        {
            try
            {
                var ms = new MemoryStream(Encoding.UTF8.GetBytes(options));
                var ser = new DataContractJsonSerializer(typeof(T));
                deserialized = ser.ReadObject(ms) as T;
                ms.Close();
                return true;
            }
            catch
            {
                deserialized = null;
                return false;
            }
        }
    }
}