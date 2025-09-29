using Humanizer;
using Microsoft.EntityFrameworkCore.Design;

namespace DacFXToolLib
{
    public class HumanizerPluralizer : IPluralizer
    {
        public string Pluralize(string identifier)
           => identifier.Pluralize(inputIsKnownToBeSingular: false);

        public string Singularize(string identifier)
            => identifier.Singularize(inputIsKnownToBePlural: false);
    }
}