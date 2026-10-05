FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["DestinationService/DestinationService.csproj", "DestinationService/"]
RUN dotnet restore "DestinationService/DestinationService.csproj"
COPY . .
WORKDIR "/src/DestinationService"
RUN dotnet build "DestinationService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DestinationService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DestinationService.dll"]
