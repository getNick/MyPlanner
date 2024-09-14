# Stage 1: Build application.
#
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build 
ARG TARGETARCH

COPY MyPlanner.API /app/MyPlanner.API
COPY MyPlanner.Data /app/MyPlanner.Data
COPY MyPlanner.Service /app/MyPlanner.Service

WORKDIR /app/MyPlanner.API
RUN dotnet restore -a $TARGETARCH
RUN dotnet build -c Release -a $TARGETARCH -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -a $TARGETARCH -o /app/publish

# Stage 2: Create runtime image.
#
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["dotnet", "MyPlanner.API.dll"]