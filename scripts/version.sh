#!/bin/sh

SEMVER_VERSION=$(sed -En "s/<Version>(.*)<\/Version>/\1/p" ./BSC.Fhir.Mapping/BSC.Fhir.Mapping.csproj | awk '{$1=$1}1')

if [ $(echo $SEMVER_VERSION | wc -l) -gt 1 ]; then
    echo "Found more than one version number in csproj file"
    exit 1
fi

echo "semver: $SEMVER_VERSION"

if [ -z SEMVER_VERSION ]; then
    echo "Could not find version number in csproj file"
    exit 1
fi

MAJOR=$(echo $SEMVER_VERSION | awk -F. '{ print $1 }')
MINOR=$(echo $SEMVER_VERSION | awk -F. '{ print $2 }')
PATCH=$(echo $SEMVER_VERSION | awk -F. '{ print $3 }')

if [ -z "$MAJOR" ]; then
    echo "No major version was defined in $SEMVER_VERSION"
    exit 1
fi

if [ -z "$MINOR" ]; then
    echo "No minor version was defined in $SEMVER_VERSION"
    exit 1
fi

if [ -z "$PATCH" ]; then
    echo "No patch version was defined in $SEMVER_VERSION"
    exit 1
fi


echo "##vso[task.setvariable variable=semver_version]$SEMVER_VERSION"
echo "##vso[task.setvariable variable=proj_major]$MAJOR"
echo "##vso[task.setvariable variable=proj_minor]$MINOR"
echo "##vso[task.setvariable variable=proj_patch]$PATCH"
