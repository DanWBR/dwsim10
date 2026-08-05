//    The IPOPT surface the engine calls, waiting for its managed implementation.
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using System;

namespace Cureos.Numerics
{
    /// <summary>
    /// The interior point solver the Gibbs reactor, two of the flash algorithms and the binary
    /// interaction parameter regression reach for. On .NET Framework this came from
    /// Cureos.Numerics, a wrapper over the native IPOPT, which has no arm64 build. The managed
    /// replacement being written keeps this shape, so nothing that calls it has to change; see
    /// docs/ipopt-contract.md for what the callers expect of it.
    ///
    /// Until then every solve says so rather than returning a wrong answer.
    /// </summary>
    public class Ipopt : IDisposable
    {
        public const double PositiveInfinity = 2e19;
        public const double NegativeInfinity = -2e19;

        private const string NotBuiltYet =
            "This build has no IPOPT. The unit operations and flash algorithms that minimise " +
            "Gibbs energy through it, and the binary interaction parameter regression, are not " +
            "available here.";

        public Ipopt(int n, double[] x_L, double[] x_U,
                     int m, double[] g_L, double[] g_U,
                     int nele_jac, int nele_hess,
                     EvaluateObjectiveDelegate eval_f,
                     EvaluateConstraintsDelegate eval_g,
                     EvaluateObjectiveGradientDelegate eval_grad_f,
                     EvaluateJacobianDelegate eval_jac_g,
                     EvaluateHessianDelegate eval_h)
        {
        }

        public bool AddOption(string keyword, string val) => false;

        public bool AddOption(string keyword, double val) => false;

        public bool AddOption(string keyword, int val) => false;

        public bool OpenOutputFile(string file_name, int print_level) => false;

        public bool SetScaling(double obj_scaling, double[] x_scaling, double[] g_scaling) => false;

        public bool SetIntermediateCallback(IntermediateDelegate intermediate) => false;

        public IpoptReturnCode SolveProblem(double[] x, ref double obj_val, double[] g,
                                            double[] mult_g, double[] mult_x_L, double[] mult_x_U)
        {
            throw new NotSupportedException(NotBuiltYet);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    public delegate bool EvaluateObjectiveDelegate(
        int n, double[] x, bool new_x, ref double obj_value);

    public delegate bool EvaluateObjectiveGradientDelegate(
        int n, double[] x, bool new_x, ref double[] grad_f);

    public delegate bool EvaluateConstraintsDelegate(
        int n, double[] x, bool new_x, int m, ref double[] g);

    public delegate bool EvaluateJacobianDelegate(
        int n, double[] x, bool new_x, int m, int nele_jac,
        ref int[] iRow, ref int[] jCol, ref double[] values);

    public delegate bool EvaluateHessianDelegate(
        int n, double[] x, bool new_x, double obj_factor, int m, double[] lambda,
        bool new_lambda, int nele_hess, ref int[] iRow, ref int[] jCol, ref double[] values);

    public delegate bool IntermediateDelegate(
        IpoptAlgorithmMode alg_mod, int iter_count, double obj_value,
        double inf_pr, double inf_du, double mu, double d_norm,
        double regularization_size, double alpha_du, double alpha_pr, int ls_trials);

    public enum IpoptAlgorithmMode
    {
        RegularMode = 0,
        RestorationPhaseMode = 1,
    }

    /// <summary>IPOPT's own ApplicationReturnStatus.</summary>
    public enum IpoptReturnCode
    {
        Solve_Succeeded = 0,
        Solved_To_Acceptable_Level = 1,
        Infeasible_Problem_Detected = 2,
        Search_Direction_Becomes_Too_Small = 3,
        Diverging_Iterates = 4,
        User_Requested_Stop = 5,
        Feasible_Point_Found = 6,
        Maximum_Iterations_Exceeded = -1,
        Restoration_Failed = -2,
        Error_In_Step_Computation = -3,
        Maximum_CpuTime_Exceeded = -4,
        Not_Enough_Degrees_Of_Freedom = -10,
        Invalid_Problem_Definition = -11,
        Invalid_Option = -12,
        Invalid_Number_Detected = -13,
        Unrecoverable_Exception = -100,
        NonIpopt_Exception_Thrown = -101,
        Insufficient_Memory = -102,
        Internal_Error = -199,
    }
}
