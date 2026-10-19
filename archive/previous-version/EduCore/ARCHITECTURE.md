# Architecture

This document describes the high-level architecture for the EduCore initial foundation.

- Backend: EduCore.Api (ASP.NET Core)
- Application: EduCore.Application (business contracts)
- Domain: EduCore.Domain (entities)
- Infrastructure: EduCore.Infrastructure (EF Core, persistence)
- Web: EduCore.Web (Blazor shell placeholder)
- App: EduCore.App (.NET MAUI placeholder)

Dependency direction follows Clean Architecture: Api -> Application -> Domain; Infrastructure -> Application -> Domain.
