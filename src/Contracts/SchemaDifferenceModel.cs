namespace DacFXToolLib.Common
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents a single schema difference detected during schema comparison.
    /// </summary>
    [DataContract]
    public class SchemaDifferenceModel
    {
        /// <summary>
        /// Gets or sets the fully qualified object name.
        /// </summary>
        [DataMember]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the database object type (e.g. Table, View, Procedure).
        /// </summary>
        [DataMember]
        public string ObjectType { get; set; }

        /// <summary>
        /// Gets or sets the type of difference: Added, Deleted, or Changed.
        /// </summary>
        [DataMember]
        public string DifferenceType { get; set; }

        /// <summary>
        /// Gets or sets the update action: Deploy, Skip, or Ignore.
        /// </summary>
        [DataMember]
        public string UpdateAction { get; set; }

        /// <summary>
        /// Gets or sets the T-SQL script for the source endpoint object.
        /// </summary>
        [DataMember]
        public string SourceScript { get; set; }

        /// <summary>
        /// Gets or sets the T-SQL script for the target endpoint object.
        /// </summary>
        [DataMember]
        public string TargetScript { get; set; }
    }
}
