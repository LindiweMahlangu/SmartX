# Smart-X Gateway

A simulated IoT ingestion gateway and real-time device management dashboard for **Smart-X**, a hybrid IoT mesh ecosystem that monitors distributed environments such as hydroponic farms, automated real estate utility trackers, and smart grid installations.

Built for a Portfolio of Evidence (PoE):

- **Part 1:** Sensor Data Ingestion and Telemetry (.NET architecture and API integration)
- **Part 2:** Real-Time Processing and Device Management (queues, dictionaries, sets, undo, and a predictive recommendation engine)
- **Part 3:** Network Topology and Mesh Routing (graphs, shortest paths, minimum spanning tree, heaps and an AVL tree for historical queries)

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
  '-- Network Topology & Mesh Routing       -> /network-topology     (Part 3)
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

### Part 3

- Network Topology page: an SVG mesh of the gateway, four relays and the sensors, with link costs
- Cheapest route (Dijkstra) and fewest-hops route (BFS) to any sensor, chosen by MAC address
- Minimum spanning tree overlay and its total cost
- **Simulate failure** and **Restore** buttons for any node, with automatic rerouting (self-healing)
- Low-battery watchlist built on a min-heap
- Historical range query over a time window, answered by an AVL tree (From/To, row limit, order, quick ranges)
- New sensors registered on the Register page join the mesh automatically
- **Predicted failures and recommended actions:** battery time-to-empty (ranked by a min-heap) and what-if relay failures
- Active pillar highlighted in the top navigation, and a banner when the API cannot be reached
- Sensor validation, a registered-sensors list, and download links for attached files

### Data structures used in Part 3

| Requirement | Structure | Where |
|---|---|---|
| Mesh network | Weighted undirected graph (adjacency list) | `MeshGraph` |
| Cheapest route | Dijkstra with a binary min-heap | `MeshGraph`, `BinaryMinHeap` |
| Fewest hops, reachability | Breadth-first search | `MeshGraph` |
| Minimum spanning tree | Kruskal with union-find | `MeshGraph`, `DisjointSet` |
| Low-battery watchlist | Binary min-heap | `MeshTopologyService` |
| Historical readings by time | AVL tree (range query, count in O(log n)) | `AvlTree`, `TelemetryArchive` |

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
| Landing | `/` | Three enabled pillars |
| Sensor menu | `/sensor-menu` | Live Dashboard, Register Sensor, Meter Aggregation |
| Live Dashboard | `/dashboard` | Status flips to Connected and rows stream in |
| Command Stream | `/command-stream` | Queue counters, device list, suggestions panel |
| Network Topology | `/network-topology` | Mesh drawn with 8 nodes, route panel, history query |

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

## Testing the network topology, mesh routing and historical queries

These tests use the Network Topology page (`/network-topology`) and a few API calls. Keep the page open in a browser while you test.

### Before you start

**1. Know the seeded mesh.** The API builds this weighted, undirected graph at startup (cost = link weight, lower is better):

| Link | Cost | Link | Cost |
|---|---|---|---|
| Gateway to Relay 1 | 2 | Relay 3 to Relay 4 | 4 |
| Gateway to Relay 2 | 5 | Relay 3 to Node A | 2 |
| Relay 1 to Relay 2 | 2 | Relay 4 to Node B | 2 |
| Relay 1 to Relay 3 | 3 | Relay 4 to Node C | 3 |
| Relay 2 to Relay 4 | 3 | Node A to Node B | 4 |
| Relay 3 to Node B | 6 | Node B to Node C | 5 |

The three sensors appear on the page as **Node A**, **Node B** and **Node C**. The node dropdown lists them by MAC address.

**2. Know which structure does what.**

| Feature on the page | Structure | Where |
|---|---|---|
| Cheapest route | Dijkstra over an adjacency-list graph, driven by a binary min-heap | `MeshGraph`, `BinaryMinHeap` |
| Fewest hops | Breadth-first search | `MeshGraph` |
| Minimum spanning tree | Kruskal with a disjoint set (union-find) | `MeshGraph`, `DisjointSet` |
| Low-battery watchlist | Binary min-heap keyed by battery level | `MeshTopologyService`, `BinaryMinHeap` |
| Historical range query | Self-balancing AVL tree keyed by timestamp | `AvlTree`, `TelemetryArchive` |

**3. Pick a way to send requests** (only needed for Tests 7 to 9). The API address is `http://localhost:5189`. Any browser can do the `GET` calls. For `POST` calls use PowerShell:

