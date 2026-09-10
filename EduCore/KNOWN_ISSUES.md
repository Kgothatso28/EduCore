Blocking

- Application Control policy prevents launching EduCore.Web.exe during dotnet run in this environment. This blocks running the Blazor Web UI from the CLI (error: "An Application Control policy has blocked this file"). Developer must allow execution or run from Visual Studio with appropriate permissions.

Non-blocking

- Several NuGet packages have known vulnerabilities (Azure.Identity 1.7.0, Microsoft.Data.SqlClient 5.1.1, Microsoft.Extensions.Caching.Memory 8.0.0, Microsoft.IdentityModel.JsonWebTokens 6.24.0, System.IdentityModel.Tokens.Jwt 6.24.0). These are warnings from restore/build and should be updated in a future maintenance pass.
- EduCore.slnx is a lightweight solution manifest used by automation; a proper .sln should be created if Visual Studio is required for debugging.
- appsettings.json in EduCore.Web currently points to http://localhost:5000 matching the API run in this session. If the API URL changes, update the Web setting.
