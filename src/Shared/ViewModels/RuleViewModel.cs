using System;
using System.Collections.Generic;
using GalaSoft.MvvmLight;

namespace SqlProjectsPowerTools
{
    public class RuleViewModel : ViewModelBase
    {
        private bool isEnabled;
        private string severity;
        private bool isVisible = true;

        public RuleViewModel(string id, string description, string category, bool enabled, string severity)
        {
            Id = id;
            Description = description;
            Category = category;
            GroupName = GetGroupName(id, category);
            isEnabled = enabled;
            this.severity = severity;
        }

        public event EventHandler IsEnabledChanged;

        public string Id { get; }

        public string Description { get; }

        public string Category { get; }

        public string GroupName { get; }

        public string DisplayText => $"{GetLastIdPart(Id)}: {Description}";

        public string HelpLink => GetHelpLink(Id, Category);

        public bool HasHelpLink => !string.IsNullOrEmpty(HelpLink);

        public IList<string> AvailableSeverities { get; } = new List<string> { "Warning", "Error" };

        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (Equals(value, isEnabled))
                {
                    return;
                }

                isEnabled = value;
                RaisePropertyChanged();
                IsEnabledChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string Severity
        {
            get => severity;
            set
            {
                if (Equals(value, severity))
                {
                    return;
                }

                severity = value;
                RaisePropertyChanged();
            }
        }

        public bool IsVisible
        {
            get => isVisible;
            set
            {
                if (Equals(value, isVisible))
                {
                    return;
                }

                isVisible = value;
                RaisePropertyChanged();
            }
        }

        private static string GetLastIdPart(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return string.Empty;
            }

            var lastDot = id.LastIndexOf('.');
            return lastDot >= 0 ? id.Substring(lastDot + 1) : id;
        }

        private static string GetHelpLink(string id, string category)
        {
            if (string.IsNullOrEmpty(id))
            {
                return string.Empty;
            }

            var lastIdDot = id.LastIndexOf('.');
            var ruleId = lastIdDot >= 0 ? id.Substring(lastIdDot + 1) : id;

            var lastCatDot = category?.LastIndexOf('.') ?? -1;
            var categoryName = string.IsNullOrEmpty(category)
                ? string.Empty
                : (lastCatDot >= 0 ? category.Substring(lastCatDot + 1) : category);

            if (id.StartsWith("Microsoft.Rules.Data.", StringComparison.OrdinalIgnoreCase))
            {
                return ruleId.ToUpperInvariant() switch
                {
                    "SR0001" => "https://learn.microsoft.com/sql/tools/sql-database-projects/concepts/sql-code-analysis/t-sql-design-issues#sr0001-avoid-select--in-stored-procedures-views-and-table-valued-functions",
                    "SR0008" => "https://learn.microsoft.com/sql/tools/sql-database-projects/concepts/sql-code-analysis/t-sql-design-issues#sr0008-consider-using-scope_identity-instead-of-identity",
                    "SR0009" => "https://learn.microsoft.com/sql/tools/sql-database-projects/concepts/sql-code-analysis/t-sql-design-issues#sr0009-avoid-using-types-of-variable-length-that-are-size-1-or-2",
                    "SR0010" => "https://learn.microsoft.com/sql/tools/sql-database-projects/concepts/sql-code-analysis/t-sql-design-issues#sr0010-avoid-using-deprecated-syntax-when-you-join-tables-or-views",
                    "SR0013" => "https://learn.microsoft.com/sql/tools/sql-database-projects/concepts/sql-code-analysis/t-sql-design-issues#sr0013-output-parameter-parameter-isnt-populated-in-all-code-paths",
                    "SR0014" => "https://learn.microsoft.com/sql/tools/sql-database-projects/concepts/sql-code-analysis/t-sql-design-issues#sr0014-data-loss-might-occur-when-casting-from-type1-to-type2",
                    "SR0011" => "https://learn.microsoft.com/sql/tools/sql-database-projects/concepts/sql-code-analysis/t-sql-naming-issues#sr0011-avoid-using-special-characters-in-object-names",
                    "SR0012" => "https://learn.microsoft.com/sql/tools/sql-database-projects/concepts/sql-code-analysis/t-sql-naming-issues#sr0012-avoid-using-reserved-words-for-type-names",
                    "SR0016" => "https://learn.microsoft.com/sql/tools/sql-database-projects/concepts/sql-code-analysis/t-sql-naming-issues#sr0016-avoid-using-sp_-as-a-prefix-for-stored-procedures",
                    "SR0004" => "https://learn.microsoft.com/sql/tools/sql-database-projects/concepts/sql-code-analysis/t-sql-performance-issues#sr0004-avoid-using-columns-that-dont-have-indexes-as-test-expressions-in-in-predicates",
                    "SR0005" => "https://learn.microsoft.com/sql/tools/sql-database-projects/concepts/sql-code-analysis/t-sql-performance-issues#sr0005-avoid-using-patterns-that-start-with--in-like-predicates",
                    "SR0006" => "https://learn.microsoft.com/sql/tools/sql-database-projects/concepts/sql-code-analysis/t-sql-performance-issues#sr0006-move-a-column-reference-to-one-side-of-a-comparison-operator-to-use-a-column-index",
                    "SR0007" => "https://learn.microsoft.com/sql/tools/sql-database-projects/concepts/sql-code-analysis/t-sql-performance-issues#sr0007-use-isnullcolumn-default_value-on-nullable-columns-in-expressions",
                    "SR0015" => "https://learn.microsoft.com/sql/tools/sql-database-projects/concepts/sql-code-analysis/t-sql-performance-issues#sr0015-extract-deterministic-function-calls-from-where-predicates",
                    _ => string.Empty,
                };
            }

            if (id.StartsWith("SqlServer.Rules.", StringComparison.OrdinalIgnoreCase))
            {
                if (categoryName is "Design" or "Naming" or "Performance")
                {
                    return $"https://github.com/ErikEJ/SqlServer.Rules/blob/master/docs/{categoryName}/{ruleId}.md";
                }
            }

            if (id.StartsWith("Smells.", StringComparison.OrdinalIgnoreCase))
            {
                return $"https://github.com/ErikEJ/SqlServer.Rules/blob/master/docs/CodeSmells/{ruleId}.md";
            }

            return string.Empty;
        }

        private static string GetGroupName(string id, string category)
        {
            var lastDot = category?.LastIndexOf('.') ?? -1;
            var baseName = string.IsNullOrEmpty(category)
                ? string.Empty
                : (lastDot >= 0 ? category.Substring(lastDot + 1) : category);

            if (string.IsNullOrEmpty(id))
            {
                return baseName;
            }

            var parts = id.Split('.');
            var suffix = parts.Length >= 4
                ? $"({parts[0]})"
                : parts.Length == 3
                    ? $"({parts[0]}.{parts[1]})"
                    : parts.Length == 2
                        ? $"({parts[0]})"
                        : string.Empty;

            return string.IsNullOrEmpty(suffix) ? baseName : $"{baseName} {suffix}";
        }
    }
}
