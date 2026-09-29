# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

App.Mimosi.Ge ("მიმოსი") automates operations and record-keeping for a training center (სასწავლო ცენტრი): students and their contracts, groups and lesson schedules, teacher contracts and salary calculation, payments, CRM calls to families, and reports. The backend is ASP.NET Core (.NET 10) with EF Core on SQL Server, and the same host serves a React SPA.

Code comments, XML docs, DB column comments (`HasComment(...)`) and UI text are in Georgian. Write new ones in Georgian too.

## Multi-repo layout

This repo is only the thin host. `AppMimosiGe.slnx` pulls in projects from **sibling directories** (`../<Name>`). Each one is a separate git repo (`github.com/merabza/<Name>`) and must be checked out next to this one:

| Location | Contents |
|---|---|
| this repo | `AppMimosiGe`: web host and composition root (`Program.cs`). `AppMimosiGe.Application`: CQRS handlers, FluentValidation validators and repository interfaces, one folder per use case. `AppMimosiGe.Infrastructure`: the repository implementations (they see only `IMimosiGeDbContext`). `AppMimosiGe.WebApi`: minimal-API endpoints and the rights filters. `AppMimosiGe.Application.Tests`. `AppMimosiGeRepositories`: the repository bindings that the carcass leaves to the host app |
| `../MimosiGeCore` | `MimosiGeCore.Domain`: the training-center entities (`Models/`, plain classes, no EF). `MimosiGeCore.Application.Abstractions`: `IMimosiGeDbContext` (the `DbSet`s). `MimosiGeCore.Tests`: the entities' `IDataType` tests |
| `../MimosiGeDbPart` | `MimosiGeDbPart.Db`: `MimosiGeDbContext` (implements `IMimosiGeDbContext`) and the entities' `IEntityTypeConfiguration`s (`Configurations/`) |
| `../AppMimosiGeShared` | `AppMimosiGeShared.Contracts`: Mimosi API contracts: `AppMimosiGeApiRoutes`, request/response DTOs (`V1/`), error definitions (`Errors/`) |
| `../BackendCarcass` | Reusable backend "carcass": JWT auth, users/roles, rights, menu, data types, generic master-data CRUD |
| `../BackendCarcassShared` | API contracts: `CarcassApiRoutes`, request/response DTOs, error definitions |
| `../SystemTools`, `../WebSystemTools` | Generic infrastructure: `Result`, CQRS abstractions, EF conventions, Serilog, Swagger, CORS, config encryption, Windows-service hosting, SPA static files |
| `../AppMimosiGeFront/appmimosigefrontend` | React 19 + TypeScript + Vite SPA |
| `…/appmimosigefrontend/src/appcarcass` | **Separate nested git repo** (`ReactAppCarcass`), gitignored by AppMimosiGeFront: login, rights editor, master-data grids and forms, RTK Query APIs |

- A change often spans several repos, so commit in each repo you touch. Commit messages are usually a `yyyyMMddHHmm` timestamp, and one change uses the same timestamp in every repo.
- The carcass and tools repos are app-agnostic (none of them references MimosiGe code), and each has its own `.slnx`. Keep training-center logic in this repo, in `MimosiGeCore`, `MimosiGeDbPart`, `AppMimosiGeShared`, or in the frontend outside `src/appcarcass`.
- The layering follows SupportToolsServer: Host → WebApi → Application → MimosiGeCore; Infrastructure → Application + `IMimosiGeDbContext`; only the host (for DI) and the test project reference `MimosiGeDbPart`. `MimosiGeDbPart` depends only on `MimosiGeCore`, the carcass and SystemTools, never on this repo, because the migration tooling (`MimosiGeDbTools`) builds it from its own clones.
- Every repo has its own `Directory.Build.props` and `Directory.Packages.props` (central package management), and a project uses the files from the repo it lives in. A new `PackageReference` therefore needs its `PackageVersion` in that repo's props file. `.editorconfig` is the same in every repo.

## Commands

Backend commands, run from this repo's root:

```bash
dotnet build AppMimosiGe.slnx
dotnet run --project AppMimosiGe        # http://localhost:5070, Swagger UI at /swagger
dotnet publish AppMimosiGe -c Release   # the PublishSpa target also runs npm install + npm run build and packs the SPA's dist/ into wwwroot
```

The build treats all analyzer warnings as errors (`TreatWarningsAsErrors`, `AnalysisMode=All`, SonarAnalyzer, `EnforceCodeStyleInBuild`), and `.editorconfig` marks many style rules as errors. The ones you are most likely to hit:
- file-scoped namespaces and braces everywhere
- no `var` unless the type is apparent
- expression-bodied properties, accessors and lambdas
- `ImplicitUsings` is disabled, so write every `using` explicitly
- nullable reference types are enabled

