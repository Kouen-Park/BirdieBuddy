# Birdie Buddy

Golf round tracking and performance analysis. ASP.NET Core Web API + EF Core +
SQL Server backend, vanilla HTML/CSS/JS + Chart.js frontend served from `wwwroot`.

> **Note on this build**: this project was generated in a sandbox without the
> .NET SDK installed, so it has **not** been compiled or run here. The code
> follows standard ASP.NET Core 8 / EF Core 8 patterns throughout, but you
> should treat the first `dotnet build` on your machine as the real test —
> see "Remaining issues" at the bottom for the handful of things most likely
> to need a small fix.

## 1. Project structure

```
BirdieBuddy/
├── Controllers/        CoursesController, RoundsController, StatisticsController
├── Models/              Course, CourseHole, Round, Hole
├── Data/                ApplicationDbContext, DbInitializer (seed data)
├── DTOs/                Request/response shapes - entities are never returned directly
├── Services/            ICourseService/CourseService, IRoundService/RoundService,
│                        IStatisticsService/StatisticsService - all business logic lives here
├── wwwroot/
│   ├── css/styles.css   Shared design system (fairway-green / scorecard theme)
│   ├── js/              api.js (fetch wrapper) + one script per page
│   ├── index.html       Dashboard
│   ├── rounds.html      Rounds list
│   ├── add-round.html   Scorecard entry
│   ├── round-details.html
│   ├── statistics.html
│   └── practice.html    Placeholder
├── Program.cs
├── appsettings.json
└── BirdieBuddy.csproj
```

## 2. Database schema

```
Course (1) ──< CourseHole (many)      Cascade delete
Course (1) ──< Round (many)           Restrict delete (a course with rounds can't be deleted)
Round  (1) ──< Hole (many)            Cascade delete
```

- `CourseHole`: unique index on `(CourseId, HoleNumber)`, check constraint `HoleNumber BETWEEN 1 AND 18`.
- `Hole`: unique index on `(RoundId, HoleNumber)`, check constraints on `HoleNumber`, `Score > 0`, `Putts >= 0`, `Penalty >= 0`.
- `Hole.Par` is a **snapshot** of `CourseHole.Par`, copied by the service layer when a round is
  created. This is a deliberate design choice: if it instead read `CourseHole.Par` live, editing
  a course's par later would silently change the "score to par" of rounds already played. The
  trade-off is a few extra bytes per hole row for statistics that stay stable over time.
- `Course → Round` is `Restrict` rather than `Cascade` specifically so it can't collide with the
  `Round → Hole` cascade path (SQL Server rejects a migration with multiple cascade paths to the
  same table). Deleting a course with recorded rounds returns `409 Conflict` from the API instead.

## 3. Configure SQL Server

Default connection string (in `appsettings.json`) targets LocalDB, which ships with Visual
Studio / the SQL Server Express tooling on Windows:

```
Server=(localdb)\mssqllocaldb;Database=BirdieBuddyDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

- **Windows + LocalDB**: no setup needed, this works out of the box.
- **macOS/Linux, or a real SQL Server/Azure SQL instance**: replace the connection string, e.g.
  `Server=localhost,1433;Database=BirdieBuddyDb;User Id=sa;Password=YourPassword;TrustServerCertificate=True`.
  You can run SQL Server in Docker: `docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourPassword" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest`.
- For local dev, prefer `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."` over
  editing `appsettings.json`, so credentials don't end up in source control.

## 4. Migration commands

No migration has been generated yet (no SDK in the sandbox this was built in). From the project folder:

```bash
dotnet tool install --global dotnet-ef   # once, if not already installed
dotnet ef migrations add InitialCreate
dotnet ef database update                 # optional - Program.cs also calls Database.Migrate() on startup
```

## 5. How to run the application

```bash
cd BirdieBuddy
dotnet restore
dotnet ef migrations add InitialCreate     # first time only
dotnet run
```

