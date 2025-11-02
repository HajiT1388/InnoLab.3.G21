# ---------------------------
# STAGE 1: Build the project
# ---------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY Server/*.csproj ./
RUN dotnet restore

# Copy everything else and build the app
COPY Server/. ./
RUN dotnet publish -c Release -o /app/publish

# ---------------------------
# STAGE 2: Run the app
# ---------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Environment variables
# ENV ASPNETCORE_URLS=http://+:5000
# ENV DOTNET_URLS=http://+:5000
# EXPOSE 5000

ENTRYPOINT ["dotnet", "TestserverCS.dll"]

