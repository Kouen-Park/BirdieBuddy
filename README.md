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

## 7. Test data

`DbInitializer` seeds automatically on first run (only when the `Courses` table is empty):

- **Fairway Ridge Golf Club** (Hamilton, NZ) - par 72, parkland-style layout
- **Coastal Dunes Links** (Tauranga, NZ) - par 71, links-style layout with more par 3s
- 5 sample rounds across both courses with varying skill levels (deterministic random seeds, so
  the same data appears every time the database is recreated), enough to populate the dashboard
  charts and trend lines immediately.

This is clearly separated from real user data only by timing (it runs once, on an empty
database) - there's no `IsSeed` flag in the schema. That's fine for a student project; call it
out as a known simplification if asked.

## 8. Remaining issues / things to double-check on first build

- **Not compiled**: this sandbox has no .NET SDK, so run `dotnet build` first and expect to fix
  minor issues (a missing `using`, a package version mismatch) rather than assuming it's perfect.
- **`HasCheckConstraint` on `ToTable`**: this uses the EF Core 7+ syntax. If your installed
  `Microsoft.EntityFrameworkCore.SqlServer` version is older, this call signature will differ.
- **Tee names**: `Tee` is a free-text string on `Round`, not its own entity. The Add Round page
  offers a fixed dropdown (White/Blue/Black/Gold/Red) as a reasonable default - adjust if your
  courses use different tee naming.
- **No authentication**: as specified, every round belongs to nobody in particular. Adding auth
  later means adding a `UserId` to `Round` and a migration, not a redesign.
- **GIR% denominator**: per spec this divides by 18 (or however many holes are recorded), not by
  "holes where GIR is possible" - unlike fairway%, GIR applies to every hole so this is correct
  as specified, just worth knowing it's not filtered the way fairway% is.
