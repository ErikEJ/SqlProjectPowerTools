using System.Collections.Generic;
using System.Linq;
using DacFXToolLib.Common;
using Microsoft.SqlServer.Dac.CodeAnalysis;
using Microsoft.SqlServer.Dac.Model;

namespace DacFXToolLib
{
    /// <summary>
    /// Lists all available static analysis rules for a given SQL Server version.
    /// </summary>
    public class RulesLister
    {
        private readonly SqlServerVersion sqlServerVersion;

        /// <summary>
        /// Initializes a new instance of the <see cref="RulesLister"/> class.
        /// </summary>
        /// <param name="sqlServerVersion">The SQL Server version to load rules for.</param>
        public RulesLister(string sqlServerVersion)
        {
            ArgumentNullException.ThrowIfNull(sqlServerVersion);
            this.sqlServerVersion = Enum.Parse<SqlServerVersion>(sqlServerVersion, ignoreCase: true);
        }

        /// <summary>
        /// Returns all available analyzer rules as a list of <see cref="IssueTypeModel"/>.
        /// </summary>
        public IList<IssueTypeModel> GetRules()
        {
            var factory = new CodeAnalysisServiceFactory();
            var service = factory.CreateAnalysisService(sqlServerVersion);

            return GetIssueTypes(service.GetRules()).ToList();
        }

        private static IEnumerable<IssueTypeModel> GetIssueTypes(IList<RuleDescriptor> rules)
        {
            return from r in rules
                   select new IssueTypeModel
                   {
                       Id = r.ShortRuleId,
                       Severity = r.Severity.ToString(),
                       Description = r.DisplayDescription,
                       Category = $"{r.Namespace}.{r.Metadata.Category}",
                   };
        }
    }
}
