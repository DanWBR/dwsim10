// Copyright (C) 2026 -- published under the Eclipse Public License, like Ipopt.
//
// A pass-through SparseSymLinearSolverInterface that forwards every call to a
// real inner solver (e.g. MA57) and, for each new factorization, appends the
// symmetric KKT system to a binary "IKKT" dump for offline replay through the
// managed C# solver. This is the Phase-0 instrumentation: it lets you validate
// the managed solver's inertia against MA57 and measure the KKT size
// distribution of real problems, with zero native/managed interop.
//
// Drop this pair of files into src/Algorithm/LinearSolvers, add them to
// src/Makefile.am next to the other solver interfaces, and wrap the created
// SolverInterface in IpAlgBuilder.cpp (see dotnet/native/README.md). Capture is
// enabled at run time by setting the environment variable IPOPT_KKT_DUMP to the
// output file path.

#ifndef __IPKKTDUMPSOLVERINTERFACE_HPP__
#define __IPKKTDUMPSOLVERINTERFACE_HPP__

#include "IpSparseSymLinearSolverInterface.hpp"
#include <cstdio>
#include <string>
#include <vector>

namespace Ipopt
{

class KktDumpSolverInterface: public SparseSymLinearSolverInterface
{
public:
   /** Wrap @p inner and append captured systems to @p dumpPath. */
   KktDumpSolverInterface(
      SmartPtr<SparseSymLinearSolverInterface> inner,
      const std::string&                       dumpPath
   );

   virtual ~KktDumpSolverInterface();

   bool InitializeImpl(
      const OptionsList& options,
      const std::string& prefix
   ) override;

   ESymSolverStatus InitializeStructure(
      Index        dim,
      Index        nonzeros,
      const Index* ia,
      const Index* ja
   ) override;

   Number* GetValuesArrayPtr() override;

   ESymSolverStatus MultiSolve(
      bool         new_matrix,
      const Index* ia,
      const Index* ja,
      Index        nrhs,
      Number*      rhs_vals,
      bool         check_NegEVals,
      Index        numberOfNegEVals
   ) override;

   Index NumberOfNegEVals() const override;

   bool IncreaseQuality() override;

   bool ProvidesInertia() const override;

   EMatrixFormat MatrixFormat() const override;

   bool ProvidesDegeneracyDetection() const override;

   ESymSolverStatus DetermineDependentRows(
      const Index*      ia,
      const Index*      ja,
      std::list<Index>& c_deps
   ) override;

private:
   KktDumpSolverInterface(const KktDumpSolverInterface&);
   void operator=(const KktDumpSolverInterface&);

   void writeHeaderIfNeeded();
   void writeRecord(
      const Index* ia,
      const Index* ja,
      Index        nrhs,
      const Number* rhs_snapshot,
      bool         check_NegEVals,
      Index        requestedNegEVals,
      Index        nativeNegEVals
   );

   SmartPtr<SparseSymLinearSolverInterface> inner_;
   std::string dumpPath_;
   std::FILE*  file_;
   bool        headerWritten_;

   Index dim_;
   Index nonzeros_;
};

} // namespace Ipopt

#endif
