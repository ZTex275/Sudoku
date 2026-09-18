#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"

dotnet publish src/Sudoku.Maui/Sudoku.Maui.csproj \
  -c Release \
  -f net8.0-android \
  -p:AndroidPackageFormat=apk \
  -p:AndroidKeyStore=false

mkdir -p artifacts
apk="$(find src/Sudoku.Maui/bin/Release/net8.0-android -name '*.apk' ! -name '*-unsigned.apk' | sort | tail -n 1)"
if [ -z "$apk" ]; then
  echo "APK не найден после сборки" >&2
  find src/Sudoku.Maui/bin/Release -name '*.apk' >&2 || true
  exit 1
fi

cp -f "$apk" artifacts/sudoku.apk
echo "Готово: $apk -> artifacts/sudoku.apk"
