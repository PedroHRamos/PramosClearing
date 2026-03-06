FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/PramosClearing.User.Domain/PramosClearing.User.Domain.csproj                 src/PramosClearing.User.Domain/
COPY src/PramosClearing.User.Application/PramosClearing.User.Application.csproj       src/PramosClearing.User.Application/
COPY src/PramosClearing.User.Infrastructure/PramosClearing.User.Infrastructure.csproj src/PramosClearing.User.Infrastructure/
COPY src/PramosClearing.User.API/PramosClearing.User.API.csproj                       src/PramosClearing.User.API/

RUN dotnet restore src/PramosClearing.User.API/PramosClearing.User.API.csproj

COPY src/ src/

RUN dotnet publish src/PramosClearing.User.API/PramosClearing.User.API.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PramosClearing.User.API.dll"]
