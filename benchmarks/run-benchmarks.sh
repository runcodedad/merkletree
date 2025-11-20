#!/bin/bash

# MerkleTree Benchmarks Runner Script
# This script provides convenient ways to run various benchmark scenarios

set -e

cd "$(dirname "$0")/MerkleTree.Benchmarks"

echo "MerkleTree Performance Benchmarks"
echo "=================================="
echo ""

# Parse arguments
case "${1:-all}" in
  all)
    echo "Running ALL benchmarks..."
    dotnet run -c Release
    ;;
    
  tree)
    echo "Running tree building benchmarks..."
    dotnet run -c Release -- --filter *TreeBuilding*
    ;;
    
  proof)
    echo "Running proof generation and verification benchmarks..."
    dotnet run -c Release -- --filter *Proof*
    ;;
    
  cache)
    echo "Running cache performance benchmarks..."
    dotnet run -c Release -- --filter *Cache*
    ;;
    
  serialize)
    echo "Running serialization benchmarks..."
    dotnet run -c Release -- --filter *Serialization*
    ;;
    
  fast)
    echo "Running fast benchmarks only (small datasets)..."
    dotnet run -c Release -- --filter *Small*
    ;;
    
  streaming)
    echo "Running streaming tree benchmarks..."
    dotnet run -c Release -- --filter *Streaming*
    ;;
    
  memory)
    echo "Running benchmarks with memory profiling..."
    dotnet run -c Release -- --memory
    ;;
    
  *)
    echo "Usage: $0 [all|tree|proof|cache|serialize|fast|streaming|memory]"
    echo ""
    echo "Options:"
    echo "  all        - Run all benchmarks (default)"
    echo "  tree       - Run tree building benchmarks"
    echo "  proof      - Run proof generation and verification benchmarks"
    echo "  cache      - Run cache performance benchmarks"
    echo "  serialize  - Run serialization/deserialization benchmarks"
    echo "  fast       - Run only fast benchmarks (small datasets)"
    echo "  streaming  - Run streaming tree benchmarks"
    echo "  memory     - Run with memory profiling enabled"
    exit 1
    ;;
esac

echo ""
echo "Benchmarks complete! Results are in BenchmarkDotNet.Artifacts/"