```powershell
$api = "http://localhost:5189"
Invoke-RestMethod -Method Post "$api/api/mesh/fail?mac=RELAY-MAC"
```

You can also use the buttons on the page instead of the `POST` calls.

> The simulator keeps running while you test. It can disconnect a sensor, battery percentages drift, and new history readings arrive every minute. If a number is off, check whether a sensor is red (offline) and repeat the test. The costs below assume all three sensors are online.

---

### Test 1: The mesh renders

**Goal:** confirm the graph and the spanning tree are built.

1. Open `/network-topology`.
2. Check the metric tiles: **Nodes online** `8 / 8` and **Cut-off nodes** `0`.
3. Tick **Show minimum spanning tree**. The tree links turn blue and **Spanning tree cost** shows `17`.

Expected: eight nodes (Gateway, Relay 1 to 4, Node A, B, C), and a spanning tree with seven links costing 17 in total.

---

### Test 2: Route tracing by MAC address

**Goal:** prove the page finds the cheapest route (Dijkstra) and the fewest-hops route (BFS).

1. Pick each sensor in the node dropdown. The green line shows the route and the **Route** panel lists both answers.

| Sensor | Cheapest route (cost) | Fewest hops |
|---|---|---|
| Node A | Gateway, Relay 1, Relay 3, Node A (**7**) | Same path |
| Node B | Gateway, Relay 1, Relay 2, Relay 4, Node B (**9**, 4 hops) | Gateway, Relay 1, Relay 3, Node B (**10**, 3 hops) |
| Node C | Gateway, Relay 1, Relay 2, Relay 4, Node C (**10**) | Gateway, Relay 1, Relay 3, Relay 4, Node C (**11**) |

2. The **Selected route cost** tile matches the cheapest cost.

Expected: for Node B the two methods disagree. The cheapest route takes more hops but costs less. That difference shows why both algorithms are included.

---

### Test 3: Self-healing when a relay fails

**Goal:** prove traffic is rerouted around a failed node.

1. Select **Node B**, then click **Simulate failure of Relay 2**. The relay turns red and its links turn dashed red.
   Expected: Node B's cheapest route is now Gateway, Relay 1, Relay 3, Relay 4, Node B with cost **11**. Node C becomes **12**. **Spanning tree cost** drops to **16**.
2. Click **Restore Relay 2**. Node B returns to **9**.
3. Now fail **Relay 1** only.
   Expected: Node A is rerouted Gateway, Relay 2, Relay 4, Relay 3, Node A with cost **14**. The spanning tree cost is **19**.
4. Fail **Relay 2** as well, so both Relay 1 and Relay 2 are down.
   Expected: the Gateway has no working link. All three sensors turn amber (cut off), **Cut-off nodes** rises to `3`, and the Route panel says there is no working path.
5. Restore both relays. Everything returns to the Test 2 values.

> **Restore** also resets that node's battery level, which matters for Test 5.

---

### Test 4: An offline sensor drops out of the mesh

**Goal:** prove a sensor the simulator disconnects is excluded from routing and the spanning tree.

1. Watch the mesh and the Command Stream page. The simulator disconnects a sensor now and then (it turns red on the mesh).
2. When a sensor is red, its route panel reports no route, **Nodes online** falls and the spanning tree is rebuilt without it. For example, with Node C offline the tree costs **14**.
3. When the sensor reconnects it rejoins the tree and the route returns.

Expected: an offline node is never used as a hop or a destination.

---

### Test 5: Low-battery watchlist (min-heap)

**Goal:** prove the heap returns the weakest batteries first.

1. Read the **Low-battery watchlist** panel. It lists the three nodes with the lowest battery, lowest first.
2. Wait a minute. Percentages drain slowly and the order can change.
3. Click **Restore** on the lowest node. Its battery resets and it leaves the top three.

Expected: always exactly three entries, sorted from lowest to highest.

---

### Test 6: Historical range query (AVL tree)

**Goal:** prove a time-range query over a large history reads only a few tree nodes.

1. Scroll to the history query. Choose a sensor in the node dropdown.
2. Click **14:00 to 16:00** (the page uses today's window if it has finished, otherwise yesterday's).
3. Click **Run query**.

Expected:

- **Readings in range** is about **120** (one reading per minute for two hours).
- **Showing** is the lower of that count and the **Rows** setting (50, 100, 200 or 500).
- **Lowest**, **Highest** and **Average** are filled in and the lowest is no greater than the average, which is no greater than the highest.
- The line under the tiles reads like: *The AVL tree holds about 1,440 readings (height about 11). This query touched only N nodes.* N is a small number, far below 1,440.

