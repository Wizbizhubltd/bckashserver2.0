# Build context is the repository root (see docker-compose.yml: `context: .`), so this can
# reach the sibling projects BCKash.Api depends on.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from just the .csproj files, so dependency layers cache across code changes.
COPY BCKash.Api/BCKash.Api.csproj BCKash.Api/
COPY BCKash.Application/BCKash.Application.csproj BCKash.Application/
COPY BCKash.Domain/BCKash.Domain.csproj BCKash.Domain/
COPY BCKash.Infrastructure/BCKash.Infrastructure.csproj BCKash.Infrastructure/
COPY BCKash.SharedKernel/BCKash.SharedKernel.csproj BCKash.SharedKernel/

# Cache the NuGet package download across builds (BuildKit cache mount, not a Docker layer) —
# without this, every build re-downloads the full package graph from nuget.org from scratch,
# which is slow and prone to timing out on a flaky connection (as opposed to just the packages
# that actually changed).
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore BCKash.Api/BCKash.Api.csproj

COPY BCKash.Api/ BCKash.Api/
COPY BCKash.Application/ BCKash.Application/
COPY BCKash.Domain/ BCKash.Domain/
COPY BCKash.Infrastructure/ BCKash.Infrastructure/
COPY BCKash.SharedKernel/ BCKash.SharedKernel/

# Same cache mount as the restore step above — packages restored under a cache mount live
# only in that mount, not in the image layer, so a later RUN (even with --no-restore, which
# still needs the already-restored package assets on disk to resolve analyzers/build tasks)
# needs the mount too or it can't find them.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish BCKash.Api/BCKash.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN useradd --uid 5678 --user-group --no-create-home appuser \
    && mkdir -p /app/App_Data/uploads \
    && chown -R appuser:appuser /app
USER appuser

COPY --from=build --chown=appuser:appuser /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "BCKash.Api.dll"]
