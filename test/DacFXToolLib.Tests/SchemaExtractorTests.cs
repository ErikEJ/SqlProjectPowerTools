using System.Reflection;

namespace DacFXToolLib.Tests
{
    public class SchemaExtractorTests
    {
        [Theory]
        [InlineData("DbScopedConfigLegacyCardinalityEstimation", true, "On")]
        [InlineData("DbScopedConfigLegacyCardinalityEstimation", false, "Off")]
        [InlineData("DbScopedConfigParameterSniffing", true, "On")]
        [InlineData("DbScopedConfigParameterSniffing", false, "Off")]
        public void GetModelOptionValue_ForKnownDbScopedConfigFlags_ConvertsToOnOffWhenWorkaroundEnabled(string propertyName, bool input, string expected)
        {
            var result = InvokeGetModelOptionValue(propertyName, input, true);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("DbScopedConfigLegacyCardinalityEstimation", true, "True")]
        [InlineData("DbScopedConfigLegacyCardinalityEstimation", false, "False")]
        [InlineData("DbScopedConfigParameterSniffing", true, "True")]
        [InlineData("DbScopedConfigParameterSniffing", false, "False")]
        public void GetModelOptionValue_ForKnownDbScopedConfigFlags_ReturnsBoolStringWhenWorkaroundDisabled(string propertyName, bool input, string expected)
        {
            var result = InvokeGetModelOptionValue(propertyName, input, false);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetModelOptionValue_ForOtherProperty_ReturnsStringValue()
        {
            var result = InvokeGetModelOptionValue("SomeOtherProperty", true, true);

            Assert.Equal("True", result);
        }

        [Fact]
        public void GetModelOptionValue_ForNull_ReturnsNull()
        {
            var result = InvokeGetModelOptionValue("SomeOtherProperty", null, false);

            Assert.Null(result);
        }

        private static string? InvokeGetModelOptionValue(string propertyName, object? value, bool useDbScopedConfigOnOffWorkaround)
        {
            var method = typeof(SchemaExtractor).GetMethod("GetModelOptionValue", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (string?)method!.Invoke(null, [propertyName, value, useDbScopedConfigOnOffWorkaround]);
        }
    }
}
