# SmartX

A simulated IoT ingestion gateway and client-facing configuration dashboard for Smart-X — a hybrid IoT mesh ecosystem monitoring distributed environments such as hydroponic farms, automated real estate utility trackers, and smart grid installations.
Overview

Smart-X ingests high-throughput, multi-typed telemetry (floats for soil moisture, integers for power wattage, booleans for valve state) from thousands of simulated ESP32-style sensor nodes, validates and processes it through a .NET Web API, and surfaces it in real time through a Blazor WebAssembly dashboard with anomaly-aware, colour-coded alerting.

#Architecture

The solution is split into three projects:
SmartX.Shared,SmartX.Api,SmartX.Client

#Features

Startup interface — a landing page presenting three architectural pillars, only one of which is currently enabled.
API integration layer — an ASP.NET Core Minimal API backend, consumed exclusively by the Blazor client over HTTP and SignalR.
Sensor payload management — register a device by MAC address, sensor category, and a nested Facility → Zone → Node deployment location.
Media / log attachment — attach a configuration file, deployment photo, or hardware log to a sensor profile via multipart upload.
Dynamic engagement feature — real-time, colour-coded telemetry feed pushed over SignalR, with context-aware (baseline z-score) anomaly highlighting rather than a static progress bar.
Mock data seeding — a background service continuously seeds simulated telemetry, including occasional spikes and simulated sensor disconnects, so the pipeline can be demonstrated without physical hardware.

#Tech stack

.NET 10 (or .NET 8, if targeting the LTS release instead)
ASP.NET Core Minimal API
SignalR
Blazor WebAssembly (standalone)
