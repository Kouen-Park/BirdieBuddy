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
      devices.each_with_object([]) do |device, available|
        next unless device["isAvailable"] && device["name"].start_with?("iPhone")
        model_priority = case device["name"]
                         when /^iPhone \d+$/ then 3
                         when /^iPhone \d+e$/ then 2
                         when /Pro/ then 1
                         else 0
                         end
        available << [version, model_priority, device["name"], device["udid"]]
      end
    end
    abort "No available iPhone Simulator is installed." if candidates.empty?
    puts candidates.max_by { |version, priority, name, _| [version, priority, name] }.last
  ')"
fi

echo "Using iOS Simulator: $simulator_id"

# Start from a known simulator state. GitHub-hosted runners can report an
# available device before XCTest's runner services are ready to accept work.
xcrun simctl shutdown "$simulator_id" >/dev/null 2>&1 || true
xcrun simctl boot "$simulator_id"
xcrun simctl bootstatus "$simulator_id" -b

artifact_directory="${BIRDIEBUDDY_IOS_ARTIFACT_DIRECTORY:-$PWD/output/ios}"
run_identifier="${GITHUB_RUN_ID:-local}-${GITHUB_RUN_ATTEMPT:-1}-${BASHPID:-$$}"
derived_data_path="${RUNNER_TEMP:-${TMPDIR:-/tmp}}/birdiebuddy-derived-data-$run_identifier"
result_bundle_path="$artifact_directory/BirdieBuddyTests-$run_identifier.xcresult"
mkdir -p "$artifact_directory"

xcodebuild_arguments=(
  -project ios/BirdieBuddyApp.xcodeproj
  -scheme BirdieBuddyApp
  -destination "platform=iOS Simulator,id=$simulator_id"
  -derivedDataPath "$derived_data_path"
  -parallel-testing-enabled NO
  -maximum-concurrent-test-simulator-destinations 1
)

# Building separately prevents XCTest from trying to compile and materialize
# simulator workers at the same time, which is unreliable on hosted runners.
xcodebuild build-for-testing "${xcodebuild_arguments[@]}"
xcodebuild test-without-building \
  "${xcodebuild_arguments[@]}" \
  -resultBundlePath "$result_bundle_path"
