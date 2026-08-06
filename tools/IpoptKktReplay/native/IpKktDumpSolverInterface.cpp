// Copyright (C) 2026 -- published under the Eclipse Public License, like Ipopt.
//
// Implementation of the KKT-dumping pass-through solver interface. See the header
// and dotnet/native/README.md. The binary format matches Ipopt.Sparse's KktDump
// (little-endian int32 / float64), streamed record-by-record.

#include "IpKktDumpSolverInterface.hpp"

#include <cstdint>
#include <cstring>

namespace Ipopt
{

KktDumpSolverInterface::KktDumpSolverInterface(
   SmartPtr<SparseSymLinearSolverInterface> inner,
   const std::string&                       dumpPath
)
   : inner_(inner),
     dumpPath_(dumpPath),
     file_(NULL),
     headerWritten_(false),
     dim_(0),
     nonzeros_(0)
{ }

KktDumpSolverInterface::~KktDumpSolverInterface()
{
   if( file_ != NULL )
   {
      std::fclose(file_);
      file_ = NULL;
   }
}

bool KktDumpSolverInterface::InitializeImpl(
   const OptionsList& options,
   const std::string& prefix
)
{
   return inner_->InitializeImpl(options, prefix);
}

ESymSolverStatus KktDumpSolverInterface::InitializeStructure(
   Index        dim,
   Index        nonzeros,
   const Index* ia,
   const Index* ja
)
{
   dim_ = dim;
   nonzeros_ = nonzeros;
   return inner_->InitializeStructure(dim, nonzeros, ia, ja);
}

Number* KktDumpSolverInterface::GetValuesArrayPtr()
{
   return inner_->GetValuesArrayPtr();
}

ESymSolverStatus KktDumpSolverInterface::MultiSolve(
   bool         new_matrix,
   const Index* ia,
   const Index* ja,
   Index        nrhs,
   Number*      rhs_vals,
   bool         check_NegEVals,
   Index        numberOfNegEVals
)
{
   // Snapshot the right-hand side(s) before the solve overwrites them.
   std::vector<Number> rhs_snapshot;
   if( new_matrix && nrhs > 0 && rhs_vals != NULL )
   {
      rhs_snapshot.assign(rhs_vals, rhs_vals + (std::size_t) nrhs * (std::size_t) dim_);
   }

   ESymSolverStatus status = inner_->MultiSolve(
      new_matrix, ia, ja, nrhs, rhs_vals, check_NegEVals, numberOfNegEVals);

   if( new_matrix )
   {
      Index nativeNeg = -1;
      if( inner_->ProvidesInertia() && status == SYMSOLVER_SUCCESS )
      {
         nativeNeg = inner_->NumberOfNegEVals();
      }
      writeRecord(ia, ja, nrhs,
                  rhs_snapshot.empty() ? NULL : &rhs_snapshot[0],
                  check_NegEVals, numberOfNegEVals, nativeNeg);
   }

   return status;
}

Index KktDumpSolverInterface::NumberOfNegEVals() const
{
   return inner_->NumberOfNegEVals();
}

bool KktDumpSolverInterface::IncreaseQuality()
{
   return inner_->IncreaseQuality();
}

bool KktDumpSolverInterface::ProvidesInertia() const
{
   return inner_->ProvidesInertia();
}

SparseSymLinearSolverInterface::EMatrixFormat KktDumpSolverInterface::MatrixFormat() const
{
   return inner_->MatrixFormat();
}

bool KktDumpSolverInterface::ProvidesDegeneracyDetection() const
{
   return inner_->ProvidesDegeneracyDetection();
}

ESymSolverStatus KktDumpSolverInterface::DetermineDependentRows(
   const Index*      ia,
   const Index*      ja,
   std::list<Index>& c_deps
)
{
   return inner_->DetermineDependentRows(ia, ja, c_deps);
}

// ---------------------------------------------------------------------------

static void put_i32(std::FILE* f, std::int32_t v)
{
   // x86-64 is little-endian; write the native bytes.
   std::fwrite(&v, sizeof(std::int32_t), 1, f);
}

static void put_f64(std::FILE* f, double v)
{
   std::fwrite(&v, sizeof(double), 1, f);
}

void KktDumpSolverInterface::writeHeaderIfNeeded()
{
   if( headerWritten_ )
   {
      return;
   }
   if( file_ == NULL )
   {
      file_ = std::fopen(dumpPath_.c_str(), "wb");
      if( file_ == NULL )
      {
         return;
      }
   }
   std::fputc('I', file_);
   std::fputc('K', file_);
   std::fputc('K', file_);
   std::fputc('T', file_);
   put_i32(file_, 1); // version

   // One-time note so the operator can confirm the index convention.
   std::fprintf(stderr,
      "[KktDump] writing '%s'; MatrixFormat=%d (0=Triplet). "
      "Managed replay expects Triplet_Format, 0-based lower triangle.\n",
      dumpPath_.c_str(), (int) inner_->MatrixFormat());

   headerWritten_ = true;
}

void KktDumpSolverInterface::writeRecord(
   const Index*  ia,
   const Index*  ja,
   Index         nrhs,
   const Number* rhs_snapshot,
   bool          check_NegEVals,
   Index         requestedNegEVals,
   Index         nativeNegEVals
)
{
   writeHeaderIfNeeded();
   if( file_ == NULL )
   {
      return;
   }

   const Index effectiveNrhs = (rhs_snapshot != NULL) ? nrhs : 0;

   put_i32(file_, (std::int32_t) dim_);
   put_i32(file_, (std::int32_t) nonzeros_);
   put_i32(file_, (std::int32_t) effectiveNrhs);
   put_i32(file_, (std::int32_t) requestedNegEVals);
   put_i32(file_, (std::int32_t) nativeNegEVals);
   put_i32(file_, check_NegEVals ? 1 : 0);

   for( Index k = 0; k < nonzeros_; ++k )
   {
      put_i32(file_, (std::int32_t) ia[k]);
   }
   for( Index k = 0; k < nonzeros_; ++k )
   {
      put_i32(file_, (std::int32_t) ja[k]);
   }

   Number* vals = inner_->GetValuesArrayPtr();
   for( Index k = 0; k < nonzeros_; ++k )
   {
      put_f64(file_, (double) vals[k]);
   }

   const std::size_t rhsLen = (std::size_t) effectiveNrhs * (std::size_t) dim_;
   for( std::size_t k = 0; k < rhsLen; ++k )
   {
      put_f64(file_, (double) rhs_snapshot[k]);
   }

   std::fflush(file_);
}

} // namespace Ipopt
