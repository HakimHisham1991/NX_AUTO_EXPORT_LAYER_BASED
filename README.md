# NX_AUTO_EXPORT_LAYER_BASED

![NX Version](https://img.shields.io/badge/Siemens%20NX-2512-blue)
![Language](https://img.shields.io/badge/Language-C%23-green)
![License](https://img.shields.io/badge/License-MIT-yellow)

A Siemens NX Open journal that exports geometry from individual layers into separate Parasolid (`.x_t`) files — automatically, one layer at a time.

---

## The Problem

In large NX models, geometry is often organized across many layers. Isolating each layer and exporting manually is slow, repetitive, and error-prone. This journal eliminates that entirely.

---

## What It Does

- Iterates through a configurable layer range
- Isolates each layer's geometry with strict visibility control
- Exports only **Solid Bodies** and **Sheet Bodies** — ignoring everything else
- Produces one `.x_t` file per non-empty layer
- Logs progress, skipped layers, and errors to the NX Listing Window
- Recovers gracefully — a failure on one layer never stops the rest

**Output example:**
```
090.x_t
091.x_t
092.x_t
...
100.x_t
```

---

## Requirements

- Siemens NX 2512 (tested)
- Write access to `C:\Users\Public\Documents\NX_AUTO_EXPORT_XT\EXPORT`
- Geometry organized onto layers within the configured export range

---

## Usage

**1. Open your `.prt` file in NX.**

**2. Run the journal:**
> Tools → Journal → Play → select `NX_AUTO_EXPORT_XT.cs`

The journal will switch to Modeling, validate layer states, process each layer, and generate a summary report in the Listing Window.

---

## Configuration

Edit these constants at the top of `NX_AUTO_EXPORT_XT.cs`:

```csharp
private const int FirstExportLayer = 90;
private const int LastExportLayer  = 100;

private const string ExportFolder =
    @"C:\Users\Public\Documents\NX_AUTO_EXPORT_XT\EXPORT";
```

---

## Export Sequence

1. Switch to Modeling; set Layer 1 as work layer
2. Show all geometry; normalize layer states
3. Hide the entire export range to create a controlled baseline
4. Validate — check for unexpected visible bodies
5. For each layer: make selectable → collect bodies → export → hide
6. Write summary report (exported layers, skipped layers, body counts, errors)

---

## Project Structure

```
NX_AUTO_EXPORT_LAYER_BASED/
├── NX_AUTO_EXPORT_XT/
│   ├── NX_AUTO_EXPORT_XT.cs          ← Main journal
│   └── SAMPLE_CODE/
│       ├── 01_Modeling.cs            ← Switch to Modeling
│       ├── 02_layer_all_ON.cs        ← Enable all layers
│       ├── 03_show_all.cs            ← Show all geometry
│       ├── 04_layer_OFF.cs           ← Disable layer visibility
│       ├── 05_layer_2_ON.cs          ← Enable a specific layer
│       ├── 06_layer_2_export.cs      ← Export a test layer
│       └── 07_layer_2_OFF.cs         ← Disable a specific layer
├── LICENSE
└── README.md
```

The `SAMPLE_CODE` journals are standalone snippets useful for learning NXOpen layer manipulation or troubleshooting individual steps.

---

## Logging Example

```
[INFO] Export Layer 090 | Bodies Found: 12 | Export Successful
[INFO] Export Layer 091 | Bodies Found: 0  | Layer Skipped
[INFO] Export Layer 092 | Bodies Found: 7  | Export Successful
```

---

## Use Cases

| Domain | Application |
|---|---|
| Manufacturing | Export machining setups stored on separate layers |
| Supplier Exchange | Deliver isolated geometry without sharing the full model |
| CAE / Simulation | Generate Parasolid files for meshing workflows |
| CAM Automation | Feed layer-specific geometry into automated CAM processes |
| Data Cleanup | Quickly identify and extract populated layers |

---

## Contributing

Contributions welcome. Ideas for improvement:

- GUI configuration window
- Export format selection (STEP, IGES, STL)
- Auto-generated folder naming from layer names
- CSV export report
- Teamcenter integration

Fork → feature branch → PR.

---

## Disclaimer

Test on non-production files first. Always back up your NX data. Verify exported geometry before downstream use. This software is provided as-is with no warranty.

---

## License

MIT — see [LICENSE](LICENSE) for details.

---

*Built for the Siemens NX community by [Hakim Hisham](https://github.com/HakimHisham1991).*
