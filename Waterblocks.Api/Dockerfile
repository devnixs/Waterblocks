# Build stage
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY all_assets.json ./
COPY all_assets.json /all_assets.json
COPY Waterblocks.Api/Waterblocks.Api.csproj Waterblocks.Api/
RUN dotnet restore Waterblocks.Api/Waterblocks.Api.csproj

# Copy source code and build
COPY . .
RUN dotnet build Waterblocks.Api/Waterblocks.Api.csproj -c Release -o /app/build
RUN dotnet publish Waterblocks.Api/Waterblocks.Api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy published app from build stage
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080

# Run the application
ENTRYPOINT ["dotnet", "Waterblocks.Api.dll"]
