#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
dotnet run --project src/Sudoku.Web --urls "http://0.0.0.0:5088"
