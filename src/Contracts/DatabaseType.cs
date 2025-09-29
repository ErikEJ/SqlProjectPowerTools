// ReSharper disable InconsistentNaming
namespace DacFXToolLib.Common
{
#pragma warning disable CA1027 // Mark enums with FlagsAttribute
    public enum DatabaseType
#pragma warning restore CA1027 // Mark enums with FlagsAttribute
    {
        Undefined = 0,
        SQLServer = 1,
        SQLServerDacpac = 8,
    }
}