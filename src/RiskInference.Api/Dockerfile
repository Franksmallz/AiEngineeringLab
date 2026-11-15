# Chapter 7, Step 6: the risk inference service with the frozen Chapter 6 model.
#
# Build from the repository root, because the image needs the model files:
#   docker build -f src/RiskInference.Api/Dockerfile -t risk-inference:chapter-06-v1 .
# Run:
#   docker run --rm -p 8080:8080 risk-inference:chapter-06-v1

ARG DOTNET_VERSION=10.0

# ---- Build: restore, compile and publish in Release mode ---------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Restore first, with only the project files, so this layer is cached
# until a package changes.
COPY src/RiskClassificationLab/RiskClassificationLab.csproj src/RiskClassificationLab/
COPY src/RiskInference.Api/RiskInference.Api.csproj src/RiskInference.Api/
RUN dotnet restore src/RiskInference.Api/RiskInference.Api.csproj

COPY src/RiskClassificationLab/ src/RiskClassificationLab/
COPY src/RiskInference.Api/ src/RiskInference.Api/
# The model artifact: model, manifest and policy file. RiskInference.Api.csproj
# puts them in the Models folder of the published output.
COPY Models/chapter-06-risk-classifier.zip \
     Models/chapter-06-risk-classifier.manifest.json \
     Models/chapter-06-risk-classifier.policy.json \
     Models/

RUN dotnet publish src/RiskInference.Api/RiskInference.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Runtime: the matching ASP.NET Core image ---------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# The manifest path in appsettings.json is relative. With no solution file in
# the image, the service resolves it from the app folder (/app), so it works on
# any host. Override it with RiskModel__ManifestPath if the model is mounted.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    RiskModel__InferenceMode=Pooled

# Run as the non-root user that the image provides.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "RiskInference.Api.dll"]
