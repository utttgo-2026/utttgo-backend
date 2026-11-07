FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/UTTTGO.Api/UTTTGO.Api.csproj src/UTTTGO.Api/UTTTGO.Api.csproj
RUN dotnet restore src/UTTTGO.Api/UTTTGO.Api.csproj

COPY src/UTTTGO.Api/ src/UTTTGO.Api/
RUN dotnet publish src/UTTTGO.Api/UTTTGO.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_gcServer=0

EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "UTTTGO.Api.dll"]
