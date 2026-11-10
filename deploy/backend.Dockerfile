FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/src/SalesSystem.Api/SalesSystem.Api.csproj backend/src/SalesSystem.Api/
RUN dotnet restore backend/src/SalesSystem.Api/SalesSystem.Api.csproj
COPY backend/src/SalesSystem.Api/ backend/src/SalesSystem.Api/
RUN dotnet publish backend/src/SalesSystem.Api/SalesSystem.Api.csproj -c Release --no-restore -p:UseAppHost=false -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /out/ ./
RUN mkdir -p /app/App_Data/uploads && chown -R $APP_UID:$APP_UID /app/App_Data
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SalesSystem.Api.dll"]
