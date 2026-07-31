FROM mcr.microsoft.com/dotnet/sdk:10.0.103 AS restore
WORKDIR /source

COPY .editorconfig Directory.Build.props Directory.Packages.props global.json LoadIx.slnx ./
COPY src/LoadIx.Api/LoadIx.Api.csproj src/LoadIx.Api/
COPY src/LoadIx.Application/LoadIx.Application.csproj src/LoadIx.Application/
COPY src/LoadIx.Contracts/LoadIx.Contracts.csproj src/LoadIx.Contracts/
COPY src/LoadIx.Domain/LoadIx.Domain.csproj src/LoadIx.Domain/
COPY src/LoadIx.Infrastructure/LoadIx.Infrastructure.csproj src/LoadIx.Infrastructure/
RUN dotnet restore src/LoadIx.Api/LoadIx.Api.csproj

FROM restore AS build
COPY src/ src/
RUN dotnet publish src/LoadIx.Api/LoadIx.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.3 AS final
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
COPY --from=build /app/publish .
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R $APP_UID:$APP_UID /home/app/.aspnet
USER $APP_UID
ENTRYPOINT ["dotnet", "LoadIx.Api.dll"]
