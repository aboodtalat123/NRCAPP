FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NRCAPP/NRCAPP.csproj NRCAPP/
RUN dotnet restore NRCAPP/NRCAPP.csproj

COPY NRCAPP/ NRCAPP/
RUN dotnet publish NRCAPP/NRCAPP.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT dotnet NRCAPP.dll --urls http://0.0.0.0:${PORT:-8080}
