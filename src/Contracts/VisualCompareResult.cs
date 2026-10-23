using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DacFXToolLib.Common;

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
    public IList<SchemaDifferenceModel> Differences { get; set; }

    /// <summary>
    /// Gets or sets the DacFx-generated deployment script.
    /// </summary>
    [DataMember]
    public string DeploymentScript { get; set; }
}
