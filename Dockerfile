# syntax=docker/dockerfile:1

# The client build output is not committed, so the image has to produce it. Doing it in its own
# stage keeps Node out of the runtime image and lets the npm layer cache on package-lock alone.
# The working directory mirrors the repository layout because vite's outDir is the relative
# ../wwwroot/app, which is where the .NET app serves the client from.
FROM node:24-slim AS client
WORKDIR /src/TalonAdmin/ClientApp

COPY TalonAdmin/ClientApp/package.json TalonAdmin/ClientApp/package-lock.json ./
RUN npm ci

COPY TalonAdmin/ClientApp/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TalonAdmin.slnx ./
COPY TalonAdmin/TalonAdmin.csproj TalonAdmin/
RUN dotnet restore TalonAdmin/TalonAdmin.csproj

COPY . .
COPY --from=client /src/TalonAdmin/wwwroot/app TalonAdmin/wwwroot/app
RUN dotnet publish TalonAdmin/TalonAdmin.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Chromium is what renders report PDFs. Installing it here means the first report request does not
# have to download a browser at runtime; PUPPETEER_EXECUTABLE_PATH points PuppeteerSharp at it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends chromium fonts-liberation \
    && rm -rf /var/lib/apt/lists/*
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium

COPY --from=build /app ./

EXPOSE 8080

# Render and Railway both assign a port at run time and pass it as PORT, while Kestrel reads
# ASPNETCORE_URLS. Bridging the two here rather than in Program.cs keeps the platform's convention out
# of the application, and means a container needs no configuration beyond the database URL to serve
# traffic. Binding 0.0.0.0 is required because the platform's proxy connects from outside the
# container. `exec` keeps dotnet as PID 1 so it still receives SIGTERM on shutdown.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=\"http://0.0.0.0:${PORT:-8080}\" exec dotnet TalonAdmin.dll"]
