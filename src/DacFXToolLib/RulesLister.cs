using System.Collections.Generic;
using System.Linq;
using DacFXToolLib.Common;
using Microsoft.SqlServer.Dac.CodeAnalysis;
using Microsoft.SqlServer.Dac.Model;

namespace DacFXToolLib
{
    /// <summary>
    /// Lists all available static analysis rules for a given DACPAC file.
    /// </summary>
    public class RulesLister
    {
        private readonly string dacpacPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="RulesLister"/> class.
        /// </summary>
        /// <param name="dacpacPath">Path to the DACPAC file to load rules for.</param>
        public RulesLister(string dacpacPath)
        {
            ArgumentNullException.ThrowIfNull(dacpacPath);
            this.dacpacPath = dacpacPath;
        }

        /// <summary>
        /// Returns all available analyzer rules for the DACPAC model as a list of <see cref="IssueTypeModel"/>.
        /// </summary>
        public IList<IssueTypeModel> GetRules()
        {
            using var model = TSqlModel.LoadFromDacpac(
                dacpacPath,
                new ModelLoadOptions()
                {
                    LoadAsScriptBackedModel = true,
                    ModelStorageType = Microsoft.SqlServer.Dac.DacSchemaModelStorageType.Memory,
                });

            var factory = new CodeAnalysisServiceFactory();
            var service = factory.CreateAnalysisService(model);

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