4. Change **Order** between oldest first and newest first. The rows reverse, and the count and tiles stay the same.
5. Try **Last hour**, **Last 2h**, **Last 6h** and **Last 24h**. The count grows with the window. A new reading arrives each minute.
6. Set a window with no readings (for example, From later than To, or a time in the future). Expected: zero readings and no rows, with no error.

---

### Test 7: API checks for history, route and topology

**Goal:** confirm the same answers come back from the API.

1. **History.** Open in a browser (replace the MAC and the times with your own, in UTC):
   `http://localhost:5189/api/mesh/history?mac=AA:BB:CC:DD:EE:01&fromUtc=2026-01-01T00:00:00Z&toUtc=2026-12-31T23:59:59Z&take=5&newestFirst=true`
   Expected: five readings, newest first, with a `total` that matches the count on the page for the same window.
2. **Route.** `http://localhost:5189/api/mesh/route?mac=AA:BB:CC:DD:EE:01`
   Expected: a cheapest path with total cost, and a fewest-hops path.
3. **Topology.** `http://localhost:5189/api/mesh/topology`
   Expected: nodes, links, `spanningTree` and the low-battery watchlist.

---

### Test 8: Benchmark, AVL tree against a full scan

**Goal:** show the tree does far less work than scanning every reading.

1. Open `http://localhost:5189/api/mesh/archive-benchmark?readings=200000`.
   The call fills a temporary tree with 200,000 readings and runs the same range query both ways.

Expected:

| Field | Value |
|---|---|
| `matches` | 2,401 |
| `sameResultAsFullScan` | `true` |
| `nodesVisitedByTree` | about 2,400 |
| `readingsCheckedByScan` | 200,000 |
| tree height | close to the best possible (18) and under the allowed 25 |

Timings (milliseconds) vary from machine to machine, so compare the counts, not the times. The `readings` value is clamped between 50,000 and 1,000,000.

---

### Test 9: A new sensor joins the mesh

**Goal:** prove registering a sensor adds it to the graph.

1. Open `/register` and register a new sensor with a new MAC address (for example `AA:BB:CC:DD:EE:09`).
2. Return to `/network-topology`.

Expected:

- The new node appears in a row at the bottom, linked to **Relay 4** (cost 3) and **Relay 2** (cost 5).
- Its cheapest route is Gateway, Relay 1, Relay 2, then the sensor, cost **9**. Fewest hops cost is **10**.
- **Spanning tree cost** becomes **20**.
- Its history is empty at first, because only live readings are stored for new sensors.

---

### Test 10: Page state is remembered

**Goal:** prove the selection survives navigation.

1. On the Network Topology page, choose Node B and tick **Show minimum spanning tree**.
2. Click **Back**, then open **Network Topology** again.

Expected: Node B is still selected and the spanning tree is still shown.

---

### Test 11: Active pillar in the navigation bar

**Goal:** prove the top navigation shows which pillar you are in.

1. Open `/`. **Home** is highlighted.
2. Open **Sensor Data Ingestion & Telemetry**, then **Live Dashboard**, **Register Sensor**, **Registered Sensors** and **Meter Aggregation**.
   Expected: the **Sensor Data Ingestion & Telemetry** pill stays highlighted on every one of those pages.
3. Open **Real-Time Command Stream & History**, then **Network Topology & Mesh Routing**.
   Expected: only the matching pill is highlighted each time.

---

### Test 12: Clear message when the API cannot be reached

**Goal:** prove a wrong address or a stopped API is never silent.

1. Stop `SmartX.API` (leave the client running).
2. Wait up to 10 seconds, or click **Retry**.
   Expected: a red banner reads *Cannot reach the Smart-X API at http://localhost:5189/*, with a hint to check `ApiBaseUrl`.
3. Start `SmartX.API` again and click **Retry**. The banner disappears.

**Run it on https:** start both projects with the **https** launch profile (`https://localhost:7189` for the API, `https://localhost:7122` for the client). With `ApiBaseUrl` left empty in `wwwroot/appsettings.json`, the client calls `https://localhost:7189/` automatically. The dashboard, the Command Stream hub and the simulated devices all keep working.

> If Chrome warns about the certificate, run `dotnet dev-certs https --trust` once and restart the browser.

---

### Test 13: Sensor validation

**Goal:** prove a bad sensor is refused with a clear reason.

1. Open `/register`. Enter `hello` as the MAC address and submit.
   Expected: a red message *"hello" is not a valid MAC address. Use six hex pairs...*
