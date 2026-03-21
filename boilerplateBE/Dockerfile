# -- Stage 1: Restore --
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

# Copy solution-level files first (maximizes layer cache)
COPY Starter.sln Directory.Build.props Directory.Packages.props ./

# Copy all .csproj files preserving directory structure
COPY src/Starter.Domain/Starter.Domain.csproj src/Starter.Domain/
COPY src/Starter.Shared/Starter.Shared.csproj src/Starter.Shared/
COPY src/Starter.Application/Starter.Application.csproj src/Starter.Application/
COPY src/Starter.Infrastructure/Starter.Infrastructure.csproj src/Starter.Infrastructure/
COPY src/Starter.Infrastructure.Identity/Starter.Infrastructure.Identity.csproj src/Starter.Infrastructure.Identity/
COPY src/Starter.Api/Starter.Api.csproj src/Starter.Api/

# Create stub test project (tests excluded from Docker context but .sln references it)
RUN mkdir -p tests/Starter.Api.Tests && \
    echo '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>' \
    > tests/Starter.Api.Tests/Starter.Api.Tests.csproj

RUN dotnet restore

# -- Stage 2: Build & Publish --
FROM restore AS build
COPY src/ src/
RUN dotnet publish src/Starter.Api/Starter.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# -- Stage 3: Runtime --
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "Starter.Api.dll"]
