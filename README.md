# Smart-X Gateway

A simulated IoT ingestion gateway and real-time device management dashboard for **Smart-X**, a hybrid IoT mesh ecosystem that monitors distributed environments such as hydroponic farms, automated real estate utility trackers, and smart grid installations.

Built for a Portfolio of Evidence (PoE):

- **Part 1:** Sensor Data Ingestion and Telemetry (.NET architecture and API integration)
- **Part 2:** Real-Time Processing and Device Management (queues, dictionaries, sets, undo, and a predictive recommendation engine)

## Overview

Smart-X ingests multi-typed telemetry (floats for soil moisture, integers for power wattage, booleans for valve state) from simulated ESP32-style devices. Each device sends its readings to the .NET Web API over HTTP, identified only by its MAC address. The API routes every packet through a FIFO queue or, for critical alerts, a priority queue, then processes it: anomaly detection, history, alert de-duplication and a live SignalR broadcast to a Blazor WebAssembly dashboard.

## Architecture

```
SmartX/
  SmartX.sln
  SmartX.Shared/   Class library: shared models, generics, operator overloading, recursive tree types
  SmartX.API/      ASP.NET Core Minimal API: ingestion gateway, queues, device state, recommendation engine
  SmartX.Client/   Blazor WebAssembly: the client-facing dashboard
```

| Project | Responsibility |
|---|---|
| `SmartX.Shared` | `TelemetryPacket<T>`, `MeterReading`, `DeploymentNode`, device, alert and insight models |
| `SmartX.API` | Minimal API endpoints, SignalR hub, telemetry queues, device registry, alert tracker, undo stack, recommendation engine, background simulator |
| `SmartX.Client` | Landing page, sensor menu, live dashboard, sensor registration, meter aggregation, Real-Time Command Stream |

### How a reading travels through the API

```
Device (simulated)  --HTTP POST /api/telemetry/ingest-->  Dictionary lookup by MAC (O(1))
                                                              |
                                          critical reading?   |
                                       yes /               \ no
                          PriorityQueue (served first)    Queue (FIFO)
                                       \                   /
                                        TelemetryProcessingService
                                                  |
                  anomaly detection -> SortedDictionary history -> device state
                                                  |
                       HashSet alert check -> recommendation engine -> SignalR -> dashboard
```

## Navigation flow

```
Landing (/)
  |-- Sensor Data Ingestion & Telemetry     -> Sensor Menu (/sensor-menu)
  |      |-- Live Dashboard      (/dashboard)
  |      |-- Register Sensor     (/register)
  |      '-- Meter Aggregation   (/meters)
  |-- Real-Time Command Stream & History    -> /command-stream        (Part 2)
  '-- Network Topology & Mesh Routing       -> disabled, final PoE
```

The Command Stream page keeps its search text, filters, selected device and command log in a singleton `AppState` service, so moving between pages does not reset them.

## Features

### Part 1

- Landing page with three architectural pillars
- Sensor registration by MAC address, category and a nested Facility, Zone, Node location (validated recursively)
- File attachment (configuration file, photo or hardware log) against a sensor profile
- Live, colour-coded telemetry feed with baseline z-score anomaly detection
- Generics, operator overloading, jagged arrays and recursion (see the table below)

### Part 2

- Real-Time Command Stream page: live device list, queue statistics, active alerts
- Manual override commands (trigger pump, stop pump, toggle relay, shut down all valves) with an **Undo** button
- Search by MAC address or metric, plus filters for operational category and alert state
- Timeline chart per device, drawn from time-ordered history
- **Suggested actions** and **Automated insights** from a pattern-analysis engine

### Data structures used in Part 2

| Requirement | Structure | Where |
|---|---|---|
| FIFO message queue | `Queue<TelemetryPacket<float>>` | `TelemetryQueueService` |
| Priority queue for critical alerts | `PriorityQueue<TelemetryPacket<float>, (int, long)>` | `TelemetryQueueService`, `CriticalAlertClassifier` |
| Undo history for override commands | `Stack<UndoEntry>` | `CommandUndoService` |
| Live registered devices, keyed by MAC | `Dictionary<string, SensorProfile>` | `InMemorySensorRepository` |
| Historical logs ordered by timestamp | `SortedDictionary<DateTime, TelemetryPacket<float>>` | `TelemetryHistoryService` |
| Unique active alerts / disconnected nodes | `HashSet<(Guid, AnomalyStatus)>` | `AlertTracker` |
| Pattern memory and ranking | `Dictionary<string, Dictionary<string, int>>`, `PriorityQueue` | `RecommendationEngine` |

