FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PresetService/PresetService.csproj", "PresetService/"]
RUN dotnet restore "PresetService/PresetService.csproj"

COPY . .
WORKDIR "/src/PresetService"
RUN dotnet build "PresetService.csproj" -c Release -o /app/build
RUN dotnet publish "PresetService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PresetService.dll"]