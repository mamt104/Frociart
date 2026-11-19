#!/usr/bin/env bash
# Linux CI toolchain. Exact official archives, verified before extraction.
set -euo pipefail
sdk="${1:?Usage: bash tools/ci/install_android_sdk.sh SDK_DIRECTORY}"
mkdir -p "$sdk"
sdk="$(cd "$sdk" && pwd)"
download="$(mktemp -d)"
trap 'rm -rf "$download"' EXIT
curl --fail --silent --show-error --location --retry 3 --max-time 300 \
  https://dl.google.com/android/repository/platform-35_r02.zip -o "$download/platform.zip"
curl --fail --silent --show-error --location --retry 3 --max-time 300 \
  https://dl.google.com/android/repository/build-tools_r35_linux.zip -o "$download/build-tools.zip"
(
  cd "$download"
  printf '%s\n' \
    '0988cacad01b38a18a47bac14a0695f246bc76c1b06c0eeb8eb0dc825ab0c8e0  platform.zip' \
    'bd3a4966912eb8b30ed0d00b0cda6b6543b949d5ffe00bea54c04c81e1561d88  build-tools.zip' \
    | sha256sum --check --strict
  unzip -q platform.zip -d platform
  unzip -q build-tools.zip -d tools
)
mkdir -p "$sdk/platforms" "$sdk/build-tools"
# Use a new SDK directory: never replace an existing developer installation.
test ! -e "$sdk/platforms/android-35"
test ! -e "$sdk/build-tools/35.0.0"
mv "$download/platform/android-35" "$sdk/platforms/android-35"
mv "$download/tools/android-15" "$sdk/build-tools/35.0.0"
