# ASP.NET Core Identity -> Kratos Migrator

A simple migrator that allows you to migrate all your users to [Ory Kratos] from [ASP.NET Core Identity]-based solution.

It migrates all the accounts as-is, mapping only the minimal set of properties. Consider it a skeleton only and adjust to your needs.

Example usage:

```sh
export KRATOS_ADMIN_URL='https://...' # The base address to Kratos API
export KRATOS_ACCESS_TOKEN='...' # The access token to Kratos API
export CONNECTION_STRING='...' # The connection string to SQL Server database with ASP.NET Core Identity data in [dbo] schema

dotnet run # This will migrate all users to Kratos
```

[Ory Kratos]: https://www.ory.sh/kratos/
[ASP.NET Core Identity]: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity
