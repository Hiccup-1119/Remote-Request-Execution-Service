# build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./src/RreService ./src/RreService
RUN dotnet publish ./src/RreService/RreService.csproj -c Release -o /out

# runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
COPY --from=build /out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "RreService.dll"]
