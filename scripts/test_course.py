#!/usr/bin/env python3
"""Focused regression test for Golf NZ club 515."""

from __future__ import annotations

from scrape_golfnz import ApiClient, scrape_club


def main() -> int:
    club = {"club_id": 515, "club_name": "Maramarua Golf Club"}
    result, reason = scrape_club(ApiClient(), club)
    if result is None:
        raise AssertionError(f"club 515 was not scraped: {reason}")
    assert result["club_id"] == 515
    assert {course["type"] for course in result["courses"]} == {"MN", "MY", "WN", "WY"}
    for course in result["courses"]:
        expected = 9 if course["nine_holes"] else 18
        assert course["markers"], f"{course['type']} has no markers"
        for marker in course["markers"]:
            assert len(marker["holes"]) == expected
            assert [hole["number"] for hole in marker["holes"]] == list(range(1, expected + 1))
            assert all(hole["par"] > 0 and hole["distance_metres"] > 0 for hole in marker["holes"])
    print("club 515: PASS (MN/MY/WN/WY, markers, and complete hole numbering validated)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