Then open the URL shown in the console (typically `https://localhost:5001` or similar) - the
frontend is served from `wwwroot` at the app root, and `/swagger` gives you the API explorer in
development mode. On startup the app applies pending migrations and, if the database is empty,
seeds two courses and five sample rounds automatically (see `Data/DbInitializer.cs`).

## 6. API endpoint list

```
GET    /api/courses
GET    /api/courses/{id}
POST   /api/courses
PUT    /api/courses/{id}
DELETE /api/courses/{id}          409 if the course has recorded rounds

GET    /api/rounds
GET    /api/rounds/{id}
POST   /api/rounds                 body includes CourseId, Date, Tee, and all Holes at once
PUT    /api/rounds/{id}            updates Date/Tee only
DELETE /api/rounds/{id}

GET    /api/rounds/{roundId}/holes
POST   /api/rounds/{roundId}/holes
PUT    /api/rounds/{roundId}/holes/{holeId}

GET    /api/statistics/round/{roundId}
GET    /api/statistics/overview
```

## 7. Golf New Zealand course database

The application no longer calls an external golf-course API. It imports the bundled
`scripts/golf_nz_courses.json` file through `IGolfNzCourseImporter`. The file is copied into the
published application. The application applies EF Core migrations at startup, while the large
course import is triggered manually from the Courses page through **Load Golf NZ courses** at
`POST /api/courses/import-golf-nz`. This keeps the Render health check responsive and allows the
import to be run again safely.

Import is idempotent. A course is matched by `GolfNzClubId`; when that identifier is not yet stored,
the importer falls back to a normalized case-insensitive club name. This fallback links the existing
**Whitford Park Golf Club** record to Golf NZ club `491` instead of inserting a duplicate. Tees are
matched by course type, gender, nine-hole flag, and case-insensitive tee name. Holes are matched by
tee and hole number, then updated in place.

The source contains multiple course variants and repeated marker records. The importer merges
repeated records rather than creating duplicates and keeps the first valid record for a repeated hole
number. It also accepts partial scorecards because Golf NZ contains 9-hole and other non-standard
layouts.

## 8. CourseTee and round data

Course data is modeled as `Course → CourseTee → CourseHole`. Each tee stores its Golf NZ course type,
gender, nine-hole flag, rating, slope, colour, par totals, distances, and hole definitions. A round
stores a nullable `CourseTeeId`; `LegacyTee` preserves the old free-text tee label for existing rounds
that cannot be resolved during migration.

When recording a round, the user selects one of the imported tees or enters a custom tee name. If the
selected tee has no hole definitions, Birdie Buddy creates a manual scorecard and the user supplies
the par for each entered hole. Existing rounds and score snapshots remain intact.

## 9. Database migration and deployment

The `AddGolfNzCourseData` migration creates `CourseTees`, moves existing `CourseHoles` under a
legacy tee, copies the former `Rounds.Tee` value to `LegacyTee`, and adds the Golf NZ club identifier.
It is designed to preserve recorded rounds. Run `dotnet ef database update` in an environment with
the configured PostgreSQL connection, or let the application apply pending migrations on startup as
configured in `Program.cs`. Golf NZ data loading is intentionally manual after deployment so the
web process can start before the large JSON import begins.

The JSON file must remain at `scripts/golf_nz_courses.json` in the source tree. The project file
marks it for both output and publish, so Render's Docker build includes it in the application image.
No external API key is required.

## 10. Remaining issues / things to double-check

- **No authentication**: every round currently belongs to the shared application rather than a user.
  Adding authentication later means adding a `UserId` to `Round` and a migration.
- **Partial layouts**: Golf NZ contains 9-hole and other partial layouts. The UI displays the holes
  present for the selected tee; for a tee without hole data, it creates a manual scorecard.
- **GIR% denominator**: statistics use the number of recorded holes as the denominator, while fairway
  percentage only considers holes where fairway data is applicable.
