FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy solution and project files to restore dependencies
COPY FuelStation.Simulator/FuelStation.Simulator.csproj ./FuelStation.Simulator/
COPY FuelStation.Shared/FuelStation.Shared.csproj ./FuelStation.Shared/

# restore dependencies
RUN dotnet restore FuelStation.Simulator/FuelStation.Simulator.csproj

# copy all other sources and publish to /app directory
COPY . .
RUN dotnet publish FuelStation.Simulator/FuelStation.Simulator.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

# copy published app
COPY --from=build /app .

# run
ENTRYPOINT ["dotnet", "FuelStation.Simulator.dll"]