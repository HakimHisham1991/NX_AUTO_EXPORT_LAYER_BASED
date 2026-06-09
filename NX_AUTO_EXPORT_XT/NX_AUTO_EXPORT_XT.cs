// =============================================================================
//  NX_AUTO_EXPORT_XT.cs
//  NX2512 — Layer-by-layer Parasolid (.x_t) export automation
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NXOpen;
using NXOpen.Assemblies;
using NXOpen.Layer;             // StateInfo, State — same pattern as BatchMirrorImport
using NXOpen.UF;

public class NXJournal
{
    private const int    MinLayer         = 1;
    private const int    MaxLayer         = 256;
    private const int    FirstExportLayer = 90;
    private const int    LastExportLayer  = 100;
    private const string ExportFolder     = @"C:\Users\Public\Documents\NX_AUTO_EXPORT_XT\EXPORT";

    // Static fields — same pattern as BatchMirrorImport
    private static Session   _session;
    private static Part      _workPart;
    private static UFSession _ufSession;
    private static readonly StringBuilder _log = new StringBuilder();

    public static void Main(string[] args)
    {
        _session   = Session.GetSession();
        _workPart  = _session.Parts.Work;
        _ufSession = UFSession.GetUFSession();

        _session.ListingWindow.Open();
        Log("=== NX_AUTO_EXPORT_XT starting ===");
        Log(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        if (_workPart == null)
        {
            Fail("No work part is open. Open the part and run again.");
            return;
        }
        Log("Work part: " + _workPart.FullPath);

        try { Directory.CreateDirectory(ExportFolder); }
        catch (Exception ex) { Fail("Cannot create export folder: " + ex.Message); return; }

        try
        {
            // Step 1 — Modeling
            _session.ApplicationSwitchImmediate("UG_APP_MODELING");
            Log("Step 1: Switched to Modeling.");

            // ── Set layer 1 as work layer via ChangeStates with State.WorkLayer ──
            // This is the exact same mechanism used by BatchMirrorImport's HideLayer.
            // Using State.WorkLayer in ChangeStates is the correct NX2512 way to
            // designate the work layer without touching the UF API.
            SetSingleLayerState(1, State.WorkLayer);
            Log("Work layer set to 1.");

            // Step 2 — Layers 3-247 Selectable (layer 1 is WorkLayer, skip it)
            SetLayerRangeState(FirstExportLayer, LastExportLayer, State.Selectable);
            Log("Step 2: Layers " + FirstExportLayer + "-" + LastExportLayer + " set to Selectable.");

            // Step 3 — Show All
            ShowAll();
            Log("Step 3: Show All done.");

            // Step 4 — Hide layers 3-247
            SetLayerRangeState(FirstExportLayer, LastExportLayer, State.Hidden);
            Log("Step 4: Layers " + FirstExportLayer + "-" + LastExportLayer + " hidden.");

            // Step 5 — Safety check:
            // After hiding layers 2-256, the only visible Solid/Sheet bodies allowed
            // are those on layer 1 (the work layer — always visible, expected).
            // If any Solid/Sheet body is visible on a layer OTHER than 1, something
            // is wrong (object not assigned to a proper layer) — abort and report.
            List<int> stray = GetLayersWithVisibleBodies();
            stray.Remove(1); // layer 1 is the work layer — always visible, always OK
            // Only care about layers inside the export range — bodies outside it are untouched
            stray.RemoveAll(l => l < FirstExportLayer || l > LastExportLayer);
            if (stray.Count > 0)
            {
                Fail("Solid/Sheet bodies still visible on layers inside export range " +
                     FirstExportLayer + "-" + LastExportLayer + " after hiding them. " +
                     "Layers: " + string.Join(", ", stray) +
                     ". These layers may be locked or read-only.");
                return;
            }
            int layer1Count = CollectBodiesOnLayer(1).Count;
            Log("Step 5: Safety check passed. Layer 1 has " + layer1Count +
                " body/bodies (expected — will be skipped by export loop).");

            // Steps 6-9 — Loop
            int exportedCount = 0;
            int skippedCount  = 0;

            for (int layer = FirstExportLayer; layer <= LastExportLayer; layer++)
            {
                SetSingleLayerState(layer, State.Selectable);

                List<Body> bodies = CollectBodiesOnLayer(layer);
                if (bodies.Count == 0)
                {
                    SetSingleLayerState(layer, State.Hidden);
                    skippedCount++;
                    continue;
                }

                bool ok = ExportLayerToParasolid(layer, bodies);
                SetSingleLayerState(layer, State.Hidden);
                if (ok) exportedCount++;
            }

            Log("");
            Log("=== EXPORT COMPLETE ===");
            Log("Layers exported      : " + exportedCount);
            Log("Empty layers skipped : " + skippedCount);
            Log("Output folder        : " + ExportFolder);

            FlushToListingWindow();
        }
        catch (Exception ex)
        {
            Fail("Unexpected error: " + ex.Message + "\n" + ex.StackTrace);
        }
    }

    // ── Layer state helpers ──────────────────────────────────────────────────
    // Mirrors BatchMirrorImport's HideLayer — uses StateInfo[], fitAll=true

    private static void SetSingleLayerState(int layer, State state)
    {
        StateInfo[] info = new StateInfo[1];
        info[0] = new StateInfo(layer, state);
        _workPart.Layers.ChangeStates(info, true);
    }

    private static void SetLayerRangeState(int from, int to, State state)
    {
        int count = to - from + 1;
        if (count <= 0) return;
        StateInfo[] info = new StateInfo[count];
        for (int i = 0; i < count; i++)
            info[i] = new StateInfo(from + i, state);
        _workPart.Layers.ChangeStates(info, true);
    }

    // ── Show All ─────────────────────────────────────────────────────────────

    private static void ShowAll()
    {
        Session.UndoMarkId markId = _session.SetUndoMark(
            Session.MarkVisibility.Invisible, "Show All");
        _session.DisplayManager.ShowByType(
            "SHOW_HIDE_TYPE_ALL",
            DisplayManager.ShowHideScope.AnyInAssembly);
        _session.UpdateManager.DoUpdate(markId);
        _workPart.ModelingViews.WorkView.FitAfterShowOrHide(View.ShowOrHideType.ShowOnly);
        _session.DeleteUndoMark(markId, null);
    }

    // ── Visibility check ─────────────────────────────────────────────────────
    // A body is "visibly stray" if:
    //   - it is a Solid or Sheet body
    //   - it is NOT blanked (explicitly hidden via Show/Hide)
    //   - its layer is NOT hidden (State.Hidden means the layer is off)
    // Note: IsBlanked = false does NOT mean visible if the layer itself is hidden.
    // We must also check the layer state.

    private static List<int> GetLayersWithVisibleBodies()
    {
        HashSet<int> set = new HashSet<int>();
        foreach (Body body in _workPart.Bodies)
        {
            if (!(body.IsSolidBody || body.IsSheetBody)) continue;
            if (body.IsBlanked) continue;
            State layerState = _workPart.Layers.GetState(body.Layer);
            if (layerState == State.Hidden) continue;
            set.Add(body.Layer);
        }

        if (_workPart.ComponentAssembly != null &&
            _workPart.ComponentAssembly.RootComponent != null)
            WalkVisibility(_workPart.ComponentAssembly.RootComponent, set);

        List<int> result = new List<int>(set);
        result.Sort();
        return result;
    }

    private static void WalkVisibility(Component comp, HashSet<int> set)
    {
        Part proto = comp.Prototype as Part;
        if (proto != null)
            foreach (Body body in proto.Bodies)
            {
                if (!(body.IsSolidBody || body.IsSheetBody)) continue;
                if (body.IsBlanked) continue;
                // Cannot call proto.Layers.GetState() — proto may not be the
                // displayed part and NX throws "not a displayed part".
                // Use _workPart.Layers.GetState() instead, since layer states
                // are governed by the work/display part in an assembly context.
                try
                {
                    State layerState = _workPart.Layers.GetState(body.Layer);
                    if (layerState == State.Hidden) continue;
                }
                catch
                {
                    // If we still can't query the state, skip this body — it's
                    // not visible in any meaningful sense.
                    continue;
                }
                set.Add(body.Layer);
            }
        foreach (Component child in comp.GetChildren())
            WalkVisibility(child, set);
    }

    // ── Body collection ──────────────────────────────────────────────────────

    private static List<Body> CollectBodiesOnLayer(int layer)
    {
        List<Body> list = new List<Body>();
        foreach (Body body in _workPart.Bodies)
            if (body.Layer == layer && (body.IsSolidBody || body.IsSheetBody))
                list.Add(body);

        if (_workPart.ComponentAssembly != null &&
            _workPart.ComponentAssembly.RootComponent != null)
            WalkBodies(_workPart.ComponentAssembly.RootComponent, layer, list);

        return list;
    }

    private static void WalkBodies(Component comp, int layer, List<Body> list)
    {
        Part proto = comp.Prototype as Part;
        if (proto != null)
            foreach (Body body in proto.Bodies)
                if (body.Layer == layer && (body.IsSolidBody || body.IsSheetBody))
                    list.Add(body);
        foreach (Component child in comp.GetChildren())
            WalkBodies(child, layer, list);
    }

    // ── Parasolid export ─────────────────────────────────────────────────────

    private static bool ExportLayerToParasolid(int layer, List<Body> bodies)
    {
        string outputFile = Path.Combine(ExportFolder, layer.ToString("000") + ".x_t");

        Session.UndoMarkId markId = _session.SetUndoMark(
            Session.MarkVisibility.Invisible, "Export L" + layer);

        ParasolidExporter exporter = null;
        try
        {
            exporter = _session.DexManager.CreateParasolidExporter();

            exporter.ObjectTypes.Curves   = false;
            exporter.ObjectTypes.Surfaces = true;
            exporter.ObjectTypes.Solids   = true;

            exporter.ExportSelectionBlock.SelectionScope =
                ObjectSelector.Scope.SelectedObjects;
            exporter.InputFile  = _workPart.FullPath;
            exporter.OutputFile = outputFile;
            exporter.ParasolidVersion =
                ParasolidExporter.ParasolidVersionOption.Current;
            exporter.FlattenAssembly = true;

            exporter.ExportSelectionBlock.SelectionComp.Add(bodies.ToArray());

            exporter.Commit();
            _session.DeleteUndoMark(markId, null);

            Log("  Layer " + layer.ToString("000") +
                " -> " + bodies.Count + " body/bodies -> " + outputFile);
            return true;
        }
        catch (Exception ex)
        {
            Log("  ERROR layer " + layer + ": " + ex.Message);
            try { _session.DeleteUndoMark(markId, null); } catch { }
            return false;
        }
        finally
        {
            try { if (exporter != null) exporter.Destroy(); } catch { }
        }
    }

    // ── Logging / error helpers ───────────────────────────────────────────────

    private static void Log(string line)
    {
        _log.AppendLine(line);
        // Also write immediately so partial output is visible if NX crashes
        try { _session.ListingWindow.WriteLine(line); } catch { }
    }

    private static void FlushToListingWindow()
    {
        // Already written line-by-line above; this is a no-op kept for clarity
    }

    private static void Fail(string message)
    {
        Log("FATAL: " + message);
        try { _ufSession.Ui.DisplayMessage(message, 1); } catch { }
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)Session.LibraryUnloadOption.Immediately;
    }
}
