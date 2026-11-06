using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoH.Presentation;

var catalog = JsonSerializer.Deserialize<ItemCatalog>(File.ReadAllText(args[0]), new JsonSerializerOptions { IncludeFields = true });
var calls = new List<string>();
void Check(bool value, string label) { if (!value) throw new Exception(label); }
// The shipped kiss row has no item. Even unverified and unavailable unrelated item files must cause zero load callbacks.
Check(!catalog.runtimeVerified, "Actual unverified guard fixture");
var empty = NativeItemLoadPlan.Create(catalog, 0, 0); empty.Load(f => calls.Add(f.source));
Check(empty.Items.Length == 0 && calls.Count == 0, "aibu:0 does not load any of the nine unrelated native bundles");
bool rejected = false;
try { NativeItemLoadPlan.Create(catalog, 0, 9); } catch (InvalidOperationException) { rejected = true; }
Check(rejected && calls.Count == 0, "Required anal-vibe remains refused while the item rig is unverified");
catalog.runtimeVerified = true; // Test-only proof fixture, never written to the product catalog.
var actual = NativeItemLoadPlan.Create(catalog, 0, 9); actual.Load(f => calls.Add(f.source));
var expected = new[] { "h/h_item/01.unity3d", "animator/h/h_item/01/aibu.unity3d" };
Check(actual.Items.Length == 1 && calls.Count == 2 && expected.All(calls.Contains), "Native aibu9 row loads only its prefab and shared base/pose controller bundle");
catalog.files = catalog.files.Where(f => expected.Contains(f.source)).ToArray(); calls.Clear();
NativeItemLoadPlan.Create(catalog, 0, 9).Load(f => calls.Add(f.source));
Check(calls.Count == 2, "Unrelated seven bundle descriptors are not dependencies");
catalog.files = catalog.files.Where(f => f.source != expected[0]).ToArray(); rejected = false;
try { NativeItemLoadPlan.Create(catalog, 0, 9); } catch (InvalidOperationException) { rejected = true; }
Check(rejected, "Missing required prefab descriptor is not treated as ready");
Console.WriteLine("ITEM_LOAD_PLAN_CHECKS_PASS 5");
