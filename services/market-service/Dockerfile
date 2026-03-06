FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/PramosClearing.Market.Domain/PramosClearing.Market.Domain.csproj                   src/PramosClearing.Market.Domain/
COPY src/PramosClearing.Market.Application/PramosClearing.Market.Application.csproj         src/PramosClearing.Market.Application/
COPY src/PramosClearing.Market.Infrastructure/PramosClearing.Market.Infrastructure.csproj   src/PramosClearing.Market.Infrastructure/
COPY src/PramosClearing.Market.API/PramosClearing.Market.API.csproj                         src/PramosClearing.Market.API/

RUN dotnet restore src/PramosClearing.Market.API/PramosClearing.Market.API.csproj

COPY src/ src/

RUN dotnet publish src/PramosClearing.Market.API/PramosClearing.Market.API.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PramosClearing.Market.API.dll"]