### Part 1 technical requirements

| Requirement | Implementation |
|---|---|
| **Generics** | `TelemetryPacket<T>` and `TelemetryPacketFactory.WrapBatches<T>` handle `float`, `int` and `bool` readings with no boxing |
| **Operator overloading** | `MeterReading` overloads `+`, `-`, `>`, `<` |
| **Jagged arrays / Collections** | `TelemetryBatchRequest.Batches` (`float[][]`) is flattened into a `List<TelemetryPacket<float>>` |
| **Recursion** | `DeploymentValidationService` validates a nested Facility, Zone, Node tree recursively |

## Tech stack

- .NET 10 (or .NET 8 if you target the LTS release)
- ASP.NET Core Minimal API, SignalR
- Blazor WebAssembly (standalone)

---

## Setup and running instructions

Two processes run at once, the API and the client, so use two terminals (or Visual Studio's multiple startup projects).

### 1. Prerequisites

- Install the [.NET SDK](https://dotnet.microsoft.com/download). The projects target **.NET 10**. Check with:
  ```bash
  dotnet --list-sdks
  ```
- If you only have .NET 8, change `<TargetFramework>net10.0</TargetFramework>` to `net8.0` in `SmartX.Shared.csproj`, `SmartX.API.csproj` and `SmartX.Client.csproj`, and set the Blazor/SignalR package versions in `SmartX.Client.csproj` to a current 8.0.x release.
- Visual Studio users need the **ASP.NET and web development** workload.

### 2. Clone the repository

```bash
git clone <your-repository-url>
cd SmartX
```

### 3. Restore dependencies

From the folder that contains `SmartX.sln`:

```bash
dotnet restore
```

(Visual Studio: right-click the solution, then **Restore NuGet Packages**.)

### 4. Compile the project

```bash
dotnet build
```

You should see `Build succeeded` with `0 Error(s)`. (Visual Studio: **Build, Rebuild Solution**.)

### 5. Boot the backend API

```bash
cd SmartX.API
dotnet run
```

Wait for `Now listening on: http://localhost:5189`. The API seeds three demo devices and, three seconds later, the simulator starts sending them readings over HTTP. Leave this terminal running.

### 6. Run the client user interface

In a second terminal:

```bash
cd SmartX.Client
dotnet run
```

Open the URL it prints (for example `http://localhost:5122`).

**Visual Studio:** right-click the solution, **Configure Startup Projects**, choose **Multiple startup projects**, set `SmartX.API` and `SmartX.Client` to **Start** (leave `SmartX.Shared` as **None**), then press F5.

### 7. Quick check

| Page | Route | Expected result |
|---|---|---|
| Landing | `/` | Two enabled pillars and one disabled |
| Sensor menu | `/sensor-menu` | Live Dashboard, Register Sensor, Meter Aggregation |
| Live Dashboard | `/dashboard` | Status flips to Connected and rows stream in |
| Command Stream | `/command-stream` | Queue counters, device list, suggestions panel |

---

## Testing the queue and dictionary implementations

These tests drive the API directly, then confirm the result on the Command Stream page (`/command-stream`). Keep that page open in a browser while you test.

### Before you start

**1. Know the seeded devices.** The API registers these at startup:

| MAC address | Category | Metric | Normal range |
|---|---|---|---|
| `AA:BB:CC:DD:EE:01` | Environmental | Soil Moisture (%) | about 42 |
| `AA:BB:CC:DD:EE:02` | PowerConsumption | Power Draw (W) | about 230 |
| `AA:BB:CC:DD:EE:03` | Actuator | Valve Position (%) | about 50 |

**2. Know what counts as high priority.** A reading goes to the priority queue when it crosses these thresholds (`CriticalAlertClassifier.cs`):

| Device type | Critical (level 1) | Severe (level 0, served first) |
|---|---|---|
| Soil moisture | below 20 % | below 10 % |
| Power draw | above 320 W | above 380 W |

Disconnect messages and actuator readings never use the priority queue.

**3. Pick a way to send requests.** Any one of these works:

- **Visual Studio (easiest):** open `SmartX.API/SmartX.API.http`. Each request has a green **Send request** arrow. The numbered requests below match the numbers in that file.
- **PowerShell:** the snippets below.
- **curl:** `curl.exe` on Windows 10 or later, for example:
  ```bash
  curl.exe -X POST http://localhost:5189/api/telemetry/ingest -H "Content-Type: application/json" -d "{\"macAddress\":\"AA:BB:CC:DD:EE:01\",\"metricName\":\"Soil Moisture\",\"unit\":\"%\",\"value\":12,\"disconnected\":false}"
  ```

**4. Set up PowerShell once** (paste this in the window you will use for the tests):

```powershell
$uri = "http://localhost:5189/api/telemetry/ingest"

function Send-Reading($mac, $metric, $unit, $value, $disconnected = $false) {
    $body = @{ macAddress = $mac; metricName = $metric; unit = $unit; value = $value; disconnected = $disconnected } | ConvertTo-Json
    Invoke-RestMethod -Method Post -Uri $uri -ContentType "application/json" -Body $body
}
```

> The simulator also sends a reading for every device every 3 seconds, and it clears and raises alerts of its own. Send test requests back to back, and if a count looks off by one or two, repeat the test.

---

### Test 1: Simulate a high-priority alert

**Goal:** prove a critical reading is routed to the priority queue.

1. **Clear any existing spike alert.** Send a normal reading first. Otherwise the new alert is treated as a duplicate and no new alert is raised.
   ```powershell
   Send-Reading "AA:BB:CC:DD:EE:01" "Soil Moisture" "%" 42
   ```
   Expected: `queued: True`, `priority: False`.

2. **Note the counters.** Open `http://localhost:5189/api/telemetry/queue-stats` in a browser and write down `processedPriority`.

3. **Send the high-priority alert.** A critical moisture drop (12 is below 20):
   ```powershell
   Send-Reading "AA:BB:CC:DD:EE:01" "Soil Moisture" "%" 12
   ```
   In Visual Studio, send request 3 in the `.http` file.

   Expected response (HTTP 202):
   ```json
   { "queued": true, "priority": true }
   ```
   `priority: true` is the key result: the packet bypassed the standard queue.

4. **Check the counters again.** `processedPriority` is now one higher. `priorityDepth` is back to 0, because the processor drains the queue within about 50 ms.

5. **Check the dashboard** (`/command-stream`):
   - The **Priority queue** tile's processed count went up.
   - **Active alerts** lists `AA:BB:CC:DD:EE:01: CriticalLow (Soil Moisture 12.0%)`.
   - The device row turns amber and shows `Spike`.
   - **Suggested actions** shows `Trigger pump on AA:BB:CC:DD:EE:03` (see Test 8).

**Other ways to raise a high-priority alert:**

| Alert | Command |
|---|---|
| Severe moisture drop | `Send-Reading "AA:BB:CC:DD:EE:01" "Soil Moisture" "%" 6` |
| Critical power spike | `Send-Reading "AA:BB:CC:DD:EE:02" "Power Draw" "W" 350` |
| Severe power spike | `Send-Reading "AA:BB:CC:DD:EE:02" "Power Draw" "W" 395` |
| Wait for the simulator | It starts a critical episode roughly once every 75 seconds per sensor. Watch **Processed (standard / priority)** climb by itself |

---

### Test 2: Standard FIFO queue

**Goal:** prove ordinary readings use the standard queue.

1. Send a normal reading:
   ```powershell
   Send-Reading "AA:BB:CC:DD:EE:03" "Valve Position" "%" 50
   ```
   Expected: `priority: False`.
2. Check `queue-stats`: `processedStandard` is higher, and `processedPriority` did not change.
3. The simulator alone sends about one reading per device every 3 seconds. Refresh `queue-stats` a few times and `processedStandard` keeps growing while `processedPriority` rises only occasionally.

Queue depths are normally 0 because the processor keeps up. Test 3 builds a backlog so you can see the queues fill.

---

### Test 3: The priority alert jumps the queue under load

**Goal:** show a critical packet is processed ahead of readings that were already waiting.

1. **Slow the processor down for the test.** Add this to `SmartX.API/appsettings.Development.json` (merge it into the existing JSON object):
   ```json
   {
     "Testing": {
       "ProcessingDelayMs": 300
     }
   }
   ```
2. Restart the API. The console shows a warning: `Testing mode: each packet is delayed by 300 ms.`
3. Run this in PowerShell:
   ```powershell
   # Flood the standard queue with 25 routine readings
   1..25 | ForEach-Object { Send-Reading "AA:BB:CC:DD:EE:03" "Valve Position" "%" 50 | Out-Null }

   # Immediately send one critical reading
   Send-Reading "AA:BB:CC:DD:EE:01" "Soil Moisture" "%" 8

   # Look at the queues straight away, then a second later
   Invoke-RestMethod http://localhost:5189/api/telemetry/queue-stats
   Start-Sleep -Seconds 1
   Invoke-RestMethod http://localhost:5189/api/telemetry/queue-stats
   ```
4. **Expected result:**

   | Reading | `standardDepth` | `priorityDepth` | `processedPriority` |
   |---|---|---|---|
   | First | a large backlog (roughly 15 to 25) | 0 or 1 | about to rise |
   | Second (1 s later) | still a backlog | **0** | **one higher** |

   The critical packet was processed while most of the 25 routine readings were still waiting. That is the priority bypass.
5. **Remove the `Testing` setting and restart the API.** Leaving it in makes the dashboard lag.

---

### Test 4: Dictionary lookup by MAC address

**Goal:** show devices are resolved from their MAC through a `Dictionary<string, SensorProfile>`.

1. **Registered MAC:**
   ```powershell
   Send-Reading "AA:BB:CC:DD:EE:01" "Soil Moisture" "%" 42
   ```
   Expected: HTTP 202.
2. **Unregistered MAC.** The lookup finds nothing, so the API rejects the packet:
   ```powershell
   Send-Reading "FF:FF:FF:FF:FF:FF" "Soil Moisture" "%" 40
   ```
   Expected: HTTP 404 with `No registered device with MAC FF:FF:FF:FF:FF:FF.` (In Visual Studio, request 8.) PowerShell shows a 404 as a red error. That is the expected result here.
3. **Case-insensitive keys.** The dictionary ignores case:
   ```powershell
   Send-Reading "aa:bb:cc:dd:ee:01" "Soil Moisture" "%" 42
   ```
   Expected: HTTP 202.
4. **Register a new device and watch it become known.**
   1. Send a reading for `AA:BB:CC:DD:EE:99`. It returns 404.
   2. Open `/register`, enter MAC `AA:BB:CC:DD:EE:99`, category Environmental, Facility A, Zone 1, Sub-Zone B, then **Register Sensor**.
   3. Send the same reading again. Now it returns 202.
   4. The device appears on `/command-stream` within a few seconds. The simulator picks up every registered device, so it will also start generating readings for it.
5. **Duplicate protection.** Register `AA:BB:CC:DD:EE:99` a second time. The form shows `A sensor with MAC AA:BB:CC:DD:EE:99 is already registered.`

The lookup is `InMemorySensorRepository.GetByMac`, a single `TryGetValue` call. Its cost does not grow as more devices are registered.

---

### Test 5: HashSet blocks duplicate disconnect alerts

**Goal:** show a device that stays offline raises one alert, not one per message.

1. Note `duplicatesSuppressed` from `http://localhost:5189/api/alerts/active`. Call it **N**.
2. Send three disconnect messages back to back (Visual Studio: request 10, three times):
   ```powershell
   1..3 | ForEach-Object { Send-Reading "AA:BB:CC:DD:EE:02" "Power Draw" "W" 0 $true | Out-Null }
   ```
3. Call `/api/alerts/active` again. **Expected:**
   - `active` contains exactly **one** entry: `AA:BB:CC:DD:EE:02 went offline`.
   - `duplicatesSuppressed` is **N + 2**. The first message created the alert and the other two were rejected by the set.
4. **Recovery clears the alert.** Send a healthy reading:
   ```powershell
   Send-Reading "AA:BB:CC:DD:EE:02" "Power Draw" "W" 230
   ```
   The `went offline` entry disappears from `active`. A later disconnect can raise a fresh alert.

The **Active alerts** and **Duplicate alerts suppressed** tiles on `/command-stream` show the same numbers.

---

### Test 6: Stack-based Undo

**Goal:** show commands are reverted in last-in, first-out order.

On `/command-stream`, find the row for `AA:BB:CC:DD:EE:03` (Actuator):

1. Click **Trigger pump**. After about half a second the badge reads `ON`.
2. Click **Stop pump**. The badge reads `OFF`.
3. Click **Toggle relay**. The badge reads `ON`.
4. The bar shows `Next undo: Toggle relay on AA:BB:CC:DD:EE:03 (3 on the stack)`. The depth can be higher if you ran commands earlier.
5. Click **Undo last command**. The badge returns to `OFF` and the log shows `Undid: Toggle relay on ...`.
6. Click it again to undo **Stop pump** (back to `ON`), then again to undo **Trigger pump** (back to `OFF`). The order is the reverse of what you did.
7. **Bulk undo.** Click **Trigger pump**, then **Shut down all valves** (the badge goes `OFF`), then **Undo last command**. The pump returns to `ON`. One undo entry restored every valve it had changed.
8. When the stack is empty, the Undo button is disabled and the bar reads `Nothing to undo`.

If a command reports `... is offline.`, the simulator has briefly disconnected that device. Wait a few seconds and try again. Failed commands are not added to the stack.

**API version** (Visual Studio requests 13 to 16; paste the pump's `sensorId` from request 1 into `@pumpId`):
`commandType` values are `0` TriggerPump, `1` ToggleRelay, `4` StopPump, `5` ShutdownAllValves.

---

### Test 7: SortedDictionary keeps history in time order

**Goal:** show the history is ordered by timestamp.

1. Click any device row on `/command-stream`. A timeline chart appears below the list. Amber dots are anomalies. A gap in the line with a red dot is a disconnect.
2. Check the API directly. Get a device's `sensorId` from `http://localhost:5189/api/devices`, then:
   ```powershell
   $id = "PASTE-SENSOR-ID-HERE"
   $t = Invoke-RestMethod "http://localhost:5189/api/devices/$id/timeline?take=50"
   $times = $t | ForEach-Object { [datetime]$_.timestamp }
   $ok = $true
   for ($i = 1; $i -lt $times.Count; $i++) { if ($times[$i] -le $times[$i-1]) { $ok = $false } }
   $ok
   ```
   Expected: `True`. Every timestamp is later than the one before it, even though readings came from the simulator and from your manual requests.

---

### Test 8: Suggested actions and Automated insights

**Goal:** show the recommendation engine proposes what operators usually do after an alert.

**Suggested actions**

1. Send a normal reading, then a critical moisture drop (Test 1, steps 1 and 3).
2. Within a couple of seconds the **Suggested actions** panel shows:
   - `Trigger pump on AA:BB:CC:DD:EE:03`, with the reason `After AA:BB:CC:DD:EE:01 reported CriticalLow, operators did this 8 of 12 times` and about **67% confidence**
   - `Search for "aa:bb:cc:dd:ee:03"`, about **33% confidence**

   Numbers can be a little higher if you ran commands shortly after earlier alerts. The engine keeps learning from your clicks.
3. Click **Run** on the pump suggestion. The pump turns on and the suggestion disappears, because you have done it since the alert.
4. Repeat step 1. The same suggestion returns with a higher count and confidence, about 69% (9 of 13). The engine learned from you.
5. **Power spike pattern.** Send a normal power reading (`230`), then `Send-Reading "AA:BB:CC:DD:EE:02" "Power Draw" "W" 350`. Expect `Toggle relay on AA:BB:CC:DD:EE:03` (about 62%) and `Shut down all valves` (about 38%).
6. **Search learning.** Type `aa:bb:cc:dd:ee:03` into **Search devices** and press Enter within two minutes of an alert. That search is counted against the alert as well.

You can also read the raw result at `http://localhost:5189/api/insights`.

**Automated insights**

1. Send three critical readings for one device in quick succession:
   ```powershell
   1..3 | ForEach-Object { Send-Reading "AA:BB:CC:DD:EE:01" "Soil Moisture" "%" 10 | Out-Null }
   ```
2. **Automated insights** lists `AA:BB:CC:DD:EE:01` with `3 anomalies in the last 5 minutes. Worth inspecting.` A device needs at least three anomalies in five minutes to be flagged. Click **Inspect** to open its timeline.

---

### Expected results at a glance

| Test | What you do | Expected |
|---|---|---|
| 1 | Moisture below 20, or power above 320 | `priority: true`, `processedPriority` +1, alert on the dashboard |
| 2 | Normal reading | `priority: false`, `processedStandard` +1 |
| 3 | 25 routine readings, then 1 critical (with the test delay) | Critical processed first, standard backlog remains |
| 4 | Unknown / known / lowercase MAC | 404 / 202 / 202 |
| 5 | Three disconnect messages | One alert, `duplicatesSuppressed` +2 |
| 6 | Trigger, Stop, Toggle, then Undo three times | State reverts in reverse order |
| 7 | Read `/timeline` | Timestamps strictly ascending |
| 8 | Critical moisture alert | Pump suggestion at about 67% |

---

## Configuration reference

| Setting | File | Purpose |
|---|---|---|
| API address | `SmartX.API/Properties/launchSettings.json` | Where the API listens (`http://localhost:5189`) |
| `ApiBaseUrl` | `SmartX.Client/wwwroot/appsettings.json` | Where the client finds the API. Must match the API address |
| `Simulator:ApiBaseUrl` | `SmartX.API/appsettings.json` (optional) | Where simulated devices send readings. Defaults to `http://localhost:5189/` |
| `Testing:ProcessingDelayMs` | `SmartX.API/appsettings.Development.json` (optional) | Delay per packet, used only for Test 3. Leave unset normally |

## API reference

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/menu` | Landing-page pillars |
| `GET` | `/api/sensors` | Registered sensors |
| `GET` | `/api/sensors/{id}` | One sensor |
| `POST` | `/api/sensors` | Register a sensor (rejects a duplicate MAC) |
| `POST` | `/api/sensors/{id}/attachments` | Upload a file for a sensor |
| `POST` | `/api/telemetry/ingest` | **Device endpoint.** Send a reading identified by MAC |
| `POST` | `/api/telemetry/float-batch` | Part 1 jagged-array batch (now uses the same queues) |
| `POST` | `/api/telemetry/int-batch`, `/bool-batch` | Generic batch demos |
| `GET` | `/api/telemetry/queue-stats` | Queue depths and processed counters |
| `GET` | `/api/telemetry/{sensorId}/history` | A sensor's history |
| `GET` | `/api/meters/aggregate` | Operator-overloading demo |
| `GET` | `/api/devices` | Latest state of every device |
| `GET` | `/api/devices/search?text=&category=&alertState=` | Search and filter devices |
| `GET` | `/api/devices/{sensorId}/timeline?take=` | Time-ordered readings for a chart |
| `GET` | `/api/devices/{sensorId}/history` | A device's command history |
| `POST` | `/api/devices/command` | Queue a manual override command |
| `POST` | `/api/commands/undo` | Undo the last command |
| `GET` | `/api/commands/undo-preview` | What Undo would revert next |
| `GET` | `/api/alerts/active` | Active alerts and duplicates suppressed |
| `GET` | `/api/insights` | Suggested actions and problem devices |
| `POST` | `/api/insights/search` | Record a search for pattern analysis |
| `WS` | `/telemetryHub` | SignalR hub for live telemetry, alerts and command results |

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `dotnet restore` or `build` fails on SDK version | Installed SDK does not match `TargetFramework` | Use the .NET 8 steps in Setup |
| Dashboard stuck on "Connecting..." | API not running, or `ApiBaseUrl` does not match its port | Start the API and check `wwwroot/appsettings.json` |
| API console shows "could not reach the gateway" | Simulator cannot call the API | Make sure the API listens on `http://localhost:5189`, or set `Simulator:ApiBaseUrl` |
| Ingest returns 404 | The MAC is not registered | Use a seeded MAC or register the device first |
| Ingest returns 415 | Missing JSON content type | Add `Content-Type: application/json` |
| Critical reading raises no new alert or suggestion | A spike alert for that device is already active | Send a normal reading first (Test 1, step 1) |
| Queue depths always show 0 | Normal: the processor drains quickly | Use the test delay in Test 3 |
| A command says `... is offline.` | The simulator briefly disconnected the device | Wait a few seconds and retry |
| Pages have no styling | `app.css` missing or cached | Confirm the file path in `index.html`, then press Ctrl+Shift+R |
| "A project with an Output Type of Class Library cannot be started directly" | `SmartX.Shared` is a startup project | Start only `SmartX.API` and `SmartX.Client` |

## Project status

A simulated PoE submission. Data lives in memory and resets whenever `SmartX.API` restarts. Network Topology and Mesh Routing is reserved for the final PoE.

## License

Educational project. No license specified.
