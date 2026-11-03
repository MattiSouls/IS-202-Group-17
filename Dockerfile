# Build stage: uses the .NET 10 SDK to restore, build and publish the MVC application.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the web project and restore its dependencies.
COPY WebApplicationInAspire/WebApplicationInAspire.csproj WebApplicationInAspire/
COPY WebApplicationInAspire/packages.lock.json WebApplicationInAspire/
RUN dotnet restore WebApplicationInAspire/WebApplicationInAspire.csproj --locked-mode

# Copy the remaining source files and publish the application.
COPY WebApplicationInAspire/ WebApplicationInAspire/
RUN dotnet publish WebApplicationInAspire/WebApplicationInAspire.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage: keeps the final image smaller by using only the ASP.NET runtime.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# ASP.NET Core listens on port 8080 inside the container.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

RUN mkdir -p /app/keys && chown $APP_UID:$APP_UID /app/keys

USER $APP_UID

ENTRYPOINT ["dotnet", "WebApplicationInAspire.dll"]
