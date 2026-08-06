// netstandard2.0 has no IsExternalInit, which the compiler requires for init accessors and for
// records. Declaring it here costs nothing and keeps the solver source free of #if.

#if !NET
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
#endif
