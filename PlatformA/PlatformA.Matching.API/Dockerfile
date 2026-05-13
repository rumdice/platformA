FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["PlatformA.Matching.API/PlatformA.Matching.API.csproj", "PlatformA.Matching.API/"]
RUN dotnet restore "./PlatformA.Matching.API/PlatformA.Matching.API.csproj"
COPY . .
WORKDIR "/src/PlatformA.Matching.API"
RUN dotnet build "./PlatformA.Matching.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./PlatformA.Matching.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PlatformA.Matching.API.dll"]
