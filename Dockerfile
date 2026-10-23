# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY InvoiceManager.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

# Run
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish ./
# Render (and similar hosts) provide the port in $PORT; default to 8080 locally
ENV PORT=8080
EXPOSE 8080
CMD ASPNETCORE_URLS=http://0.0.0.0:${PORT} dotnet InvoiceManager.dll
