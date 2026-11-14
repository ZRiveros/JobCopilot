# syntax=docker/dockerfile:1

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore with only the files that affect it, so this layer is reused when just code changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/JobCopilot.Domain/JobCopilot.Domain.csproj src/JobCopilot.Domain/
COPY src/JobCopilot.Application/JobCopilot.Application.csproj src/JobCopilot.Application/
COPY src/JobCopilot.Infrastructure/JobCopilot.Infrastructure.csproj src/JobCopilot.Infrastructure/
COPY src/JobCopilot.Web/JobCopilot.Web.csproj src/JobCopilot.Web/
RUN dotnet restore src/JobCopilot.Web/JobCopilot.Web.csproj

COPY src/ src/
RUN dotnet publish src/JobCopilot.Web/JobCopilot.Web.csproj -c Release -o /app/publish --no-restore

# ---- Runtime ----
# The regular (glibc) image rather than Alpine: it carries ICU and time-zone data, which the
# Swedish date formatting and Europe/Stockholm conversions need.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Workstation GC and a memory-conserving setting: this is a single-user app on a shared server.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0 \
    DOTNET_GCConserveMemory=7

# Non-root user that ships with the .NET images.
USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "JobCopilot.Web.dll"]
