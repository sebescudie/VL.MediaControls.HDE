# VL.MediaControls.HDE

[![vvvv](https://img.shields.io/static/v1?label=MADE%20FOR&message=VVVV&color=191919&style=for-the-badge)](https://vvvv.org/) [![NuGet Version](https://img.shields.io/nuget/v/VL.MediaControls.HDE?style=for-the-badge&logo=nuget)](https://www.nuget.org/packages/VL.MediaControls.HDE)


Control your Windows media sessions without leavvvving your favorite editor

<p align="center">
	<img src="doc/capture.png" title="" alt="VL.MediaControl.HDE screenshot" width=1024>
</p>

## Features

Allows to control the playback of all media sessions registered on Windows from the vvvv editor. You can control streaming apps like Spotify, YouTube videos in your browser or any browser-based music streaming service.

## Installation

> [!IMPORTANT]  
> This plugin was developped with vvvv 8.0 and might not work on older versions

Go to the Quad Menu, select Settings, and click the _Extensions_ tab. There, search for _VL.MediaControls.HDE_ and click _Install_. The extension will now be available in the Quad Menu under _Windows_.

## Building the library

This project uses [Fallout](https://docs.fallout.build/) to automate the build and packing processes. You don't have to install Fallout though, you can `cd` in this repository and run the following commands:

### Compile the library

```
.\build.ps1 compile
```

### Compile and create packages

```
.\build.ps1 pack
```