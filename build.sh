#!/bin/bash

nuget restore
msbuild /p:Platform="x64" /p:Configuration=Release