Frontend commands, run in `../AppMimosiGeFront/appmimosigefrontend`: `npm run dev`, `npm run build` (`tsc -b && vite build`), `npm run lint` and `npm test` (Vitest with jsdom; `npm test -- NavMenu` runs only the test files whose path matches). `.env.development` points `VITE_REACT_APP_API_URI` at the remote dev server (`https://devapp.mimosi.ge/api/v1`), not at the local backend. To use a local backend, override it in a gitignored `.env.development.local` and add the Vite origin to `CorsSettings:Origins`.

Backend tests are xUnit + Moq `*.Tests` projects in each repo's `/Tests/` solution folder. `AppMimosiGe.Application.Tests` (this repo) tests handlers and validators with mocked repositories, and the repositories against the EF InMemory provider (seed with the synchronous `SaveChanges()`: carcass `SaveChangesAsync` needs a domain events dispatcher that the test constructor doesn't set):
- Run them with, for example, `dotnet test AppMimosiGe.Application.Tests` or `dotnet test ../MimosiGeCore/MimosiGeCore.slnx`. Add `--filter "FullyQualifiedName~CourseTests"` to run a single class.
- If a running `AppMimosiGe` process locks `AppMimosiGe\bin`, build the host with `-p:OutDir=<scratch folder>`.
- Pass `--target-framework net10.0` when running Stryker.NET on these projects. The target framework comes from `Directory.Build.props`. Without the flag, Stryker's project analysis falls back to Visual Studio's .NET Framework MSBuild, which can't build .NET 10.

The frontend's test setup follows AppGanmartebaGe, the other app built on ReactAppCarcass:
- Carcass tests import `src/testUtils/testStore.ts` (`mockFetch`, `testBaseUrl`) from the host app.
- `src/setupTests.ts` registers the jest-dom matchers and cleanup.
- `tsc -b` type-checks test files as well, so a test that doesn't compile breaks `npm run build` and `dotnet publish`.

## Configuration

`appsettings.json` holds placeholders only. The real values for the keys listed in `appsetenkeys.json` (DB connection string, JWT settings, log path, CORS origins, MediatR license key) come from `appsettingsEncoded.json`, which is gitignored. How `AddConfigurationEncryption` handles that file:
- The file is required at startup.
- It is decrypted with `appKey` (in `Program.cs`) plus the machine name, so an encoded file only works on the machine that produced it.
- It is added as the last configuration source, so its values override user secrets and environment variables.

## Architecture

**Composition.** `Program.cs` wires everything through each library's `AddXxx(debugLogger, …)` and `UseXxx(debugLogger)` extension methods. `debugLogger` is non-null only in Development, where it logs each step. The APIs are minimal-API endpoints under `api/v1` (`CarcassApiRoutes.ApiBase`). Unknown `api/v1/*` paths return 404, and every other unknown path falls back to the SPA's `index.html`. The same executable can also run as a Windows service (`UseWindowsServiceOnWindows`).

**CQRS.** Endpoints inject `IQueryHandler<TQuery, TResponse>` and `ICommandHandler<TCommand, TResponse>` (`SystemTools.Application.Abstractions.Messaging`) directly. They map the returned `Result<T>` (`SystemTools.SharedKernel`) to `TypedResults.Ok` or `CustomResults.Problem(errors)`. MediatR is registered through `AddMediator` but nothing dispatches through it. Scrutor discovers handlers inside `AddApplication(debugLogger, typeof(<Assembly>.AssemblyReference))` and wraps them with validation and logging decorators. Handlers in a new assembly are only found after that assembly is added to the call.

**Data.** `MimosiGeDbContext` inherits `CarcassDbContext`, so one SQL Server database holds both the carcass tables (users, roles, rights, menu, data types) and the training-center tables. The context is registered as `ICarcassApplicationDbContext` and as `IMimosiGeDbContext`. Entity configurations load via `ApplyConfigurationsFromAssembly`. Every training-center entity has one that sets `ToTable`, lengths, defaults, indexes and Georgian `HasComment`s, and `MimosiGeDbPart.Db.Tests` checks the comments and lengths. After that, `DatabaseEntitiesDefaultConvention` maps `DateTime` → `datetime` and `decimal` → `money`. Its index and FK constraint naming never takes effect: `GetDatabaseName()` and `GetConstraintName()` already return EF's default names (`IX_…`, `FK_…`), so the convention skips every index and FK. The schema is created from the migration in `MimosiGeDbTools.DbMigration`, in the separate `D:\1WorkMimosi\MimosiGeDbTools` workspace, which has its own clones of `MimosiGeDbPart` and `MimosiGeCore`. SupportTools' RecreateDevDatabase deletes its `Migrations\*.cs`, regenerates the `Initial` migration from the model and rebuilds the dev database. A model change therefore reaches the database only after it is committed, pulled into that clone, and RecreateDevDatabase has run.

