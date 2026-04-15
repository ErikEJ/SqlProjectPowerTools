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
        public void GetModelOptionValue_ForKnownDbScopedConfigFlags_ConvertsToOnOff(string propertyName, bool input, string expected)
        {
            var result = InvokeGetModelOptionValue(propertyName, input);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetModelOptionValue_ForOtherProperty_ReturnsStringValue()
        {
            var result = InvokeGetModelOptionValue("SomeOtherProperty", true);

            Assert.Equal("True", result);
        }

        [Fact]
        public void GetModelOptionValue_ForNull_ReturnsNull()
        {
            var result = InvokeGetModelOptionValue("SomeOtherProperty", null);

            Assert.Null(result);
        }

        private static string? InvokeGetModelOptionValue(string propertyName, object? value)
        {
            var method = typeof(SchemaExtractor).GetMethod("GetModelOptionValue", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (string?)method!.Invoke(null, [propertyName, value]);
        }
    }
}
