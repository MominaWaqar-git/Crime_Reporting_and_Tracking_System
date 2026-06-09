# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Crime_Reporting_and_Tracking_System.csproj", "./"]
RUN dotnet restore "Crime_Reporting_and_Tracking_System.csproj"
COPY . .
RUN dotnet publish "Crime_Reporting_and_Tracking_System.csproj" -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Crime_Reporting_and_Tracking_System.dll"]