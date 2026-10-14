FROM mcr.microsoft.com/dotnet/sdk:10.0 AS bygg
WORKDIR /kilde

# Gjenbruk pakkene mellom bygg når bare kildekoden er endret.
COPY global.json Directory.Build.props NuGet.Config ./
COPY src/Ftpb.Ftbid.Api/Ftpb.Ftbid.Api.csproj src/Ftpb.Ftbid.Api/
COPY src/Ftpb.Ftbid.Application/Ftpb.Ftbid.Application.csproj src/Ftpb.Ftbid.Application/
COPY src/Ftpb.Ftbid.Domain/Ftpb.Ftbid.Domain.csproj src/Ftpb.Ftbid.Domain/
COPY src/Ftpb.Ftbid.Infrastructure/Ftpb.Ftbid.Infrastructure.csproj src/Ftpb.Ftbid.Infrastructure/
RUN dotnet restore src/Ftpb.Ftbid.Api/Ftpb.Ftbid.Api.csproj
COPY src/ src/
RUN dotnet publish src/Ftpb.Ftbid.Api/Ftpb.Ftbid.Api.csproj --no-restore -c Release -o /utdata

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
# Curl brukes av containerens helsesjekk.
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=bygg /utdata .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Ftpb.Ftbid.Api.dll"]
