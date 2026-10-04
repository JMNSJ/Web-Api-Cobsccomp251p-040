# ---------------------------------------------------------------------------
# Build stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, on its own layer, so package changes do not invalidate the whole build.
COPY ["src/SlseaSolarApi.Api/SlseaSolarApi.Api.csproj", "src/SlseaSolarApi.Api/"]
RUN dotnet restore "src/SlseaSolarApi.Api/SlseaSolarApi.Api.csproj"

COPY . .
WORKDIR "/src/src/SlseaSolarApi.Api"
RUN dotnet publish "SlseaSolarApi.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---------------------------------------------------------------------------
# Runtime stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Default to PostgreSQL, the public-deployment target. The connection string itself is injected by
# the host at run time (ConnectionStrings__SolarDb) and is never baked into the image, because it
# carries credentials. A free container host has an ephemeral filesystem, so a local database file
# would be destroyed on every restart — managed PostgreSQL is what makes the deployment durable.
ENV Database__Provider=Postgres

# Most managed hosts inject the port to listen on; 8080 is the sane default.
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "SlseaSolarApi.Api.dll"]