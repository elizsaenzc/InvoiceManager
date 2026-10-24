# Compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY InvoiceManager.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

# Ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish ./
# Fechas de vencimiento y "hoy" en hora peninsular
ENV TZ=Europe/Madrid \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__DefaultConnection="Data Source=/app/data/invoicemanager.db" \
    PORT=8080
RUN mkdir -p /app/data
EXPOSE 8080
CMD ASPNETCORE_URLS=http://0.0.0.0:${PORT} dotnet InvoiceManager.dll
