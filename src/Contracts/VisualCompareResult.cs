namespace DacFXToolLib.Common
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Contains the full result of a visual schema comparison.
    /// </summary>
    [DataContract]
    public class VisualCompareResult
    {
        /// <summary>
        /// Gets or sets all detected schema differences.
        /// </summary>
        [DataMember]
        public SchemaDifferenceModel[] Differences { get; set; }

        /// <summary>
        /// Gets or sets the DacFx-generated deployment script.
        /// </summary>
        [DataMember]
        public string DeploymentScript { get; set; }
    }
}