2. Try `AA:BB:CC:00:00:00`. Expected: *That MAC address is reserved for the gateway hub.*
3. Try `FF:FF:FF:FF:FF:FF`. Expected: the broadcast address is refused.
4. Try `aa-bb-cc-dd-ee-09`. Expected: it is accepted and stored as `AA:BB:CC:DD:EE:09`.
5. Register `AA:BB:CC:DD:EE:09` again. Expected: *A sensor with MAC ... is already registered.*

---

### Test 14: Attached files can be listed and downloaded

**Goal:** prove an uploaded log file can be retrieved again.

1. On `/register`, register `AA:BB:CC:DD:EE:0A` and attach a small `.txt` or `.log` file.
   Expected: a green message with a link **View all registered sensors**.
2. Open `/sensors`. The new row shows the file name and size under **Attached files**.
3. Click the file name. The browser downloads it, and the contents match what you uploaded.
4. Try attaching a `.exe` file. Expected: the sensor is registered but a yellow message says *Files of type ".exe" are not accepted*.
5. API check: `http://localhost:5189/api/sensors/{id}/attachments` lists the file (copy the `id` from `/api/sensors`).

---

### Test 15: Registered sensors list

**Goal:** prove every sensor is visible in the UI.

1. Open `/sensors`. The three seeded sensors (`...:01`, `...:02`, `...:03`) and anything you registered are listed with category, location path (Facility > Zone > Node) and registration time.
2. Type `power` in the filter. Only the power sensor remains. Clear the filter and click **Refresh**.

---

### Test 16: Predicted failures and recommended actions

**Goal:** prove the page forecasts failures and tests what-if scenarios.

1. Open `/network-topology` and scroll to **Predicted failures and recommended actions**.
2. Check the suggestion list. Right after startup the first item reads *Plan to replace the battery on Relay 4 (about 3 h 12 min left) and other sensors would pay 8 more per route.*
3. Check the table. These values assume only the three seeded sensors are registered and all are online (extra sensors change the cut-off and cost figures):

| Relay | Sensors cut off if it fails | Extra route cost if it fails |
|---|---|---|
| Relay 1 | 0 | 9 |
| Relay 2 | 0 | 4 |
| Relay 3 | 0 | 6 |
| Relay 4 | 0 | 8 |

4. The line under the table reads *4 of 6 relay pairs would cut off every sensor if both failed.*
5. Click **Simulate failure of Relay 1** above. The suggestion list adds *Relay 1 is switched off. Restore it...* within 6 seconds.
6. Wait a few minutes. A relay under 60 minutes of battery turns **critical** (red).

Time-left values shrink as the minutes pass, so use the first-start values above.

---

### Part 3 expected results at a glance

| Test | What you do | Expected |
|---|---|---|
| 1 | Open the page | 8 / 8 nodes online, spanning tree cost 17 |
| 2 | Pick Node A, B, C | Cheapest costs 7, 9, 10. Node B fewest hops costs 10 |
| 3 | Fail Relay 2 | Node B 11, Node C 12, tree 16 |
| 3 | Fail Relay 1 only | Node A 14, tree 19 |
| 3 | Fail Relay 1 and 2 | All sensors cut off (amber) |
| 4 | A sensor goes offline | No route to it, tree rebuilt without it |
| 5 | Read the watchlist | Three lowest batteries, ascending |
| 6 | 14:00 to 16:00 query | About 120 readings, tree touched only a few nodes |
| 7 | Call the `/api/mesh/*` routes | Same data as the page |
| 8 | Run the benchmark | 2,401 matches, `sameResultAsFullScan: true`, about 2,400 nodes against 200,000 scanned |
| 9 | Register a new sensor | Linked to Relay 4 and Relay 2, route cost 9, tree 20 |
| 10 | Leave and return | Selection and tree toggle kept |
| 11 | Move between pages | The matching pillar is highlighted everywhere |
| 12 | Stop the API, or run on https | Red banner with the address, then it clears. https works end to end |
| 13 | Bad MAC, gateway MAC, broadcast MAC | Clear error each time. Lower-case input stored in upper case |
| 14 | Upload a log file, then open `/sensors` | File listed and downloads back unchanged. `.exe` refused |
| 15 | Open `/sensors` | All sensors listed, filter works |
| 16 | Open the insights panel | Relay 1 to 4 extra cost 9, 4, 6, 8. 4 of 6 relay pairs isolate every sensor |

> History times are shown in your browser's time zone. If the build fails with error `0x800711C7`, Windows App Control or Smart App Control is blocking the output. See Troubleshooting.

