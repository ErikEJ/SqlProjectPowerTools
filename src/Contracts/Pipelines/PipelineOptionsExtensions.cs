using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace DacFXToolLib.Pipelines
{
    public static class PipelineOptionsExtensions
    {
        public static PipelineOptions TryRead(string optionsPath)
        {
            if (string.IsNullOrEmpty(optionsPath))
            {
                return null;
            }

            if (!File.Exists(optionsPath))
            {
                return null;
            }

            var options = File.ReadAllText(optionsPath, Encoding.UTF8);
            var result = TryReadJson(options);

            return result ?? throw new InvalidDataException($"The pipeline options file '{optionsPath}' is empty or contains invalid JSON.");
        }

        public static PipelineOptions TryReadJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var ser = new DataContractJsonSerializer(typeof(PipelineOptions));
                    return ser.ReadObject(ms) as PipelineOptions;
                }
            }
            catch (Exception ex) when (ex is SerializationException || ex is ArgumentException || ex is EncoderFallbackException)
            {
                return null;
            }
        }

        public static string Write(this PipelineOptions options)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(ms, Encoding.UTF8, true, true, "   "))
                {
                    var serializer = new DataContractJsonSerializer(typeof(PipelineOptions));
                    serializer.WriteObject(writer, options);
                    writer.Flush();
                }

                var json = ms.ToArray();
                return Encoding.UTF8.GetString(json, 0, json.Length);
            }
        }
    }
}
