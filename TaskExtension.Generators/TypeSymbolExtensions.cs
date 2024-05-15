using Microsoft.CodeAnalysis;

namespace TaskExtension.Generators;

internal static class TypeSymbolExtensions
{
    internal static bool IsDerivedFrom(this ITypeSymbol from, ITypeSymbol to)
    {
        while (true)
        {
            if (from is null)
                return false;
            if (from.Equals(to, SymbolEqualityComparer.Default))
                return true;
            from = from.BaseType;
        }
    }
}