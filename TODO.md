# TODO

## In Progress
- [ ] Investigate schema.json warning on startup
- [ ] Expand GameType coverage

## Under Review
<!-- Items being tested or evaluated before closing out -->

## Pending
<!-- Confirmed tasks not yet started -->
- [ ] Investigate missing type errors on startup (MissionTemplate, Map, OperationTemplate, MapGenerator)
- [ ] Investigate requirements for repair of `StrategyEventHooks`

## Completed
- [x] Fork and set up build environment
- [x] Fix Directory.Build.props placement
- [x] Fix versions.json path in GenerateVersion.targets
- [x] Add missing System.Linq usings to ArmyGeneration.cs, Faction.cs, TileEffects.cs, Perks.cs
- [x] Clean up GLB loading path: shared load/register flow, .gltf scan support, material/submesh fixes, and runtime asset persistence
- [x] Port Il2CppUtils
- [x] New class `GameMethods`, mirroring GameObj utility for methods
- [x] `TacticalEventHooks` refactored and repaired.


## Notes
<!-- Anything that doesn't fit above -->
- Building with .NET 10 SDK targeting net6.0 — minor behavioural differences from original dev environment expected
- Running MelonLoader v0.7.3-ci.2497 vs specified v0.7.2 — monitor for compatibility issues
- Fork DLL is built from newer source than current shipped release — extra systems (CustomMaps, Lua, GraphInterpreter) not yet in official build
