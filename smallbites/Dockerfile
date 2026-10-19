# Build context is the smallbites folder:  docker build -f Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props .
COPY src/ src/
RUN dotnet publish src/SmallBites.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8090
EXPOSE 8090
USER $APP_UID
ENTRYPOINT ["dotnet", "SmallBites.dll"]
