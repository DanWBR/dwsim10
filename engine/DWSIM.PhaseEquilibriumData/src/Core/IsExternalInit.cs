// Polyfill for C# 9+ init-only / record support on net472.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
