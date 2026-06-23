FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 7778
EXPOSE 7779


FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["PlatformA.Game.Gomoku/PlatformA.Game.Gomoku.csproj", "PlatformA.Game.Gomoku/"]
RUN dotnet restore "./PlatformA.Game.Gomoku/PlatformA.Game.Gomoku.csproj"
COPY . .
WORKDIR "/src/PlatformA.Game.Gomoku"
RUN dotnet build "./PlatformA.Game.Gomoku.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./PlatformA.Game.Gomoku.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD wget -q -O- http://localhost:7779/healthz || exit 1
ENTRYPOINT ["dotnet", "PlatformA.Game.Gomoku.dll"]
