# MikeNakis.Console<br><sub><sup>A small library for console applications</sub></sup>

[![Build](https://github.com/mikenakis/MikeNakis.Console/actions/workflows/Console-build-and-test-on-push.yml/badge.svg)](https://github.com/mikenakis/MikeNakis.Console/actions/workflows/Console-build-and-test-on-push.yml)
[![Build](https://github.com/mikenakis/MikeNakis.Console/actions/workflows/Console-publish-to-nuget-org.yml/badge.svg)](https://github.com/mikenakis/MikeNakis.Console/actions/workflows/Console-publish-to-nuget-org.yml)

Note: as it stands, this library will cause `System.Drawing.Common.dll` and probably also `Microsoft.Win32.SystemEvents.dll` to be included in any project that uses this library. This is bad, and I should do whatever it takes to prevent this from happening.
