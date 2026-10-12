#!/usr/bin/env bash
# Creates the solution file and adds the NuGet packages (latest stable). Run once from the repo root.
set -euo pipefail
dotnet new sln -n SmartRail --format sln 2>/dev/null || dotnet new sln -n SmartRail
dotnet sln add src/*/*.csproj tests/*/*.csproj
dotnet add src/SmartRail.Infrastructure package Microsoft.Extensions.DependencyInjection.Abstractions
dotnet add src/SmartRail.Api package OpenTelemetry.Extensions.Hosting
dotnet add src/SmartRail.Api package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add src/SmartRail.Api package OpenTelemetry.Instrumentation.AspNetCore
dotnet add src/SmartRail.Api package OpenTelemetry.Instrumentation.Http
dotnet add src/SmartRail.Api package OpenTelemetry.Instrumentation.Runtime
for t in tests/*/; do
  dotnet add "$t" package Microsoft.NET.Test.Sdk
  dotnet add "$t" package xunit
  dotnet add "$t" package xunit.runner.visualstudio
done
dotnet add tests/SmartRail.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing
dotnet build -warnaserror
dotnet test --no-build
