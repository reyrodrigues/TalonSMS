# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TalonAdmin.slnx ./
COPY TalonAdmin/TalonAdmin.csproj TalonAdmin/
RUN dotnet restore TalonAdmin/TalonAdmin.csproj

COPY . .
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
ENTRYPOINT ["dotnet", "TalonAdmin.dll"]
