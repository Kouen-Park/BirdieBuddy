#!/usr/bin/env python3
"""Scrape course, tee, marker, and hole data from golf.co.nz.

Golf NZ's GetHoles endpoint returns course selectors (MN/MY/WN/WY), not
individual holes. Individual holes are returned by GetMarkers after selecting
the course, gender, and nine-hole flag.
"""

from __future__ import annotations

import argparse
import json
import ssl
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

try:
    import certifi
except ImportError:  # pragma: no cover - most system Python installs have a CA store
    certifi = None


BASE_URL = "https://www.golf.co.nz/api/clubs"
DEFAULT_OUTPUT = Path(__file__).with_name("golf_nz_courses.json")


class GolfNzError(Exception):
    """An expected Golf NZ API or response-shape failure."""


@dataclass
class Skip:
    club_id: int
    club_name: str
    reason: str
    category: str


class ApiClient:
    def __init__(self, retries: int = 3, delay: float = 0.4, timeout: int = 30):
        self.retries = retries
        self.delay = delay
        self.timeout = timeout
        self.ssl_context = (
            ssl.create_default_context(cafile=certifi.where())
            if certifi is not None
            else ssl.create_default_context()
        )

    def get(self, endpoint: str, **params: Any) -> Any:
        query = urlencode({key: str(value).lower() if isinstance(value, bool) else value
                            for key, value in params.items() if value is not None})
        url = f"{BASE_URL}/{endpoint}" + (f"?{query}" if query else "")
        last_error: Exception | None = None
        for attempt in range(self.retries):
            try:
                request = Request(url, headers={"Accept": "application/json", "User-Agent": "BirdieBuddy Golf NZ scraper"})
                with urlopen(request, timeout=self.timeout, context=self.ssl_context) as response:
                    if response.status < 200 or response.status >= 300:
                        raise GolfNzError(f"HTTP {response.status}")
                    return json.load(response)
            except HTTPError as error:
                last_error = GolfNzError(f"HTTP {error.code}")
                if error.code < 500 and error.code != 429:
                    break
            except (URLError, TimeoutError, json.JSONDecodeError) as error:
                last_error = error
            if attempt + 1 < self.retries:
                time.sleep(self.delay * (attempt + 1))
        raise GolfNzError(f"{url}: {last_error}") from last_error


def get_clubs(client: ApiClient) -> list[dict[str, Any]]:
    """Get NZ club IDs. Golf NZ's broad search includes overseas clubs."""
    clubs = client.get("GetClubsByName", name="Golf")
    if not isinstance(clubs, list):
        raise GolfNzError("GetClubsByName returned a non-list response")
    # Current Golf NZ club IDs below 1000 are New Zealand clubs; larger IDs
    # in this endpoint are overseas search results.
    result = [
        {"club_id": int(club["ClubId"]), "club_name": str(club["ClubName"]).strip()}
        for club in clubs
        if isinstance(club, dict)
        and isinstance(club.get("ClubId"), int)
        and 100 <= club["ClubId"] < 1000
        and str(club.get("ClubName", "")).strip()
    ]
    return sorted({club["club_id"]: club for club in result}.values(), key=lambda club: club["club_id"])


def _require_list(value: Any, description: str) -> list[dict[str, Any]]:
    if not isinstance(value, list):
        raise GolfNzError(f"{description} returned a non-list response")
    if any(not isinstance(item, dict) for item in value):
        raise GolfNzError(f"{description} contained a non-object record")
    return value


def _number(value: Any, default: int | None = None) -> int | None:
    return value if isinstance(value, (int, float)) and not isinstance(value, bool) else default


