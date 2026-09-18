#!/usr/bin/env python3
"""Print the raw Golf NZ responses used for one club's course data."""

from __future__ import annotations

import argparse
import json

from scrape_golfnz import ApiClient


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("club_id", type=int)
    args = parser.parse_args()
    api = ApiClient()
    courses = api.get("getCourses", clubId=args.club_id)
    holes = api.get("GetHoles", clubId=args.club_id)
    print("GetCourses:")
    print(json.dumps(courses, indent=2))
    print("\nGetHoles (selectors, not individual holes):")
    print(json.dumps(holes, indent=2))
    if courses:
        course_id = courses[0]["CourseId"]
        for selector in holes:
            value = selector.get("HoleValue")
            if value not in {"MN", "MY", "WN", "WY"}:
                continue
            print(f"\nGetMarkers courseId={course_id} type={value}:")
            print(json.dumps(api.get(
                "getMarkers",
                courseId=course_id,
                gender="M" if value[0] == "M" else "F",
                isNineHoles=value[1] == "Y",
                memberUid="",
            ), indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
