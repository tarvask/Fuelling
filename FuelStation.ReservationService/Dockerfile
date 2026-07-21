FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy solution and project files to restore dependencies
COPY FuelStation.sln .
COPY FuelStation.ReservationService/FuelStation.ReservationService.csproj ./FuelStation.ReservationService/
COPY FuelStation.Shared/FuelStation.Shared.csproj ./FuelStation.Shared/
COPY Contracts/ ./Contracts/

# restore dependencies
RUN dotnet restore FuelStation.ReservationService/FuelStation.ReservationService.csproj

# copy all other sources
COPY . .

# publish to /app directory
RUN dotnet publish FuelStation.ReservationService/FuelStation.ReservationService.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# copy published app
COPY --from=build /app .

# set port for Kestrel
EXPOSE 5001
ENV ASPNETCORE_URLS=http://+:5001

# run
ENTRYPOINT ["dotnet", "FuelStation.ReservationService.dll"]