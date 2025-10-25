# Use .NET 9.0 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/TemplateApp.Api/TemplateApp.Api.csproj", "src/TemplateApp.Api/"]
COPY ["src/TemplateApp.ServiceDefaults/TemplateApp.ServiceDefaults.csproj", "src/TemplateApp.ServiceDefaults/"]

# Restore dependencies
RUN dotnet restore "src/TemplateApp.Api/TemplateApp.Api.csproj"

# Copy everything else
COPY . .

# Build the application
WORKDIR "/src/src/TemplateApp.Api"
RUN dotnet build "TemplateApp.Api.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "TemplateApp.Api.csproj" -c Release -o /app/publish

# Use .NET 9.0 runtime for final image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TemplateApp.Api.dll"]