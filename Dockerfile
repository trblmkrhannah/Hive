# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Install Python (required for WASM toolchain)
RUN apt-get update && apt-get install -y python3 && \
    ln -s /usr/bin/python3 /usr/bin/python && \
    rm -rf /var/lib/apt/lists/*

# Install wasm-tools workload
RUN dotnet workload install wasm-tools

# Copy solution and project files
COPY Directory.Build.props .
COPY Hive.sln .
COPY Hive.Common/Hive.Common.csproj Hive.Common/
COPY Hive.Browser/Hive.Browser.csproj Hive.Browser/
COPY Hive.Browser.Server/Hive.Browser.Server.csproj Hive.Browser.Server/

# Restore dependencies
RUN dotnet restore Hive.Browser.Server/Hive.Browser.Server.csproj

# Copy all source code
COPY Hive.Common/ Hive.Common/
COPY Hive.Browser/ Hive.Browser/
COPY Hive.Browser.Server/ Hive.Browser.Server/

# Publish the browser project (creates the AppBundle)
RUN dotnet publish Hive.Browser/Hive.Browser.csproj -c Release

# Debug: find where _framework and .wasm files are
RUN echo "=== Looking for _framework directories ===" && \
    find /src/Hive.Browser/bin -name "_framework" -type d 2>/dev/null && \
    echo "=== Looking for .wasm files ===" && \
    find /src/Hive.Browser/bin -name "*.wasm" -type f 2>/dev/null | head -5 && \
    echo "=== AppBundle contents ===" && \
    ls -la /src/Hive.Browser/bin/Release/net10.0/browser-wasm/AppBundle/

# Copy AppBundle (which should include _framework) and wwwroot files
RUN mkdir -p /app && \
    cp -r /src/Hive.Browser/bin/Release/net10.0/browser-wasm/AppBundle /app/AppBundle && \
    cp -r /src/Hive.Browser/wwwroot/* /app/AppBundle/ && \
    echo "=== Final AppBundle ===" && \
    ls -la /app/AppBundle/ && \
    echo "=== _framework contents ===" && \
    ls /app/AppBundle/_framework/ 2>/dev/null | head -5 || echo "No _framework"

# Publish the server
RUN dotnet publish Hive.Browser.Server/Hive.Browser.Server.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published server
COPY --from=build /app/publish .

# Copy the WASM AppBundle
COPY --from=build /app/AppBundle /app/AppBundle

# Expose port
EXPOSE 5000

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "Hive.Browser.Server.dll"]
