#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"

keystore="${ANDROID_KEYSTORE_PATH:-$PWD/signing/sudoku.keystore}"
alias_name="${ANDROID_KEY_ALIAS:-sudoku}"
store_pass="${ANDROID_KEYSTORE_PASS:-sudoku-release}"
key_pass="${ANDROID_KEY_PASS:-$store_pass}"

if [ ! -f "$keystore" ]; then
  echo "Нет keystore: $keystore" >&2
  exit 1
fi

dotnet publish src/Sudoku.Maui/Sudoku.Maui.csproj \
  -c Release \
  -f net8.0-android \
  -p:AndroidPackageFormat=apk \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore="$keystore" \
  -p:AndroidSigningKeyAlias="$alias_name" \
  -p:AndroidSigningStorePass="$store_pass" \
  -p:AndroidSigningKeyPass="$key_pass"

mkdir -p artifacts
apk="$(find src/Sudoku.Maui/bin/Release/net8.0-android -name '*-Signed.apk' | sort | tail -n 1)"
if [ -z "$apk" ]; then
  apk="$(find src/Sudoku.Maui/bin/Release/net8.0-android -name '*.apk' ! -iname '*unsigned*' | sort | tail -n 1)"
fi
if [ -z "$apk" ]; then
  echo "APK не найден после сборки" >&2
  find src/Sudoku.Maui/bin/Release -name '*.apk' >&2 || true
  exit 1
fi

cp -f "$apk" artifacts/sudoku.apk
echo "Готово: $apk -> artifacts/sudoku.apk"
