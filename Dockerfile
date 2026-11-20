# 1) Build Angular
FROM node:24-alpine AS client
WORKDIR /src/client
COPY client/package*.json ./
RUN npm ci
COPY client/ ./
RUN npm run build

# 2) Publish .NET API. Restore from the project file alone first, so source edits reuse the cached restore layer.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server
WORKDIR /src
COPY server/FiveStack.Api/FiveStack.Api.csproj FiveStack.Api/
RUN dotnet restore FiveStack.Api
COPY server/ ./
RUN dotnet publish FiveStack.Api -c Release --no-restore -o /app

# 3) Runtime: API serves the Angular build from wwwroot (same origin => cookie auth, no CORS)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=server /app ./
COPY --from=client /src/client/dist/client/browser ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080
# Render terminates TLS; trust X-Forwarded-* so OAuth builds https:// redirect URIs.
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
EXPOSE 8080
# Non-root user that ships with the image. The app writes no files: Data Protection keys live in Postgres.
USER $APP_UID
ENTRYPOINT ["dotnet", "FiveStack.Api.dll"]
