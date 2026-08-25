using System;

namespace DWSIM.Validation.Tests.Framework
{
    internal static class ValidationAssert
    {
        public static void Close(string name, double expected, double actual, double relTol)
        {
            double denom = Math.Abs(expected) > 1e-12 ? Math.Abs(expected) : 1.0;
            double err = Math.Abs(actual - expected) / denom;
            if (err > relTol)
                throw new Exception($"{name}: expected {expected}, got {actual} (rel err {err:P3} > tol {relTol:P3})");
        }
    }
}
