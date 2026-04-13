FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY nuget.config .

COPY src/PramosClearing.OrderBook.Domain/PramosClearing.OrderBook.Domain.csproj             src/PramosClearing.OrderBook.Domain/
COPY src/PramosClearing.OrderBook.Application/PramosClearing.OrderBook.Application.csproj   src/PramosClearing.OrderBook.Application/
COPY src/PramosClearing.OrderBook.Infrastructure/PramosClearing.OrderBook.Infrastructure.csproj src/PramosClearing.OrderBook.Infrastructure/
COPY src/PramosClearing.OrderBook.Worker/PramosClearing.OrderBook.Worker.csproj             src/PramosClearing.OrderBook.Worker/

RUN dotnet restore src/PramosClearing.OrderBook.Worker/PramosClearing.OrderBook.Worker.csproj \
    -r linux-x64

COPY src/ src/

RUN dotnet publish src/PramosClearing.OrderBook.Worker/PramosClearing.OrderBook.Worker.csproj \
    -c Release -o /app/publish -r linux-x64 --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PramosClearing.OrderBook.Worker.dll"]
