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
        private static readonly char[] Separator = [';'];

        private readonly SqlServerVersion sqlServerVersion;
        private readonly HashSet<string> ignoredRules = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> ignoredRuleSets = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> errorRuleSets = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="RulesLister"/> class.
        /// </summary>
        /// <param name="sqlServerVersion">The SQL Server version to load rules for.</param>
        public RulesLister(string sqlServerVersion)
        {
            ArgumentNullException.ThrowIfNull(sqlServerVersion);

            if (!Enum.TryParse(sqlServerVersion, ignoreCase: true, out SqlServerVersion parsedVersion))
            {
                throw new ArgumentException("Invalid SQL Server version.", nameof(sqlServerVersion));
            }

            this.sqlServerVersion = parsedVersion;
        }

        /// <summary>
        /// Gets the list of available rules, optionally filtered by a CodeAnalysisRules expression.
        /// </summary>
        /// <param name="rulesExpression">The CodeAnalysisRules project property value, or empty string.</param>
        /// <returns>List of <see cref="IssueTypeModel"/> with <see cref="IssueTypeModel.Enabled"/> set accordingly.</returns>
        public IList<IssueTypeModel> GetRules(string rulesExpression = "")
        {
            BuildRuleLists(rulesExpression);

            var factory = new CodeAnalysisServiceFactory();
            var service = factory.CreateAnalysisService(sqlServerVersion);

            if (ignoredRules.Count > 0 || ignoredRuleSets.Count > 0)
            {
                service.SetProblemSuppressor(p =>
                    ignoredRules.Contains(p.Rule.RuleId)
                    || ignoredRuleSets.Any(s => p.Rule.RuleId.StartsWith(s, StringComparison.OrdinalIgnoreCase)));
            }

            return GetIssueTypes(service.GetRules()).ToList();
        }

        private void BuildRuleLists(string rulesExpression)
        {
            if (!string.IsNullOrWhiteSpace(rulesExpression))
            {
                var rules = rulesExpression.Split(Separator,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var rule in rules.Where(rule => rule.StartsWith('-') && rule.Length > 1))
                {
                    if (rule.Length > 2 && rule.EndsWith('*'))
                    {
                        ignoredRuleSets.Add(rule[1..^1]);
                    }
                    else
                    {
                        ignoredRules.Add(rule[1..]);
                    }
                }

                foreach (var rule in rules.Where(rule =>
                    rule.StartsWith("+!", StringComparison.OrdinalIgnoreCase) && rule.Length > 2))
                {
                    if (rule.Length > 3 && rule.EndsWith('*'))
                    {
                        errorRuleSets.Add(rule[2..^1]);
                    }
                    else
                    {
                        errorRuleSets.Add(rule[2..]);
                    }
                }
            }
        }

        private IEnumerable<IssueTypeModel> GetIssueTypes(IList<RuleDescriptor> rules)
        {
            return rules
                .GroupBy(r => r.ShortRuleId)
                .Select(g => g.First())
                .Select(r => new IssueTypeModel
                {
                    Id = r.ShortRuleId,
                    Severity = errorRuleSets.Any(s => r.RuleId.StartsWith(s, StringComparison.OrdinalIgnoreCase))
                        ? "Error"
                        : r.Severity.ToString(),
                    Description = r.DisplayDescription,
                    Category = $"{r.Namespace}.{r.Metadata.Category}",
                    Enabled = !ignoredRules.Contains(r.RuleId)
                        && !ignoredRuleSets.Any(s => r.RuleId.StartsWith(s, StringComparison.OrdinalIgnoreCase)),
                });
        }
    }
}
