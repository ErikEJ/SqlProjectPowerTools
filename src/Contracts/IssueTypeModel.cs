namespace DacFXToolLib.Common
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents a single analyzer rule (issue type) available in the code analysis service.
    /// </summary>
    [DataContract]
    public class IssueTypeModel
    {
        /// <summary>
        /// Gets or sets the short rule identifier (e.g. "SR0001").
        /// </summary>
        [DataMember]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the severity of the rule (e.g. "Warning", "Error", "Information").
        /// </summary>
        [DataMember]
        public string Severity { get; set; }

        /// <summary>
        /// Gets or sets the human-readable description of the rule.
        /// </summary>
        [DataMember]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the fully qualified category of the rule (namespace + category).
        /// </summary>
        [DataMember]
        public string Category { get; set; }
    }
}
