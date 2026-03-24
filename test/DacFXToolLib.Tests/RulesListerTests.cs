namespace DacFXToolLib.Tests
{
    /// <summary>
    /// Unit tests for <see cref="RulesLister.GetRules"/>.
    /// </summary>
    public class RulesListerTests
    {
        private const string SqlVersion = "Sql160";

        [Fact]
        public void Constructor_WithNullVersion_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RulesLister(null!));
        }

        [Fact]
        public void Constructor_WithInvalidVersion_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new RulesLister("NotAVersion"));
        }

        [Fact]
        public void GetRules_NoExpression_ReturnsNonEmptyList()
        {
            var lister = new RulesLister(SqlVersion);
            var rules = lister.GetRules();
            Assert.NotEmpty(rules);
        }

        [Fact]
        public void GetRules_NoExpression_AllRulesEnabled()
        {
            var lister = new RulesLister(SqlVersion);
            var rules = lister.GetRules();
            Assert.All(rules, r => Assert.True(r.Enabled));
        }

        [Fact]
        public void GetRules_EmptyExpression_AllRulesEnabled()
        {
            var lister = new RulesLister(SqlVersion);
            var rules = lister.GetRules(string.Empty);
            Assert.All(rules, r => Assert.True(r.Enabled));
        }

        [Fact]
        public void GetRules_SuppressedRuleById_MarksRuleDisabled()
        {
            var lister = new RulesLister(SqlVersion);
            var allRules = lister.GetRules();
            var firstRule = allRules[0];

            // Id is already the fully qualified rule identifier (e.g. "Microsoft.Rules.Data.SR0001")
            var expression = $"-{firstRule.Id}";

            var lister2 = new RulesLister(SqlVersion);
            var rules = lister2.GetRules(expression);

            var suppressed = rules.SingleOrDefault(r => r.Id == firstRule.Id);
            Assert.NotNull(suppressed);
            Assert.False(suppressed!.Enabled);
        }

        [Fact]
        public void GetRules_SuppressedRuleByWildcard_MarksMatchingRulesDisabled()
        {
            var lister = new RulesLister(SqlVersion);
            var allRules = lister.GetRules();

            // Pick the namespace prefix of the first rule's category (e.g. "SqlServer.Rules")
            var categoryParts = allRules[0].Category.Split('.');
            var prefix = string.Join(".", categoryParts.Take(categoryParts.Length - 1));

            var expression = $"-{prefix}.*";

            var lister2 = new RulesLister(SqlVersion);
            var rules = lister2.GetRules(expression);

            // All rules whose category starts with that prefix should be disabled
            var matchingRules = rules.Where(r =>
                r.Category.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

            Assert.NotEmpty(matchingRules);
            Assert.All(matchingRules, r => Assert.False(r.Enabled));
        }

        [Fact]
        public void GetRules_SuppressedRuleByWildcard_DoesNotDisableNonMatchingRules()
        {
            var lister = new RulesLister(SqlVersion);
            var allRules = lister.GetRules();

            // Find a prefix that only covers some rules (not all)
            var categoryParts = allRules[0].Category.Split('.');
            var prefix = string.Join(".", categoryParts.Take(categoryParts.Length - 1));

            var expression = $"-{prefix}.*";

            var lister2 = new RulesLister(SqlVersion);
            var rules = lister2.GetRules(expression);

            // Rules from a different namespace should still be enabled
            var nonMatchingRules = rules.Where(r =>
                !r.Category.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

            if (nonMatchingRules.Any())
            {
                Assert.All(nonMatchingRules, r => Assert.True(r.Enabled));
            }
        }

        [Fact]
        public void GetRules_ErrorRuleSetWildcard_SetsSeverityToError()
        {
            var lister = new RulesLister(SqlVersion);
            var allRules = lister.GetRules();

            var categoryParts = allRules[0].Category.Split('.');
            var prefix = string.Join(".", categoryParts.Take(categoryParts.Length - 1));

            var expression = $"+!{prefix}.*";

            var lister2 = new RulesLister(SqlVersion);
            var rules = lister2.GetRules(expression);

            var elevated = rules.Where(r =>
                r.Category.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

            Assert.NotEmpty(elevated);
            Assert.All(elevated, r => Assert.Equal("Error", r.Severity));
        }

        [Fact]
        public void GetRules_ErrorRuleSetWildcard_DoesNotChangeNonMatchingSeverity()
        {
            var lister = new RulesLister(SqlVersion);
            var allRules = lister.GetRules();

            var categoryParts = allRules[0].Category.Split('.');
            var prefix = string.Join(".", categoryParts.Take(categoryParts.Length - 1));

            var expression = $"+!{prefix}.*";

            var lister2 = new RulesLister(SqlVersion);
            var rules = lister2.GetRules(expression);

            // Non-matching rules keep their original severity (not forced to "Error")
            var nonMatchingRules = rules.Where(r =>
                !r.Category.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

            if (nonMatchingRules.Any())
            {
                // Original severities were not "Error" before; get them from an unfiltered call
                var originalRules = allRules.ToDictionary(r => r.Id, r => r.Severity);
                foreach (var r in nonMatchingRules)
                {
                    if (originalRules.TryGetValue(r.Id, out var originalSeverity))
                    {
                        Assert.Equal(originalSeverity, r.Severity);
                    }
                }
            }
        }

        [Fact]
        public void GetRules_CombinedExpression_AppliesAllRules()
        {
            var lister = new RulesLister(SqlVersion);
            var allRules = lister.GetRules();

            var firstRule = allRules[0];
            var categoryParts = firstRule.Category.Split('.');
            var prefix = string.Join(".", categoryParts.Take(categoryParts.Length - 1));

            // Suppress by individual ID and elevate rest of namespace to error
            // Id is already the fully qualified rule identifier (e.g. "Microsoft.Rules.Data.SR0001")
            var expression = $"-{firstRule.Id};+!{prefix}.*";

            var lister2 = new RulesLister(SqlVersion);
            var rules = lister2.GetRules(expression);

            var suppressed = rules.SingleOrDefault(r => r.Id == firstRule.Id);
            Assert.NotNull(suppressed);
            Assert.False(suppressed!.Enabled);

            // Other matching rules should have Error severity
            var elevated = rules.Where(r =>
                r.Id != firstRule.Id &&
                r.Category.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

            if (elevated.Any())
            {
                Assert.All(elevated, r => Assert.Equal("Error", r.Severity));
            }
        }

        [Fact]
        public void GetRules_MultipleCallsSameInstance_DoNotLeakState()
        {
            var lister = new RulesLister(SqlVersion);
            var allRules = lister.GetRules();
            var firstRule = allRules[0];

            // Id is already the fully qualified rule identifier (e.g. "Microsoft.Rules.Data.SR0001")
            // First call: suppress a rule
            var filtered = lister.GetRules($"-{firstRule.Id}");
            var suppressedRule = filtered.SingleOrDefault(r => r.Id == firstRule.Id);
            Assert.NotNull(suppressedRule);
            Assert.False(suppressedRule!.Enabled);

            // Second call: no expression — should not carry over suppression
            var plain = lister.GetRules();
            var restoredRule = plain.SingleOrDefault(r => r.Id == firstRule.Id);
            Assert.NotNull(restoredRule);
            Assert.True(restoredRule!.Enabled);
        }
    }
}
