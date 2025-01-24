FROM mcr.microsoft.com/dotnet/aspnet:9.0@sha256:6c4df091e4e531bb93bdbfe7e7f0998e7ced344f54426b7e874116a3dc3233ff AS build-env
WORKDIR /DocApi

# Copy everything
COPY . ./
# Restore as distinct layers
#RUN dotnet restore
# Build and publish a release
#RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0@sha256:6c4df091e4e531bb93bdbfe7e7f0998e7ced344f54426b7e874116a3dc3233ff
WORKDIR /DocApi
COPY --from=build-env /DocApi/out .
ENTRYPOINT ["./DocApi"]