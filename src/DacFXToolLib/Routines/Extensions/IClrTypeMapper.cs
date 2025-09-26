using RevEng.Core.Abstractions.Metadata;

namespace DacFXToolLib.Routines.Extensions
{
    public interface IClrTypeMapper
    {
        Type GetClrType(ModuleParameter parameter);

        Type GetClrType(ModuleResultElement resultElement);
    }
}