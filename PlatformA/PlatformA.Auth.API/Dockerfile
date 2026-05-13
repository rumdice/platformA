FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 7001
# HTTPS 전용: HTTP 비활성화, 인증서는 docker-compose 볼륨으로 주입
ENV ASPNETCORE_HTTP_PORTS=""
ENV ASPNETCORE_HTTPS_PORTS="7001"


FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["PlatformA.Auth.API/PlatformA.Auth.API.csproj", "PlatformA.Auth.API/"]
RUN dotnet restore "./PlatformA.Auth.API/PlatformA.Auth.API.csproj"
COPY . .
WORKDIR "/src/PlatformA.Auth.API"
RUN dotnet build "./PlatformA.Auth.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./PlatformA.Auth.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PlatformA.Auth.API.dll"]
