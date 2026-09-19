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
boot_simulator() {
  xcrun simctl shutdown "$simulator_id" >/dev/null 2>&1 || true
  xcrun simctl boot "$simulator_id"
  xcrun simctl bootstatus "$simulator_id" -b
}

boot_simulator

artifact_directory="${BIRDIEBUDDY_IOS_ARTIFACT_DIRECTORY:-$PWD/output/ios}"
run_identifier="${GITHUB_RUN_ID:-local}-${GITHUB_RUN_ATTEMPT:-1}-${BASHPID:-$$}"
derived_data_path="${RUNNER_TEMP:-${TMPDIR:-/tmp}}/birdiebuddy-derived-data-$run_identifier"
mkdir -p "$artifact_directory"

xcodebuild_arguments=(
  -project ios/BirdieBuddyApp.xcodeproj
  -scheme BirdieBuddyApp
  -destination "platform=iOS Simulator,id=$simulator_id"
  -derivedDataPath "$derived_data_path"
  -parallel-testing-enabled NO
  -maximum-concurrent-test-simulator-destinations 1
)

# Retry only the failures that come from the runner, never a failing test.
#
# xcodebuild reports a build error or a failed test as 65. Anything in the set
# below is an environment-class failure — the simulator or the XCTest daemon,
# not this code — and is the flake this script already warns about above. A
# retry on 65 would hide a real regression, so 65 exits immediately.
#
#   69 EX_UNAVAILABLE  a service (usually CoreSimulator) was not available
#   70 EX_SOFTWARE     internal xcodebuild / test-daemon error
#   74 EX_IOERR        I/O failure talking to the simulator
retryable_status() {
  case "$1" in
    69 | 70 | 74) return 0 ;;
    *) return 1 ;;
  esac
}

max_attempts="${BIRDIEBUDDY_IOS_TEST_ATTEMPTS:-2}"
attempt=1

while true; do
  # A fresh bundle path per attempt: xcodebuild refuses to overwrite one.
  result_bundle_path="$artifact_directory/BirdieBuddyTests-$run_identifier-attempt$attempt.xcresult"

  # Keep build and test in one XCTest invocation. Splitting these phases caused
  # Xcode 26 to inject libXCTestBundleInject twice into the hosted app, which
  # traps before the test daemon can establish its connection.
  set +e
  xcodebuild test \
    "${xcodebuild_arguments[@]}" \
    -resultBundlePath "$result_bundle_path"
  status=$?
  set -e

  if [[ $status -eq 0 ]]; then
    if [[ $attempt -gt 1 ]]; then
      echo "iOS tests passed on attempt $attempt after a runner-class failure."
    fi
    break
  fi

  if ! retryable_status "$status" || [[ $attempt -ge $max_attempts ]]; then
    echo "xcodebuild test failed with status $status." >&2
    exit "$status"
  fi

  # Surface the flake instead of hiding it: a run that needed a retry should be
  # visible in the CI log even though the job goes green.
  echo "::warning::xcodebuild exited $status (runner/simulator class, not a test failure). Rebooting the simulator and retrying: attempt $((attempt + 1)) of $max_attempts."
  boot_simulator
  attempt=$((attempt + 1))
done