---

## Configuration reference

| Setting | File | Purpose |
|---|---|---|
| API address | `SmartX.API/Properties/launchSettings.json` | Where the API listens (`http://localhost:5189`) |
| `ApiBaseUrl` | `SmartX.Client/wwwroot/appsettings.json` | Where the client finds the API. Leave it empty to follow the page's scheme (`http://localhost:5189/` for an http page, `https://localhost:7189/` for an https page). A value here always wins |
| `Simulator:ApiBaseUrl` | `SmartX.API/appsettings.json` (optional) | Where simulated devices send readings. By default the API discovers its own address at startup, so both launch profiles work |
| `Cors:AllowedOrigins` | `SmartX.API/appsettings.json` (optional) | Array of browser origins allowed to call the API. Empty means any origin (development) |
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
| `GET` | `/api/mesh/topology` | Nodes, links, spanning tree and low-battery watchlist |
| `GET` | `/api/mesh/route?mac=` | Cheapest and fewest-hops route to a sensor |
| `POST` | `/api/mesh/fail?mac=` | Simulate failure of a node |
| `POST` | `/api/mesh/restore?mac=` | Restore a node |
| `GET` | `/api/mesh/history?mac=&fromUtc=&toUtc=&take=&newestFirst=` | Range query over the AVL tree |
| `GET` | `/api/mesh/insights` | Battery forecasts, what-if failure impact and recommended actions |
| `GET` | `/api/health` | Used by the client to detect an unreachable API |
| `GET` | `/api/sensors/{id}/attachments` | Files attached to a sensor |
| `GET` | `/api/sensors/{id}/attachments/{fileName}` | Download one attached file |
| `GET` | `/api/mesh/archive-benchmark?readings=` | AVL tree against a full scan (50,000 to 1,000,000 readings) |
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
| Red banner "Cannot reach the Smart-X API" | API stopped, or `ApiBaseUrl` has the wrong address or scheme | Start the API. Make `ApiBaseUrl` match its address, or leave it empty. An https client must call an https API |
| Browser blocks the API with a certificate warning on https | The development certificate is not trusted | Run `dotnet dev-certs https --trust`, then restart the browser |
| Upload says a file type is not accepted | Only `.txt .log .json .cfg .conf .csv .xml .png .jpg .jpeg .pdf`, up to 20 MB, are allowed | Convert the file or rename it with an allowed extension |
| Dashboard is empty and the Command Stream shows 0 / 0 | No readings reach the API: the simulator cannot reach it, or the API just restarted | Look in the Visual Studio Output window (Debug) or the API console for `SIMULATOR: sending readings to ...`. A `could not reach the gateway` warning means the address is wrong: set `Simulator:ApiBaseUrl` in `SmartX.API/appsettings.json` |
| Command Stream says "No devices match the current filter" | The search box, Category or Alert state filter is remembered between pages | Clear the search box and set Category and Alert state back to their "All" options |
| `NullReferenceException` in `TelemetrySimulatorService` on start | A constructor parameter was added but not stored in its field | Make sure the constructor assigns every parameter, and that `SeedDemoSensors();` is its last line |
| `TaskCanceledException` break in the debugger when stopping | A background service was cancelled during shutdown | Harmless. Click Continue. The simulator catches it |
| Pages have no styling | `app.css` missing or cached | Confirm the file path in `index.html`, then press Ctrl+Shift+R |
| Build fails with `0x800711C7` or "an Application Control policy has blocked this file" | Windows Smart App Control or an App Control policy blocks build output | Turn off Smart App Control (Windows Security, App and browser control), or ask your IT team if the PC is managed. Then rebuild |
| Network Topology shows no history | The sensor is new, or the window has no readings | Use a seeded sensor and a quick range such as Last 24h |
| "A project with an Output Type of Class Library cannot be started directly" | `SmartX.Shared` is a startup project | Start only `SmartX.API` and `SmartX.Client` |

## Project status

A simulated PoE submission. Data lives in memory and resets whenever `SmartX.API` restarts. Mesh topology and history data are rebuilt at startup, with 24 hours of simulated history seeded for the seeded sensors.

Feedback from Parts 1 and 2 has been applied in Part 3: the API address is configurable and follows the page's scheme (http or https), sensors are validated, attached files can be listed and downloaded, a registered-sensors page exists, the active pillar is highlighted, and the Network Topology page forecasts failures and recommends actions. Tests 11 to 16 cover each change.

## License

Educational project. No license specified.
