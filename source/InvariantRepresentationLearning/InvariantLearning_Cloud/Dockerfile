#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
#COPY ["nuget.config", "."]
#COPY ["InvariantRepresentationLearning/InvariantLearning_FrameCheck/InvariantLearning_FrameCheck.csproj", "InvariantRepresentationLearning/InvariantLearning_FrameCheck/"]
#COPY ["NeoCortexApi/NeoCortexApi.csproj", "NeoCortexApi/"]
#COPY ["NeoCortexEntities/NeoCortexEntities.csproj", "NeoCortexEntities/"]
#COPY ["NeoCortexArrayLib/NeoCortexArrayLib.csproj", "NeoCortexArrayLib/"]
#COPY ["InvariantRepresentationLearning/Invariant.Entities/Invariant.Entities.csproj", "InvariantRepresentationLearning/Invariant.Entities/"]
#COPY ["ImageEncoder/HtmImageEncoder.csproj", "source/ImageEncoder/"]

COPY nuget /root/.nuget
COPY ["Cloud_Common/Cloud_Common.csproj", "Cloud_Common/"]
COPY ["Invariant.Entities/Invariant.Entities.csproj", "Invariant.Entities/"]
COPY ["InvariantLearning_Utilities/InvariantLearning_Utilities.csproj", "InvariantLearning_Utilities/"]
COPY ["InvariantLearning_Cloud/InvariantLearning_Cloud.csproj", "InvariantLearning_Cloud/"]
COPY ["Experiment/Experiment.csproj", "Experiment/"]
COPY ["./NeoCortexApi/NeoCortexApi.csproj", "NeoCortexApi/"]


#using NeoCortexApi.Classifiers;
#using NeoCortexApi.Entities;
#using NeoCortexApi.Utility;
#using NeoCortexApi;
#using NeoCortexApi.Network;
#using HtmImageEncoder;

#COPY . .
RUN pwd && ls
RUN dotnet restore "source/InvariantRepresentationLearning/InvariantLearning_Cloud/InvariantLearning_Cloud.csproj"

COPY . .
WORKDIR "source/InvariantRepresentationLearning/InvariantLearning_Cloud"
RUN dotnet build "InvariantLearning_FrameCheck.csproj" -c Release -o /app/build

FROM build AS publish
RUN pwd && ls
RUN dotnet publish "InvariantLearning_Cloud.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "InvariantLearning_Cloud.dll"]