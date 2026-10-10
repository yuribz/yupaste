#!/usr/bin/env bash
# Builds yupaste as a self-contained, single-file Linux binary.
# Usage: ./build.sh [runtime] [--install]
#   runtime    RID to publish for (default: linux-x64)
#   --install  also copy the binary to ~/.local/bin so `yupaste` is on PATH
set -euo pipefail

cd "$(dirname "$0")"
PROJECT_ROOT="$(pwd)"

RUNTIME="linux-x64"
INSTALL=false

for arg in "$@"; do
  case "$arg" in
    --install) INSTALL=true ;;
    *) RUNTIME="$arg" ;;
  esac
done

OUT_DIR="$PROJECT_ROOT/publish"

cd yupaste
dotnet publish -c Release -r "$RUNTIME" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o "$OUT_DIR"

echo
echo "Built: $OUT_DIR/yupaste"

if [ "$INSTALL" = true ]; then
  BIN_DIR="$HOME/.local/bin"
  mkdir -p "$BIN_DIR"
  cp "$OUT_DIR/yupaste" "$BIN_DIR/yupaste"
  chmod +x "$BIN_DIR/yupaste"
  echo "Installed: $BIN_DIR/yupaste"

  case ":$PATH:" in
    *":$BIN_DIR:"*)
      echo "Run it with: yupaste --port 5005"
      ;;
    *)
      echo
      echo "$BIN_DIR is not on your PATH. Add this to ~/.bashrc (or ~/.zshrc) and restart your shell:"
      echo "  export PATH=\"\$HOME/.local/bin:\$PATH\""
      ;;
  esac
else
  echo "Run it with: $OUT_DIR/yupaste --port 5005"
  echo "(or re-run with --install to put it on your PATH as 'yupaste')"
fi
