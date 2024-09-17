# Stage 1: Build application.
#
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build 

COPY MyPlanner.API /app/MyPlanner.API
COPY MyPlanner.Data /app/MyPlanner.Data
COPY MyPlanner.Service /app/MyPlanner.Service

WORKDIR /app/MyPlanner.API
RUN dotnet restore
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Create runtime image.
#
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["dotnet", "MyPlanner.API.dll"]