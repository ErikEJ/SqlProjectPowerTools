// Polyfill for C# 'init' keyword support on .NET Framework 4.8.
// This type is provided by the runtime in .NET 5+ but must be
// defined manually when targeting older frameworks.
namespace System.Runtime.CompilerServices
{
#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable S2094 // Classes should not be empty
    internal static class IsExternalInit
    {
    }
#pragma warning restore S2094 // Classes should not be empty
#pragma warning restore SA1649 // File name should match first type name
}
