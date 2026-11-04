# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so that dependency layers are cached between builds.
COPY global.json Directory.Build.props Directory.Packages.props Klinova.slnx ./
COPY src/Klinova.Domain/Klinova.Domain.csproj src/Klinova.Domain/
COPY src/Klinova.Application/Klinova.Application.csproj src/Klinova.Application/
COPY src/Klinova.Infrastructure/Klinova.Infrastructure.csproj src/Klinova.Infrastructure/
COPY src/Klinova.Web/Klinova.Web.csproj src/Klinova.Web/
RUN dotnet restore src/Klinova.Web/Klinova.Web.csproj

COPY .editorconfig ./
COPY src/ src/
RUN dotnet publish src/Klinova.Web/Klinova.Web.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        -p:ContinuousIntegrationBuild=true \
        -p:UseAppHost=false

# ---------------------------------------------------------------------------
# Runtime stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

LABEL org.opencontainers.image.title="Klinova" \
      org.opencontainers.image.description="Open-source outpatient clinic and patient tracking system" \
      org.opencontainers.image.licenses="MIT"

# ICU is required for Turkish culture-aware formatting and sorting.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__Default="Data Source=/app/data/klinova.db" \
    Klinova__DataProtectionKeysPath=/app/data/keys \
    Klinova__UseHttpsRedirection=false

COPY --from=build /app/publish .

# Persistent data (SQLite database and data-protection keys) lives in a volume
# owned by the unprivileged 'app' user that ships with the .NET images.
RUN mkdir -p /app/data && chown -R app:app /app/data
VOLUME ["/app/data"]

USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Klinova.Web.dll"]
