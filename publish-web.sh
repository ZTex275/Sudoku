#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"

dotnet publish src/Sudoku.Web/Sudoku.Web.csproj -c Release -p:GitHubPages=true

rm -rf docs
mkdir -p docs
cp -a src/Sudoku.Web/bin/Release/net8.0/publish/wwwroot/. docs/
sed -i 's|<base href="/" />|<base href="/Sudoku/" />|' docs/index.html
cp docs/index.html docs/404.html
touch docs/.nojekyll

echo "Готово: docs/ обновлена для GitHub Pages"
