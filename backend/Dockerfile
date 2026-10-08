# Build context: backend/   (docker build -t interviewprep-api backend)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the layer is cached until a csproj changes.
COPY src/InterviewPrep.Api/InterviewPrep.Api.csproj src/InterviewPrep.Api/
RUN dotnet restore src/InterviewPrep.Api/InterviewPrep.Api.csproj

COPY src/ src/
RUN dotnet publish src/InterviewPrep.Api/InterviewPrep.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# The PostgreSQL connection string is supplied at run time via ConnectionStrings__Default.
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "InterviewPrep.Api.dll"]
