FROM mcr.microsoft.com/dotnet/sdk:10.0.103 AS build
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
    --version 10.0.3

FROM mcr.microsoft.com/dotnet/sdk:10.0.103 AS final
WORKDIR /workspace
COPY --from=build /source .
COPY --from=build /tools /tools
RUN rm -f /workspace/src/LoadIx.Api/appsettings.Development.json
ENV PATH="/tools:${PATH}" \
    NUGET_PACKAGES=/home/loadix/.nuget/packages \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_RUNNING_IN_CONTAINER=true
RUN groupadd --gid 10001 loadix \
    && useradd --uid 10001 --gid 10001 --create-home --shell /usr/sbin/nologin loadix \
    && chown -R 10001:10001 /workspace /tools
COPY --from=build --chown=10001:10001 /home/loadix/.nuget/packages /home/loadix/.nuget/packages
USER 10001
ENTRYPOINT ["dotnet-ef", "database", "update", "--project", "src/LoadIx.Infrastructure", "--startup-project", "src/LoadIx.Api", "--configuration", "Release", "--no-build"]
