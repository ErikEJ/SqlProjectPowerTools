using DacFXToolLib.Dab;

namespace DacFXToolLib.Tests
{
    /// <summary>
    /// Basic unit tests for DabBuilder that don't require a real dacpac file
    /// </summary>
    public class DabBuilderBasicTests
    {
        [Fact]
        public void Constructor_WithNullOptions_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DabBuilder(null!));
        }

        [Fact]
        public void GetDabConfigCmdFile_WithNullProjectPath_ReturnsEmptyString()
        {
            // Arrange
            var options = new DataApiBuilderOptions
            {
                DatabaseType = DacFXToolLib.Common.DatabaseType.SQLServerDacpac,
                ProjectPath = null,
            };

            var builder = new DabBuilder(options);

            // Act
            var result = builder.GetDabConfigCmdFile();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void DataApiBuilderOptions_DefaultConnectionStringName_IsCorrect()
        {
            // Arrange & Act
            var options = new DataApiBuilderOptions();

            // Assert
            Assert.Equal("dab-connection-string", options.ConnectionStringName);
        }

        [Fact]
        public void DataApiBuilderOptions_CanSetCustomConnectionStringName()
        {
            // Arrange
            var customName = "my-custom-connection";
            var options = new DataApiBuilderOptions
            {
                ConnectionStringName = customName,
            };

            // Act & Assert
            Assert.Equal(customName, options.ConnectionStringName);
        }
    }
}
