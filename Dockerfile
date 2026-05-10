FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/ExamenSecurity.Api/ExamenSecurity.Api.csproj src/ExamenSecurity.Api/
RUN dotnet restore src/ExamenSecurity.Api/ExamenSecurity.Api.csproj
COPY src/ExamenSecurity.Api/ src/ExamenSecurity.Api/
RUN dotnet publish src/ExamenSecurity.Api/ExamenSecurity.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ExamenSecurity.Api.dll"]
