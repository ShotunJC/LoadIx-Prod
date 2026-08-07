FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS restore
WORKDIR /source

COPY .editorconfig Directory.Build.props Directory.Packages.props global.json LoadIx.slnx ./
COPY src/LoadIx.Api/LoadIx.Api.csproj src/LoadIx.Api/
COPY src/LoadIx.Application/LoadIx.Application.csproj src/LoadIx.Application/
COPY src/LoadIx.Contracts/LoadIx.Contracts.csproj src/LoadIx.Contracts/
COPY src/LoadIx.Domain/LoadIx.Domain.csproj src/LoadIx.Domain/
COPY src/LoadIx.Infrastructure/LoadIx.Infrastructure.csproj src/LoadIx.Infrastructure/
COPY src/LoadIx.Worker/LoadIx.Worker.csproj src/LoadIx.Worker/
RUN dotnet restore src/LoadIx.Worker/LoadIx.Worker.csproj

FROM restore AS build
COPY src/ src/
RUN mkdir -p src/LoadIx.Api/bin/Debug \
    src/LoadIx.Application/bin/Debug \
    src/LoadIx.Contracts/bin/Debug \
    src/LoadIx.Domain/bin/Debug \
    src/LoadIx.Infrastructure/bin/Debug \
    src/LoadIx.Worker/bin/Debug
RUN for project in LoadIx.Api LoadIx.Infrastructure LoadIx.Worker; do \
      manifest="/tmp/$project.CompileItems.props"; \
      { \
        printf '<Project><ItemGroup>\n'; \
        find "/source/src/$project" -type f -name '*.cs' \
          -not -path '*/bin/*' \
          -not -path '*/obj/*' \
          | sort \
          | while IFS= read -r source; do \
              printf '  <Compile Include="%s" />\n' "$source"; \
            done; \
        printf '</ItemGroup></Project>\n'; \
      } > "$manifest"; \
    done
RUN LoadIxApiCompileManifest=/tmp/LoadIx.Api.CompileItems.props \
    LoadIxInfrastructureCompileManifest=/tmp/LoadIx.Infrastructure.CompileItems.props \
    LoadIxWorkerCompileManifest=/tmp/LoadIx.Worker.CompileItems.props \
    LoadIxApiIncludeConfigurationFiles=false \
    dotnet publish src/LoadIx.Worker/LoadIx.Worker.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10 AS final
WORKDIR /app
ENV DOTNET_EnableDiagnostics=0 \
    DOTNET_gcServer=1 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
COPY --from=build /app/publish .
RUN rm -f /app/appsettings.Development.json \
    && mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R $APP_UID:$APP_UID /home/app
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD test -r /proc/1/cmdline && grep --quiet 'LoadIx.Worker.dll' /proc/1/cmdline || exit 1
ENTRYPOINT ["dotnet", "LoadIx.Worker.dll"]
