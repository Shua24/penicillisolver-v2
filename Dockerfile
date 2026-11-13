# Build stage. The project file is copied on its own first so that a change to
# source code does not invalidate the NuGet restore layer.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/penicillisolver-v2.csproj src/
RUN dotnet restore src/penicillisolver-v2.csproj

COPY src/ src/
RUN dotnet publish src/penicillisolver-v2.csproj -c Release -o /out

# Runtime stage. The ASP.NET image has no curl or wget, so health checks run from
# the host or through bash's /dev/tcp; see src/MANUAL.md section 3.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /out .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

# The container runs as root, which is what the backup instructions in
# src/MANUAL.md assume: a backup directory must be writable by root.
ENTRYPOINT ["dotnet", "penicillisolver-v2.dll"]