**Generic master data.** Most list and edit screens are not hand-written. Instead, the carcass `masterdata` endpoints (`api/v1/masterdata/{tableName}/…`):
- find the entity in the EF model by its **database table name**
- read the grid and validation rules from that table's row in the carcass `DataType` table (`DtGridRulesJson`)
- check the user's rights on the table (`UserTableRightsFilter`)

The SPA renders grids and forms from the same metadata (routes `mdList/:tableName` and `mdItemEdit/:tableName/:mdIdValue`). To expose an entity this way:
1. Implement `BackendCarcass.Domain.IDataType` on the model (`MimosiGeCore.Domain/Models`). `Course`, `AcademicYear` and `BankAccount` show the property mapping: `[NotMapped]` `Id`/`Key`/`Name`/`ParentId` over the real columns, and `EditFields()` returning the editable projection.
   - `UpdateTo(other)` must copy the editable fields from `other` into `this`, the way `DataType.UpdateTo` does. `MasterDataCrud.Update` calls it on the loaded record and then saves that record.
   - Implement `ISortedDataType` too if rows are ordered by `SortId`.
2. Add the table's `DataType` row (grid rules JSON) plus its rights and menu entries. These are database data, not code.

`AppMimosiGeRepositories` provides the bindings that the carcass leaves to the host: `ICarcassMasterDataRepository` → `MimosiGeMasterDataRepository`, plus `IUserRightsRepository`, `IMasterDataLoaderCreator`, `IReturnValuesLoaderCreator` and `IUnitOfWork`. Carcass classes marked `/*open*/` are the extension points. For example, `MasterDataLoaderCreator.CreateMasterDataCrud` is virtual. For table-specific behavior, subclass one of these classes and register the subclass here.

**Frontend.** Project-specific routes, slices, RTK Query APIs and middlewares go in the marked `project` sections of `src/App.tsx`, `src/TopNavMenu.tsx` and `src/redux/store.ts`. Code under `src/appcarcass` is shared and gets committed in the ReactAppCarcass repo. The backend serves the main menu (`userrights/getmainmenu`), built from the menu tables according to the user's rights.

**Adding a Mimosi feature with its own endpoints and page.** Student contracts (`studentContracts`) is the reference implementation:
1. **Contracts** (`../AppMimosiGeShared`): routes in `AppMimosiGeApiRoutes`, request/response records in `V1/`, errors in `Errors/` (`Error.Problem` → 400, `NotFound` → 404, `Conflict` → 409).
2. **Application**: `<Feature>/<UseCase>/` with the query/command record, its handler and (for commands) an `AbstractValidator<TCommand>`. The validator runs automatically in the `ValidationDecorator`. It's registered by `AddFluentValidation(..., AppMimosiGe.Application.AssemblyReference.Assembly)` and may inject repositories. The repository interface lives in `<Feature>/`.
3. **Infrastructure**: the repository over `IMimosiGeDbContext` (`AsNoTracking` + projection for reads). Register it in `AddAppMimosiGeInfrastructure`. Saving goes through `IUnitOfWork.SaveChangesAsync` in the handler.
4. **WebApi**: a static `Use<Feature>Endpoints` class in `Endpoints/V1` (a `MapGroup` with `RequireAuthorization()` and the rights filter), called from `UseAppMimosiGeApi`. The rights filter subclasses the carcass `UserMenuRightsFilter` with the menu item's `MenKey`, so whoever sees the menu item may call the endpoints. A user without the right gets 403.
5. **Menu and rights** (`MimosiGeDbTools.DataSeederRules`): a `MenuItmSeederModel` with `MenKey = MenLinkKey = <feature key>`, no `MenValue`, in one of the Mimosi menu groups. Manager gets every item of the Mimosi groups automatically. The database gets it only after RecreateDevDatabase + TransferProdCopyToDevByPairs.
6. **Frontend**: `src/<feature>/` (pages, pure logic with Vitest tests), the RTK Query API in `src/redux/api/` (register it in `store.ts`), types in `src/redux/types/`, and routes `<feature key>` (list) plus an editor route in `App.tsx`. The page checks the menu right via `flatMenu` (`menLinkKey`). A paged list uses the carcass `GridView` and sends its `IFilterSortRequest` through `encodeFilterSortRequest` (`src/studentContracts/filterSortRequestEncoding.ts`: URL-encoded JSON in base64, so Georgian text survives), which the backend reads with `FilterSortRequestFactory`.
