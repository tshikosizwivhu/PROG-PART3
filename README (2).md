# RaceDay

**PROG6212 Portfolio of Evidence** | Student: Zwivhuya Tshikosi | Student number: ST10492177

RaceDay is an event management platform for runs, walks and cycle tours. Organisers create events and capture results. Participants browse events, enrol and follow their race history.

| Part | Folder | What it is |
|---|---|---|
| Part 2 | `Part2-API` | RESTful API (ASP.NET Core, EF Core, SQL Server, session authentication) |
| **Part 3** | **`Part3-MVC`** | **ASP.NET Core MVC web app that consumes the API, with Azure Blob Storage and Docker** |

## Demo video

YouTube (unlisted): **PASTE YOUR YOUTUBE LINK HERE**

The video demonstrates the running application, the MVC structure, Azure Blob Storage integration and the Docker setup.

## CI/CD: successful green build

The GitHub Actions workflow (`.github/workflows/ci.yml`) runs on every push and pull request. It:

1. builds the Part 2 API and runs its unit tests,
2. builds the Part 3 API and MVC project and runs all unit tests,
3. builds the Docker images for the API and the MVC application.

![Green build](docs/ci-green-build.png)

---

# Part 3: RaceDay MVC web application

## Contents

1. [Architecture](#architecture)
2. [Prerequisites](#prerequisites)
3. [Run with Docker (single command)](#run-with-docker-single-command)
4. [Run in Visual Studio](#run-in-visual-studio)
5. [Azure Blob Storage setup](#azure-blob-storage-setup)
6. [Demo accounts](#demo-accounts)
7. [The two roles and their pages](#the-two-roles-and-their-pages)
8. [Authentication and role-based navigation](#authentication-and-role-based-navigation)
9. [Project structure](#project-structure)
10. [Unit tests](#unit-tests)
11. [Troubleshooting](#troubleshooting)

## Architecture

```
Browser --> RaceDay.Web (MVC, port 5090) --> RaceDay.Api (port 5081 in Docker, 5080 locally) --> SQL Server
                                                   |
                                                   +--> Azure Blob Storage (images)
```

- **RaceDay.Web** (MVC) contains no database access and no business logic. Every data operation is an HTTP call to the API through one class, `Services/ApiClient.cs`.
- **RaceDay.Api** is the Part 2 API, extended with Azure Blob Storage for event banners and profile pictures.
- The MVC application never talks to Azure directly. It sends the file to the API, the API stores it in Azure Blob Storage and saves the image URL in the database, and the MVC pages display that URL.
- **RaceDay.Tests** holds the xUnit tests for the API and the MVC project.

## Prerequisites

| To run with | You need |
|---|---|
| Docker | Docker Desktop (with WSL 2 on Windows), at least 4 GB of memory available to Docker |
| Visual Studio | Visual Studio 2022 with the **ASP.NET and web development** and **Data storage and processing** workloads (gives LocalDB), and the .NET 8 SDK |
| Image uploads (both) | A free Azure Storage account (see [Azure Blob Storage setup](#azure-blob-storage-setup)) |

## Run with Docker (single command)

Everything (SQL Server, the API and the MVC website) starts from one command.

1. Open a terminal in the `Part3-MVC` folder.

2. Create your settings file by copying the example:

   Windows (Command Prompt):
   ```
   copy .env.example .env
   ```
   macOS / Linux:
   ```
   cp .env.example .env
   ```

3. Open `.env` and replace the Azure placeholder with your storage account connection string (see [Azure Blob Storage setup](#azure-blob-storage-setup)). The site still starts without it, but image uploads then show a friendly "not configured" message.

4. Start the application:
   ```
   docker compose up --build
   ```

5. Wait for the logs to show the API and the web app listening (the first start takes a few minutes while images build and SQL Server starts), then open:

   | What | Address |
   |---|---|
   | **RaceDay website** | http://localhost:5090 |
   | API Swagger page | http://localhost:5081/swagger |

   On first start the API creates the database tables and seeds the demo accounts and events automatically.

6. Stop the application with `Ctrl + C`, then run:
   ```
   docker compose down
   ```
   To also delete the database data and start from scratch, run `docker compose down -v`.

### Run only the MVC container

If the API is already running on your computer (for example from Visual Studio on port 5080), you can run just the MVC application in Docker:

```
docker build -t raceday-web ./RaceDay.Web
docker run --rm -p 5090:8080 -e ApiBaseUrl=http://host.docker.internal:5080 raceday-web
```

Then open http://localhost:5090. The containerised application behaves the same as the one running locally; only the `ApiBaseUrl` setting changes.

### Docker files

| File | Purpose |
|---|---|
| `RaceDay.Web/Dockerfile` | Multi-stage build for the MVC app: restore, publish with the .NET 8 SDK image, run on the smaller ASP.NET runtime image as a non-root user |
| `RaceDay.Api/Dockerfile` | Same structure for the API |
| `docker-compose.yml` | Runs SQL Server, the API and the MVC app together, with a SQL Server health check so the API starts only when the database is ready |
| `.env.example` | Template for secrets (Azure connection string, SQL password). Your real `.env` file is ignored by Git |

## Run in Visual Studio

1. Open `Part3-MVC/RaceDay.sln` in Visual Studio 2022.
2. Right-click the solution, choose **Configure Startup Projects**, select **Multiple startup projects**, set **RaceDay.Api** and **RaceDay.Web** to **Start** and leave **RaceDay.Tests** on **None**. Click **Apply**.
3. Use the **http** launch profile and press **F5**.
4. The API runs at http://localhost:5080 (Swagger at `/swagger`) and the website at **http://localhost:5090**. Both must stay running.
5. The API creates the `RaceDayApiDB` database on LocalDB and seeds demo data on first start.

To enable image uploads locally, right-click **RaceDay.Api**, choose **Manage User Secrets**, and add:

```json
{
  "AzureBlob": {
    "ConnectionString": "YOUR AZURE STORAGE CONNECTION STRING"
  }
}
```

### Run from the command line

In two separate terminals:

```
cd Part3-MVC/RaceDay.Api
dotnet run --launch-profile http
```
```
cd Part3-MVC/RaceDay.Web
dotnet run --launch-profile http
```

## Azure Blob Storage setup

1. In the Azure portal, create a **Storage account** (Standard performance, locally-redundant storage is fine).
2. Open the storage account, go to **Settings > Configuration**, and set **Allow Blob anonymous access** to **Enabled**. Save.
3. Go to **Security + networking > Access keys** and copy a **Connection string**.
4. Paste it into `.env` as `AZURE_STORAGE_CONNECTION_STRING` (Docker) or into the API's user secrets (Visual Studio).

How it works:

- The API creates a container named `raceday-images` on first upload (public read access for blobs).
- Event banners are stored under `banners/` and profile pictures under `profiles/`, each with a unique file name.
- The blob URL is saved in the database (`Events.BannerImageUrl` and `Participants.ProfilePictureUrl`) and shown on the event detail page, the event cards and the profile page.
- The API accepts JPG, PNG, WEBP and GIF images up to 5 MB and checks the file signature, not only the file name.
- If Azure is not configured or unavailable, the API returns a clear error and the website shows a friendly message instead of crashing.

## Demo accounts

All demo accounts use the password `Password123!`.

| Role | Email |
|---|---|
| Organiser | thandiwe.mokoena@raceday.co.za |
| Organiser | johan.vdm@raceday.co.za |
| Participant | sipho.ndlovu@example.com |
| Participant | emma.peters@example.com |

New organisers register with the invite code `RACEDAY-ORG-2026` (set in the API's `appsettings.json`).

## The two roles and their pages

### Organiser

Creates and manages events.

| Page | What it does |
|---|---|
| Dashboard | Summary of the organiser's events, total enrolments per event and upcoming event dates |
| Manage events | Create, edit and delete events (name, description, date, location, distance, type) and upload an event banner image |
| Categories | Add, edit and delete distance or age categories for an event |
| Enrolments | See every participant enrolled for an event, with their category and enrolment status |
| Results entry | Capture and publish finish times and finishing positions after the event has concluded |

### Participant

Takes part in events.

| Page | What it does |
|---|---|
| Home | Upcoming events with filtering by type and location |
| Event details | Full event information, route notes and available categories, with an **Enter event** button |
| My events | Every event the participant enrolled for, with colour-coded status, and the option to cancel |
| My results | Race history with event, date, category, finish time and position (for example "47th out of 312 finishers") |
| Profile | View and update personal information and upload a profile picture |

### Everyone

Visitors who are not logged in can browse and filter events, open event details, register and log in. Enrolment statuses use colours: green for Confirmed, amber for Pending and red for Cancelled.

## Authentication and role-based navigation

- **Register and login go through the API.** After login the API returns its session cookie. The MVC app keeps that cookie, plus the user's id, name and role, in the user's server-side MVC session and sends the cookie with every API call.
- **Protected pages** use the `[RequireRole]` filter (`Filters/RequireRoleAttribute.cs`). A visitor who is not logged in is redirected to the Login page. A logged-in user with the wrong role is redirected to an Access Denied page.
- **Different navigation per role:** `Views/Shared/_Layout.cshtml` shows one menu for visitors, another for Participants and another for Organisers.
- **Log out** ends both the API session and the MVC session and redirects to the Login page.
- **Expired sessions:** if the API session ends, the user is sent back to Login with a message.
- **Error handling:** invalid input shows validation messages, and API failures show friendly pages. Users never see raw error text.

## API endpoints (used by the MVC app)

| Method | Route | Access |
|---|---|---|
| POST | /api/auth/register/participant | Public |
| POST | /api/auth/register/organiser | Public (needs organiser code) |
| POST | /api/auth/login | Public |
| POST | /api/auth/logout | Logged in |
| GET | /api/events | Public (filters: eventType, location, includePast) |
| GET | /api/events/{id} | Public |
| GET | /api/events/mine | Organiser (events with enrolment counts) |
| POST / PUT / DELETE | /api/events, /api/events/{id} | Organiser (own events) |
| POST | /api/events/{id}/banner | Organiser (image upload to Azure Blob Storage) |
| POST / PUT / DELETE | /api/events/{eventId}/categories, /api/categories/{id} | Organiser |
| GET / PUT | /api/events/{eventId}/route | Public / Organiser |
| POST | /api/categories/{categoryId}/enrolments | Participant |
| GET | /api/enrolments/my | Participant |
| GET | /api/events/{eventId}/enrolments | Organiser |
| DELETE | /api/enrolments/{id} | Participant |
| POST / PUT | /api/enrolments/{enrolmentId}/results, /api/results/{id} | Organiser |
| GET | /api/results/my | Participant |
| GET | /api/events/{eventId}/results | Public |
| GET / PUT | /api/participants/{id} | Participant (own profile) |
| POST | /api/participants/{id}/picture | Participant (image upload to Azure Blob Storage) |

## Project structure

```
Part3-MVC/
  RaceDay.Api/        API: Controllers, Models, Data, Dtos, Security, Services (Azure Blob Storage), Dockerfile
  RaceDay.Web/        MVC app: Controllers, Views, Models (API models and view models),
                      Services (ApiClient), Filters, wwwroot (CSS), Dockerfile
  RaceDay.Tests/      xUnit tests for the API and the MVC project
  docker-compose.yml  SQL Server + API + MVC
  .env.example        Template for secrets
```

Key files in the MVC app:

| File | Purpose |
|---|---|
| `Services/ApiClient.cs` | The only place that talks to the API: sends requests, forwards the session cookie, uploads images, turns errors into friendly messages |
| `Filters/RequireRoleAttribute.cs` | Redirects visitors and wrong-role users away from protected pages |
| `Filters/ApiExceptionFilter.cs` | Handles API failures and expired sessions |
| `Controllers/OrganiserController.cs` | All organiser pages |
| `Controllers/ParticipantController.cs` | Participant pages |
| `Views/Shared/_Layout.cshtml` | Role-aware navigation and the RaceDay theme |

## Unit tests

Run in Visual Studio with **Test > Test Explorer > Run All Tests**, or from a terminal:

```
dotnet test Part3-MVC/RaceDay.sln
```

The tests use an in-memory database and a fake blob storage, so they need no SQL Server or Azure account. They cover:

- the API: authentication, events, enrolments, results, profiles, role enforcement and image uploads,
- the MVC project: the role protection filter, API error handling and display helpers.

## Troubleshooting

| Problem | Fix |
|---|---|
| "The RaceDay service is not reachable" | The API is not running. Start it (Docker, or Visual Studio on http://localhost:5080) and refresh |
| "Startup project cannot be launched" in Visual Studio | Set **RaceDay.Api** and **RaceDay.Web** to Start and **RaceDay.Tests** to None |
| Image upload says storage is not configured | Add your Azure connection string (`.env` for Docker, user secrets for Visual Studio) and restart |
| Image upload fails with a storage error | In the Azure storage account, enable **Allow Blob anonymous access** |
| Docker: port already in use | Stop other programs using ports 5090 or 5081, or change the left-hand port numbers in `docker-compose.yml` |
| Docker: the `db` service is unhealthy | Give Docker Desktop at least 4 GB of memory and run `docker compose down -v`, then `docker compose up --build` |
| Old database without the new columns | The API rebuilds an outdated development database automatically on start-up |

---

# Part 2: RESTful API

The Part 2 API is in `Part2-API`. Open `Part2-API/RaceDay.sln`, use the **http** profile and press **F5**. Swagger opens at http://localhost:5080/swagger. The Part 3 API contains everything in Part 2, plus the image upload endpoints and `GET /api/events/mine`.
