#!/usr/bin/env bash
set -euo pipefail

if ! xcodebuild -version >/dev/null 2>&1; then
  echo "Full Xcode is required to run the iOS test suite." >&2
  exit 1
fi

simulator_id="${BIRDIEBUDDY_IOS_SIMULATOR_ID:-}"
if [[ -z "$simulator_id" ]]; then
  simulator_id="$({ xcrun simctl list devices available -j || exit 1; } | ruby -rjson -e '
    runtimes = JSON.parse(STDIN.read).fetch("devices")
    candidates = runtimes.flat_map do |runtime, devices|
      next [] unless runtime.include?(".iOS-")
      version = runtime.split(".iOS-").last.split("-").map(&:to_i)
      devices.filter_map do |device|
        next unless device["isAvailable"] && device["name"].start_with?("iPhone")
        [version, device["name"], device["udid"]]
      end
    end
    abort "No available iPhone Simulator is installed." if candidates.empty?
    puts candidates.max_by { |version, name, _| [version, name] }.last
  ')"
fi

echo "Using iOS Simulator: $simulator_id"
xcodebuild test \
  -project ios/BirdieBuddyApp.xcodeproj \
  -scheme BirdieBuddyApp \
  -destination "platform=iOS Simulator,id=$simulator_id" \
  CODE_SIGNING_ALLOWED=NO
