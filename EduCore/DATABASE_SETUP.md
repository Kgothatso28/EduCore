# Database setup

This project uses SQL Server via Entity Framework Core (Microsoft.EntityFrameworkCore.SqlServer).

Connection string keys:

- ConnectionStrings:EduCoreDatabase

Development note:
- The initial foundation will not modify or drop any existing database. EF Core migrations should be created and reviewed prior to applying to any environment.
