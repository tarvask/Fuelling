FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy solution and project files to restore dependencies
COPY FuelStation.PaymentService/FuelStation.PaymentService.csproj ./FuelStation.PaymentService/
COPY FuelStation.Shared/FuelStation.Shared.csproj ./FuelStation.Shared/

# restore dependencies
RUN dotnet restore FuelStation.PaymentService/FuelStation.PaymentService.csproj

# copy all other sources and publish to /app directory
COPY . .
RUN dotnet publish FuelStation.PaymentService/FuelStation.PaymentService.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

# copy published app
COPY --from=build /app .

# run
ENTRYPOINT ["dotnet", "FuelStation.PaymentService.dll"]