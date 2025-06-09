#!/bin/bash
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"
dotnet restore Orion.sln
dotnet build Orion.sln -c Release

