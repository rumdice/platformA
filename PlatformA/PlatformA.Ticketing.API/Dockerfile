FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 7003
ENV ASPNETCORE_HTTP_PORTS=""
ENV ASPNETCORE_HTTPS_PORTS="7003"


FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["PlatformA.Ticketing.API/PlatformA.Ticketing.API.csproj", "PlatformA.Ticketing.API/"]
RUN dotnet restore "./PlatformA.Ticketing.API/PlatformA.Ticketing.API.csproj"
COPY . .
WORKDIR "/src/PlatformA.Ticketing.API"
RUN dotnet build "./PlatformA.Ticketing.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./PlatformA.Ticketing.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PlatformA.Ticketing.API.dll"]