def scrape_club(client: ApiClient, club: dict[str, Any]) -> tuple[dict[str, Any] | None, str | None]:
    club_id = club["club_id"]
    club_name = club["club_name"]
    courses = _require_list(client.get("getCourses", clubId=club_id), "GetCourses")
    if not courses:
        return None, "no course records returned"

    selectors = _require_list(client.get("GetHoles", clubId=club_id), "GetHoles")
    selectors = [selector for selector in selectors if selector.get("HoleValue") in {"MN", "MY", "WN", "WY"}]
    if not selectors:
        return None, "no MN/MY/WN/WY course selectors returned"

    output_courses: list[dict[str, Any]] = []
    for selector in selectors:
        course_type = selector["HoleValue"]
        course_id = courses[0].get("CourseId")
        if not isinstance(course_id, int) or course_id <= 0:
            return None, "course record has no usable CourseId"
        gender = "M" if course_type[0] == "M" else "F"
        nine_holes = course_type[1] == "Y"
        markers = _require_list(
            client.get(
                "getMarkers",
                courseId=course_id,
                gender=gender,
                isNineHoles=nine_holes,
                memberUid="",
            ),
            f"GetMarkers {course_type}",
        )
        output_markers: list[dict[str, Any]] = []
        seen_marker_names: set[str] = set()
        for marker in markers:
            name = str(marker.get("DisplayMarkerName") or marker.get("MarkerName") or "").strip()
            if not name or name.casefold() in seen_marker_names:
                continue
            seen_marker_names.add(name.casefold())
            raw_holes = marker.get("Holes")
            if not isinstance(raw_holes, list):
                raise GolfNzError(f"GetMarkers {course_type} marker {name} has no hole list")
            holes: list[dict[str, Any]] = []
            for number, raw_hole in enumerate(raw_holes, start=1):
                if not isinstance(raw_hole, dict):
                    raise GolfNzError(f"GetMarkers {course_type} marker {name} has malformed hole data")
                par = _number(raw_hole.get("Par"))
                distance = _number(raw_hole.get("DistanceMetres"))
                stroke = _number(raw_hole.get("Stroke"))
                if par is None or distance is None or stroke is None:
                    raise GolfNzError(f"GetMarkers {course_type} marker {name} has incomplete hole {number}")
                holes.append({
                    "number": number,
                    "par": int(par),
                    "stroke_index": int(stroke),
                    "distance_metres": int(distance),
                    "distance_yards": _number(raw_hole.get("DistanceYards")),
                })
            expected = 9 if nine_holes else 18
            if len(holes) != expected:
                print(
                    f"WARNING club {club_id} {club_name}: {course_type}/{name} has "
                    f"{len(holes)} holes; expected {expected}",
                    file=sys.stderr,
                )
            output_markers.append({
                "name": name,
                "rating": marker.get("UsgaNzcr"),
                "slope": marker.get("SlopeRating"),
                "colour": marker.get("MarkerColor"),
                "total_par": marker.get("TotalPar"),
                "front_nine_par": marker.get("FrontNinePar"),
                "back_nine_par": marker.get("BackNinePar"),
                "front_nine_metres": marker.get("FrontNineDistanceMetres"),
                "back_nine_metres": marker.get("BackNineDistanceMetres"),
                "holes": holes,
            })
        if output_markers:
            output_courses.append({
                "type": course_type,
                "gender": gender,
                "nine_holes": nine_holes,
                "markers": output_markers,
            })
    if not output_courses:
        return None, "course selectors returned no usable markers"
    return {"club_id": club_id, "club_name": club_name, "courses": output_courses}, None


def scrape(clubs: list[dict[str, Any]], client: ApiClient) -> tuple[list[dict[str, Any]], list[Skip]]:
    successful: list[dict[str, Any]] = []
    skipped: list[Skip] = []
    for club in clubs:
        try:
            result, reason = scrape_club(client, club)
            if result is None:
                skipped.append(Skip(club["club_id"], club["club_name"], reason or "unknown", "no-data"))
            else:
                successful.append(result)
        except GolfNzError as error:
            category = "network/API" if str(error).startswith("http") or "HTTP" in str(error) else "malformed-data"
            skipped.append(Skip(club["club_id"], club["club_name"], str(error), category))
    return successful, skipped


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--club-id", type=int, help="Scrape one club instead of the full NZ list")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    client = ApiClient()
    clubs = get_clubs(client)
    if args.club_id is not None:
        clubs = [club for club in clubs if club["club_id"] == args.club_id]
        if not clubs:
            print(f"Club ID {args.club_id} was not found in the complete Golf NZ club list", file=sys.stderr)
            return 2
    successful, skipped = scrape(clubs, client)
    args.output.write_text(json.dumps(successful, indent=2) + "\n", encoding="utf-8")
    print(f"Total clubs: {len(clubs)}")
    print(f"Successfully scraped: {len(successful)}")
    print(f"No course/hole data available: {sum(item.category == 'no-data' for item in skipped)}")
    print(f"Failed to scrape: {sum(item.category != 'no-data' for item in skipped)}")
    for item in skipped:
        print(f"Skipped/failed club {item.club_id} - {item.club_name}: [{item.category}] {item.reason}")
    # Clubs with no published course data are expected in Golf NZ's directory;
    # only API/network or malformed-data failures make the scrape unsuccessful.
    return 0 if all(item.category == "no-data" for item in skipped) else 1


if __name__ == "__main__":
    raise SystemExit(main())
