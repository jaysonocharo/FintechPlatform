# ==========================================
# STAGE 1: Build & Publish
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies first (optimizes Docker layer caching)
COPY ["FintechBackend.csproj", "./"]
RUN dotnet restore "FintechBackend.csproj"

# Copy the rest of the source code and build in Release mode
COPY . .
RUN dotnet publish "FintechBackend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# STAGE 2: Final Secure Runtime
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Create directory and set ownership to 'app' user before dropping root privileges
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet

# Switch to the non-root 'app' user provided natively in .NET 8 base images
USER app

# Copy compiled binaries from the build stage
COPY --from=build /app/publish .

# Set container environment variables
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose internal container port
EXPOSE 8080

# Define container entrypoint
ENTRYPOINT ["dotnet", "FintechBackend.dll"]