#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

COPY source/nuget /root/.nuget
COPY . .

WORKDIR "/src/source/InvariantRepresentationLearning/InvariantLearning_Cloud"
RUN dotnet restore "InvariantLearning_Cloud.csproj"
RUN dotnet build "InvariantLearning_Cloud.csproj"

FROM build AS publish
WORKDIR "/src/source/InvariantRepresentationLearning/InvariantLearning_Cloud"
RUN dotnet publish "InvariantLearning_Cloud.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER root
ENTRYPOINT ["dotnet", "InvariantLearning_Cloud.dll"]