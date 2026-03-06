FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/PramosClearing.Portfolio.Domain/PramosClearing.Portfolio.Domain.csproj                   src/PramosClearing.Portfolio.Domain/
COPY src/PramosClearing.Portfolio.Application/PramosClearing.Portfolio.Application.csproj         src/PramosClearing.Portfolio.Application/
COPY src/PramosClearing.Portfolio.Infrastructure/PramosClearing.Portfolio.Infrastructure.csproj   src/PramosClearing.Portfolio.Infrastructure/
COPY src/PramosClearing.Portfolio.API/PramosClearing.Portfolio.API.csproj                         src/PramosClearing.Portfolio.API/

RUN dotnet restore src/PramosClearing.Portfolio.API/PramosClearing.Portfolio.API.csproj

COPY src/ src/

RUN dotnet publish src/PramosClearing.Portfolio.API/PramosClearing.Portfolio.API.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PramosClearing.Portfolio.API.dll"]
