FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 7004
ENV ASPNETCORE_HTTP_PORTS=""
ENV ASPNETCORE_HTTPS_PORTS="7004"


FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["PlatformA.Utils.API/PlatformA.Utils.API.csproj", "PlatformA.Utils.API/"]
RUN dotnet restore "./PlatformA.Utils.API/PlatformA.Utils.API.csproj"
COPY . .
WORKDIR "/src/PlatformA.Utils.API"
RUN dotnet build "./PlatformA.Utils.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./PlatformA.Utils.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PlatformA.Utils.API.dll"]
