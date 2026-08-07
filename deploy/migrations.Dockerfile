FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /source
ENV NUGET_PACKAGES=/home/loadix/.nuget/packages

COPY .editorconfig Directory.Build.props Directory.Packages.props global.json LoadIx.slnx dotnet-tools.json ./
COPY src/LoadIx.Api/LoadIx.Api.csproj src/LoadIx.Api/
COPY src/LoadIx.Application/LoadIx.Application.csproj src/LoadIx.Application/
COPY src/LoadIx.Contracts/LoadIx.Contracts.csproj src/LoadIx.Contracts/
COPY src/LoadIx.Domain/LoadIx.Domain.csproj src/LoadIx.Domain/
COPY src/LoadIx.Infrastructure/LoadIx.Infrastructure.csproj src/LoadIx.Infrastructure/
RUN dotnet restore src/LoadIx.Api/LoadIx.Api.csproj
COPY src/ src/
RUN mkdir -p src/LoadIx.Api/bin/Debug \
    src/LoadIx.Application/bin/Debug \
    src/LoadIx.Contracts/bin/Debug \
    src/LoadIx.Domain/bin/Debug \
    src/LoadIx.Infrastructure/bin/Debug
RUN for project in LoadIx.Api LoadIx.Infrastructure; do \
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
    dotnet build src/LoadIx.Api/LoadIx.Api.csproj \
    --configuration Release \
    --no-restore
RUN dotnet tool install dotnet-ef \
    --tool-path /tools \
    --version 10.0.10
RUN LoadIxApiCompileManifest=/tmp/LoadIx.Api.CompileItems.props \
    LoadIxInfrastructureCompileManifest=/tmp/LoadIx.Infrastructure.CompileItems.props \
    LoadIxApiIncludeConfigurationFiles=false \
    /tools/dotnet-ef migrations bundle \
    --project src/LoadIx.Infrastructure \
    --startup-project src/LoadIx.Api \
    --configuration Release \
    --no-build \
    --output /app/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10 AS final
WORKDIR /app
ENV DOTNET_EnableDiagnostics=0 \
    DOTNET_RUNNING_IN_CONTAINER=true
COPY --from=build --chown=$APP_UID:$APP_UID /app/efbundle .
USER $APP_UID
ENTRYPOINT ["./efbundle"]
